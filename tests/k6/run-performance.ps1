param(
    [Parameter(Mandatory=$true)][string]$SecretsPath,
    [string]$K6Executable = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $repoRoot
$reportRoot = Join-Path $repoRoot 'reports/k6'
New-Item -ItemType Directory -Force -Path $reportRoot | Out-Null
if (!(Test-Path -LiteralPath $SecretsPath)) { throw 'QA credential file not found.' }
$credentials = Get-Content -LiteralPath $SecretsPath -Raw | ConvertFrom-Json
$email = if ($credentials.adminEmail) { $credentials.adminEmail } elseif ($credentials.correo) { $credentials.correo } else { $null }
$password = if ($credentials.adminPassword) { $credentials.adminPassword } elseif ($credentials.password) { $credentials.password } else { $null }
if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($password)) { throw 'QA file needs a local principal email and password.' }
if (!$K6Executable) {
    $command = Get-Command k6 -ErrorAction SilentlyContinue
    if ($command) { $K6Executable = $command.Source }
    elseif (Test-Path (Join-Path $env:ProgramFiles 'k6/k6.exe')) { $K6Executable = Join-Path $env:ProgramFiles 'k6/k6.exe' }
    else { throw 'k6 executable not found; install k6 or pass -K6Executable.' }
}
$K6Executable = (Resolve-Path -LiteralPath $K6Executable).Path
$version = (& $K6Executable version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not obtain k6 version.' }
Write-Output $version

$env:QA_ADMIN_EMAIL = $email
$env:QA_ADMIN_PASSWORD = $password
$env:K6_VERSION = $version
$env:K6_WEB_DASHBOARD = 'true'
$env:K6_WEB_DASHBOARD_HOST = '127.0.0.1'
$env:K6_WEB_DASHBOARD_PORT = '-1'
$env:K6_WEB_DASHBOARD_OPEN = 'false'
$env:K6_WEB_DASHBOARD_PERIOD = '2s'
$env:BASE_URL = 'http://127.0.0.1:8080'
$env:K6_NO_USAGE_REPORT = 'true'

function Invoke-K6Run([string]$Mode, [string]$Script, [string]$Html, [string]$Summary, [string]$RunName) {
    $env:PERF_MODE = $Mode
    $env:PERF_RUN_NAME = $RunName
    $env:PERF_SUMMARY_PATH = $Summary
    $env:K6_WEB_DASHBOARD_EXPORT = $Html
    $scriptPath = Join-Path $PSScriptRoot $Script
    $logPath = Join-Path $reportRoot ($RunName + '.log')
    Write-Output "Running $RunName against 127.0.0.1 only."
    $runStarted = Get-Date
    if ($Mode -eq 'stress') {
        $timeseries = 'reports/k6/stress-timeseries.json'
        & $K6Executable run --no-color --out ('json=' + $timeseries) $scriptPath *> $logPath
    } else {
        & $K6Executable run --no-color $scriptPath *> $logPath
    }
    $result = $LASTEXITCODE
    if ($result -ne 0) { throw "k6 $RunName failed (exit $result); inspect local reports/k6/$RunName.log." }
    if (!(Test-Path (Join-Path $repoRoot $Html)) -or !(Test-Path (Join-Path $repoRoot $Summary))) { throw "k6 $RunName did not create both report files." }
    foreach ($file in @($Html,$Summary)) {
        if ((Get-Item -LiteralPath (Join-Path $repoRoot $file)).LastWriteTime -lt $runStarted) { throw "Stale report after ${RunName}: $file" }
    }
    $parsed = Get-Content -LiteralPath (Join-Path $repoRoot $Summary) -Raw | ConvertFrom-Json
    if ([DateTimeOffset]::Parse($parsed.metadata.generatedAt).UtcDateTime.ToFileTimeUtc() -lt $runStarted.ToUniversalTime().ToFileTimeUtc()) { throw "Stale k6 summary after $RunName." }
    $incompleteCases = @($parsed.metadata.cases | Where-Object { $_.status -ne 'Pass' -and !($Mode -eq 'stress' -and $_.id -eq 'CON-565') })
    if ($incompleteCases.Count -gt 0) { throw "No successful k6 samples for cases: $(($incompleteCases.id -join ', '))." }
    return $parsed
}

try {
    $loadFirst = Invoke-K6Run 'load' 'load.js' 'reports/k6/load-run-1.html' 'reports/k6/load-run-1-summary.json' 'load-run-1'
    $loadFinal = Invoke-K6Run 'load' 'load.js' 'reports/k6/load-report.html' 'reports/k6/load-run-2-summary.json' 'load-run-2'
    $stress = Invoke-K6Run 'stress' 'stress.js' 'reports/k6/stress-report.html' 'reports/k6/stress-summary.json' 'stress-final'
    & node (Join-Path $PSScriptRoot 'analyze-stress.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'Could not analyze real stress time-series.' }
    $loadCombined = [ordered]@{
        generatedAt = (Get-Date).ToUniversalTime().ToString('o')
        tool = $version
        targetPolicy = @('http://127.0.0.1:8080','http://127.0.0.1:8081')
        repeatability = 'Two independent load executions with identical profile and local QA account.'
        runs = @($loadFirst,$loadFinal)
        finalRun = $loadFinal
    }
    $loadCombined | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $reportRoot 'load-summary.json') -Encoding UTF8

    # Remove the QA credentials from this PowerShell process as soon as runs finish.
    $env:QA_ADMIN_EMAIL = $null
    $env:QA_ADMIN_PASSWORD = $null

    $allFiles = @('load-report.html','stress-report.html','load-summary.json','stress-summary.json','load-run-1-summary.json','load-run-2-summary.json','stress-timeseries.json')
    foreach ($name in $allFiles) {
        $path = Join-Path $reportRoot $name
        if (!(Test-Path -LiteralPath $path)) { throw "Missing fresh report: $name" }
        $content = Get-Content -LiteralPath $path -Raw
        if ($content.Contains($password) -or $content.Contains($email) -or $content -match 'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+') { throw "Sensitive value found in $name." }
    }
    $version | Set-Content -LiteralPath (Join-Path $reportRoot 'k6-version.txt') -Encoding UTF8
    Write-Output 'Load was repeated; load and stress reports passed freshness and secret checks.'
} finally {
    $env:QA_ADMIN_EMAIL = $null
    $env:QA_ADMIN_PASSWORD = $null
    $env:K6_WEB_DASHBOARD_EXPORT = $null
    $env:PERF_SUMMARY_PATH = $null
    $env:PERF_MODE = $null
    $env:PERF_RUN_NAME = $null
}
