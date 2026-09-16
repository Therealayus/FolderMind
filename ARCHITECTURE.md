# Architecture

## Layers (Clean Architecture)

```
AIFolderAssistant.App            WinUI 3 shell — Views, ViewModels, tray, DI composition
        │  references
        ▼
AIFolderAssistant.Infrastructure SQLite (Microsoft.Data.Sqlite), FileSystemWatcher,
                                 DPAPI secret store, OpenAI-compatible provider,
                                 registry (startup, Explorer menu)
        │  references
        ▼
AIFolderAssistant.Core           Pure domain logic, net8.0, zero Windows dependencies:
                                 analysis pipeline, keyword classifier, name generator,
                                 confidence, sanitizer/validator, rename + settings services
```

Rules:

- **Core** references nothing above it. It defines interfaces
  (`ISettingsStore`, `IRenameHistoryRecorder`, `ITrayController`, …);
  Infrastructure **adapts** its repositories to them.
- UI, business logic, infrastructure, persistence are separated; no business
  logic in XAML code-behind (code-behind only wires ViewModels + pickers).
- All external dependencies sit behind interfaces (`IFileSystemMonitor`,
  `IAIProvider`, `ISecretStore`, `IUpdateService`, …).

## Analysis pipeline

```
Folder → File Discovery (streaming enumerate, 500-file cap, system-file skip)
  → Metadata Extraction (dates; best-effort, never throws)
  → Content Extraction (text formats ≤256KB; binary → null, never throws)
  → Content Classification (offline keyword→category map)
  → Candidate Generation (trigger-share ≥25% → primary suggestion)
  → Confidence Calculation (file count, extension spread, keywords, topics)
  → Suggestion (+ sanitized alternatives)
```

`IFolderAnalysisPipeline` (`Core/Engines/LocalAnalysisPipeline`) is the composition
point: caching/fingerprints, sampling, and AI enhancement plug in there.

## Monitoring pipeline

```
FileSystemWatcher (per folder, subdirs) → dedup (500ms) → FolderWatchService
  → per-folder debounce (default 3s, configurable) → bounded queue (1000)
  → FolderSettled event → App: analyze → threshold gate (≥0.80)
  → dedup vs last pending → persist → tray-balloon notify
```

Error recovery: watcher `Error` event re-arms; dispatch retries 3× with backoff;
missing folders skip; every handler is exception-guarded.

## Key decisions

- **No Windows Service in V1**: WinUI app + background worker + tray + SQLite.
  `FolderWatchService` and repositories are Service-ready for later extraction.
- **Microsoft.Data.Sqlite directly** (no EF Core): avoids version-skew issues,
  tiny footprint, full control over schema (`EnsureCreated`, idempotent).
- **H.NotifyIcon** for tray: unpackaged-safe; toasts via balloon tips
  (AppNotifications require package identity — future MSIX path).
- **Per-user registry** for startup (`HKCU\...\Run`) and Explorer menu
  (`HKCU\Software\Classes\Directory\shell`): no admin required.
- **DPAPI file store** for API keys: per-user encrypted, never plain text.

## Future extension points

- `IAIProvider`: OpenAI / Azure / local LLM / Mock (tests run offline)
- `IUpdateService`: GitHub Releases / private server / Store (stubbed `NoOp`)
- `IFolderAnalysisPipeline`: fingerprint cache, semantic image analysis
- Explorer: COM surrogate / sparse MSIX package for Store builds
- Windows Service extraction: `FolderWatchService` + repositories move as-is
