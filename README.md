# FolderMind AI

**FolderMind AI** is a native Windows 10/11 utility that watches the folders you choose
and suggests meaningful names based on their contents. It never renames anything
without your explicit confirmation.

Example: a folder containing `invoice_123.pdf`, `gst_receipt.pdf`, `payment_receipt.pdf`
gets the suggestion **Financial Documents** with an explanation and a confidence band.

## Principles

- **Privacy first** — local analysis is ON, cloud AI is OFF by default.
- **User control** — no automatic renames, ever.
- **Local first** — the deterministic offline engine works without internet or API keys.
- **Explainable** — every suggestion carries a reason.
- **Lightweight** — event-driven monitoring with debouncing; quiet when nothing changes.
- **Reliable** — a bad file, locked PDF, or dead network never crashes the app.
- **Native** — WinUI 3, system tray, notifications, startup, Explorer context menu.

## Requirements

- Windows 10 version 1809+ / Windows 11 (x64; x86/ARM64 also buildable)
- .NET 8 SDK (for building Core/Infrastructure/Tests)
- Visual Studio 2022 17.8+ with the **Windows App SDK** workload (for the WinUI 3 App project —
  XAML compilation requires full MSBuild; see DEVELOPMENT.md)
- Windows App Runtime 1.6+ (installed automatically by the installer script)

## Quick start (engine + tests, no VS required)

```powershell
# Build the engine libraries
& "C:\Program Files\dotnet\dotnet" build "D:\FolderMind\AIFolderAssistant.Core\AIFolderAssistant.Core.csproj" -c Release -p:Platform=x64
& "C:\Program Files\dotnet\dotnet" build "D:\FolderMind\AIFolderAssistant.Infrastructure\AIFolderAssistant.Infrastructure.csproj" -c Release -p:Platform=x64

# Run the test suite (40 tests, no network / no API keys needed)
& "C:\Program Files\dotnet\dotnet" test "D:\FolderMind\AIFolderAssistant.Tests\AIFolderAssistant.Tests.csproj" -c Release -p:Platform=x64
```

## Install (recommended)

```powershell
powershell -File D:\FolderMind\installer\Publish.ps1   # builds Tray + CLI
powershell -File D:\FolderMind\installer\Install.ps1   # Start Menu, tray app, context menu
```

This installs the **tray app** (`FolderMind.exe`: lives in the system tray,
balloon notifications, suggestions window, pause/resume, start-with-Windows)
plus the **engine CLI** (`FolderMind.Cli.exe`) for scripting. No console window
ever flashes: both entry points are windowed/headless-safe.

## Running the full WinUI app

1. Open `FolderMind.sln` in Visual Studio 2022 (required for XAML compilation).
2. Set `AIFolderAssistant.App` as startup project, platform **x64**.
3. Press F5. On first run: pick folders to monitor → keep Local-only mode → Ready.
4. The app lives in the system tray. Create a folder, drop files in, wait ~3s, get a suggestion.

## How it works

```
Folder activity → debounce (3s) → collect files → metadata + text extraction
  → keyword classification → candidate names → confidence → suggestion
  → notify (if ≥ threshold) → user clicks Rename → safe rename → history
```

Cloud AI (optional, off by default) receives only a structured **summary**
(file count, extensions, topics, sample names) — never full documents.

## Project layout

```
D:\FolderMind\
├── AIFolderAssistant.Core/            # domain: analysis pipeline, rules, services (net8.0, testable)
├── AIFolderAssistant.Infrastructure/  # SQLite, FileSystemWatcher, DPAPI, OpenAI provider, registry
├── AIFolderAssistant.App/             # WinUI 3 shell: tray, pages, ViewModels, DI composition
├── AIFolderAssistant.Tests/           # xUnit: 40 tests, MockAIProvider, no external services
├── installer/                         # publish + install/uninstall PowerShell scripts
├── README.md ARCHITECTURE.md DEVELOPMENT.md PRIVACY.md SECURITY.md TROUBLESHOOTING.md
```

## Commands

| Task | Command |
|---|---|
| Restore | `dotnet restore <csproj> -p:Platform=x64` |
| Build | `dotnet build <csproj> -c Release -p:Platform=x64` |
| Test | `dotnet test Tests.csproj -c Release -p:Platform=x64` |
| Publish | `powershell -File installer/Publish.ps1 -Platform x64 -Configuration Release` |
| Install | `powershell -File installer/Install.ps1` (Start Menu, context menu) |
| Uninstall | `powershell -File installer/Uninstall.ps1` (stops app, removes everything) |
| Reset DB | delete `%AppData%\FolderMind\database\FolderMind.db` (recreated on launch) |
| Logs | `%AppData%\FolderMind\logs\` or Settings → Diagnostics → Open logs folder |

## Documentation

- `ARCHITECTURE.md` — clean-architecture overview, pipeline, extension points
- `DEVELOPMENT.md` — setup, build, debug, packaging notes (incl. VS requirement for WinUI)
- `PRIVACY.md` — data handling, what is/isn't sent, key storage
- `SECURITY.md` — threat model, rename safety, path validation, reporting
- `TROUBLESHOOTING.md` — common issues and fixes
