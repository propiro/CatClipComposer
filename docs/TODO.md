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
| MOD-004 | P2 | Open | Extract repeated WPF desktop interaction helpers. | Explorer launch and error presentation are not duplicated across windows. |
| OVERLAY-001 | P2 | Open | Support multiple image/text overlays with individual start/end times. | Timeline or overlay editor controls timing and placement for multiple elements. |
| PREVIEW-001 | P2 | Open | Add FFmpeg contact-sheet/slideshow fallback preview. | Unsupported Windows codecs still have a generated content preview. |
| PROJECT-001 | P2 | Open | Save/reopen named timeline projects. | Ordered clips, still screens, and render settings round-trip without embedding media. |
| EDIT-001 | P3 | Deferred | Add trim-in/out and per-clip volume. | Narrow controls work without expanding into general NLE scope. |

## Audit TODOs

| ID | Priority | Status | Audit | Completion evidence |
|---|---:|---:|---|---|
| AUD-LIC-001 | P0 | Done | Verify default FFmpeg command uses no GPL/nonfree component. | Native encoder and Media Foundation smoke outputs verified by FFprobe; command inventory recorded; exact distributed FFmpeg binary remains a release audit responsibility. |
| AUD-CLI-001 | P0 | Done | Verify headless commands are deterministic and automation-safe. | Help/config/list/history JSON, exit codes 2/3/4/5, overwrite safety, real scan/render/history/use-count, codec, and dimensions verified. |
| AUD-CFG-001 | P0 | Done | Verify INI escaping, missing keys, malformed values, and writable-location behavior. | 2026-08-06 round-trip/malformed-input smoke passed; atomic same-directory write and explicit permission error implemented. |
| AUD-DEP-001 | P1 | Done | Audit NuGet dependencies for known vulnerabilities. | 2026-08-06 audit reports zero known vulnerable packages after SQLitePCLRaw 2.1.12 pin. |
| AUD-ARCH-001 | P1 | Open | Re-audit class responsibilities after P0 refactors. | Updated responsibility table in `ARCHITECTURE.md`. |
| AUD-DOC-001 | P1 | Open | Check requested/done/not-done feature documentation against code. | Audit entry confirms all TODO IDs and statuses match implementation. |
