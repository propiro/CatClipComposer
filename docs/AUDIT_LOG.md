# Audit log

This is an append-only audit trail. Each audit records scope, findings, action IDs, and evidence. Closing an item requires a later closure entry; do not erase the original finding.

## AUDIT-2026-08-06-015 — Workspace visual and scale audit

Scope: `UI-001`, `WORKSPACE-001`, `BROWSER-001`, and `AUD-UX-001`.

Findings:

- The original white client area came from relying on an implicit `Window` style for derived window types. Every window and root surface now explicitly uses the dark window brush.
- The replacement palette contains warm neutral gray/beige accents and no blue accent brush; button templates use one-pixel corners and reduced padding.
- Browser, preview, layers/used-clips, and timeline are peers in one four-slot grid with user-resizable splitters.
- Dock commands swap the requested panel with the occupied slot, guaranteeing exactly one panel per slot; normalized values persist in `[Workspace]`.
- The catalog uses `VirtualizingStackPanel` recycling instead of `WrapPanel`, so source videos are never opened merely to populate the list and cached thumbnails bind only for realized rows.
- Drag/drop transfers one selected catalog view model and adds its durable media record to the timeline.

Verification:

- Release build passed with zero warnings/errors.
- A captured 1440x900 main-window image was visually inspected: all client surfaces are dark, density is reduced, corners are square, and all four panels are visible.
- Configuration/CLI projection includes all four workspace slots.

Result: `UI-001`, `WORKSPACE-001`, `BROWSER-001`, and `AUD-UX-001` closed.

## AUDIT-2026-08-06-014 — Architecture and documentation closure

Scope: `AUD-ARCH-001` and `AUD-DOC-001` after all planned responsibility refactors.

Architecture findings:

- Core contains models, utilities, contracts, and the shared export application service; it has no WPF, CLI, SQLite, or FFmpeg process dependency.
- Infrastructure has focused INI, SQLite, scan/probe/thumbnail, render construction/execution, and composition modules behind Core contracts.
- WPF owns desktop presentation and delegates timeline state, durable workflows, persistence, rendering, and repeated desktop interactions.
- CLI owns parsing, command validation/dispatch, text/JSON projection, and exit-code mapping while sharing application services with WPF.
- Larger classes were reviewed by responsibility. Their remaining size follows cohesive workflow/format construction rather than unrelated ownership or GUI/CLI duplication.

Documentation findings:

- `PROJECT.md` covers every requested product area and distinguishes Done, Partial, Not done, and Deferred behavior.
- `TODO.md` has stable IDs and acceptance evidence for configuration, CLI, licensing, bootstrapping, modularity, product work, and audits.
- README indexes project, architecture, stack/licenses, configuration, headless, TODO, worklog, audit, and third-party documents.
- INI location/schema, CLI commands/JSON/exit codes, dependency versions/licenses, GPL opt-in boundary, work history, and audit evidence match the code.
- Remaining open work is limited to timed multiple overlays (`OVERLAY-001`), fallback preview (`PREVIEW-001`), and named project persistence (`PROJECT-001`); trim/volume remains explicitly deferred (`EDIT-001`).

Verification: final Release build, dependency vulnerability audit, CLI JSON help, prior isolated headless/catalog/render smokes, hidden GUI startup, `git diff --check`, and Git status review passed.

Result: `AUD-ARCH-001` and `AUD-DOC-001` closed.

## AUDIT-2026-08-06-013 — WPF desktop interaction closure

Scope: `MOD-004`.

Findings:

- `DesktopShell` is the single File Explorer launch implementation and normalizes its selected file path.
- `DesktopDialogs` consistently presents startup and owned-window exception details.
- Window code-behind retains only window-specific event flow and contextual validation/information prompts.

Verification: Release build and hidden GUI startup smoke passed.

Result: `MOD-004` closed.

## AUDIT-2026-08-06-012 — SQLite responsibility closure

Scope: `MOD-003`.

Findings:

- `SqliteMediaCatalog` retains only `IMediaCatalog` operation and transaction coordination.
- Connection-string creation, schema initialization, invariant UTC conversion, media parameter/row mapping, and export-history aggregation have one focused internal class each.
- The database schema, SQL semantics, and public Core interface did not change.
- Export-history order is projected as one-based for both GUI and CLI; the CLI's redundant display offset was removed.

Verification: Release build and direct schema/upsert/query/availability/export/history mapping smoke tests passed.

Result: `MOD-003` closed.

## AUDIT-2026-08-06-011 — Headless automation closure

Scope: `CLI-001`, `BOOT-001`, and `AUD-CLI-001`.

Implementation findings:

- The console executable consumes `ApplicationServicesFactory` and the same INI, SQLite, scanner, renderer, and `CompositionExportService` contracts as WPF.
- Config, scan, list, render, and history are separate command modules; WPF is never loaded by the CLI.
- `--json` emits one stdout document and suppresses progress; non-JSON progress goes to stderr.
- Existing render outputs require `--overwrite`; ordered `--clip`/`--screen` options preserve segment order.
- Documented exit codes distinguish invalid arguments, configuration, partial scans, execution failure, and cancellation.

Verification:

- Release solution build passed with zero warnings/errors.
- Help, config, empty list/history, JSON parsing, invalid render arguments/configuration (`2`/`3`), partial-scan warnings (`4`), and FFmpeg launch failure (`5`) passed against an isolated data folder.
- Rendering to an existing destination without `--overwrite` was rejected with exit code `2` and left the file untouched.
- A real native-MPEG-4 source was scanned through FFprobe/FFmpeg; list returned its durable ID.
- CLI rendered an ordered still plus catalog clip with overlay/progress settings; FFprobe verified codec `mpeg4` and 1920x1080 dimensions.
- History recorded one export with the correct media ID and the catalog usage count advanced to one.
- The full four-project NuGet audit reported no known vulnerable packages from the configured sources.

Result: `CLI-001`, `BOOT-001`, and `AUD-CLI-001` closed.

## AUDIT-2026-08-06-010 — Shared export workflow review

Scope: GUI/CLI duplication risk before `CLI-001`.

Findings:

- `CompositionExportService` now owns the renderer call and successful export-history write.
- WPF presentation owns busy state and catalog refresh only; it no longer duplicates the durable export transaction.
- The composition root exposes the application workflow rather than its lower-level renderer dependency.

Result: shared workflow accepted; `BOOT-001` remains in progress until CLI consumption is verified.

## AUDIT-2026-08-06-009 — Default encoder license closure

Scope: `LIC-001` and `AUD-LIC-001`.

Implementation findings:

- `NativeMpeg4` is the application and INI default and emits `-c:v mpeg4`; FFmpeg documents this as its native MPEG-4 Part 2 encoder without requiring the GPL libxvid wrapper.
- `WindowsMediaFoundationH264` emits `-c:v h264_mf` with Media Foundation quality/archive options and uses the safer `nv12` pixel format documented by FFmpeg.
- `Libx264Gpl` is never implicit and is labeled as GPL in enum, UI, INI, README, stack inventory, and third-party notices.
- No preset uses `--enable-nonfree` components.

Verification:

- Installed FFmpeg encoder inventory exposed native `mpeg4` and `h264_mf`.
- A smoke input was generated with native `mpeg4` rather than libx264.
- Default render completed and FFprobe returned video codec `mpeg4`.
- Media Foundation render completed and FFprobe returned video codec `h264`.
- Release build passed with zero warnings/errors.

Limit: the locally installed FFmpeg distribution itself was configured with GPL components. Cat Clip Composer did not invoke them in the two verified presets. Every commercial release must separately inspect the exact external FFmpeg build with `ffmpeg -version`; this standing release check does not reopen the application-code finding.

Result: `LIC-001` and `AUD-LIC-001` closed.

## AUDIT-2026-08-06-008 — FFmpeg module responsibility closure

Scope: `MOD-002`.

Findings:

- Render request validation and atomic-output coordination remain in `FfmpegVideoRenderer`.
- Filter graph construction has no process-launch responsibility.
- Command construction uses `ProcessStartInfo.ArgumentList` and has no execution responsibility.
- Process execution exclusively owns start errors, cancellation, progress parsing, exit code, and standard error collection.
- Temporary cleanup is a narrow shared helper.

Verification: Release build passed; a real mixed video/still render with audio, text, PNG overlay, per-clip progress, and 1920×1080 output passed.

Result: `MOD-002` closed. Encoder licensing remains separately open under `LIC-001`.

## AUDIT-2026-08-06-007 — Timeline responsibility closure

Scope: `MOD-001`.

Findings:

- `MainViewModel` no longer owns timeline collection mutation, selection, ordering, target calculations, axis labels, or render projection.
- `TimelineViewModel` has one cohesive state-management responsibility and exposes a read-only collection.
- WPF bindings now address the timeline module directly.

Verification: Release build passed; a direct temporary harness passed add/insert/move/reindex/summary/target/projection/remove/clear checks; GUI startup smoke will run with the final verification set.

Result: `MOD-001` closed.

## AUDIT-2026-08-06-006 — Composition-root progress review

Scope: `BOOT-001` and GUI/CLI construction duplication risk.

Findings:

- Added one Infrastructure composition root returning Core interface types.
- WPF startup now consumes the factory and contains no concrete scanner, probe, thumbnail, catalog, or renderer construction.
- The factory accepts optional data/configuration paths for deterministic CLI and test use.

Result: design accepted; `BOOT-001` remains in progress until CLI consumption is verified.

## AUDIT-2026-08-06-005 — INI configuration closure

Scope: `CFG-001` and `AUD-CFG-001`.

Implementation findings:

- Configuration resolves to `CatClipComposer.ini` under `AppContext.BaseDirectory`.
- Parsing, application mapping/normalization, and atomic file replacement are separate responsibilities.
- The JSON settings class and all references to `settings.json` were removed.
- Saving to a protected executable directory produces an explicit configuration error instead of silently redirecting.

Verification:

- Release solution build: passed with zero warnings and errors.
- Temporary round-trip harness: passed for folders containing `=`, folder ordering, booleans, doubles, enums, all overlay fields, newline/backslash text escaping, missing files, malformed values, defaults, and clamping.
- Temporary test artifacts were removed after the run.

Result: `CFG-001` and `AUD-CFG-001` closed.

## AUDIT-2026-08-06-004 — Documentation and TODO baseline

Scope: requested product features, documentation requirements, and durable task tracking.

Findings:

- The original README documented current behavior but did not provide a stable requested/done/not-done matrix.
- No worklog, architecture record, durable TODO IDs, or append-only audit record existed.
- Added `PROJECT.md`, `ARCHITECTURE.md`, `STACK_AND_LICENSES.md`, `TODO.md`, `WORKLOG.md`, and this audit log.

Actions: `AUD-DOC-001` remains open until the P0 implementation changes are reflected and cross-checked.

## AUDIT-2026-08-06-003 — Architecture baseline

Scope: project boundaries and class responsibilities.

Findings:

- Core/Infrastructure/WPF project boundaries are directionally correct.
- `MainViewModel` combines catalog, scanning, settings, timeline, and rendering responsibilities (`MOD-001`).
- `FfmpegVideoRenderer` combines filter construction and process execution (`MOD-002`).
- `SqliteMediaCatalog` combines schema, mapping, commands, and history (`MOD-003`).
- GUI and planned CLI need shared service composition (`BOOT-001`).
- Repeated desktop helpers exist in WPF code-behind (`MOD-004`).

Action: complete P0 modular work, then perform `AUD-ARCH-001`.

## AUDIT-2026-08-06-002 — License baseline

Scope: runtime libraries and FFmpeg requirements.

Findings:

- .NET, Microsoft.Data.Sqlite, SQLite, and SQLitePCLRaw have acceptable current license status for the desired product direction.
- FFmpeg is correctly external rather than linked or bundled.
- The renderer currently hardcodes `libx264`, which requires a GPL-enabled FFmpeg build and violates the desired default-license policy.

Critical action: `LIC-001` / `AUD-LIC-001` must close before an MVP release.

## AUDIT-2026-08-06-001 — Dependency vulnerability baseline

Scope: all direct and transitive NuGet packages.

Evidence:

```text
dotnet list .\CatClipComposer\CatClipComposer.sln package --vulnerable --include-transitive
```

Initial finding: `SQLitePCLRaw.lib.e_sqlite3 2.1.6` was reported with high-severity advisory `GHSA-2m69-gcr7-jv3q` / `CVE-2025-6965`.

Remediation: explicitly pinned `SQLitePCLRaw.bundle_e_sqlite3 2.1.12`, rebuilt, reran the application startup smoke test, and reran the audit.

Result: zero known vulnerable packages from configured sources. `AUD-DEP-001` closed.
