# Privacy

FolderMind AI is **local-first**. You own your data.

## Defaults

| Feature | Default |
|---|---|
| Local analysis | ON |
| Cloud AI | OFF |
| Local-only mode | ON |
| Telemetry/analytics | None (not implemented) |

## What stays on your PC (always)

- File enumeration, metadata, extracted text snippets, classification — all in-process.
- SQLite database at `%AppData%\FolderMind\database\FolderMind.db`:
  folder paths, file counts, suggestions, rename history, settings.
- Logs at `%AppData%\FolderMind\logs\` (paths + suggestion metadata; **no file contents**).
- API keys: DPAPI-encrypted per-user files at `%AppData%\FolderMind\secrets\`
  (decryptable only by your Windows user profile).

## What leaves your PC (only with explicit opt-in)

Cloud AI requires **all** of: AI enabled + provider configured + "Allow cloud
analysis" ON + API key saved. When enabled, each request sends **only** a summary:

```json
{
  "fileCount": 12,
  "extensions": [".pdf", ".docx"],
  "topics": ["invoice", "payment", "tax"],
  "sampleFileNames": ["invoice_123.pdf", "gst_receipt.pdf"]
}
```

Full document contents are **never** sent. Responses are validated, sanitized,
and on any failure the app falls back to local suggestions.

## Your controls

- Settings → Privacy → Local-only mode (master kill-switch for cloud).
- Settings → Advanced → Clear local database; Delete history (History page).
- Uninstall removes database, logs, secrets, registry entries.

## No telemetry

V1 contains zero analytics. If telemetry is ever added it will be explicit
opt-in, documented here, and trivially disabled.
