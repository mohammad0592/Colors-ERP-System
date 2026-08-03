# Starts the backend and the frontend in two windows.
# Close a window to stop that part. See docs/running-the-system.md.

$root = $PSScriptRoot

Write-Host ""
Write-Host "Colors ERP - starting development servers" -ForegroundColor Cyan
Write-Host ""

# Warn early if a port is already taken; otherwise Vite silently moves to 5174
# and every request fails with a CORS error that looks like a bug in the code.
foreach ($port in 5211, 5173) {
    $inUse = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($inUse) {
        $owner = Get-Process -Id $inUse[0].OwningProcess -ErrorAction SilentlyContinue
        Write-Host "  Port $port is already used by $($owner.ProcessName) (PID $($owner.Id))." -ForegroundColor Yellow
        Write-Host "  Close it first, or the servers will not start correctly." -ForegroundColor Yellow
        Write-Host ""
    }
}

Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "Set-Location '$root\Backend'; Write-Host 'BACKEND  http://localhost:5211' -ForegroundColor Green; dotnet run --project src/Colors.Api"
)

# Give the API a moment, so the first page load does not hit a server that is not up.
Start-Sleep -Seconds 3

Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "Set-Location '$root\Frontend'; Write-Host 'FRONTEND  http://localhost:5173' -ForegroundColor Green; npm run dev"
)

Write-Host "  Backend   http://localhost:5211" -ForegroundColor Green
Write-Host "  Frontend  http://localhost:5173" -ForegroundColor Green
Write-Host ""
Write-Host "  Open http://localhost:5173 in the browser." -ForegroundColor Cyan
Write-Host "  Close the two windows to stop." -ForegroundColor DarkGray
Write-Host ""
