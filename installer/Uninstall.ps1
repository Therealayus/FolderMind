<#!
.SYNOPSIS
    Fully uninstalls FolderMind AI: stops the app, removes install dir,
    shortcuts, context menu, startup entry, database, logs, and secrets.
#>
$ErrorActionPreference = "Continue"

# 1. Stop any running instance (tray / app / CLI).
Get-Process -Name "AIFolderAssistant.App","FolderMind","FolderMind.Cli" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# 2. Remove startup entry.
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
    -Name "FolderMind AI" -ErrorAction SilentlyContinue

# 3. Remove Explorer context menu.
Remove-Item -Path "HKCU:\Software\Classes\Directory\shell\FolderMindAI" `
    -Recurse -Force -ErrorAction SilentlyContinue

# 4. Remove shortcuts.
Remove-Item (Join-Path ([Environment]::GetFolderPath("Programs")) "FolderMind AI.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath("Desktop")) "FolderMind AI.lnk") -Force -ErrorAction SilentlyContinue

# 5. Remove install dir + all local data (DB, logs, secrets).
Remove-Item (Join-Path $env:LOCALAPPDATA "FolderMind") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:APPDATA "FolderMind") -Recurse -Force -ErrorAction SilentlyContinue

# 6. Verify nothing is left running.
$left = Get-Process -Name "AIFolderAssistant.App","FolderMind","FolderMind.Cli" -ErrorAction SilentlyContinue
if ($left) { Write-Warning "A FolderMind process is still running; reboot to finish removal." }
else { Write-Host "FolderMind AI uninstalled completely." -ForegroundColor Green }
