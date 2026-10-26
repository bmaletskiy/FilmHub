param(
    [string]$BaseUrl = 'http://localhost:5103',
    [int]$GenreId = 0
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command newman -ErrorAction SilentlyContinue)) {
    throw 'Install Node.js LTS, then run: npm install --global newman@6'
}
$source = Join-Path $PSScriptRoot 'FilmHub-QA1.postman_collection.json'
$state = Join-Path $PSScriptRoot '.qa1-state.json'
if (Test-Path -LiteralPath $state) { $source = $state }
$arguments = @('run', $source, '--collection-var', "baseUrl=$($BaseUrl.TrimEnd('/'))",
    '--export-collection', $state, '--reporters', 'cli', '--timeout-request', '15000')
if ($GenreId -gt 0) { $arguments += @('--collection-var', "genreId=$GenreId") }
& newman @arguments
exit $LASTEXITCODE
