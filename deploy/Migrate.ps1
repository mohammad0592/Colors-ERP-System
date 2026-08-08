<#
.SYNOPSIS
    Backs the database up, then applies any pending migrations.

.DESCRIPTION
    Specification section 15: migrations are an explicit step in the deployment, with a
    backup taken immediately before. A bad migration then costs minutes instead of a day
    of production history.

    It is deliberately separate from Deploy.ps1. Code rolls back in a minute by pointing
    at the previous folder; a schema does not, and the only thing that saves you is the
    backup this takes first.

    Nothing is applied until the backup file exists and has a size.

.PARAMETER BackupFolder
    Where the dump goes. Defaults to C:\Colors\backups.

.PARAMETER ConnectionString
    The database to migrate. Defaults to the COLORS_DB environment variable.

.EXAMPLE
    .\Migrate.ps1
    .\Migrate.ps1 -BackupFolder D:\Backups
#>
[CmdletBinding()]
param(
    [string]$BackupFolder = 'C:\Colors\backups',
    [string]$ConnectionString = $env:COLORS_DB
)

$ErrorActionPreference = 'Stop'

if (-not $ConnectionString) {
    throw 'No connection string. Set COLORS_DB or pass -ConnectionString.'
}

$repository = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'

# ---------------------------------------------------------------- backup

New-Item -ItemType Directory -Path $BackupFolder -Force | Out-Null
$backup = Join-Path $BackupFolder "colors_erp_$stamp.dump"

Write-Host "Backing up to $backup" -ForegroundColor Cyan

$pgDump = Get-ChildItem 'C:\Program Files\PostgreSQL' -Filter 'pg_dump.exe' -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $pgDump) {
    throw 'pg_dump.exe was not found. PostgreSQL must be installed on this machine.'
}

# The custom format, because it restores selectively and compresses. A plain SQL file of
# a year of production is slow to write and slower to read back.
& $pgDump --dbname=$ConnectionString --format=custom --file=$backup
if ($LASTEXITCODE -ne 0) { throw 'The backup failed. Nothing has been migrated.' }

$size = (Get-Item $backup).Length
if ($size -lt 1024) {
    throw "The backup is only $size bytes, which cannot be right. Nothing has been migrated."
}

Write-Host ("Backup written, {0:N1} MB" -f ($size / 1MB)) -ForegroundColor Green

# ---------------------------------------------------------------- migrate

Write-Host "`nApplying migrations..." -ForegroundColor Cyan

$env:ConnectionStrings__ColorsDb = $ConnectionString

dotnet ef database update `
    --project (Join-Path $repository 'Backend\src\Colors.Infrastructure') `
    --startup-project (Join-Path $repository 'Backend\src\Colors.Api')

if ($LASTEXITCODE -ne 0) {
    Write-Host @"

The migration failed. The database may be part-migrated.

Restore it with:

    pg_restore --dbname="$ConnectionString" --clean --if-exists "$backup"
"@ -ForegroundColor Red
    throw 'Migration failed.'
}

Write-Host @"

Done. The backup is kept at:

    $backup

Keep it until the new version has run a full shift. Section 15 also asks that backups
go to an external drive, not only this server — a backup stored on the machine it
protects is not a backup.
"@ -ForegroundColor Green
