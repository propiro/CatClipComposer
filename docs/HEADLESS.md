# Headless command-line interface

Last reviewed: 2026-08-06

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

| Option | Meaning |
|---|---|
| `--config <file>` | Override the INI path. The default is `CatClipComposer.ini` beside the CLI executable. |
| `--data <folder>` | Override the data folder containing `catalog.db`, `thumbnails`, `previews`, and recovery. The default comes from the INI metadata folder. |
| `--json` | Write one JSON result document to stdout and suppress progress output. Errors are also JSON on stdout. |
| `--help` | Show help without creating a database or data directory. Combine with `--json` for structured help and exit-code metadata. |

Deploy the GUI and CLI executables to the same directory when they should use one default INI. During development their build output directories differ, so pass `--config` explicitly to share a file. Both use the same default local application-data catalog unless `--data` is supplied.

All commands except help initialize the selected SQLite catalog. Human-readable results use stdout, while progress and diagnostics use stderr. In JSON mode stdout contains exactly one JSON document and no progress lines.

## Commands

### `config`

Shows the resolved INI path, whether the file exists, the resolved data/database paths, and all effective settings. A missing INI is valid and displays normalized defaults.

```powershell
CatClipComposer.Cli.exe config
CatClipComposer.Cli.exe config --json --config "D:\Portable\CatClipComposer.ini"
```

### `scan`

Scans the source folders in the selected INI, probes videos, generates static thumbnails and configurable contact sheets, updates metadata, and marks disappeared catalog files unavailable. At least one source folder must be configured.

```powershell
CatClipComposer.Cli.exe scan
CatClipComposer.Cli.exe scan --json --config "D:\Portable\CatClipComposer.ini"
```

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

| Option | Meaning |
|---|---|
| `--output <file>` | Required. A relative path is resolved under the INI output folder. |
| `--clip <catalog-id>` | Add an available catalog clip. Repeat as needed, including the same ID more than once. |
| `--screen "<seconds>\|<image-path>"` | Add a still screen of positive duration. In PowerShell, quote the value because `\|` is a shell operator. |
| `--orientation <value>` | Optional `landscape` or `portrait`; otherwise use the INI. |
| `--encoder <value>` | Optional `native-mpeg4`, `windows-h264`, or `libx264-gpl`; otherwise use the INI. |
| `--project-file <file>` | Validate and associate a saved project with successful-export history. |
| `--project-name <name>` | Associate a name when no project file supplies one. |
| `--overwrite` | Explicitly permit replacement when the output already exists. |

At least one `--clip` or `--screen` is required. The INI controls progress-bar style, overlay image/text/font/position, and FFmpeg path.

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

Creates or inspects a versioned `.ccproject` document. `--project-file <file>` is required. Add `--create`, optional `--project-name <name>`, and optional `--overwrite` to create a new empty five-track document; without `--create`, the command validates and prints the existing project. JSON includes identity, timestamps, output settings, ordered track metadata, and item counts.

```powershell
CatClipComposer.Cli.exe project --create --project-file "D:\Projects\Cats.ccproject" --project-name "Cats"
CatClipComposer.Cli.exe project --project-file "D:\Projects\Cats.ccproject" --json
```

### `history`

Lists completed outputs and their ordered catalog clips.

```powershell
CatClipComposer.Cli.exe history
CatClipComposer.Cli.exe history --json
```

## Exit codes

| Code | Meaning | Automation guidance |
|---:|---|---|
| `0` | Success | Consume stdout result. |
| `2` | Invalid arguments or unsafe implicit overwrite | Correct command syntax/options. |
| `3` | Invalid/unusable configuration or data location | Correct INI/data paths or required source settings. |
| `4` | Scan completed with one or more warnings | Catalog updates were committed; inspect warning details. |
| `5` | Execution failed | Inspect FFmpeg, filesystem, SQLite, or other runtime error. |
| `130` | Cancelled with Ctrl+C or a cancellation token | Treat as an interrupted operation. |

JSON error results have `status: "error"`, `exitCode`, `error`, and an optional `hint`. Successful render JSON includes the output path, duration, resolved orientation/encoder, and segment count. Durations and UTC timestamps use the standard `System.Text.Json` invariant string representation.

## Automation guarantees and limits

- Commands are non-interactive; they never open a window or prompt.
- `render` never silently overwrites an existing destination.
- Output is first rendered to a unique partial file and moved into place only after FFmpeg succeeds.
- A completed render and its ordered clip usage are recorded through the same application service as the GUI.
- JSON property names use camel case. Additive fields may appear later; automation should ignore fields it does not consume.
- Catalog media IDs are durable for one database but should not be assumed portable across different `--data` folders.
