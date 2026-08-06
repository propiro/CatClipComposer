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
WPF -----+
         +--> Core <-- Infrastructure
CLI -----+          (implements Core contracts)
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

Each component is listed separately to keep its responsibility and boundary readable in narrow editors.

- **`MainViewModel`:** WPF catalog/settings/scan/export presentation. Timeline state and the shared
  render/history transaction are delegated.
- **`CompositionExportService`:** Render and record successful output history. It is shared by GUI and CLI
  and owns neither presentation nor FFmpeg construction.
- **`JsonProjectStore`:** Validate and atomically save/load normal and recovery project documents. It owns
  no timeline, UI, catalog, or render behavior.
- **`TimelineViewModel`:** Ordered segments, selection, editing, duration, and axis summaries. `MOD-001`
  is closed.
- **`ProjectRenderMapper`:** Convert enabled persisted tracks/items into renderer values. It is pure Core
  code shared by GUI and CLI.
- **`FfmpegVideoRenderer`:** Validate and coordinate temporary render output.
- **`FfmpegFilterGraphBuilder`:** Build normalization, concat, overlay, and progress filters.
- **`FfmpegRenderCommandBuilder`:** Build argument-safe FFmpeg process configuration.
- **`FfmpegProcessRunner`:** Execute FFmpeg, cancel, collect errors, and report progress. `MOD-002` is closed.
- **`SqliteMediaCatalog`:** Media/tag CRUD and successful-export/history SQL behind `IMediaCatalog`.
  Schema, connection, time conversion, mapping, and history projection are delegated; `MOD-003` is closed.
- **Preview generators:** Produce a static thumbnail and evenly sampled contact sheet. A shared process
  runner removes duplicated process/cancellation behavior; images remain replaceable cache files.
- **SQLite persistence helpers:** Own one schema, connection, conversion, or row-projection task each.
- **WPF window code-behind:** Own window events, validation prompts, and dialog flow. Explorer launch and
  exception presentation are delegated; `MOD-004` is closed.
- **WPF desktop helpers:** Own shell launch and consistent exception presentation only.
- **`WorkspaceLayoutController`:** Map panels to dock slots and apply temporary browser focus. Browser focus
  never overwrites durable settings.
- **Content browser:** Search cached metadata and recycle virtualized rows. It does not eagerly decode video;
  full-width focus retains the timeline drop target.
- **`CliApplication`:** Parse invocation, initialize shared services, dispatch, and map failures to exit codes.
- **CLI command modules:** Implement config, scan, list, metadata, project render, and history behavior while
  sharing Core/Infrastructure workflows.
- **Portable publisher:** Compose the two single-file entry points, INI, docs, and mandatory pinned FFmpeg
  payload. It validates hashes, license flags, versions, and required render capabilities before publishing.
- **Application startup:** Provide the shared `ApplicationServicesFactory` composition root. `BOOT-001` is
  closed.
- **INI configuration:** Split generic reading, application mapping, and atomic storage. `CFG-001` and
  `AUD-CFG-001` are closed.

## Architectural rules

- A class should have one primary reason to change.
- Process execution, FFmpeg argument construction, filter graph construction, and UI orchestration are separate responsibilities.
- Persistence SQL stays in Infrastructure; domain state stays in Core.
- Shared GUI/CLI behavior must live behind Core interfaces or in focused application services, never copied between executables.
- Superseded implementations are deleted in the same change that replaces them.

## Final responsibility audit conclusion

The 2026-08-06 post-MVP audit found no remaining P0/P1 responsibility violation. The larger presentation,
scanning, CLI dispatch, filter-graph, INI mapping, and catalog classes each retain one cohesive workflow and
delegate process execution, persistence projection, timeline state, executable composition, and desktop
integration to focused collaborators. The exact bundled-FFmpeg audit is closed; deferred trimming remains
the only known scoped feature gap.
