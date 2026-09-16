<#!
.SYNOPSIS
    Installs FolderMind AI: Start Menu entry, optional desktop shortcut,
    Explorer context menu. No admin required (per-user install).
.EXAMPLE
    powershell -File installer\Install.ps1
    powershell -File installer\Install.ps1 -DesktopShortcut
#>
param([switch]$DesktopShortcut)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$exe = Get-ChildItem (Join-Path $root "publish\win-*\AIFolderAssistant.App.exe") `
    | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { throw "Published exe not found. Run installer\Publish.ps1 first (in Visual Studio)." }

$installDir = Join-Path $env:LOCALAPPDATA "FolderMind"
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item (Join-Path $exe.Directory.FullName "*") $installDir -Recurse -Force
$target = Join-Path $installDir "AIFolderAssistant.App.exe"

function New-Shortcut($path, $targetPath, $args) {
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($path)
    $sc.TargetPath = $targetPath
    if ($args) { $sc.Arguments = $args }
    $sc.WorkingDirectory = $installDir
    $sc.Save()
}

$startMenu = Join-Path ([Environment]::GetFolderPath("Programs")) "FolderMind AI.lnk"
New-Shortcut $startMenu $target ""
Write-Host "Start Menu entry created." -ForegroundColor Green

if ($DesktopShortcut) {
    $desk = Join-Path ([Environment]::GetFolderPath("Desktop")) "FolderMind AI.lnk"
    New-Shortcut $desk $target ""
    Write-Host "Desktop shortcut created." -ForegroundColor Green
}

# Explorer context menu (per-user, no admin): right-click folder → suggest name.
$menuKey = "HKCU:\Software\Classes\Directory\shell\FolderMindAI"
New-Item -Path $menuKey -Force | Out-Null
Set-ItemProperty -Path $menuKey -Name "(Default)" -Value "Suggest Folder Name with FolderMind AI"
Set-ItemProperty -Path $menuKey -Name "Icon" -Value "`"$target`",0"
New-Item -Path "$menuKey\command" -Force | Out-Null
Set-ItemProperty -Path "$menuKey\command" -Name "(Default)" -Value "`"$target`" --analyze `"%1`""
Write-Host "Explorer context menu registered." -ForegroundColor Green

Write-Host "`nFolderMind AI installed to $installDir" -ForegroundColor Cyan
Write-Host "Launch from Start Menu. Enable 'Start with Windows' inside Settings if desired."
