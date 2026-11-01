param([string]$Filter = "")
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$run = 'reports/unit/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
New-Item $run -ItemType Directory -Force | Out-Null
$arguments = @('test', 'ConnectaOficios.Tests/ConnectaOficios.Tests.csproj', '--collect', 'XPlat Code Coverage', '--logger', 'trx;LogFileName=resultado_pruebas.trx', '--results-directory', $run)
if ($Filter) { $arguments += @('--filter', $Filter) }
& dotnet @arguments
$testExit = $LASTEXITCODE
& ./generate-test-report.ps1 -TrxPath "$run/resultado_pruebas.trx" -OutputPath "$run/index.html"
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet tool run reportgenerator "-reports:$run/**/coverage.cobertura.xml" '-targetdir:reports/coverage' '-reporttypes:Html;TextSummary' '-assemblyfilters:+ConnectaOficios.Api;+ConnectaOficios.Domain'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
exit $testExit
