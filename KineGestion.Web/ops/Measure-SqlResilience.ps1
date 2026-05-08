param(
    [string]$ProjectPath = "./KineGestion.Web/KineGestion.Web.csproj",
    [string]$BaseUrl = "http://localhost:5000",
    [int]$Iterations = 20,
    [int]$PauseMs = 100,
    [int]$StartupTimeoutSeconds = 60,
    [string[]]$Routes = @('/', '/Home/Index', '/Account/Login'),
    [int[]]$RetryProfiles = @(0, 5)
)

Add-Type -AssemblyName System.Net.Http

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

function Wait-AppReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = $script:HttpClient.GetAsync($Url).GetAwaiter().GetResult()
            if ($null -ne $response) {
                return $true
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    return $false
}

function Measure-Profile {
    param(
        [int]$RetryCount,
        [string[]]$TestRoutes,
        [string]$TargetBaseUrl,
        [int]$SampleCount,
        [int]$PauseBetweenMs,
        [int]$TimeoutSeconds,
        [string]$CsprojPath
    )

    $oldRetry = $env:SqlResilience__MaxRetryCount
    $oldDelay = $env:SqlResilience__MaxRetryDelaySeconds
    $oldEnv = $env:ASPNETCORE_ENVIRONMENT
    $oldUrls = $env:ASPNETCORE_URLS

    $env:SqlResilience__MaxRetryCount = $RetryCount.ToString()
    $env:SqlResilience__MaxRetryDelaySeconds = '2'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $TargetBaseUrl

    $stdoutFile = Join-Path $env:TEMP ("kg-sqlresilience-{0}-out.log" -f $RetryCount)
    $stderrFile = Join-Path $env:TEMP ("kg-sqlresilience-{0}-err.log" -f $RetryCount)

    $proc = Start-Process dotnet -ArgumentList @('run', '--project', $CsprojPath, '--no-build', '--no-launch-profile') -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile

    try {
        if (-not (Wait-AppReady -Url $TargetBaseUrl -TimeoutSeconds $TimeoutSeconds)) {
            $stderrTail = if (Test-Path $stderrFile) { (Get-Content $stderrFile -Tail 20) -join "`n" } else { "(sin stderr)" }
            $stdoutTail = if (Test-Path $stdoutFile) { (Get-Content $stdoutFile -Tail 20) -join "`n" } else { "(sin stdout)" }
            throw "La app no respondió en $TimeoutSeconds segundos para RetryCount=$RetryCount.`n--- STDOUT ---`n$stdoutTail`n--- STDERR ---`n$stderrTail"
        }

        $rows = @()
        foreach ($route in $TestRoutes) {
            $samples = @()
            $errors = 0
            $url = "$TargetBaseUrl$route"

            for ($i = 1; $i -le $SampleCount; $i++) {
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                try {
                    $response = $script:HttpClient.GetAsync($url).GetAwaiter().GetResult()
                    $statusCode = [int]$response.StatusCode
                    if ($statusCode -ge 400) {
                        $errors++
                    }
                }
                catch {
                    $errors++
                }
                finally {
                    $sw.Stop()
                    $samples += $sw.Elapsed.TotalMilliseconds
                }

                Start-Sleep -Milliseconds $PauseBetweenMs
            }

            $rows += [PSCustomObject]@{
                RetryCount = $RetryCount
                Route = $route
                Samples = $samples.Count
                Errors = $errors
                ErrorRatePct = [Math]::Round(($errors * 100.0) / [Math]::Max($samples.Count, 1), 2)
                P50ms = Get-Percentile -Values $samples -Percentile 50
                P95ms = Get-Percentile -Values $samples -Percentile 95
                Maxms = [Math]::Round(($samples | Measure-Object -Maximum).Maximum, 2)
            }
        }

        return $rows
    }
    finally {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force
        }

        $env:SqlResilience__MaxRetryCount = $oldRetry
        $env:SqlResilience__MaxRetryDelaySeconds = $oldDelay
        $env:ASPNETCORE_ENVIRONMENT = $oldEnv
        $env:ASPNETCORE_URLS = $oldUrls
    }
}

$all = @()

$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$script:HttpClient = New-Object System.Net.Http.HttpClient($handler)
$script:HttpClient.Timeout = [TimeSpan]::FromSeconds(10)

foreach ($profile in $RetryProfiles) {
    Write-Host "Ejecutando perfil SqlResilience__MaxRetryCount=$profile ..." -ForegroundColor Cyan
    $all += Measure-Profile -RetryCount $profile -TestRoutes $Routes -TargetBaseUrl $BaseUrl -SampleCount $Iterations -PauseBetweenMs $PauseMs -TimeoutSeconds $StartupTimeoutSeconds -CsprojPath $ProjectPath
}

Write-Host "`nResultados por perfil:" -ForegroundColor Yellow
$all | Sort-Object RetryCount, Route | Format-Table -AutoSize

Write-Host "`nComparativa (delta p95 y error rate):" -ForegroundColor Yellow
$comparison = foreach ($route in $Routes) {
    $r0 = $all | Where-Object { $_.RetryCount -eq $RetryProfiles[0] -and $_.Route -eq $route } | Select-Object -First 1
    $r1 = $all | Where-Object { $_.RetryCount -eq $RetryProfiles[1] -and $_.Route -eq $route } | Select-Object -First 1

    if ($null -ne $r0 -and $null -ne $r1) {
        [PSCustomObject]@{
            Route = $route
            RetryA = $RetryProfiles[0]
            RetryB = $RetryProfiles[1]
            P95A = $r0.P95ms
            P95B = $r1.P95ms
            DeltaP95ms = [Math]::Round(($r1.P95ms - $r0.P95ms), 2)
            ErrorRateA = $r0.ErrorRatePct
            ErrorRateB = $r1.ErrorRatePct
            DeltaErrorRatePct = [Math]::Round(($r1.ErrorRatePct - $r0.ErrorRatePct), 2)
        }
    }
}

$comparison | Format-Table -AutoSize

$script:HttpClient.Dispose()
