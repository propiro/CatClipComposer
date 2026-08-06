# Cat Clip Composer

Cat Clip Composer is a focused Windows desktop application for building YouTube-ready compilations from folders of short video clips. It catalogs clips once, lets you assemble a simple ordered timeline, and renders the result through FFmpeg.

Current application and component version: **0.1.4**.

## Documentation

- [Project goals and feature status](docs/PROJECT.md)
- [Architecture and module boundaries](docs/ARCHITECTURE.md)
- [Software stack and license policy](docs/STACK_AND_LICENSES.md)
- [INI configuration reference](docs/CONFIGURATION.md)
- [Headless CLI reference](docs/HEADLESS.md)
- [Project files and crash recovery](docs/PROJECT_FILES.md)
- [Output presets and render layers](docs/OUTPUT_PRESETS.md)
- [Portable deployment](docs/DEPLOYMENT.md)
- [Prioritized TODO register](docs/TODO.md)
- [Worklog](docs/WORKLOG.md)
- [Audit log](docs/AUDIT_LOG.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)

## Current features

- Configure one or more source folders, an output folder, a target duration, and landscape or portrait output.
- Scan MP4, WebM, AVI, MOV, MKV, and M4V files, including optional subfolder scanning.
- Store clip metadata and export history in a durable SQLite database.
- Generate and cache static thumbnails plus a configurable, evenly sampled contact sheet with FFmpeg.
- Search names, paths, and editable tags in selectable, recycled catalog rows without opening every video.
- Preview a selected clip through the Windows media stack, with the contact sheet as a codec-independent content preview.
- Add clips more than once, reorder them, remove them, and compare the total against a configurable timeline axis.
- Add a still image anywhere on the timeline. Put it first for a splash screen, between videos for a mid-roll, or last for an outro.
- Add, edit, time, and remove multiple PNG/JPG, text/custom-font, music, and progress layers.
- Set clip Fit, Fill, Stretch, or animated Blur Background plus fade-in/out and source volume.
- Render no progress bar, one bar for the complete compilation, or a separate bar for every timeline segment.
- Choose YouTube 1080p/4K/Shorts, square, classic 4:3, or custom resolution/FPS/bitrate/quality with configurable MPEG-4/H.264 video and AAC audio.
- Safely render to a temporary file before replacing the selected destination.
- Show render progress, support cancellation, and record which source clips were used in every completed output.
- Browse export history and per-clip completed-project use, including project name/path, date, and final output.
- Create, save, reopen, and automatically recover versioned editable project timelines.
- Run config, scan/list, tag/usage, project, layered render, and history workflows headlessly with text or JSON output and stable exit codes.
- Work in a compact, high-contrast monochrome four-panel editor workspace with resizable splitters and persisted panel docking.
- Browse large catalogs through a recycled virtualized list, expand the browser across the workspace while keeping the timeline available, and drag thumbnail rows directly onto it.
- Show the shared semantic version in the main title/status bars and through the headless `--version` option.
- Render saved layered projects headlessly and publish the GUI/CLI/runtime as two single-file executables
  with the pinned audited FFmpeg runtime under `thirdparty`.

## Requirements

- Windows 10 or later
- .NET 8 SDK for development, or the .NET 8 Desktop Runtime for a framework-dependent deployment

The repository includes a pinned Windows x64 FFmpeg/FFprobe shared runtime with the required DLLs and
`drawtext` filter. Normal builds and portable packages copy it automatically under `thirdparty\ffmpeg`.

The bundled LGPL v3 build provides native MPEG-4, AAC, and Media Foundation H.264 without GPL/nonfree build
flags. `libx264` remains an explicitly labeled opt-in for a user-supplied GPL build and is never required.

An explicit `ffmpeg.exe` can still be selected in Options as a local override; its matching `ffprobe.exe`
must sit beside it. See [deployment](docs/DEPLOYMENT.md) for the pinned version, hashes, notices, and upgrade
procedure.

## Build and run

```powershell
dotnet build .\CatClipComposer\CatClipComposer.sln
dotnet run --project .\CatClipComposer\CatClipComposer.csproj
dotnet run --project .\CatClipComposer.Cli\CatClipComposer.Cli.csproj -- --version
dotnet run --project .\CatClipComposer.Cli\CatClipComposer.Cli.csproj -- --help
```

On the first run:

1. Open **Options**.
2. Add the folders containing source clips.
3. Choose the output folder. Leave the FFmpeg field at its default to use the bundled runtime.
4. Choose the timeline target, orientation, overlays, and progress-bar style.
5. Select **Update catalog**.
6. Double-click clips or use **Add to timeline**, arrange the timeline, add any still screens, and select **Export MP4**.

## Configuration and local application data

Configuration is stored beside the executable:

```text
<executable directory>\CatClipComposer.ini
```

The executable directory must be writable when Options are saved. The catalog and generated cache remain outside the repository:

```text
%LOCALAPPDATA%\CatClipComposer\
├── catalog.db
├── thumbnails\
├── previews\
└── recovery\
```

The SQLite schema contains media metadata, availability and usage fields, completed render jobs, and the ordered source clips used by each render.

## Project structure

```text
CatClipComposer/                 WPF user interface and presentation models
CatClipComposer.Cli/             Headless commands, text/JSON output, and exit codes
CatClipComposer.Core/            Domain models and service contracts
CatClipComposer.Infrastructure/  SQLite, settings, FFprobe, FFmpeg thumbnails and rendering
```

## License notes

All current runtime components are free to use, but “free” does not mean “without a license or obligations.” See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) before distributing the application.

The bundled FFmpeg shared build is licensed under LGPL v3 and ships with its exact license, source location,
configuration, and hashes. Cat Clip Composer launches it as a separate executable and keeps its replaceable
DLLs under `thirdparty`. Optional GPL components are absent; `libx264` is only a clearly labeled custom-tool
opt-in.

## Next refinements

- Add capability detection and additional non-GPL hardware encoder presets.
- Add trimming without turning the application into a general-purpose editor.
