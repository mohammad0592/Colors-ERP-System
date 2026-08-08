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

# Runs a command line tool and fails only if the tool itself failed.
#
# `$ErrorActionPreference = 'Stop'` is right for everything else in this script, but for
# an external program it is actively wrong: PowerShell treats anything the program writes
# to stderr as a terminating error, whatever the program's exit code was. npm writes
# warnings there as a matter of course — a deprecated package, a node version it would
# have preferred — so the first harmless warning killed the deployment with a
# "NativeCommandError" that reads like a crash.
#
# The exit code is the tool's actual verdict, so that is what is checked.
function Invoke-Tool {
    param(
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][scriptblock]$Command
    )

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command 2>&1 | ForEach-Object { Write-Host $_ }
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed (exit code $LASTEXITCODE)."
    }
}

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

$apiOutput = Join-Path $release 'api'

Write-Host 'Publishing the API...'
Invoke-Tool 'Publishing the API' {
    dotnet publish (Join-Path $repository 'Backend\src\Colors.Api\Colors.Api.csproj') `
        --configuration Release `
        --output $apiOutput `
        --nologo
}

Write-Host 'Building the screens...'
Push-Location (Join-Path $repository 'Frontend')
try {
    # `ci`, not `install`: it installs exactly what the lock file says and nothing else,
    # so what goes to the factory is what was tested rather than whatever was newest
    # this morning.
    Invoke-Tool 'Installing the screen packages' { npm ci }
    Invoke-Tool 'Building the screens' { npm run build }

    # The API serves these itself in production, so there is no second web server and no
    # cross-origin request at all.
    $wwwroot = Join-Path $apiOutput 'wwwroot'
    New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null
    Copy-Item -Path 'dist\*' -Destination $wwwroot -Recurse -Force
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

# ---------------------------------------------------------------- settings check

# The two settings a published build cannot supply for itself. In development they come
# from `dotnet user-secrets`, which is a developer's tool reading a developer's Windows
# profile — a published build knows nothing about it.
#
# Checked here rather than left to be discovered: the API validates them on startup and
# refuses to run, so without this the first sign that anything is wrong is a service that
# will not start, at whatever hour somebody chose to deploy.
#
# This is a warning and not a failure. The deployment itself is sound; it is the machine
# that is not ready, and saying so is more use than refusing to publish.
$required = @('ConnectionStrings__ColorsDb', 'Jwt__SigningKey')
$missing = $required | Where-Object {
    -not [Environment]::GetEnvironmentVariable($_, 'Machine')
}

if ($missing) {
    Write-Host "`nThis machine is missing settings the API needs to start:" -ForegroundColor Yellow
    foreach ($name in $missing) { Write-Host "    $name" -ForegroundColor Yellow }
    Write-Host @"

Set each one for the whole machine, not just for you, or the service will not
see it:

    [Environment]::SetEnvironmentVariable('NAME', 'value', 'Machine')

See docs/running-the-system.md, "First time on the factory server".
"@ -ForegroundColor Yellow
}

Write-Host @"

Done. The service still points at 'current', so restart it to pick this up:

    Restart-Service ColorsErp

To go back, run Rollback.ps1 — it moves 'current' to the previous folder and
nothing is rebuilt.
"@ -ForegroundColor Cyan
