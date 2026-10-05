<#
.SYNOPSIS
    Sets the environment variables for the opt-in integration tests (docs/TestingGuide.md#integration-tests)
    in the current PowerShell session, and checks that the Abacus server answers.

.EXAMPLE
    .\scripts\Set-AbacusTestEnvironment.ps1 -BaseUrl https://abacus.example.ch -Mandant 7777 -ClientId 0000-…
    dotnet test src/AbacusApi.slnx --filter "Category=Integration" --logger "console;verbosity=detailed"
#>
param(
    # Server root only, without /portal/... or /api/... (e.g. https://abacus.example.ch).
    [Parameter(Mandatory)] [Uri] $BaseUrl,

    # Abacus client (Mandant) number.
    [Parameter(Mandatory)] [int] $Mandant,

    # Client-ID of the user-dependent service user (Q910, client type "Public").
    [Parameter(Mandatory)] [string] $ClientId,

    # Must be registered for that service user in Q910.
    [Uri] $RedirectUri = "http://localhost:53682/callback",

    # Requested scopes; must be assigned to the service user (Q910) and your user (Q981).
    [string[]] $Scopes = @("openid", "profile", "email", "abacus.entity.projectbase.read"),

    # Send PKCE (the server doesn't advertise support); switch off if the login rejects it.
    [bool] $UsePkce = $true,

    # Also store the variables in the Windows user environment (for Visual Studio / Rider; restart them afterwards).
    [switch] $Persist
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\AbacusEnvironment.ps1"

$root = $BaseUrl.GetLeftPart([UriPartial]::Authority)
if ($BaseUrl.AbsolutePath -ne "/") {
    Write-Warning "Using the server root $root instead of $BaseUrl."
}

$discoveryUrl = "$root/.well-known/openid-configuration"
try {
    $discovery = Invoke-RestMethod -Uri $discoveryUrl -TimeoutSec 20
}
catch {
    throw "The Abacus server doesn't answer at $discoveryUrl : $($_.Exception.Message)"
}
Write-Host "Abacus server OK: $($discovery.issuer) (login: $($discovery.authorization_endpoint))"

# Interactive login only: drop leftovers (e.g. a ClientSecret from the demo server setup) before setting the new values.
Clear-AbacusVariables -Persist:$Persist

Set-AbacusVariable Abacus__BaseUrl $root -Persist:$Persist
Set-AbacusVariable Abacus__Mandant "$Mandant" -Persist:$Persist
Set-AbacusVariable Abacus__ClientId $ClientId -Persist:$Persist
Set-AbacusVariable Abacus__RedirectUri "$RedirectUri" -Persist:$Persist
Set-AbacusVariable Abacus__UsePkce "$UsePkce".ToLowerInvariant() -Persist:$Persist
for ($i = 0; $i -lt $Scopes.Count; $i++) {
    Set-AbacusVariable "Abacus__Scopes__$i" $Scopes[$i] -Persist:$Persist
}

Show-AbacusVariables -Persist:$Persist
