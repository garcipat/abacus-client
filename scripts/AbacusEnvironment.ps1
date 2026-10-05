# Shared by the Set-/Clear-Abacus*Environment scripts (dot-sourced). The Abacus__* variables live in the current
# PowerShell session, and with -Persist also in the Windows user environment (HKCU\Environment), so programs started
# afterwards (Visual Studio, Rider, a new terminal) see them too.

function Set-AbacusVariable([string] $Name, [string] $Value, [switch] $Persist) {
    Set-Item "Env:$Name" $Value
    if ($Persist) {
        [Environment]::SetEnvironmentVariable($Name, $Value, "User")
    }
}

function Clear-AbacusVariables([switch] $Persist) {
    Get-ChildItem Env: | Where-Object Name -like "Abacus__*" | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    if ($Persist) {
        [Environment]::GetEnvironmentVariables("User").Keys |
            Where-Object { $_ -like "Abacus__*" } |
            ForEach-Object { [Environment]::SetEnvironmentVariable($_, $null, "User") }
    }
}

function Show-AbacusVariables([switch] $Persist) {
    Get-ChildItem Env: | Where-Object Name -like "Abacus__*" | Sort-Object Name |
        Select-Object Name, @{ Name = "Value"; Expression = { if ($_.Name -like "*Secret*") { "********" } else { $_.Value } } } |
        Format-Table -AutoSize
    if ($Persist) {
        Write-Host "Also stored in your Windows user environment. Restart Visual Studio / Rider / terminals to pick them up; remove them with .\scripts\Clear-AbacusTestEnvironment.ps1."
    }
    else {
        Write-Host "Set for this PowerShell window only. Use -Persist to keep them for Visual Studio / Rider."
    }
    Write-Host "Run: dotnet test src/AbacusApi.slnx --filter `"Category=Integration`" --logger `"console;verbosity=detailed`""
}
