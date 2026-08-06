# Architecture

Last reviewed: 2026-08-06

## Repository modules

```text
CatClipComposer.Core
    Domain models, formatting utilities, and service contracts

CatClipComposer.Infrastructure
    INI parsing/mapping/store, SQLite, filesystem paths, FFprobe, FFmpeg, and service composition

CatClipComposer
    WPF application, windows, presentation models, and desktop-specific interaction

CatClipComposer.Cli
    Headless command dispatch, text/JSON output, and process exit codes
```

All modules remain in this repository. Git submodules are prohibited.

## Dependency direction

```text
WPF ─────┐
         ├──> Core <── Infrastructure
CLI ─────┘          (implements Core contracts)
```

The executable modules may reference Core and Infrastructure for composition. Core must not reference WPF, SQLite, FFmpeg process details, or CLI concerns.

## Runtime data flow

### Catalog scan

1. An executable loads `CatClipComposer.ini` through `ISettingsStore`.
2. `IMediaScanner` enumerates configured folders and accepted extensions.
3. `IMediaProbe` invokes FFprobe and parses duration, dimensions, and audio stream presence.
4. `IThumbnailGenerator` invokes FFmpeg and writes a cache image.
5. `IMediaCatalog` upserts metadata into SQLite and marks removed files unavailable.

### Composition and render

1. The GUI or CLI creates ordered `RenderSegment` values.
2. Render options are copied from application settings, with allowed command-line overrides.
3. `ICompositionExporter` owns the shared GUI/CLI export transaction.
4. `IVideoRenderer` validates inputs and produces a normalized filter graph.
5. FFmpeg renders to a unique partial path.
6. A successful render atomically replaces the selected output.
7. `ICompositionExporter` records the render job and ordered media IDs through `IMediaCatalog`.

## Responsibility audit

| Component | Current responsibility | Audit result |
|---|---|---|
| `MainViewModel` | WPF catalog/settings/scan/export presentation | Timeline state and the shared render/history transaction are delegated. |
| `CompositionExportService` | Render a request and record successful output history | Shared application workflow for GUI and CLI; no presentation or FFmpeg construction responsibility. |
| `TimelineViewModel` | Ordered segments, selection, editing, duration/axis summaries | Focused and independently smoke-tested; `MOD-001` closed. |
| `FfmpegVideoRenderer` | Validate/orchestrate temporary render output | Focused coordinator. |
| `FfmpegFilterGraphBuilder` | Build normalization, concat, overlay, and progress filters | Pure construction responsibility. |
| `FfmpegRenderCommandBuilder` | Build argument-safe FFmpeg process configuration | Focused command responsibility. |
| `FfmpegProcessRunner` | Execute FFmpeg, cancel, collect errors, and report progress | Focused process responsibility; `MOD-002` closed. |
| `SqliteMediaCatalog` | Media CRUD and history SQL operations behind `IMediaCatalog` | Schema initialization, connection creation, UTC conversion, media mapping, and history aggregation are delegated; `MOD-003` closed. |
| SQLite persistence helpers | One focused schema, connection, conversion, or row-projection responsibility each | Internal implementation details; no Core contract or schema change. |
| WPF window code-behind | Dialog and desktop interaction | Acceptable where limited to UI events; repeated Explorer/error helpers should be extracted (`MOD-004`). |
| `CliApplication` | Parse global invocation, initialize shared services, dispatch, map failures to exit codes | Command behavior remains in focused command modules. |
| CLI command modules | Config, scan, list, render, and history behavior | Share Core/Infrastructure workflows; text/JSON formatting stays in the CLI. |
| Application startup | Focused `ApplicationServicesFactory` composition root | Consumed by both GUI and CLI; `BOOT-001` closed. |
| INI configuration | Generic reader, application mapper, atomic store | Focused split; configuration audit passed (`CFG-001`, `AUD-CFG-001`). |

## Architectural rules

- A class should have one primary reason to change.
- Process execution, FFmpeg argument construction, filter graph construction, and UI orchestration are separate responsibilities.
- Persistence SQL stays in Infrastructure; domain state stays in Core.
- Shared GUI/CLI behavior must live behind Core interfaces or in focused application services, never copied between executables.
- Superseded implementations are deleted in the same change that replaces them.
