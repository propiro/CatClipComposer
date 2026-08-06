# TODO register

Last audited: 2026-08-06

Statuses: `Open`, `In progress`, `Blocked`, `Done`, `Deferred`.

## Product and engineering TODOs

| ID | Priority | Status | Work | Acceptance criteria |
|---|---:|---:|---|---|
| CFG-001 | P0 | Open | Replace JSON settings with `CatClipComposer.ini` beside the executable. | GUI round-trip test passes; no JSON settings implementation remains; docs show schema/location. |
| CLI-001 | P0 | Open | Add a headless CLI project in this repository. | Commands support config inspection, scan, list, render, and history; documented exit codes and JSON output; Release build passes. |
| LIC-001 | P0 | Open | Remove required `libx264`/GPL encoding from the default render path. | Default export passes with an LGPL-compatible FFmpeg build; optional GPL preset is clearly opt-in. |
| BOOT-001 | P0 | Open | Share service composition between GUI and CLI. | One bootstrap/service factory constructs paths, settings, catalog, scanner, probe, thumbnails, and renderer. |
| MOD-001 | P1 | Open | Split `MainViewModel` orchestration and timeline state. | Timeline operations and summary calculations have a focused independently testable class. |
| MOD-002 | P1 | Open | Split FFmpeg filter/argument construction from process execution. | Renderer coordinates focused builder and runner services; filter generation can be tested without launching FFmpeg. |
| MOD-003 | P2 | Open | Split SQLite schema creation and row mapping from catalog operations. | Schema/mapping are focused internal classes without changing the Core catalog interface. |
| MOD-004 | P2 | Open | Extract repeated WPF desktop interaction helpers. | Explorer launch and error presentation are not duplicated across windows. |
| OVERLAY-001 | P2 | Open | Support multiple image/text overlays with individual start/end times. | Timeline or overlay editor controls timing and placement for multiple elements. |
| PREVIEW-001 | P2 | Open | Add FFmpeg contact-sheet/slideshow fallback preview. | Unsupported Windows codecs still have a generated content preview. |
| PROJECT-001 | P2 | Open | Save/reopen named timeline projects. | Ordered clips, still screens, and render settings round-trip without embedding media. |
| EDIT-001 | P3 | Deferred | Add trim-in/out and per-clip volume. | Narrow controls work without expanding into general NLE scope. |

## Audit TODOs

| ID | Priority | Status | Audit | Completion evidence |
|---|---:|---:|---|---|
| AUD-LIC-001 | P0 | Open | Verify default FFmpeg command uses no GPL/nonfree component. | Command inventory and LGPL-build smoke result recorded in `AUDIT_LOG.md`. |
| AUD-CLI-001 | P0 | Open | Verify headless commands are deterministic and automation-safe. | Exit-code/error/JSON tests recorded. |
| AUD-CFG-001 | P0 | Open | Verify INI escaping, missing keys, malformed values, and writable-location behavior. | Round-trip and malformed-config tests recorded. |
| AUD-DEP-001 | P1 | Done | Audit NuGet dependencies for known vulnerabilities. | 2026-08-06 audit reports zero known vulnerable packages after SQLitePCLRaw 2.1.12 pin. |
| AUD-ARCH-001 | P1 | Open | Re-audit class responsibilities after P0 refactors. | Updated responsibility table in `ARCHITECTURE.md`. |
| AUD-DOC-001 | P1 | Open | Check requested/done/not-done feature documentation against code. | Audit entry confirms all TODO IDs and statuses match implementation. |
