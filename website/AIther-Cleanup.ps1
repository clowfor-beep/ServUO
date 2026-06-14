# AIther - Full Cleanup Script
# Removes all traces of a previous install for clean testing

Write-Host "Cleaning up AIther installation..." -ForegroundColor Cyan

# 1. Run the official uninstaller if present
$uninstaller = "$env:LOCALAPPDATA\AIther\unins000.exe"
if (Test-Path $uninstaller) {
    Write-Host "Running uninstaller..."
    Start-Process $uninstaller "/SILENT" -Wait
}

# 2. Remove install folder
$installDir = "$env:LOCALAPPDATA\AIther"
if (Test-Path $installDir) {
    Write-Host "Removing $installDir..."
    Remove-Item $installDir -Recurse -Force
}

# 3. Remove ClassicUO Launcher DB and config
$launcherDir = "$env:APPDATA\ClassicUOLauncher"
if (Test-Path $launcherDir) {
    Write-Host "Removing ClassicUO Launcher data ($launcherDir)..."
    Remove-Item $launcherDir -Recurse -Force
}

# 4. Remove Start Menu shortcut
$startMenu = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\AIther"
if (Test-Path $startMenu) {
    Write-Host "Removing Start Menu shortcuts..."
    Remove-Item $startMenu -Recurse -Force
}

# 5. Remove desktop shortcut
$desktop = "$env:USERPROFILE\Desktop\AIther.lnk"
if (Test-Path $desktop) {
    Write-Host "Removing desktop shortcut..."
    Remove-Item $desktop -Force
}

Write-Host ""
Write-Host "Done! Clean slate ready for fresh install." -ForegroundColor Green
