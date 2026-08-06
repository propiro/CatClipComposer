# Cat Clip Composer

Cat Clip Composer is a focused Windows desktop application for building YouTube-ready compilations from folders of short video clips. It catalogs clips once, lets you assemble a simple ordered timeline, and renders the result through FFmpeg.

## Documentation

- [Project goals and feature status](docs/PROJECT.md)
- [Architecture and module boundaries](docs/ARCHITECTURE.md)
- [Software stack and license policy](docs/STACK_AND_LICENSES.md)
- [INI configuration reference](docs/CONFIGURATION.md)
- [Prioritized TODO register](docs/TODO.md)
- [Worklog](docs/WORKLOG.md)
- [Audit log](docs/AUDIT_LOG.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)

## Current features

- Configure one or more source folders, an output folder, FFmpeg, a target duration, and landscape or portrait output.
- Scan MP4, WebM, AVI, MOV, MKV, and M4V files, including optional subfolder scanning.
- Store clip metadata and export history in a durable SQLite database.
- Generate and cache thumbnails with FFmpeg.
- Search and browse selectable thumbnail cards with index, duration, dimensions, optional file name, and usage count.
- Preview a selected clip through the Windows media stack. Files with unsupported Windows codecs can still be rendered by FFmpeg.
- Add clips more than once, reorder them, remove them, and compare the total against a configurable timeline axis.
- Add a still image anywhere on the timeline. Put it first for a splash screen, between videos for a mid-roll, or last for an outro.
- Render an optional PNG/JPG watermark, text overlay, system font or custom TTF/OTF font, and choose the overlay position.
- Render no progress bar, one bar for the complete compilation, or a separate bar for every timeline segment.
- Normalize mixed resolutions and aspect ratios to 1920×1080 landscape or 1080×1920 portrait, 30 fps, H.264 video, and AAC audio.
- Safely render to a temporary file before replacing the selected destination.
- Show render progress, support cancellation, and record which source clips were used in every completed output.
- Browse export history and jump to an existing output or original source file in File Explorer.

## Requirements

- Windows 10 or later
- .NET 8 SDK for development, or the .NET 8 Desktop Runtime for a framework-dependent deployment
- `ffmpeg.exe` and `ffprobe.exe` from the same FFmpeg build
- A GPL-enabled FFmpeg build containing `libx264` for the current H.264 export preset
- An FFmpeg build with the `drawtext` filter for text overlays

FFmpeg can be placed on `PATH`, or `ffmpeg.exe` can be selected in the application’s Options window. When an explicit executable is selected, `ffprobe.exe` is expected beside it.

FFmpeg binaries are intentionally not committed or bundled. This keeps the application independent from a particular FFmpeg distribution and its exact license/configuration.

## Build and run

```powershell
dotnet build .\CatClipComposer\CatClipComposer.sln
dotnet run --project .\CatClipComposer\CatClipComposer.csproj
```

On the first run:

1. Open **Options**.
2. Add the folders containing source clips.
3. Choose the output folder and `ffmpeg.exe` if FFmpeg is not on `PATH`.
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
└── thumbnails\
```

The SQLite schema contains media metadata, availability and usage fields, completed render jobs, and the ordered source clips used by each render.

## Project structure

```text
CatClipComposer/                 WPF user interface and presentation models
CatClipComposer.Core/            Domain models and service contracts
CatClipComposer.Infrastructure/  SQLite, settings, FFprobe, FFmpeg thumbnails and rendering
```

## License notes

All current runtime components are free to use, but “free” does not mean “without a license or obligations.” See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) before distributing the application.

In particular, FFmpeg is LGPL 2.1-or-later by default, but optional GPL components change the resulting FFmpeg build to GPL. The current exporter requests `libx264`, so it expects a GPL-enabled build. Cat Clip Composer launches FFmpeg as a separate executable and does not currently redistribute it.

## Next refinements

- Save and reopen named timeline projects.
- Add multiple overlays with individual start/end times instead of one compilation-wide image and text layer.
- Add FFmpeg-generated contact-sheet/slideshow preview when Windows cannot decode a source format.
- Add configurable software/hardware encoder presets and an LGPL-only export option.
- Add trimming and per-segment volume controls without turning the application into a general-purpose editor.
