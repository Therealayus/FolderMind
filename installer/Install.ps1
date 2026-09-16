<#!
.SYNOPSIS
    Installs FolderMind AI (per-user, no admin): copies the published exe,
    creates Start Menu (+optional desktop) shortcut, registers the Explorer
    context menu. Prefers the WinUI app exe, falls back to the engine host.
.EXAMPLE
    powershell -File installer\Install.ps1
    powershell -File installer\Install.ps1 -DesktopShortcut
#>
param([switch]$DesktopShortcut)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

$exe = Get-ChildItem (Join-Path $root "publish\win-*\AIFolderAssistant.App.exe") -ErrorAction SilentlyContinue `
    | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) {
    $exe = Get-ChildItem (Join-Path $root "publish\win-*\FolderMind.exe") -ErrorAction SilentlyContinue `
        | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $exe) { throw "Published exe not found. Run installer\Publish.ps1 first." }

$installDir = Join-Path $env:LOCALAPPDATA "FolderMind"
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item (Join-Path $exe.Directory.FullName "*") $installDir -Recurse -Force
$target = Join-Path $installDir $exe.Name

function New-Shortcut($path, $targetPath) {
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($path)
    $sc.TargetPath = $targetPath
    $sc.WorkingDirectory = $installDir
    $sc.Save()
}

$isCli = $exe.Name -eq "FolderMind.exe"
$shortcutArgs = if ($isCli) { "" } else { "" }

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
$analyzeArgs = if ($isCli) { "analyze `"%1`"" } else { "--analyze `"%1`"" }
Set-ItemProperty -Path "$menuKey\command" -Name "(Default)" -Value "`"$target`" $analyzeArgs"
Write-Host "Explorer context menu registered." -ForegroundColor Green

Write-Host "`nFolderMind AI installed to $installDir" -ForegroundColor Cyan
if ($isCli) {
    Write-Host "Engine host installed. Try: FolderMind analyze <folder>"
    Write-Host "The full WinUI shell builds in Visual Studio 2022 (see DEVELOPMENT.md)."
} else {
    Write-Host "Launch from Start Menu. Enable 'Start with Windows' inside Settings if desired."
}
