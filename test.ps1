# test.ps1 — Compila la solucion una sola vez y ejecuta ambas suites en paralelo
$root = $PSScriptRoot

Write-Host "Compilando solucion..." -ForegroundColor Cyan
$buildResult = dotnet build "$root\KineGestion.sln" -c Debug --nologo -v minimal 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $buildResult
    Write-Host "Compilacion fallida." -ForegroundColor Red
    exit 1
}
Write-Host "Compilacion OK.`n" -ForegroundColor Green

$start = [System.Diagnostics.Stopwatch]::StartNew()

# Ejecutar ambas suites en paralelo sin recompilar
$core = Start-Job {
    dotnet test "$using:root\KineGestion.Tests\KineGestion.Tests.csproj" --no-build --nologo -v minimal
}
$web = Start-Job {
    dotnet test "$using:root\KineGestion.Web.Tests\KineGestion.Web.Tests.csproj" --no-build --nologo -v minimal
}

Wait-Job $core, $web | Out-Null

Write-Host "--- KineGestion.Tests ---" -ForegroundColor Yellow
Receive-Job $core
Write-Host "--- KineGestion.Web.Tests ---" -ForegroundColor Yellow
Receive-Job $web

Remove-Job $core, $web

$start.Stop()
Write-Host "`nTiempo total: $([Math]::Round($start.Elapsed.TotalSeconds, 1))s" -ForegroundColor Cyan
