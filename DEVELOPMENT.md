# Development

## Prerequisites

- Windows 10 1809+ / 11, x64 recommended
- .NET 8 SDK (`dotnet --version` → 8.x)
- Visual Studio 2022 17.8+ with workloads:
  - **.NET desktop development**
  - **Windows application development** (Windows App SDK)
- Git

No WSL, Docker, or Node.js required.

## Build matrix

| Project | Builds with `dotnet` CLI | Requires VS |
|---|---|---|
| Core | ✅ | — |
| Infrastructure | ✅ | — |
| Tests | ✅ | — |
| App (WinUI 3) | ❌ (XAML compiler needs full MSBuild) | ✅ |

This is standard for WinUI 3: the XAML markup compiler (`XamlCompiler.exe`)
requires Visual Studio MSBuild. CI should use `windows-latest` with VS
preinstalled. All domain/infrastructure logic and the full test suite build
and run with the .NET SDK alone.

## Commands (PowerShell)

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet"

# Restore / build / test (always pass -p:Platform=x64; the projects declare Platforms)
& $dotnet restore "D:\FolderMind\AIFolderAssistant.Core\AIFolderAssistant.Core.csproj" -p:Platform=x64
& $dotnet build   "D:\FolderMind\AIFolderAssistant.Core\AIFolderAssistant.Core.csproj" -c Release -p:Platform=x64
& $dotnet build   "D:\FolderMind\AIFolderAssistant.Infrastructure\AIFolderAssistant.Infrastructure.csproj" -c Release -p:Platform=x64
& $dotnet test    "D:\FolderMind\AIFolderAssistant.Tests\AIFolderAssistant.Tests.csproj" -c Release -p:Platform=x64

# Full solution in Visual Studio: open D:\FolderMind\FolderMind.sln, x64, F5.
```

## Debugging

- Logs: `%AppData%\FolderMind\logs\foldermind-<date>.log` (Serilog, 14-day retention).
- Diagnostics page in-app: recent entries, AI usage, export-to-file.
- Database: `%AppData%\FolderMind\database\FolderMind.db` (open with DB Browser for SQLite).
- Increase verbosity: Settings → Advanced → LogLevel.

## Resetting state

```powershell
Remove-Item "$env:APPDATA\FolderMind\database\FolderMind.db" -Force  # recreated on launch
Remove-Item "$env:APPDATA\FolderMind\logs\*" -Force
```

## Packaging

```powershell
# Self-contained single-file publish (unpackaged exe)
powershell -File D:\FolderMind\installer\Publish.ps1 -Platform x64 -Configuration Release
# Installs: Start Menu entry, optional desktop shortcut, Explorer context menu
powershell -File D:\FolderMind\installer\Install.ps1
powershell -File D:\FolderMind\installer\Uninstall.ps1
```

MSIX/Store packaging: add a Windows Application Packaging Project in VS
wrapping `AIFolderAssistant.App` (standard flow, not scripted here).

## Conventions

- Nullable reference types ON; warnings treated seriously.
- Async with `CancellationToken` on all I/O; UI thread never blocked.
- No business logic in code-behind; ViewModels via CommunityToolkit.Mvvm.
- New file types: extend `SimpleContentExtractor.TextExtensions` + classifier map.
- New settings: extend `AppSettings` + `SettingsViewModel` load/save.
