<#
.SYNOPSIS
    Points the site back at the previous release.

.DESCRIPTION
    The other half of what makes timestamped folders worth having (specification section
    15). Nothing is rebuilt and nothing is downloaded — the previous folder is still on
    disk exactly as it was, so this is one minute over remote desktop.

    It shows what it is about to do and asks, because it changes what the factory is
    running.

    A bad *migration* is not undone by this. That is why section 15 makes a backup an
    explicit step before migrating: code rolls back in a minute, a schema does not.

.PARAMETER Root
    Where deployments live. Defaults to C:\Colors.

.PARAMETER To
    A specific release folder name. Defaults to the one before the live release.

.EXAMPLE
    .\Rollback.ps1
    .\Rollback.ps1 -To 2026-08-08_1430
#>
[CmdletBinding()]
param(
    [string]$Root = 'C:\Colors',
    [string]$To
)

$ErrorActionPreference = 'Stop'

# Removes the 'current' link without touching what it points at.
#
# Remove-Item on a junction asks "the item has children, are you sure?" — which stops a
# script dead, and answered wrongly with -Recurse would delete the release itself.
# Directory.Delete removes the link and nothing else.
function Remove-CurrentLink {
    param([string]$Path)

    if (Test-Path $Path) {
        [System.IO.Directory]::Delete($Path, $false)
    }
}


$releases = Join-Path $Root 'releases'
$current = Join-Path $Root 'current'

if (-not (Test-Path $releases)) {
    throw "There is nothing at $releases. Has anything been deployed here?"
}

$all = Get-ChildItem $releases -Directory | Sort-Object Name -Descending
if ($all.Count -lt 2 -and -not $To) {
    throw 'There is only one release, so there is nothing to go back to.'
}

$live = if (Test-Path $current) { (Get-Item $current).Target | Select-Object -First 1 } else { $null }

$target = if ($To) {
    $named = $all | Where-Object { $_.Name -eq $To }
    if (-not $named) { throw "There is no release called $To." }
    $named
}
else {
    # The newest that is not the live one.
    $all | Where-Object { $_.FullName -ne $live } | Select-Object -First 1
}

Write-Host "Live now:  $live"
Write-Host "Going to:  $($target.FullName)" -ForegroundColor Yellow

$answer = Read-Host 'Type yes to switch'
if ($answer -ne 'yes') {
    Write-Host 'Nothing changed.'
    return
}

Remove-CurrentLink -Path $current

New-Item -ItemType Junction -Path $current -Target $target.FullName | Out-Null

Write-Host @"

current -> $($target.FullName)

Restart the service to pick it up:

    Restart-Service ColorsErp
"@ -ForegroundColor Green
