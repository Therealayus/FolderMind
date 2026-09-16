# Security

## Threat model (V1, standard-user desktop app)

- Runs as **standard user**, never requires admin.
- Untrusted inputs: file names, file contents, AI responses, registry state.
- Assets: user files (read-only access), API keys, SQLite database.

## Controls

- **Never auto-rename**: renames happen only via explicit user action.
- **Rename safety** (`RenameService`): verify source exists → validate + sanitize
  destination → never overwrite (auto-suffix `Name (2)`) → re-verify source
  (race guard) → move → verify success → record history. Undo refuses when the
  original path was reused.
- **Path validation** (`PathValidator`): traversal-safe full-path resolution,
  system directories (`Windows`, `Program Files`, `ProgramData`) rejected,
  length limits, invalid-char checks.
- **Name sanitization** (`NameSanitizer`): strips `<>:"/\|?*`, control chars,
  trailing dots/spaces; blocks reserved names (`CON`, `PRN`, …); 100-char cap.
- **AI output distrust**: structured-JSON parsing with validation; sanitize
  before display/apply; retry → local fallback; errors logged, never crash.
- **Credential storage**: DPAPI per-user encryption (`DpapiSecretStore`); no
  secrets in config files, logs, or the database.
- **Least privilege**: per-user registry only (`HKCU` Run key, `HKCU` Classes);
  per-user `%AppData%` storage; no services, no drivers, no shell extensions.
- **No code execution**: never executes files or extracted content; text
  extraction is read-only with size caps (256KB) and `FileShare.ReadWrite`.
- **Resilience**: locked/disappearing/denied files, unparsable PDFs, dead
  network, unavailable DB, failed toasts — all handled; user sees a friendly
  message, never a stack trace.

## Reporting

Found a vulnerability? Please open a private security advisory on the GitHub
repository instead of a public issue. Include version, Windows build, and
reproduction steps (no sensitive files).
