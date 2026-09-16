<#!
.SYNOPSIS
    Installs FolderMind AI (per-user, no admin): tray app + engine CLI,
    Start Menu entry, optional desktop shortcut, Explorer context menu.
    Prefers the WinUI app exe when present, else the tray app.
.EXAMPLE
    powershell -File installer\Install.ps1
    powershell -File installer\Install.ps1 -DesktopShortcut
#>
param([switch]$DesktopShortcut)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

$exe = Get-ChildItem (Join-Path $root "publish\win-*\AIFolderAssistant.App.exe") -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) {
    $exe = Get-ChildItem (Join-Path $root "publish\win-*\FolderMind.exe") -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $exe) { throw "Published exe not found. Run installer\Publish.ps1 first." }

$isApp = $exe.Name -eq "AIFolderAssistant.App.exe"
$analyzeArgs = "--analyze `"%1`""

$installDir = Join-Path $env:LOCALAPPDATA "FolderMind"
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

# Stop a running instance so files can be replaced.
Get-Process -Name "FolderMind","AIFolderAssistant.App" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# Main exe + companions (debug symbols explicitly removed afterwards —
# Get-ChildItem -Exclude is unreliable with wildcard paths).
Get-ChildItem (Join-Path $exe.Directory.FullName "*") | Copy-Item -Destination $installDir -Recurse -Force
Remove-Item (Join-Path $installDir "*.pdb") -Force -ErrorAction SilentlyContinue
$target = Join-Path $installDir $exe.Name

# Engine CLI alongside (scripting/admin), when published.
$cli = Get-ChildItem (Join-Path $root "publish\cli\win-*\FolderMind.Cli.exe") -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($cli) {
    Get-ChildItem (Join-Path $cli.Directory.FullName "*") |
        Where-Object { $_.Name -ne "FolderMind.exe" } |
        Copy-Item -Destination $installDir -Recurse -Force
    Remove-Item (Join-Path $installDir "*.pdb") -Force -ErrorAction SilentlyContinue
    $cliTarget = Join-Path $installDir "FolderMind.Cli.exe"
    if (Test-Path $cliTarget) { Write-Host "Engine CLI installed alongside." -ForegroundColor Green }
}

function New-Shortcut($path, $targetPath) {
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($path)
    $sc.TargetPath = $targetPath
    $sc.WorkingDirectory = $installDir
    $sc.Save()
}

$startMenu = Join-Path ([Environment]::GetFolderPath("Programs")) "FolderMind AI.lnk"
New-Shortcut $startMenu $target
Write-Host "Start Menu entry created." -ForegroundColor Green

if ($DesktopShortcut) {
    $desk = Join-Path ([Environment]::GetFolderPath("Desktop")) "FolderMind AI.lnk"
    New-Shortcut $desk $target
    Write-Host "Desktop shortcut created." -ForegroundColor Green
}

# Explorer context menu (per-user, no admin): right-click folder → suggest name.
$menuKey = "HKCU:\Software\Classes\Directory\shell\FolderMindAI"
New-Item -Path $menuKey -Force | Out-Null
Set-ItemProperty -Path $menuKey -Name "(Default)" -Value "Suggest Folder Name with FolderMind AI"
Set-ItemProperty -Path $menuKey -Name "Icon" -Value "`"$target`",0"
New-Item -Path "$menuKey\command" -Force | Out-Null
Set-ItemProperty -Path "$menuKey\command" -Name "(Default)" -Value "`"$target`" $analyzeArgs"
Write-Host "Explorer context menu registered." -ForegroundColor Green

Write-Host "`nFolderMind AI installed to $installDir" -ForegroundColor Cyan
if ($isApp) {
    Write-Host "Launch from Start Menu. Enable 'Start with Windows' inside Settings if desired."
} else {
    Write-Host "Launch from Start Menu: the app lives in the system tray."
    Write-Host "Right-click the tray icon for Pause/Resume, Analyze Folder, Suggestions, Exit."
}
