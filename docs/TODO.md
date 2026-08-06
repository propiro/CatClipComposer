# TODO register

Last audited: 2026-08-06

Statuses: `Open`, `In progress`, `Blocked`, `Done`, `Deferred`.

## Product and engineering TODOs

| ID | Priority | Status | Work | Acceptance criteria |
|---|---:|---:|---|---|
| CFG-001 | P0 | Done | Replace JSON settings with `CatClipComposer.ini` beside the executable. | INI round-trip/malformed-input smoke passed; JSON store removed; schema documented. |
| CLI-001 | P0 | Done | Add a headless CLI project in this repository. | Config, scan/list, tag/usage, project, layered render, and history commands passed text/JSON, exit-code, and end-to-end render smoke tests; Release build passes. |
| LIC-001 | P0 | Done | Remove required `libx264`/GPL encoding from the default render path. | Native `mpeg4` is default; `h264_mf` non-GPL option passed; `libx264` is explicit GPL opt-in. |
| BOOT-001 | P0 | Done | Share service composition between GUI and CLI. | Both executables consume `ApplicationServicesFactory`; render/history transaction is shared through `ICompositionExporter`. |
| MOD-001 | P1 | Done | Split `MainViewModel` orchestration and timeline state. | Focused `TimelineViewModel` owns editing, ordering, selection, summaries, axis values, and render-segment projection; direct smoke passed. |
| MOD-002 | P1 | Done | Split FFmpeg filter/argument construction from process execution. | Coordinator, filter builder, command builder, process runner, and cleanup helper are separate; mixed-input render smoke passed. |
| MOD-003 | P2 | Done | Split SQLite schema creation and row mapping from catalog operations. | Focused internal schema, connection, UTC, media mapper, and history reader classes preserve the Core catalog interface and schema. |
| MOD-004 | P2 | Done | Extract repeated WPF desktop interaction helpers. | Focused helpers own Explorer launch and consistent exception presentation across application/windows. |
| UI-001 | P0 | Done | Replace the leaking/high-padding theme with a compact monochrome editor design. | All derived windows explicitly paint dark client surfaces; title bars request dark mode; controls use warm neutral colors and 0-1 px corner radii; screenshot reviewed. |
| WORKSPACE-001 | P0 | Done | Add resizable and repositionable content, preview, layers/used-clips, and timeline panels. | Four dock slots use splitters; every panel can swap slots; unique layout persists in INI. |
| BROWSER-001 | P0 | Done | Make the content browser safe for very large libraries and support drag/drop. | Recycling virtualization is enabled; only cached realized thumbnails bind; clips drag to the timeline. |
| CATMETA-001 | P1 | Done | Add editable tags, static/contact-sheet preview metadata, and named-project usage details. | Additive SQLite migration, five-frame preview generation, tag editing/filtering, and project history queries passed. |
| LAYERS-001 | P1 | Done | Persist and edit a project layer/track model. | Five track types and requested item fields persist; add/edit/remove controls and a shared GUI/CLI renderer projection pass. |
| FX-001 | P1 | Done | Add timed fades, overlays, music, progress ranges, and fit/fill/blur-background modes. | Controls and a real six-second FFmpeg render cover timed text/PNG/progress/music, fades, volume, and animated blur without a GPL-only filter. |
| OUTPUT-001 | P1 | Done | Add common resolution/aspect/FPS/codec/quality presets and custom output values. | Officially sourced presets plus validated custom 640×360/24 settings reached FFmpeg and FFprobe. |
| DEPLOY-001 | P1 | Done | Produce a one-folder deployment layout with a tidy `thirdparty` boundary. | Framework-dependent/self-contained folders ran; copied FFmpeg tools were discovered and rendered from `thirdparty`. Exact release binary audit remains mandatory. |
| VERSION-001 | P1 | Done | Version the application and its components and expose the version to users. | All four projects build with shared 0.1.0 assembly/file metadata; the main title/status bars and CLI text/JSON output show 0.1.0. |
| DEPLOY-002 | P1 | Done | Keep application DLL/runtime clutter out of the portable package root. | Framework-dependent/self-contained single-file publishes leave only two executable entry points and the INI beside organized `docs`/`thirdparty` folders. |
| OVERLAY-001 | P2 | Done | Support multiple image/text overlays with individual start/end times. | Layer editor controls timing and placement for multiple elements; timed render passed. |
| PREVIEW-001 | P2 | Done | Add FFmpeg contact-sheet/slideshow fallback preview. | Configurable cached contact sheets are displayed below Windows playback and remain available for unsupported codecs. |
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
| AUD-CATMETA-001 | P1 | Done | Verify metadata migration, preview cache, tag updates, and successful-export usage semantics. | Synthetic six-second MP4 produced 800x90 five-frame sheet; tags normalized/persisted; usage stayed zero before export and returned named project/path after export. |
| AUD-FX-001 | P1 | Done | Verify layer projection, filter termination, timing, output settings, and codecs. | Real project render produced 640×360/24 MPEG-4 + AAC for exactly 6.000 s; frame sheet showed animated blur, fades, timed text/PNG, and progress; music mix was present. |
| AUD-PORTABLE-001 | P1 | Done | Verify one-folder publish and packaged-tool discovery. | Framework-dependent and 154 MB self-contained publishes ran CLI; FFmpeg/FFprobe copied under `thirdparty` and a layered render succeeded through automatic discovery. |
| AUD-VERSION-001 | P1 | Done | Verify shared version metadata and user-visible version reporting. | Release assemblies report 0.1.0.0; title/status bindings and CLI text/JSON report 0.1.0 without initializing data. |
| AUD-PORTABLE-002 | P1 | Done | Verify compact root layout and FFmpeg package guards. | Single-file package/root checks, published CLI execution, GPL rejection, explicit personal opt-in, and notice/build-info copy passed. |
| AUD-RELEASE-FFMPEG-001 | P0 | Open | Audit the exact FFmpeg binary and notices selected for public/commercial distribution. | Confirm no `--enable-nonfree`; decide LGPL/GPL package boundary; include exact license/source notices and retain `BUILD_INFO.txt`. |
