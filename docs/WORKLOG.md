# Worklog

This is an append-only record of material project work. Newest entries go first. Corrections should be added as new notes rather than rewriting historical results.

## 2026-08-06 — Versioned project files and recovery

- Added a versioned `.ccproject` schema with stable project/track/item IDs and Video, Overlay, Audio, Progress, and Effects tracks.
- Added persisted output settings and item fields for timing, fit, fades, volume, text/font/position, and progress ranges in preparation for the layer editor.
- Added atomic normal project save/load plus automatic atomic recovery on every timeline mutation.
- Added GUI New/Open/Save and automatic startup recovery, and a headless project create/inspect command.
- Separated configured metadata, project, and final-output folders; metadata changes take effect on restart without moving data implicitly.
- Added project name/path to successful export history via an additive SQLite migration; editing and autosave do not increment usage.
- Project store smoke preserved schema version, GUID, five tracks, 1920x1080 output, and overwrite exit code `2`; GUI startup passed.
- Closed: `PROJECT-001`, `AUD-PROJECT-001`; `LAYERS-001` is in progress.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Compact dockable editor workspace

- Replaced the green/blue-adjacent, spacious card treatment with a warm monochrome workstation palette, compact square controls, tight gutters, and dark Windows title-bar requests.
- Explicitly painted every derived WPF window and root surface, removing the white client-area leak caused by relying on an implicit base `Window` style.
- Rebuilt the main window as four resizable slots for content browser, preview, layers/used clips, and project timeline.
- Added persisted panel swapping among left/center/right/bottom slots and an expandable browser body.
- Replaced the non-virtualizing thumbnail wrap panel with recycled virtualized rows that bind only cached preview images for realized items.
- Added content-browser drag/drop into the project timeline.
- Closed: `UI-001`, `WORKSPACE-001`, `BROWSER-001`, `AUD-UX-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Final architecture and documentation audit

- Cross-checked repository modules and larger classes against the single-responsibility and GUI/CLI sharing rules.
- Cross-checked the requested feature matrix, completed work, partial work, deferred work, stable TODO IDs, configuration/CLI references, stack inventory, and third-party notices against the implementation.
- Confirmed all P0 implementation/audit work is done; remaining open work is explicitly documented product scope.
- Closed: `AUD-ARCH-001`, `AUD-DOC-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — WPF desktop helper extraction

- Replaced duplicate File Explorer launch code with one path-normalizing desktop shell helper.
- Centralized startup and owned-window exception presentation without abstracting window-specific validation/dialog flow.
- Closed: `MOD-004`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — SQLite persistence responsibility split

- Reduced the catalog adapter to media/history operations behind `IMediaCatalog`.
- Extracted focused connection creation, schema initialization, invariant UTC conversion, media parameter/row mapping, and export-history aggregation.
- Kept the existing schema and Core interface unchanged.
- Corrected headless text history to display the already one-based stored projection order without adding a second offset.
- Closed: `MOD-003`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Headless CLI

- Added a separate console executable in the same solution and repository.
- Added config inspection, scan, catalog list, ordered render, and export-history commands.
- Added optional configuration/data isolation, human-readable output, one-document JSON output, stderr progress, stable exit codes, Ctrl+C cancellation, and explicit overwrite protection.
- Reused shared service composition and the GUI's composition export/history workflow.
- Passed help/config/list/history JSON smoke checks, exit codes `2`, `3`, `4`, and `5`, overwrite protection, and a real FFmpeg scan/render/history/use-count workflow with an ordered still plus catalog clip.
- FFprobe verified native `mpeg4` at 1920x1080 for the CLI-rendered output.
- Closed: `CLI-001`, `BOOT-001`, `AUD-CLI-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Shared composition export workflow

- Added a Core application service that owns the render-and-record-history transaction.
- Migrated WPF export onto the shared service so the CLI can reuse identical successful-export bookkeeping.
- Kept FFmpeg rendering, catalog persistence, executable composition, and presentation behind separate responsibilities.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Non-GPL default encoder policy

- Added persisted encoder presets with an explicit license boundary.
- Made FFmpeg's native `mpeg4` encoder the default compatibility preset.
- Added Windows Media Foundation `h264_mf` as the preferred non-GPL H.264 option.
- Retained `libx264` only as a UI/INI-labeled `Libx264Gpl` opt-in.
- Adjusted renderer pixel format and arguments per encoder rather than sharing incompatible options.
- Generated input without libx264, rendered with both non-GPL presets, and used FFprobe to verify `mpeg4` and `h264` output codecs.
- Closed: `LIC-001`, `AUD-LIC-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — FFmpeg rendering module split

- Replaced the multi-purpose FFmpeg renderer with a small render coordinator.
- Extracted pure filter-graph construction, argument-safe command construction, process execution/progress/cancellation, and temporary-file cleanup.
- Moved the renderer into an explicit `Rendering` feature namespace and updated shared service composition.
- Passed a mixed video/still-image render with audio, text, PNG overlay, and per-clip progress after the split.
- Closed: `MOD-002`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Timeline presentation module extraction

- Extracted ordered segments, selection, insert/move/remove/clear operations, target duration, axis labels, summary values, and render-segment projection from `MainViewModel`.
- Bound WPF timeline controls directly to the focused `TimelineViewModel`.
- Exposed timeline clips as a read-only observable collection so external code cannot bypass ordering rules.
- Passed a direct timeline smoke test covering add, insert, move, reindex, duration/progress, target changes, render order, remove, and clear.
- Closed: `MOD-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Shared application composition root

- Added a focused `ApplicationServicesFactory` that constructs paths, INI settings, SQLite, scanning, probing, thumbnails, and rendering services.
- Moved WPF startup onto the shared factory and removed duplicated manual construction from `App`.
- Prepared the same composition root for headless CLI consumption.
- `BOOT-001` is in progress until the CLI uses the factory.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Executable-directory INI configuration

- Replaced the JSON settings implementation with focused INI reader, mapper, and atomic store classes.
- Set the default path to `CatClipComposer.ini` under `AppContext.BaseDirectory`.
- Documented the complete schema, enum values, escaping, defaults, clamping, and writable-directory behavior.
- Added a direct smoke test for round trips, multiline/backslash escaping, ordered folders, missing files, malformed values, and bounded values.
- Deleted the superseded JSON store; no legacy configuration path remains.
- Closed: `CFG-001`, `AUD-CFG-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Repository policy and documentation system

- Parsed the owner’s workflow, modularity, configuration, headless, licensing, documentation, audit, and commit requirements into repository-scoped `AGENTS.md`.
- Added project goals and requested/completed/incomplete feature tracking.
- Added architecture boundaries and an initial responsibility audit.
- Added software stack, dependency, and desired-license policy documentation.
- Added stable engineering and audit TODO identifiers.
- Added this worklog and an append-only audit log.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Initial functional MVP

- Implemented WPF catalog browsing, FFprobe scanning, FFmpeg thumbnails, SQLite persistence, timeline assembly, still screens, overlays, progress bars, portrait/landscape rendering, cancellation, and export history.
- Added initial README and third-party notices.
- Pinned SQLitePCLRaw 2.1.12 after a package audit found a high-severity issue in the older transitive native SQLite bundle.
- Verified Release build, application startup, scanning, thumbnails, mixed audio/silent input, overlays, progress styles, still screens, both output orientations, and history writes.
- Commit: `bde6480` (`feat: build the initial clip composer MVP`).
