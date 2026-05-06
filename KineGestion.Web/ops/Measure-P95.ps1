param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [int]$Iterations = 30,

    [int]$PauseMs = 100
)

$routes = @(
    '/Users',
    '/Sessions',
    '/Sessions/MyAgenda'
)

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

$results = @()

foreach ($route in $routes) {
    $samples = @()
    $url = "$BaseUrl$route"

    for ($i = 1; $i -le $Iterations; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Invoke-WebRequest -Uri $url -Method Get -UseBasicParsing | Out-Null
        }
        catch {
            # Se registra latencia igualmente para detectar errores/performance
        }
        finally {
            $sw.Stop()
            $samples += $sw.Elapsed.TotalMilliseconds
        }

        Start-Sleep -Milliseconds $PauseMs
    }

    $p50 = Get-Percentile -Values $samples -Percentile 50
    $p95 = Get-Percentile -Values $samples -Percentile 95
    $max = [Math]::Round(($samples | Measure-Object -Maximum).Maximum, 2)

    $results += [PSCustomObject]@{
        Route = $route
        Samples = $samples.Count
        P50ms = $p50
        P95ms = $p95
        Maxms = $max
    }
}

$results | Format-Table -AutoSize
