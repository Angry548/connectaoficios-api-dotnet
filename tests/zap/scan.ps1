param(
    [string]$SecretsPath = (Join-Path $PSScriptRoot '../postman/local.secrets.json'),
    [string]$ZapDirectory = '',
    [string]$JavaExecutable = ''
)
$ErrorActionPreference = 'Stop'
$qaRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$qaReports = Join-Path $qaRoot 'reports/zap'
New-Item -ItemType Directory -Force $qaReports | Out-Null
if (!(Test-Path -LiteralPath $SecretsPath)) { throw 'Specify the ignored credentials file for the running local QA environment.' }
if (!$ZapDirectory) {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'ZAP/Zed Attack Proxy'),
        (Join-Path $env:ProgramFiles 'OWASP/Zed Attack Proxy'),
        (Join-Path $env:LOCALAPPDATA 'Programs/ZAP/Zed Attack Proxy')
    )
    $ZapDirectory = $candidates | Where-Object { Test-Path (Join-Path $_ 'zap.bat') } | Select-Object -First 1
}
if (!$ZapDirectory) { throw 'OWASP ZAP installation not found; pass -ZapDirectory.' }
$qaJar = Get-ChildItem -LiteralPath $ZapDirectory -Filter 'zap-*.jar' | Select-Object -First 1
if (!$qaJar) { throw 'Missing ZAP application JAR.' }
if (!$JavaExecutable) {
    if ($env:JAVA_HOME) { $JavaExecutable = Join-Path $env:JAVA_HOME 'bin/java.exe' }
    else { $JavaExecutable = (Get-Command java -ErrorAction Stop).Source }
}
$qaDaemon = $null
$qaExit = 1
$previousSecrets = $env:ZAP_QA_SECRETS
try {
    try { $qaVersion = Invoke-RestMethod 'http://127.0.0.1:8090/JSON/core/view/version/' -TimeoutSec 2 } catch { $qaVersion = $null }
    if (!$qaVersion) {
        $qaRuntime = Join-Path $qaReports ('runtime-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $qaRuntime | Out-Null
        $qaArgs = @('-Xmx1024m','-jar',('"'+$qaJar.FullName+'"'),'-daemon','-dir',('"'+$qaRuntime+'"'),'-host','127.0.0.1','-port','8090','-config','api.disablekey=true','-config','autoupdate.checkOnStart=false')
        $qaDaemon = Start-Process -FilePath $JavaExecutable -ArgumentList $qaArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $qaReports 'daemon.log') -RedirectStandardError (Join-Path $qaReports 'daemon.error.log')
        $qaDeadline = (Get-Date).AddMinutes(2)
        do {
            Start-Sleep -Seconds 2
            try { $qaVersion = Invoke-RestMethod 'http://127.0.0.1:8090/JSON/core/view/version/' -TimeoutSec 2 } catch { $qaVersion = $null }
            if ($qaDaemon.HasExited) { throw 'ZAP exited; inspect ignored reports/zap/daemon*.log.' }
        } while (!$qaVersion -and (Get-Date) -lt $qaDeadline)
        if (!$qaVersion) { throw 'ZAP startup timeout.' }
    }
    Write-Output ('OWASP ZAP ' + $qaVersion.version + '; targets: 127.0.0.1:8080 and 127.0.0.1:8081 only')
    $env:ZAP_QA_SECRETS = (Resolve-Path -LiteralPath $SecretsPath).Path
    & node (Join-Path $PSScriptRoot 'scan-local.cjs')
    $qaExit = $LASTEXITCODE
} finally {
    $env:ZAP_QA_SECRETS = $previousSecrets
    if ($qaDaemon) {
        try { Invoke-RestMethod 'http://127.0.0.1:8090/JSON/core/action/shutdown/' -TimeoutSec 5 | Out-Null } catch { Write-Warning 'Could not shut down the owned local ZAP daemon.' }
    }
}
exit $qaExit
