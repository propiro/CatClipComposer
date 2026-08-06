# TODO register

Last audited: 2026-08-06

Statuses: `Open`, `In progress`, `Blocked`, `Done`, `Deferred`.

## Product and engineering TODOs

| ID | Priority | Status | Work | Acceptance criteria |
|---|---:|---:|---|---|
| CFG-001 | P0 | Done | Replace JSON settings with `CatClipComposer.ini` beside the executable. | INI round-trip/malformed-input smoke passed; JSON store removed; schema documented. |
| CLI-001 | P0 | Done | Add a headless CLI project in this repository. | Config, scan, list, render, and history commands passed text/JSON, exit-code, and end-to-end render smoke tests; Release build passes. |
| LIC-001 | P0 | Done | Remove required `libx264`/GPL encoding from the default render path. | Native `mpeg4` is default; `h264_mf` non-GPL option passed; `libx264` is explicit GPL opt-in. |
| BOOT-001 | P0 | Done | Share service composition between GUI and CLI. | Both executables consume `ApplicationServicesFactory`; render/history transaction is shared through `ICompositionExporter`. |
| MOD-001 | P1 | Done | Split `MainViewModel` orchestration and timeline state. | Focused `TimelineViewModel` owns editing, ordering, selection, summaries, axis values, and render-segment projection; direct smoke passed. |
| MOD-002 | P1 | Done | Split FFmpeg filter/argument construction from process execution. | Coordinator, filter builder, command builder, process runner, and cleanup helper are separate; mixed-input render smoke passed. |
| MOD-003 | P2 | Done | Split SQLite schema creation and row mapping from catalog operations. | Focused internal schema, connection, UTC, media mapper, and history reader classes preserve the Core catalog interface and schema. |
| MOD-004 | P2 | Done | Extract repeated WPF desktop interaction helpers. | Focused helpers own Explorer launch and consistent exception presentation across application/windows. |
| UI-001 | P0 | Done | Replace the leaking/high-padding theme with a compact monochrome editor design. | All derived windows explicitly paint dark client surfaces; title bars request dark mode; controls use warm neutral colors and 0-1 px corner radii; screenshot reviewed. |
| WORKSPACE-001 | P0 | Done | Add resizable and repositionable content, preview, layers/used-clips, and timeline panels. | Four dock slots use splitters; every panel can swap slots; unique layout persists in INI. |
| BROWSER-001 | P0 | Done | Make the content browser safe for very large libraries and support drag/drop. | Recycling virtualization is enabled; only cached realized thumbnails bind; clips drag to the timeline. |
| CATMETA-001 | P1 | Open | Add editable tags, static/contact-sheet preview metadata, and named-project usage details. | SQLite migration, preview generation, tag editing/filtering, and project history queries pass. |
| LAYERS-001 | P1 | In progress | Persist and edit a project layer/track model. | Five track types and all requested item fields persist; editing controls and renderer projection remain. |
| FX-001 | P1 | Open | Add timed fades, overlays, music, progress ranges, and fit/fill/blur-background modes. | Timeline controls and verified FFmpeg output cover each effect without GPL-only filters. |
| OUTPUT-001 | P1 | Open | Add common resolution/aspect/FPS/codec/quality presets and custom output values. | Presets reflect official editor/platform guidance; custom validated settings reach FFmpeg. |
| DEPLOY-001 | P1 | Open | Produce a one-folder deployment layout with a tidy `thirdparty` boundary. | Published GUI/CLI/config/docs plus audited optional tools run from one folder. |
| OVERLAY-001 | P2 | Open | Support multiple image/text overlays with individual start/end times. | Timeline or overlay editor controls timing and placement for multiple elements. |
| PREVIEW-001 | P2 | Open | Add FFmpeg contact-sheet/slideshow fallback preview. | Unsupported Windows codecs still have a generated content preview. |
| PROJECT-001 | P2 | Done | Save/reopen named timeline projects with crash recovery. | Versioned five-track `.ccproject` documents and atomic recovery round-trip without embedding media; GUI and CLI checks pass. |
| EDIT-001 | P3 | Deferred | Add trim-in/out and per-clip volume. | Narrow controls work without expanding into general NLE scope. |

## Audit TODOs

| ID | Priority | Status | Audit | Completion evidence |
|---|---:|---:|---|---|
| AUD-LIC-001 | P0 | Done | Verify default FFmpeg command uses no GPL/nonfree component. | Native encoder and Media Foundation smoke outputs verified by FFprobe; command inventory recorded; exact distributed FFmpeg binary remains a release audit responsibility. |
| AUD-CLI-001 | P0 | Done | Verify headless commands are deterministic and automation-safe. | Help/config/list/history JSON, exit codes 2/3/4/5, overwrite safety, real scan/render/history/use-count, codec, and dimensions verified. |
| AUD-CFG-001 | P0 | Done | Verify INI escaping, missing keys, malformed values, and writable-location behavior. | 2026-08-06 round-trip/malformed-input smoke passed; atomic same-directory write and explicit permission error implemented. |
| AUD-DEP-001 | P1 | Done | Audit NuGet dependencies for known vulnerabilities. | 2026-08-06 audit reports zero known vulnerable packages after SQLitePCLRaw 2.1.12 pin. |
| AUD-ARCH-001 | P1 | Done | Re-audit class responsibilities after P0 refactors. | Architecture responsibility table reflects final GUI, CLI, rendering, persistence, configuration, and desktop boundaries. |
| AUD-DOC-001 | P1 | Done | Check requested/done/not-done feature documentation against code. | Project matrix and TODO register cross-checked against implementation; open/partial/deferred scope is explicit. |
| AUD-UX-001 | P0 | Done | Verify the theme leak, density, docking, virtualization, and drag/drop implementation. | Release build and captured main-window screenshot show no white client surface; XAML/code audit confirms recycling and dock persistence. |
| AUD-PROJECT-001 | P0 | Done | Verify project versioning, atomic save/load, recovery identity, and overwrite safety. | CLI create/load preserved schema/GUID/five tracks/output; overwrite returned 2; GUI startup and additive SQLite migration passed. |
