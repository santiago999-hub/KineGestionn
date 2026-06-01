param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [string]$Email = 'admin@kinegestion.com',

    [string]$AuthSecret = 'Admin1234',

    [int]$Iterations = 30,

    [int]$PauseMs = 0,

    [int]$WarmupIterations = 2,

    [int]$WarmupPauseMs = 0,

    [string[]]$Routes = @('/', '/Sessions'),

    [switch]$IncludeOpsMetrics,

    [switch]$SkipTlsValidation,

    [switch]$AsObject
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:SupportsBasicParsing = $PSVersionTable.PSVersion.Major -lt 6

if ($SkipTlsValidation) {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem) {
        return true;
    }
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
}

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    if (-not $Values -or $Values.Count -eq 0) {
        return 0
    }

    $sorted = $Values | Sort-Object
    $rank = [Math]::Ceiling(($Percentile / 100.0) * $sorted.Count)
    $index = [Math]::Min([Math]::Max($rank - 1, 0), $sorted.Count - 1)
    return [Math]::Round($sorted[$index], 2)
}

function Get-AntiForgeryToken {
    param([string]$Html)

    $pattern = 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"|name="__RequestVerificationToken"\s+value="([^"]+)"'
    $match = [Regex]::Match($Html, $pattern)
    if (-not $match.Success) {
        throw 'No se encontró __RequestVerificationToken en la página de login.'
    }

    if (-not [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
        return $match.Groups[1].Value
    }

    return $match.Groups[2].Value
}

function Invoke-RequestCompat {
    param(
        [string]$Uri,
        [string]$Method,
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession,
        [hashtable]$Body = $null
    )

    $requestParams = @{
        Uri = $Uri
        Method = $Method
        WebSession = $WebSession
        MaximumRedirection = 5
    }

    if ($null -ne $Body) {
        $requestParams.Body = $Body
    }

    if ($script:SupportsBasicParsing) {
        $requestParams.UseBasicParsing = $true
    }

    return Invoke-WebRequest @requestParams
}

function Invoke-Login {
    param(
        [string]$Base,
        [string]$UserEmail,
        [string]$UserSecret,
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession
    )

    $loginUrl = "$Base/Account/Login"
    $loginPage = Invoke-RequestCompat -Uri $loginUrl -Method Get -WebSession $WebSession
    $token = Get-AntiForgeryToken -Html $loginPage.Content

    $form = @{
        '__RequestVerificationToken' = $token
        'Email' = $UserEmail
        'Password' = $UserSecret
        'RememberMe' = 'false'
        'ReturnUrl' = ''
    }

    $response = Invoke-RequestCompat -Uri $loginUrl -Method Post -Body $form -WebSession $WebSession

    $authCookie = $WebSession.Cookies.GetCookies($Base) | Where-Object { $_.Name -like '.AspNetCore.Identity.Application*' } | Select-Object -First 1
    if ($null -eq $authCookie) {
        throw 'Login falló: no se generó cookie de autenticación.'
    }

    if ($response.BaseResponse.ResponseUri.AbsolutePath -like '*Account/Login*') {
        throw 'Login falló: la respuesta quedó en la pantalla de login.'
    }
}

function Measure-Route {
    param(
        [string]$Base,
        [string]$Route,
        [int]$Runs,
        [int]$Pause,
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession
    )

    $samples = New-Object System.Collections.Generic.List[double]
    $successCount = 0
    $errorCount = 0
    $coldMs = 0.0

    $normalizedRoute = if ($Route.StartsWith('/')) { $Route } else { "/$Route" }
    $url = "$Base$normalizedRoute"

    for ($i = 1; $i -le $Runs; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $res = Invoke-RequestCompat -Uri $url -Method Get -WebSession $WebSession
            if ($res.StatusCode -ge 200 -and $res.StatusCode -lt 400) {
                $successCount++
            }
            else {
                $errorCount++
            }
        }
        catch {
            $errorCount++
        }
        finally {
            $sw.Stop()
            $elapsed = $sw.Elapsed.TotalMilliseconds
            $samples.Add($elapsed)
            if ($i -eq 1) {
                $coldMs = $elapsed
            }
        }

        if ($Pause -gt 0) {
            Start-Sleep -Milliseconds $Pause
        }
    }

    $warm = if ($samples.Count -gt 1) { $samples | Select-Object -Skip 1 } else { @() }
    $warmP50 = Get-Percentile -Values $warm -Percentile 50
    $warmP95 = Get-Percentile -Values $warm -Percentile 95
    $max = [Math]::Round(($samples | Measure-Object -Maximum).Maximum, 2)
    $errorRate = if ($Runs -gt 0) { [Math]::Round(($errorCount * 100.0) / $Runs, 2) } else { 0 }

    return [PSCustomObject]@{
        Route = $normalizedRoute
        Samples = $Runs
        ColdMs = [Math]::Round($coldMs, 2)
        WarmP50Ms = $warmP50
        WarmP95Ms = $warmP95
        MaxMs = $max
        Errors = $errorCount
        ErrorRatePct = $errorRate
        Success = $successCount
    }
}

function Invoke-Warmup {
    param(
        [string]$Base,
        [string[]]$WarmupRoutes,
        [int]$WarmupRuns,
        [int]$Pause,
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession
    )

    if ($WarmupRuns -le 0 -or -not $WarmupRoutes -or $WarmupRoutes.Count -eq 0) {
        return
    }

    foreach ($route in $WarmupRoutes) {
        $normalizedRoute = if ($route.StartsWith('/')) { $route } else { "/$route" }
        $url = "$Base$normalizedRoute"

        for ($i = 1; $i -le $WarmupRuns; $i++) {
            try {
                Invoke-RequestCompat -Uri $url -Method Get -WebSession $WebSession | Out-Null
            }
            catch {
                Write-Warning "Warmup falló para $normalizedRoute (iteración $i): $($_.Exception.Message)"
            }

            if ($Pause -gt 0) {
                Start-Sleep -Milliseconds $Pause
            }
        }
    }
}

$base = $BaseUrl.TrimEnd('/')

if ($IncludeOpsMetrics -and -not ($Routes -contains '/ops/metrics')) {
    $Routes += '/ops/metrics'
}

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
Invoke-Login -Base $base -UserEmail $Email -UserSecret $AuthSecret -WebSession $session

Invoke-Warmup -Base $base -WarmupRoutes $Routes -WarmupRuns $WarmupIterations -Pause $WarmupPauseMs -WebSession $session

$results = foreach ($route in $Routes) {
    Measure-Route -Base $base -Route $route -Runs $Iterations -Pause $PauseMs -WebSession $session
}

if ($AsObject) {
    $results
}
else {
    $results | Format-Table -AutoSize
}
