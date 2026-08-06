# Audit log

This is an append-only audit trail. Each audit records scope, findings, action IDs, and evidence. Closing an item requires a later closure entry; do not erase the original finding.

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
