param(
    [string]$DotnetBase = 'http://127.0.0.1:8080',
    [string]$JavaBase = 'http://127.0.0.1:8081',
    [Parameter(Mandatory=$true)][string]$ProtectedPath,
    [string]$Password = 'LocalOnly-Connecta-2026!'
)
$ErrorActionPreference = 'Stop'
foreach ($url in @($DotnetBase, $JavaBase)) {
    if ($url -notmatch '^http://(localhost|127\.0\.0\.1)(:\d+)?$') { throw 'Solo servicios locales.' }
}
if (!$ProtectedPath.StartsWith('/') -or $ProtectedPath.StartsWith('//')) { throw 'Especificar ruta Java relativa.' }
foreach ($prefix in @('cliente','trabajador','admin','adminprincipal')) {
    $body = @{correo="$prefix.test@connectaoficios.local"; password=$Password} | ConvertTo-Json
    $login = Invoke-RestMethod "$DotnetBase/api/users/login" -Method Post -ContentType 'application/json' -Body $body
    $response = Invoke-WebRequest "$JavaBase$ProtectedPath" -Headers @{Authorization="Bearer $($login.token)"} -SkipHttpErrorCheck
    if ([int]$response.StatusCode -notin @(200,403)) { throw "Java no acepta JWT para $prefix" }
    # Change the first signature character; avoid base64url padding-equivalent mutations.
    $parts = $login.token.Split('.')
    $replacement = if ($parts[2][0] -eq 'A') { 'B' } else { 'A' }
    $parts[2] = $replacement + $parts[2].Substring(1)
    $invalid = Invoke-WebRequest "$JavaBase$ProtectedPath" -Headers @{Authorization="Bearer $($parts -join '.')"} -SkipHttpErrorCheck
    if ([int]$invalid.StatusCode -ne 401) { throw "Java debe rechazar firma manipulada: $prefix" }
    Write-Host "$prefix : JWT aceptado (HTTP $($response.StatusCode)); firma manipulada rechazada."
}
Write-Host 'Verificar ID y rol de cada usuario en una respuesta Java que exponga el principal; un 403 solo demuestra denegacion, no identidad correcta.'
