[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root '.env'
if (Test-Path -LiteralPath $target) {
    throw '.env already exists. It was not overwritten.'
}
function New-Password {
    $bytes = New-Object byte[] 24
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return 'Aa1!' + [Convert]::ToBase64String($bytes)
}
$lines = @(
    "SQL_SA_PASSWORD=$(New-Password)"
    "HELPDESK_DEMO_PASSWORD=$(New-Password)"
    'API_PORT=5080'
    'SQL_PORT=14333'
)
[IO.File]::WriteAllLines($target, $lines, [Text.UTF8Encoding]::new($false))
Write-Host 'Created .env with random local passwords. Do not commit this file.'
Write-Host 'Run: docker compose up --build -d'
