# Troubleshooting

## App doesn't suggest names

1. Settings → Monitoring → **Enable monitoring** must be ON.
2. The folder must be listed under Monitored Folders **and enabled**.
3. Wait for the debounce window (default 3s) after activity settles.
4. Suggestions below the confidence threshold (default 0.80) are stored but
   not notified — check the Suggestions page.
5. Check `%AppData%\FolderMind\logs\` for analysis errors.

## Tray icon missing

- The app hides to tray on close — look in the overflow (`^`) area.
- If truly gone, relaunch from Start Menu → "FolderMind AI".
- "Exit" in the tray menu is the only way to terminate the background process.

## Database locked / corrupt

```powershell
Stop-Process -Name "AIFolderAssistant.App" -ErrorAction SilentlyContinue
Remove-Item "$env:APPDATA\FolderMind\database\FolderMind.db" -Force
```

A fresh database is created on next launch (settings reset to defaults).

## Cloud AI errors

- Cloud is OFF by default. To use it: Settings → AI → enable, set an
  OpenAI-compatible endpoint + model, allow cloud, save the API key
  (stored DPAPI-encrypted, never plain text).
- `HTTP 401` → wrong/revoked key. `HTTP 429` → rate limited; local engine
  keeps working. Any failure falls back to local suggestions automatically.

## Context menu missing

Settings → toggle "Explorer context menu", or reinstall via
`installer\Install.ps1`. Per-user registration needs no admin.

## Build issues

- `dotnet build` works for Core/Infrastructure/Tests only. The WinUI App
  project requires **Visual Studio 2022 + Windows App SDK workload**
  (XAML compiler needs full MSBuild).
- NU1201/NU1605 errors → ensure `-p:Platform=x64` and consistent
  `Microsoft.Extensions.* 8.0.0` versions (see DEVELOPMENT.md).
- `dotnet test` policy/load errors → ensure Core stays on plain `net8.0`
  (it has no Windows-only code); keep Tests on `net8.0-windows…`.

## Still stuck?

Settings → Diagnostics → **Export diagnostics**, and attach the file
(redact folder names if sensitive) to a GitHub issue.
