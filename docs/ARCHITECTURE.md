# Architecture

Last reviewed: 2026-08-06

## Repository modules

```text
CatClipComposer.Core
    Domain models, formatting utilities, and service contracts

CatClipComposer.Infrastructure
    INI/configuration, SQLite, filesystem paths, FFprobe, FFmpeg, and service composition

CatClipComposer
    WPF application, windows, presentation models, and desktop-specific interaction

CatClipComposer.Cli (planned: CLI-001)
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
3. `IVideoRenderer` validates inputs and produces a normalized filter graph.
4. FFmpeg renders to a unique partial path.
5. A successful render atomically replaces the selected output.
6. `IMediaCatalog` records the render job and ordered media IDs.

## Responsibility audit

| Component | Current responsibility | Audit result |
|---|---|---|
| `MainViewModel` | Catalog loading, scanning, settings, timeline editing, rendering | Too broad; split orchestration from timeline state (`MOD-001`). |
| `FfmpegVideoRenderer` | Validation, process arguments, filter graph, execution, progress, cleanup | Too broad; split command/filter construction from execution (`MOD-002`). |
| `SqliteMediaCatalog` | Schema, media CRUD, history writes/queries | Cohesive persistence adapter but large; split schema and row mapping (`MOD-003`). |
| WPF window code-behind | Dialog and desktop interaction | Acceptable where limited to UI events; repeated Explorer/error helpers should be extracted (`MOD-004`). |
| Application startup | Manual service composition | Must be shared with the CLI (`BOOT-001`). |

## Architectural rules

- A class should have one primary reason to change.
- Process execution, FFmpeg argument construction, filter graph construction, and UI orchestration are separate responsibilities.
- Persistence SQL stays in Infrastructure; domain state stays in Core.
- Shared GUI/CLI behavior must live behind Core interfaces or in focused application services, never copied between executables.
- Superseded implementations are deleted in the same change that replaces them.
