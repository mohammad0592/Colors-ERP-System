<#
.SYNOPSIS
    Publishes the system into a new timestamped folder and points the site at it.

.DESCRIPTION
    Specification section 15 asks for three things, and this is the third: deploy to
    timestamped folders, so a rollback is switching back to the previous folder rather
    than a rebuild. One minute over remote desktop, with the developer two hours away.

    Nothing is ever overwritten. Each deployment is its own folder, and the live one is
    whichever the `current` link points at:

        C:\Colors\releases\2026-08-08_1430\    <- yesterday's, still there
        C:\Colors\releases\2026-08-08_1615\    <- today's
        C:\Colors\current  ->  ...\2026-08-08_1615

    Migrations are NOT run here. Section 15 makes them a deliberate step taken after a
    backup, so they stay a decision somebody makes rather than a side effect of
    deploying. Run Migrate.ps1 when you mean to.

.PARAMETER Root
    Where deployments live. Defaults to C:\Colors.

.PARAMETER Keep
    How many old releases to keep. Older ones are removed, never the live one.

.EXAMPLE
    .\Deploy.ps1
    .\Deploy.ps1 -Root D:\Colors -Keep 10
#>
[CmdletBinding()]
param(
    [string]$Root = 'C:\Colors',
    [int]$Keep = 5
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


$repository = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$releases = Join-Path $Root 'releases'
$release = Join-Path $releases $stamp
$current = Join-Path $Root 'current'

Write-Host "Deploying to $release" -ForegroundColor Cyan

# ---------------------------------------------------------------- build

# Into the new folder from the start. A half-published folder is never the live one,
# because nothing points at it until the very last step.
New-Item -ItemType Directory -Path $release -Force | Out-Null

Write-Host 'Publishing the API...'
dotnet publish (Join-Path $repository 'Backend\src\Colors.Api\Colors.Api.csproj') `
    --configuration Release `
    --output (Join-Path $release 'api') `
    --nologo
if ($LASTEXITCODE -ne 0) { throw 'The API did not publish.' }

Write-Host 'Building the screens...'
Push-Location (Join-Path $repository 'Frontend')
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }

    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'The screens did not build.' }

    # The API serves these itself in production, so there is no second web server and no
    # cross-origin request at all.
    Copy-Item -Path 'dist\*' -Destination (Join-Path $release 'api\wwwroot') -Recurse -Force
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------- switch

# The last act, and the only one that changes what is live. Everything above could fail
# without touching the running system.
Remove-CurrentLink -Path $current

New-Item -ItemType Junction -Path $current -Target $release | Out-Null

Write-Host "`ncurrent -> $release" -ForegroundColor Green

# ---------------------------------------------------------------- tidy

# Oldest first, keeping the newest few. The live one is never among them because it is
# always the newest, but it is excluded by name as well rather than by assumption.
$old = Get-ChildItem $releases -Directory |
    Sort-Object Name -Descending |
    Select-Object -Skip $Keep |
    Where-Object { $_.FullName -ne $release }

foreach ($folder in $old) {
    Write-Host "Removing old release $($folder.Name)"
    Remove-Item $folder.FullName -Recurse -Force
}

Write-Host @"

Done. The service still points at 'current', so restart it to pick this up:

    Restart-Service ColorsErp

To go back, run Rollback.ps1 — it moves 'current' to the previous folder and
nothing is rebuilt.
"@ -ForegroundColor Cyan
