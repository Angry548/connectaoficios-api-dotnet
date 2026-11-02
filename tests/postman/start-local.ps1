param(
    [string]$JavaRepository = $env:CONNECTA_JAVA_REPOSITORY,
    [ValidateSet('', 'dotnet', 'java')][string]$Backend = ''
)
$ErrorActionPreference = 'Stop'
$qaRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $qaRoot
$qaSecretsPath = Join-Path $PSScriptRoot 'local.secrets.json'
if (!(Test-Path $qaSecretsPath)) {
    function New-LocalRandom {
        $bytes = New-Object byte[] 48
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        return [Convert]::ToBase64String($bytes)
    }
    @{ jwtKey = (New-LocalRandom); password = ((New-LocalRandom) + '!Aa1');
       adminEmail = 'qa.newman.principal@example.test'; adminPassword = ((New-LocalRandom) + '!Aa1') } |
        ConvertTo-Json | Set-Content -LiteralPath $qaSecretsPath -Encoding UTF8
}
$qaSecrets = Get-Content -LiteralPath $qaSecretsPath -Raw | ConvertFrom-Json
foreach ($key in @('jwtKey','password','adminEmail','adminPassword')) {
    if ([string]::IsNullOrWhiteSpace($qaSecrets.$key)) { throw "Missing $key in ignored local.secrets.json" }
}
# Explicit process settings override Development user-secrets, which may point to production.
$env:ConnectionStrings__DefaultConnection = 'Server=.\SQLEXPRESS;Database=ConnectaOficiosNewman;Integrated Security=True;TrustServerCertificate=True'
$env:Jwt__Key = $qaSecrets.jwtKey
$env:Jwt__Issuer = 'ConnectaOficios.Api'
$env:Jwt__Audience = 'ConnectaOficios.Clients'
$env:BootstrapAdmin__Nombre = 'QA Newman Principal'
$env:BootstrapAdmin__Correo = $qaSecrets.adminEmail
$env:BootstrapAdmin__Password = $qaSecrets.adminPassword
$env:LocalTesting__SeedEnabled = 'false'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:8080'
if ($Backend -eq 'dotnet') {
    & dotnet run --project ConnectaOficios.Api --no-build --no-launch-profile
    exit $LASTEXITCODE
}
if (!$JavaRepository) {
    $candidates = @(
        (Join-Path (Split-Path $qaRoot -Parent) 'connectaoficios-api-java'),
        (Join-Path $env:USERPROFILE 'OneDrive\Documentos\ConnectaOficios\connectaoficios-api-java'),
        (Join-Path $env:USERPROFILE 'Documents\ConnectaOficios\connectaoficios-api-java')
    )
    $JavaRepository = $candidates | Where-Object { Test-Path (Join-Path $_ 'mvnw.cmd') } | Select-Object -First 1
}
if (!$JavaRepository -or !(Test-Path (Join-Path $JavaRepository 'mvnw.cmd'))) {
    throw 'Set CONNECTA_JAVA_REPOSITORY or pass -JavaRepository with the existing Java repository path.'
}
$JavaRepository = (Resolve-Path -LiteralPath $JavaRepository).Path
if ($Backend -eq 'java') {
    # Existing Java test datasource and test classpath; no MySQL credentials invented.
    $env:APP_JWT_SECRET = $qaSecrets.jwtKey
    $env:APP_JWT_ISSUER = 'ConnectaOficios.Api'
    $env:APP_JWT_AUDIENCE = 'ConnectaOficios.Clients'
    $env:SPRING_DATASOURCE_URL = 'jdbc:h2:mem:connectaoficios_test;MODE=MySQL;DB_CLOSE_DELAY=-1;DB_CLOSE_ON_EXIT=FALSE'
    $env:SPRING_DATASOURCE_DRIVER_CLASS_NAME = 'org.h2.Driver'
    $env:SPRING_DATASOURCE_USERNAME = 'sa'
    $env:SPRING_APPLICATION_JSON = '{"spring":{"datasource":{"password":""},"jpa":{"hibernate":{"ddl-auto":"create-drop"}}}}'
    $env:SPRING_PROFILES_ACTIVE = 'local,test'
    $env:SERVER_PORT = '8081'
    $env:SERVER_ADDRESS = '127.0.0.1'
    $env:CLOUDINARY_URL = ''
    Set-Location -LiteralPath $JavaRepository
    & .\mvnw.cmd '-Dspring-boot.run.useTestClasspath=true' '-Dspring-boot.run.profiles=local,test' spring-boot:run
    exit $LASTEXITCODE
}
function Test-LocalHealth([string]$Url, [string]$Expected) {
    try { return (Invoke-RestMethod -Uri $Url -TimeoutSec 3).status -eq $Expected } catch { return $false }
}
$qaReports = Join-Path $qaRoot 'reports/newman'
New-Item -ItemType Directory -Force -Path $qaReports | Out-Null
if (!(Test-LocalHealth 'http://127.0.0.1:8080/health' 'OK')) {
    & dotnet build ConnectaOficios.Api --nologo
    if ($LASTEXITCODE -ne 0) { throw '.NET build failed' }
    & dotnet ef database update --project ConnectaOficios.Domain --startup-project ConnectaOficios.Api --no-build
    if ($LASTEXITCODE -ne 0) { throw 'SQL Express migration failed; requires local Windows integrated access, never a production password.' }
    Start-Process -FilePath powershell.exe -WindowStyle Hidden -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $PSCommandPath + '"'),'-Backend','dotnet') -RedirectStandardOutput (Join-Path $qaReports 'dotnet-local.log') -RedirectStandardError (Join-Path $qaReports 'dotnet-local.error.log') | Out-Null
}
if (!(Test-LocalHealth 'http://127.0.0.1:8081/api/health' 'UP')) {
    Start-Process -FilePath powershell.exe -WindowStyle Hidden -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $PSCommandPath + '"'),'-Backend','java','-JavaRepository',('"' + $JavaRepository + '"')) -RedirectStandardOutput (Join-Path $qaReports 'java-local.log') -RedirectStandardError (Join-Path $qaReports 'java-local.error.log') | Out-Null
}
$qaDeadline = (Get-Date).AddMinutes(3)
do {
    $dotnetReady = Test-LocalHealth 'http://127.0.0.1:8080/health' 'OK'
    $javaReady = Test-LocalHealth 'http://127.0.0.1:8081/api/health' 'UP'
    if ($dotnetReady -and $javaReady) {
        Write-Output 'Both local APIs are healthy. Run npm run api from tests/.'
        Write-Output '.NET: SQL Express / ConnectaOficiosNewman. Java: existing H2 test datasource (MySQL mode).'
        exit 0
    }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $qaDeadline)
throw 'Local API startup timed out. Inspect ignored reports/newman/*-local*.log.'
