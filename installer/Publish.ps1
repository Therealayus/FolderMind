<#!
.SYNOPSIS
    Publishes FolderMind AI as a self-contained single-file unpackaged exe.
.EXAMPLE
    powershell -File installer\Publish.ps1 -Platform x64 -Configuration Release
#>
param(
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$rid = "win-$($Platform.ToLower())"
$outDir = Join-Path $root "publish\$rid"

Write-Host "Publishing FolderMind AI ($Configuration|$Platform, $rid)..." -ForegroundColor Cyan
& "C:\Program Files\dotnet\dotnet" publish `
    (Join-Path $root "AIFolderAssistant.App\AIFolderAssistant.App.csproj") `
    -c $Configuration -p:Platform=$Platform -r $rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
Write-Host "Published to $outDir" -ForegroundColor Green
Write-Host "NOTE: WinUI packaging (XAML compile) requires Visual Studio 2022. In VS: Build > Publish > Folder."
