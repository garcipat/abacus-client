<#
.SYNOPSIS
    Points the client-credentials integration tests at one of Abacus' public test servers (Mandant 7777).

.DESCRIPTION
    Abacus runs a test server per version (https://apihub.abacus.ch/odata, tab "Testservers"). Each has a
    pre-configured service user: open the server's /createuser page and double-click the Abacus icon in the top-left
    corner to see its Client-ID and Client-Secret. This script asks for them (the secret is typed hidden), checks that
    the server answers, and sets the environment variables in the current PowerShell session.

    Rules from Abacus: no customer or private data on these servers; they run pre-release builds, are reinstalled
    regularly and erase all data after at most 45 days.

.EXAMPLE
    .\scripts\Set-AbacusDemoServerEnvironment.ps1
    dotnet test src/AbacusApi.slnx --filter "Category=Integration" --logger "console;verbosity=detailed"
#>
param(
    [ValidateSet("2026", "2025", "2024")] [string] $Version = "2026",

    # Client-ID of the pre-configured service user (asked for if not given).
    [string] $ClientId,

    # Client-Secret of that service user (asked for, hidden, if not given).
    [SecureString] $ClientSecret,

    # Scopes to request. Empty: Abacus grants all scopes assigned to the service user.
    [string[]] $Scopes = @(),

    # Also store the variables in the Windows user environment (for Visual Studio / Rider; restart them afterwards).
    # The secret is then stored in plain text in HKCU\Environment; fine for the shared demo service user.
    [switch] $Persist
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\AbacusEnvironment.ps1"

$servers = @{
    "2026" = "https://entity-api1-6.demo.abacus.ch"
    "2025" = "https://entity-api1-5.demo.abacus.ch"
    "2024" = "https://entity-api1-4.demo.abacus.ch"
}
$baseUrl = $servers[$Version]

try {
    $discovery = Invoke-RestMethod -Uri "$baseUrl/.well-known/openid-configuration" -TimeoutSec 20
}
catch {
    throw "The Abacus test server $baseUrl doesn't answer (they come without availability guarantee): $($_.Exception.Message)"
}
Write-Host "Abacus test server OK: $($discovery.issuer)"

if (-not $ClientId -or -not $ClientSecret) {
    Write-Host "Open $baseUrl/createuser and double-click the Abacus icon in the top-left corner to see the service user's credentials."
}
if (-not $ClientId) {
    $ClientId = Read-Host "Client-ID"
}
if (-not $ClientSecret) {
    $ClientSecret = Read-Host "Client-Secret" -AsSecureString
}
$secret = [System.Net.NetworkCredential]::new("", $ClientSecret).Password
if (-not $ClientId -or -not $secret) {
    throw "Client-ID and Client-Secret are required."
}

# Client credentials only: drop leftovers of the interactive setup, so the interactive tests stay skipped.
Clear-AbacusVariables -Persist:$Persist

Set-AbacusVariable Abacus__BaseUrl $baseUrl -Persist:$Persist
Set-AbacusVariable Abacus__Mandant "7777" -Persist:$Persist
Set-AbacusVariable Abacus__ClientId $ClientId -Persist:$Persist
Set-AbacusVariable Abacus__ClientSecret $secret -Persist:$Persist
for ($i = 0; $i -lt $Scopes.Count; $i++) {
    Set-AbacusVariable "Abacus__Scopes__$i" $Scopes[$i] -Persist:$Persist
}

Show-AbacusVariables -Persist:$Persist
