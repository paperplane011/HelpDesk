[CmdletBinding()]
param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$settings = @{}
Get-Content -LiteralPath (Join-Path $root '.env') | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=(.*)$') { $settings[$matches[1]] = $matches[2] }
}
$password = $settings['HELPDESK_DEMO_PASSWORD']
if (-not $password) { throw 'No demo password in .env. Run New-LocalSettings.ps1 first.' }
function Login([string]$email) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    $result = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login?useCookies=false" -Method Post -ContentType 'application/json' -Body $body
    return @{ Authorization = "Bearer $($result.accessToken)" }
}
function Send([string]$method, [string]$path, $headers, $body) {
    $request = @{ Uri = "$BaseUrl$path"; Method = $method; Headers = $headers }
    if ($null -ne $body) {
        $request['ContentType'] = 'application/json'
        $request['Body'] = $body | ConvertTo-Json
    }
    Invoke-RestMethod @request
}
function Assert-Status([string]$method, [string]$path, $headers, $body, [int]$expected) {
    try {
        $null = Send $method $path $headers $body
        throw "Expected HTTP $expected but request succeeded."
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $expected) { throw }
    }
}
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    try {
        $null = Invoke-RestMethod -Uri "$BaseUrl/health/ready" -TimeoutSec 3
        $ready = $true
        break
    } catch { Start-Sleep -Seconds 2 }
}
if (-not $ready) { throw 'API did not become ready. Inspect docker compose logs api db.' }
$alice = Login 'alice@example.test'
$bob = Login 'bob@example.test'
$agent = Login 'support@example.test'
$ticket = Send 'Post' '/api/tickets' $alice @{ title = 'Smoke test VPN'; description = 'Created by Smoke.ps1'; priority = 'High' }
$path = "/api/tickets/$($ticket.id)"
Assert-Status 'Get' $path $bob $null 404
$ticket = Send 'Post' "$path/take" $agent @{ version = $ticket.version }
$ticket = Send 'Post' "$path/comments" $agent @{ body = 'Connection restored'; version = $ticket.version }
$ticket = Send 'Post' "$path/close" $agent @{ version = $ticket.version }
Assert-Status 'Put' $path $alice @{ title = 'Blocked edit'; description = 'Closed'; priority = 'Low'; version = $ticket.version } 409
$ticket = Send 'Post' "$path/reopen" $alice @{ version = $ticket.version }
if ($ticket.status -ne 'Open') { throw 'Reopen did not produce Open status.' }
Write-Host "PASS: login, create, foreign-ticket 404, take, comment, close, closed-edit 409, reopen. Ticket: $($ticket.id)"
