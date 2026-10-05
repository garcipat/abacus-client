<#
.SYNOPSIS
    Removes all Abacus__* integration test variables from the current PowerShell session and from the Windows user
    environment (where -Persist stored them). Restart Visual Studio / Rider afterwards.
#>
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\AbacusEnvironment.ps1"

Clear-AbacusVariables -Persist
Write-Host "Removed all Abacus__* variables from this session and from your Windows user environment."
