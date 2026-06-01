param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [string]$Email = 'admin@kinegestion.com',

    [string]$AuthSecret = 'Admin1234',

    [int]$Iterations = 20,

    [int]$WarmupIterations = 2,

    [int]$WarmupPauseMs = 0,

    [string[]]$Routes = @('/', '/Sessions'),

    [double]$BaselineHomeP95 = 89.08,

    [double]$BaselineSessionsP95 = 71.52,

    [bool]$IncludeServerMetrics = $true,

    [string]$ChecklistPath = '.\CHECKLIST-DIARIA-P95-20MIN.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:SupportsBasicParsing = $PSVersionTable.PSVersion.Major -lt 6

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$measureScript = Join-Path $scriptDir 'Measure-P95-Authenticated.ps1'

if (-not (Test-Path -Path $measureScript)) {
    throw "No se encontró el script de medición en: $measureScript"
}

function Get-Semaforo {
    param(
        [double]$WarmP95,
        [double]$Baseline,
        [double]$ErrorRate
    )

    if ($ErrorRate -gt 0 -or $WarmP95 -gt ($Baseline * 1.2)) {
        return 'Rojo'
    }

    if ($WarmP95 -gt ($Baseline * 1.1)) {
        return 'Amarillo'
    }

    return 'Verde'
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

function Invoke-LoginSession {
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
        throw 'Login falló para /ops/metrics: no se generó cookie de autenticación.'
    }

    if ($response.BaseResponse.ResponseUri.AbsolutePath -like '*Account/Login*') {
        throw 'Login falló para /ops/metrics: la respuesta quedó en la pantalla de login.'
    }
}

function Measure-Run {
    param([string]$Tag)

    Write-Host "Ejecutando corrida $Tag..."

    $results = & $measureScript `
        -BaseUrl $BaseUrl `
        -Email $Email `
        -AuthSecret $AuthSecret `
        -Iterations $Iterations `
        -WarmupIterations $WarmupIterations `
        -WarmupPauseMs $WarmupPauseMs `
        -PauseMs 0 `
        -Routes $Routes `
        -AsObject

    if (-not $results) {
        throw "La corrida $Tag no devolvió resultados."
    }

    return $results
}

function Get-OpsMetricsSnapshot {
    param(
        [string]$Base,
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession
    )

    try {
        return Invoke-RestMethod -Uri "$Base/ops/metrics" -Method Get -MaximumRedirection 5 -WebSession $WebSession
    }
    catch {
        Write-Warning "No se pudo obtener /ops/metrics: $($_.Exception.Message)"
        return $null
    }
}

function Get-ObjectPropertyValue {
    param(
        [object]$InputObject,
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties |
        Where-Object { $_.Name -ieq $PropertyName } |
        Select-Object -First 1

    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-ServerRouteSummary {
    param(
        [object]$BeforeSnapshot,
        [object]$AfterSnapshot,
        [string]$Route
    )

    $afterTopPaths = Get-ObjectPropertyValue -InputObject $AfterSnapshot -PropertyName 'TopPaths'
    if ($null -eq $afterTopPaths) {
        return $null
    }

    $normalizedRoute = if ($Route.StartsWith('/')) { $Route } else { "/$Route" }
    $key = "GET $normalizedRoute"

    $after = $afterTopPaths |
        Where-Object { (Get-ObjectPropertyValue -InputObject $_ -PropertyName 'Path') -eq $key } |
        Select-Object -First 1
    if ($null -eq $after) {
        return $null
    }

    $before = $null
    $beforeTopPaths = Get-ObjectPropertyValue -InputObject $BeforeSnapshot -PropertyName 'TopPaths'
    if ($null -ne $beforeTopPaths) {
        $before = $beforeTopPaths |
            Where-Object { (Get-ObjectPropertyValue -InputObject $_ -PropertyName 'Path') -eq $key } |
            Select-Object -First 1
    }

    $beforeRequests = if ($null -ne $before) { [double](Get-ObjectPropertyValue -InputObject $before -PropertyName 'Requests') } else { 0d }
    $afterRequests = [double](Get-ObjectPropertyValue -InputObject $after -PropertyName 'Requests')
    $deltaRequests = [Math]::Max(0d, $afterRequests - $beforeRequests)

    $beforeAverageMs = if ($null -ne $before) { [double](Get-ObjectPropertyValue -InputObject $before -PropertyName 'AverageDurationMs') } else { 0d }
    $afterAverageMs = [double](Get-ObjectPropertyValue -InputObject $after -PropertyName 'AverageDurationMs')

    $beforeTotalMs = if ($null -ne $before) { $beforeAverageMs * $beforeRequests } else { 0d }
    $afterTotalMs = $afterAverageMs * $afterRequests
    $deltaTotalMs = [Math]::Max(0d, $afterTotalMs - $beforeTotalMs)

    $deltaAverageMs = if ($deltaRequests -gt 0d) {
        [Math]::Round($deltaTotalMs / $deltaRequests, 2)
    }
    else {
        [Math]::Round($afterAverageMs, 2)
    }

    $afterMaxMsRaw = Get-ObjectPropertyValue -InputObject $after -PropertyName 'MaxDurationMs'
    $afterMaxMs = if ($null -ne $afterMaxMsRaw) { [double]$afterMaxMsRaw } else { 0d }

    return [PSCustomObject]@{
        Path = $key
        RequestsDelta = [int][Math]::Round($deltaRequests, 0)
        AverageMs = $deltaAverageMs
        MaxMs = [Math]::Round($afterMaxMs, 2)
    }
}

$base = $BaseUrl.TrimEnd('/')

$opsSession = $null
if ($IncludeServerMetrics) {
    $opsSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    Invoke-LoginSession -Base $base -UserEmail $Email -UserSecret $AuthSecret -WebSession $opsSession
}

$beforeRun1Server = if ($IncludeServerMetrics) { Get-OpsMetricsSnapshot -Base $base -WebSession $opsSession } else { $null }
$run1 = Measure-Run -Tag '1'
$afterRun1Server = if ($IncludeServerMetrics) { Get-OpsMetricsSnapshot -Base $base -WebSession $opsSession } else { $null }

$beforeRun2Server = if ($IncludeServerMetrics) { Get-OpsMetricsSnapshot -Base $base -WebSession $opsSession } else { $null }
$run2 = Measure-Run -Tag '2'
$afterRun2Server = if ($IncludeServerMetrics) { Get-OpsMetricsSnapshot -Base $base -WebSession $opsSession } else { $null }

$today = Get-Date -Format 'yyyy-MM-dd'
$rows = New-Object System.Collections.Generic.List[string]

foreach ($route in $Routes) {
    $normalizedRoute = if ($route.StartsWith('/')) { $route } else { "/$route" }

    $r1 = $run1 | Where-Object { $_.Route -eq $normalizedRoute } | Select-Object -First 1
    $r2 = $run2 | Where-Object { $_.Route -eq $normalizedRoute } | Select-Object -First 1

    if ($null -eq $r1 -or $null -eq $r2) {
        Write-Warning "No se encontró la ruta $normalizedRoute en ambas corridas. Se omite del registro."
        continue
    }

    $baseline = if ($normalizedRoute -eq '/Sessions') { $BaselineSessionsP95 } else { $BaselineHomeP95 }
    $deltaPct = if ($r1.WarmP95Ms -gt 0) {
        [Math]::Round((([double]$r2.WarmP95Ms - [double]$r1.WarmP95Ms) / [double]$r1.WarmP95Ms) * 100, 2)
    }
    else {
        0
    }

    $semaforo = Get-Semaforo -WarmP95 ([double]$r2.WarmP95Ms) -Baseline $baseline -ErrorRate ([double]$r2.ErrorRatePct)

    $observacion = "Corrida2; variación vs corrida1: $deltaPct%"
    if ([Math]::Abs($deltaPct) -gt 20) {
        $observacion += '; posible variabilidad transitoria (>20%)'
    }

    if ($IncludeServerMetrics) {
        $serverRun1 = Get-ServerRouteSummary -BeforeSnapshot $beforeRun1Server -AfterSnapshot $afterRun1Server -Route $normalizedRoute
        $serverRun2 = Get-ServerRouteSummary -BeforeSnapshot $beforeRun2Server -AfterSnapshot $afterRun2Server -Route $normalizedRoute

        if ($null -ne $serverRun2) {
            $observacion += "; server run2 avg~$($serverRun2.AverageMs)ms max=$($serverRun2.MaxMs)ms req=$($serverRun2.RequestsDelta)"
        }

        if ($null -ne $serverRun1) {
            $observacion += "; server run1 avg~$($serverRun1.AverageMs)ms"
        }
    }

    $row = "| $today | $normalizedRoute | $($r2.ColdMs) | $($r2.WarmP50Ms) | $($r2.WarmP95Ms) | $($r2.ErrorRatePct) | $semaforo | $observacion |"
    $rows.Add($row) | Out-Null
}

if ($rows.Count -eq 0) {
    throw 'No hubo filas para registrar en checklist.'
}

$checklistResolved = Resolve-Path -Path $ChecklistPath -ErrorAction SilentlyContinue
if ($null -eq $checklistResolved) {
    throw "No se encontró el checklist en la ruta: $ChecklistPath"
}

Add-Content -Path $checklistResolved -Value ""
foreach ($line in $rows) {
    Add-Content -Path $checklistResolved -Value $line
}

Write-Host "Resultados corrida 2:"
$run2 | Format-Table -AutoSize
Write-Host ""
Write-Host "Filas agregadas en checklist:"
$rows | ForEach-Object { Write-Host $_ }
