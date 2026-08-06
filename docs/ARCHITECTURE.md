# Architecture

Last reviewed: 2026-08-06

## Repository modules

```text
CatClipComposer.Core
    Domain models, formatting utilities, and service contracts

CatClipComposer.Infrastructure
    INI and project stores, SQLite, filesystem paths, FFprobe, FFmpeg, and service composition

CatClipComposer
    WPF application, compact dock workspace, presentation models, and desktop-specific interaction

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
4. Focused thumbnail/contact-sheet generators invoke FFmpeg and write keyed JPEG cache files.
5. `IMediaCatalog` upserts paths and technical/search metadata into SQLite while preserving user tags.
6. Project-use rows are queried from successful render jobs; merely adding a clip to a project never counts as use.

### Composition and render

1. The GUI synchronizes timeline items into the versioned project; the CLI can load that same project or create ad-hoc ordered segments.
2. `ProjectRenderMapper` projects enabled Video, Overlay, Audio, and Progress track items into one renderer plan without WPF/CLI duplication.
3. Project output dimensions/FPS/encoder/quality/bitrates are copied into the render request, with narrow command-line overrides.
4. `ICompositionExporter` owns the shared GUI/CLI export transaction.
5. `IVideoRenderer` validates inputs and produces a normalized layered filter graph.
6. FFmpeg renders to a unique partial path.
7. A successful render atomically replaces the selected output.
8. `ICompositionExporter` records the render job and ordered media IDs through `IMediaCatalog`.

### Project save and recovery

1. The presentation timeline raises a change event only for mutations, not selection.
2. `MainViewModel` synchronizes ordered video/still items into the project's Video track.
3. `IProjectStore` writes the complete versioned document atomically to recovery.
4. Normal Save writes the selected `.ccproject` and refreshes recovery.
5. Startup loads recovery after the catalog so media IDs/paths can be resolved into timeline view models.
6. A successful render carries project identity into history; editing and autosave alone never update catalog usage.

## Responsibility audit

| Component | Current responsibility | Audit result |
|---|---|---|
| `MainViewModel` | WPF catalog/settings/scan/export presentation | Timeline state and the shared render/history transaction are delegated. |
| `CompositionExportService` | Render a request and record successful output history | Shared application workflow for GUI and CLI; no presentation or FFmpeg construction responsibility. |
| `JsonProjectStore` | Validate and atomically serialize/load normal and recovery project documents | No timeline, UI, catalog, or render responsibility. |
| `TimelineViewModel` | Ordered segments, selection, editing, duration/axis summaries | Focused and independently smoke-tested; `MOD-001` closed. |
| `ProjectRenderMapper` | Convert enabled persisted tracks/items into renderer-domain values | Shared pure Core projection used by GUI and CLI; no WPF, process, or persistence dependency. |
| `FfmpegVideoRenderer` | Validate/orchestrate temporary render output | Focused coordinator. |
| `FfmpegFilterGraphBuilder` | Build normalization, concat, overlay, and progress filters | Pure construction responsibility. |
| `FfmpegRenderCommandBuilder` | Build argument-safe FFmpeg process configuration | Focused command responsibility. |
| `FfmpegProcessRunner` | Execute FFmpeg, cancel, collect errors, and report progress | Focused process responsibility; `MOD-002` closed. |
| `SqliteMediaCatalog` | Media/tag CRUD and successful-export/history SQL operations behind `IMediaCatalog` | Schema initialization, connection creation, UTC conversion, media mapping, and history aggregation are delegated; `MOD-003` closed. |
| Preview generators | Produce one static thumbnail and one configurable evenly sampled contact sheet | A shared process runner removes duplicate process/cancellation behavior; files are content-keyed cache assets rather than database blobs. |
| SQLite persistence helpers | One focused schema, connection, conversion, or row-projection responsibility each | Internal implementation details; no Core contract or schema change. |
| WPF window code-behind | Window-specific events, validation prompts, and dialog flow | File Explorer launch and exception presentation are delegated to focused desktop helpers; `MOD-004` closed. |
| WPF desktop helpers | Shell launch and consistent exception presentation | No catalog, rendering, settings, or window-workflow responsibility. |
| `WorkspaceLayoutController` | Map four panels to four dock slots, swap occupied positions, and apply temporary browser-focus layout | WPF-only layout mechanics; browser focus never overwrites durable slot values in shared application settings. |
| Content browser | Search tags/names/paths and recycle virtualized rows of cached metadata | Does not decode source video eagerly; full-width focus retains the timeline drop target and drag data contains only the selected catalog view model. |
| `CliApplication` | Parse global invocation, initialize shared services, dispatch, map failures to exit codes | Command behavior remains in focused command modules. |
| CLI command modules | Config, scan, list, metadata, project render, and history behavior | Share Core/Infrastructure workflows; text/JSON formatting stays in the CLI. |
| Portable publisher | Compose two single-file entry points plus INI/docs/thirdparty layout | Build-time script validates output safety and tool licensing flags; exact external-tool licensing remains an explicit release audit. |
| Application startup | Focused `ApplicationServicesFactory` composition root | Consumed by both GUI and CLI; `BOOT-001` closed. |
| INI configuration | Generic reader, application mapper, atomic store | Focused split; configuration audit passed (`CFG-001`, `AUD-CFG-001`). |

## Architectural rules

- A class should have one primary reason to change.
- Process execution, FFmpeg argument construction, filter graph construction, and UI orchestration are separate responsibilities.
- Persistence SQL stays in Infrastructure; domain state stays in Core.
- Shared GUI/CLI behavior must live behind Core interfaces or in focused application services, never copied between executables.
- Superseded implementations are deleted in the same change that replaces them.

## Final responsibility audit conclusion

The 2026-08-06 post-MVP audit found no remaining P0/P1 responsibility violation. The larger presentation, scanning, CLI dispatch, filter-graph, INI mapping, and catalog classes each retain one cohesive workflow and delegate process execution, persistence projection, timeline state, executable composition, and desktop integration to focused collaborators. Future feature work must preserve these boundaries. Remaining work is the exact release-FFmpeg audit and deferred trimming, not known class-splitting or duplicated-workflow debt.
