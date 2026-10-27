param(
    [string]$BaseUrl = 'http://localhost:8081',
    [switch]$Headed
)
$ErrorActionPreference = 'Stop'
$uri = $null
if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$uri) -or
    $uri.Scheme -notin @('http', 'https') -or
    $uri.Host -notin @('localhost', '127.0.0.1', '[::1]') -or
    $uri.AbsolutePath -ne '/' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) {
    throw 'Only a loopback HTTP(S) origin is allowed, for example http://localhost:8081.'
}
$project = Join-Path $PSScriptRoot 'FilmHub.E2E.Tests.csproj'
$oldUrl = $env:FILMHUB_E2E_BASE_URL
$oldHeaded = $env:FILMHUB_E2E_HEADED
try {
    $env:FILMHUB_E2E_BASE_URL = $BaseUrl
    $env:FILMHUB_E2E_HEADED = if ($Headed) { '1' } else { '0' }
    & dotnet build $project --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'E2E build failed.' }
    & (Join-Path $PSScriptRoot 'bin/Debug/net10.0/playwright.ps1') install chromium
    if ($LASTEXITCODE -ne 0) { throw 'Chromium installation failed.' }
    & dotnet test $project --no-build --no-restore --logger 'console;verbosity=normal' --logger trx
    if ($LASTEXITCODE -ne 0) { throw 'E2E tests failed. Inspect the test output and TestResults artifacts.' }
}
finally {
    $env:FILMHUB_E2E_BASE_URL = $oldUrl
    $env:FILMHUB_E2E_HEADED = $oldHeaded
}
