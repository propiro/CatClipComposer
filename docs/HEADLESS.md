# Headless command-line interface

Last reviewed: 2026-08-07

`CatClipComposer.Cli` exposes catalog, metadata, project, render, history, and configuration workflows without starting WPF. It uses the same Core services, Infrastructure adapters, SQLite catalog, INI/project schemas, FFmpeg renderer, and export-history transaction as the desktop application.

## Build and invoke

```powershell
dotnet build .\CatClipComposer\CatClipComposer.sln --configuration Release
dotnet .\CatClipComposer.Cli\bin\Release\net8.0\CatClipComposer.Cli.dll --help
```

After publishing, invoke `CatClipComposer.Cli.exe` in place of `dotnet <dll>`.

```text
CatClipComposer.Cli <command> [options]
```

Options may appear before or after the command. Option names and commands are case-insensitive. Quote paths containing spaces. Repeated `--clip` and `--screen` options retain their relative command-line order.

## Shared paths and common options

- **`--config <file>`:** Override the INI path. The default is `CatClipComposer.ini` beside the CLI.
- **`--data <folder>`:** Override the folder containing `catalog.db`, previews, thumbnails, and recovery.
- **`--json`:** Write one JSON result to stdout and suppress progress. Errors are also JSON on stdout.
- **`--version`:** Show the shared version without creating a database or data directory.
- **`--help`:** Show help without creating a database or data directory.

Deploy the GUI and CLI executables to the same directory when they should use one default INI. During development their build output directories differ, so pass `--config` explicitly to share a file. Both use the same default local application-data catalog unless `--data` is supplied.

All commands except help/version initialize the selected SQLite catalog. Human-readable results use stdout, while progress and diagnostics use stderr. In JSON mode stdout contains exactly one JSON document and no progress lines.

## Commands

### `config`

Shows the resolved INI path, whether the file exists, the resolved data/database paths, loaded plugin
modules/diagnostics, and all effective settings, including content-browser mode, thumbnail sizes, panel docks,
window/workspace dimensions, preview layout/tab, focused/expanded panels, and first-startup completion state.
A missing INI is valid and displays normalized defaults.

```powershell
CatClipComposer.Cli.exe config
CatClipComposer.Cli.exe config --json --config "D:\Portable\CatClipComposer.ini"
```

### `scan`

Scans the source folders in the selected INI, probes videos, generates static thumbnails and configurable contact sheets, updates metadata, and marks disappeared catalog files unavailable. At least one source folder must be configured.

```powershell
CatClipComposer.Cli.exe scan
CatClipComposer.Cli.exe scan --regenerate-previews
CatClipComposer.Cli.exe scan --json --config "D:\Portable\CatClipComposer.ini"
```

Normal scanning keeps valid cached images. `--regenerate-previews` forces every static thumbnail and contact
sheet to be rebuilt while catalog metadata is refreshed.

A completed scan with any folder/file warning returns exit code `4`; its JSON result has `status: "completedWithWarnings"` and includes the ordered `errors` array.

### `list`

Lists available media IDs for render automation. Add `--all` to include files currently recorded as unavailable.

```powershell
CatClipComposer.Cli.exe list
CatClipComposer.Cli.exe list --all --json
```

The JSON item fields include ID, file name/path, duration, dimensions, audio and availability flags, tags, both preview paths, usage count, last-use time, and last output path.

### `tag` and `usage`

`tag` replaces normalized semicolon/comma-separated tags for a catalog ID; `--clear-tags` removes them. `usage` returns only completed exports containing that clip, including project name/path, output, date, and occurrences.

```powershell
CatClipComposer.Cli.exe tag --clip 42 --tags "orange cat; indoor"
CatClipComposer.Cli.exe tag --clip 42 --clear-tags
CatClipComposer.Cli.exe usage --clip 42 --json
```

### `render`

Renders an ordered composition and records the completed export and source usage in the shared catalog.

- **`--output <file>`:** Required. A relative path resolves under the INI output folder.
- **`--clip <catalog-id>`:** Add an available catalog clip. Repeat it to reuse a clip.
- **`--screen "<seconds>|<image-path>"`:** Add a positive-duration still. Quote it in PowerShell because
  `|` is a shell operator.
- **`--orientation <value>`:** Optional `landscape` or `portrait`; otherwise use the saved project's shape
  or landscape for an ad-hoc render.
- **`--encoder <value>`:** Optional `native-mpeg4`, `windows-h264`, or `libx264-gpl`; otherwise use the saved
  project or the non-GPL native MPEG-4 default.
- **`--project-file <file>`:** Render a saved project's enabled tracks and output settings and associate its
  identity with history.
- **`--project-name <name>`:** Associate a name when no project file supplies one.
- **`--overwrite`:** Explicitly permit replacement when the output already exists.

Choose either a saved `--project-file` or at least one ad-hoc `--clip`/`--screen`. A saved project supplies
video/still ordering, effects, timed overlays, progress, audio layers, output dimensions/FPS/quality/bitrates,
and history identity. It cannot be mixed with ad-hoc segments. The INI supplies tool and output-folder paths,
not compilation-wide effects.

```powershell
CatClipComposer.Cli.exe render `
  --output "weekly-cats.mp4" `
  --screen "3|D:\Artwork\splash.png" `
  --clip 12 `
  --clip 7 `
  --screen "2|D:\Artwork\outro.png" `
  --orientation landscape `
  --encoder native-mpeg4
```

Existing outputs are rejected with exit code `2` unless `--overwrite` is present. The `libx264-gpl` value is an explicit GPL-dependent opt-in; it is never the default.

### `project`

Creates or inspects a versioned `.nya` document. `--project-file <file>` is required. Add `--create`, optional
`--project-name <name>`, and optional `--overwrite` to create a new empty six-track document; without
`--create`, the command validates and prints the existing project. JSON includes identity, timestamps,
output settings, ordered track metadata, and item counts.

```powershell
CatClipComposer.Cli.exe project --create --project-file "D:\Projects\Cats.nya" --project-name "Cats"
CatClipComposer.Cli.exe project --project-file "D:\Projects\Cats.nya" --json
```

### `history`

Lists completed outputs and their ordered catalog clips.

```powershell
CatClipComposer.Cli.exe history
CatClipComposer.Cli.exe history --json
```

## Exit codes

- **0 — Success:** Consume the stdout result.
- **2 — Invalid arguments or unsafe implicit overwrite:** Correct command syntax or options.
- **3 — Invalid configuration/data location:** Correct INI, data, or required source paths.
- **4 — Scan completed with warnings:** Catalog updates were committed; inspect warning details.
- **5 — Execution failed:** Inspect FFmpeg, filesystem, SQLite, or another runtime error.
- **130 — Cancelled:** Treat Ctrl+C or cancellation-token termination as an interrupted operation.

JSON error results have `status: "error"`, `exitCode`, `error`, and an optional `hint`. Successful render JSON includes the output path, duration, resolved orientation/encoder, and segment count. Durations and UTC timestamps use the standard `System.Text.Json` invariant string representation.

## Automation guarantees and limits

- Commands are non-interactive; they never open a window or prompt.
- `render` never silently overwrites an existing destination.
- Output is first rendered to a unique partial file and moved into place only after FFmpeg succeeds.
- A completed render and its ordered clip usage are recorded through the same application service as the GUI.
- JSON property names use camel case. Additive fields may appear later; automation should ignore fields it does not consume.
- Catalog media IDs are durable for one database but should not be assumed portable across different `--data` folders.
