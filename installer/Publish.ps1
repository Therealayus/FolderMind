<#!
.SYNOPSIS
    Publishes FolderMind AI as a self-contained single-file exe.
    - Engine host (CLI): builds headless with the .NET SDK.
    - WinUI app: requires Visual Studio 2022 (XAML compiler needs full MSBuild).
.EXAMPLE
    powershell -File installer\Publish.ps1 -Platform x64 -Configuration Release
    powershell -File installer\Publish.ps1 -Platform x64 -Target App   # in VS Developer shell
#>
param(
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Cli", "App")]
    [string]$Target = "Cli"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$rid = "win-$($Platform.ToLower())"
$outDir = Join-Path $root "publish\$rid"

$project = if ($Target -eq "App") {
    if ($Target -eq "App") {
        Write-Host "NOTE: the WinUI App project requires Visual Studio 2022 (Build > Publish), the dotnet CLI cannot compile XAML." -ForegroundColor Yellow
    }
    Join-Path $root "AIFolderAssistant.App\AIFolderAssistant.App.csproj"
} else {
    Join-Path $root "AIFolderAssistant.Cli\AIFolderAssistant.Cli.csproj"
}

Write-Host "Publishing FolderMind ($Target, $Configuration|$Platform, $rid)..." -ForegroundColor Cyan
& "C:\Program Files\dotnet\dotnet" publish $project `
    -c $Configuration -p:Platform=$Platform -r $rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
Write-Host "Published to $outDir" -ForegroundColor Green
Get-ChildItem (Join-Path $outDir "*.exe") | Select-Object Name, @{N="MB";E={[math]::Round($_.Length/1MB,1)}}
