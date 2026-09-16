<#!
.SYNOPSIS
    Publishes FolderMind AI as self-contained single-file exes.
    - Tray (default): system-tray background app, builds headless with the .NET SDK.
    - Cli: engine-host console for scripting/admin, builds headless.
    - App (WinUI): requires Visual Studio 2022 (XAML compiler needs full MSBuild).
.EXAMPLE
    powershell -File installer\Publish.ps1                        # Tray + Cli, x64 Release
    powershell -File installer\Publish.ps1 -Target Cli
#>
param(
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("All", "Tray", "Cli", "App")]
    [string]$Target = "All"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$rid = "win-$($Platform.ToLower())"
$dotnet = "C:\Program Files\dotnet\dotnet"

function Publish-Project($name, $project, $outDir) {
    Write-Host "Publishing $name ($Configuration|$Platform, $rid)..." -ForegroundColor Cyan
    & $dotnet publish $project `
        -c $Configuration -p:Platform=$Platform -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none -p:DebugSymbols=false `
        -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $name." }
    Get-ChildItem (Join-Path $outDir "*.exe") | Select-Object Name, @{N="MB";E={[math]::Round($_.Length/1MB,1)}}
}

if ($Target -in @("All", "Tray")) {
    Publish-Project "Tray" (Join-Path $root "AIFolderAssistant.Tray\AIFolderAssistant.Tray.csproj") (Join-Path $root "publish\$rid")
}
if ($Target -in @("All", "Cli")) {
    Publish-Project "Cli" (Join-Path $root "AIFolderAssistant.Cli\AIFolderAssistant.Cli.csproj") (Join-Path $root "publish\cli\$rid")
}
if ($Target -eq "App") {
    Write-Host "The WinUI App project requires Visual Studio 2022: open FolderMind.sln, Build > Publish." -ForegroundColor Yellow
}
