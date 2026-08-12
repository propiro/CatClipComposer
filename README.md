# Cat Clip Composer

Cat Clip Composer is ENTIRELY vibecoded. This is an experiment to see how good (or bad!) vibecoding without manual touching code can get.

Cat Clip Composer is a focused Windows desktop application for building YouTube-ready compilations from folders of short video clips. It catalogs clips once, lets you assemble a simple ordered timeline, and renders the result through FFmpeg.

The software includes a photo of Mr. Cat as its splash screen.

Current application and component version: **0.1.19**.

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

- Configure one or more source folders, portable metadata/custom-font locations, output/project folders, and automatic startup rescanning.
- Scan MP4, WebM, AVI, MOV, MKV, and M4V files, including optional subfolder scanning.
- Store clip metadata and export history in a durable SQLite database.
- Generate and cache static thumbnails plus a configurable, evenly sampled contact sheet with FFmpeg.
- Search names, paths, and editable tags in a multi-select recycled browser without opening every video.
  Its header cycles between a thumbnail list, small grid, and large grid; both grid sizes are configurable in
  Preferences, and single or mass tag edits are available from the context menu.
- Preview a selected library clip—or double-click its Video timeline block—in muted Clip Preview, with a
  permanently visible autoplay checkbox. Clip and Project Preview can remain joined as tabs or split into
  resizable left/right viewports. Prerender Preview renders the selected timeline range when one exists and
  otherwise renders only the current frame; adjacent Frame and All actions are explicit. Temporary previews
  use Windows-compatible H.264, and neither preview action records an export.
- Add clips more than once, reorder them, remove them, and compare the total against a project-specific target duration.
- Add a still image anywhere on the timeline. Put it first for a splash screen, between videos for a mid-roll, or last for an outro.
- Add, edit, time, and remove multiple PNG/JPG, text, music, and individually styled progress effects; choose installed Windows or visibly marked portable-folder fonts.
- Select visible text or image overlays directly in Project Preview, then drag, scale, or rotate their
  on-canvas gizmos. OK/Enter commits a transform and Cancel/Escape restores it. Preview selection stays
  synchronized with the timeline and Layers / Used Clips panel, while exact X/Y, scale, rotation, placement
  presets, and transparency fade-in/out remain editable in the overlay dialog.
- Time effects with Start/End fields (or optional duration entry), set them to the whole timeline in one click,
  adjust the interval on one compact mini timeline, and use bounded sliders/arrows for effect values while
  retaining exact manual numeric entry.
- Set clip Fit, Fill, or Stretch plus fade-in/out and source volume; add the configurable blur-content
  background module on the Background timeline when vertical media should fill a horizontal project.
- Render solid, segmented, or tick progress effects over the complete project, a selected segment, or a custom range.
- Choose YouTube 1080p/4K/Shorts, square, classic 4:3, or custom resolution/FPS/bitrate/quality with configurable MPEG-4/H.264 video and AAC audio.
- Safely render to a temporary file before replacing the selected destination.
- Show render progress, support cancellation, and record which source clips were used in every completed output.
- Browse export history and per-clip completed-project use, including project name/path, date, and final output.
- Create, save, reopen, automatically recover, undo, and redo versioned `.nya` project timelines. Unsaved
  projects show an asterisk and closing offers literal Save, Don't save, and Cancel choices.
- Run config, scan/list, tag/usage, project, layered render, and history workflows headlessly with text or JSON output and stable exit codes.
- Work in a compact, high-contrast monochrome four-panel editor workspace. Panel docking, window geometry,
  splitter sizes, preview split/join state, active preview tab, focused panel, and expanded panel persist.
- Add, remove, collapse, color-code, and vertically reorder named timelines. The topmost visual track is
  composited above visual tracks below it; Ctrl-select/drag, snapping, frame playhead selection, Shift/Ctrl
  range selection, draggable range edges, Mark start/end controls, fit controls, and usable minus/value/plus
  controls for time zoom and track height are available. Timed-block dragging preserves the original grab
  point and previews the exact landing range; either edge resizes non-primary timed blocks, with optional
  snapping to source-clip boundaries. Drag track names vertically to reorder the render stack; double-click a
  Video track name to bring Project Preview forward, or double-click a timed effect/overlay to edit it.
- Right-click the playhead to preview from that frame or mark either range edge; right-click a selected ruler
  range to preview only that interval. Left-clicking an empty compatible lane opens its add-effect menu;
  track headers and compatible items expose the same filtered effect actions.
- Select or double-click an item under Layers / Used Clips to edit its scaling mode, fades, and volume, or add
  a plugin effect pre-timed to that item's exact project interval. Timeline and Used Clips selections remain
  synchronized, and a muted-yellow block edge identifies media not covered by the current rendered preview.
- Browse large catalogs through a recycled virtualized grid, expand the browser across the workspace while
  keeping the timeline available, and drag thumbnail cards onto a chosen Video timeline.
- Load versioned effect/source modules from the portable `plugins` folder. The built-in module assembly
  supplies blur-content background, timed video blur, and PNG splash-screen functionality.
- Compose overlays and Video blur according to visual track order, and preview the selected timeline frame in
  a window snapped beside the effect editor with manual or debounced automatic refresh.
- Show the shared semantic version in the main title/status bars and through the headless `--version` option;
  place the extensionless `version_<version>` marker beside both executables so an unpacked build is visibly
  distinguishable without launching it.
- Render saved layered projects headlessly and publish the GUI/CLI/runtime as two single-file executables
  with the pinned audited FFmpeg runtime under `thirdparty` and portable custom fonts under `fonts`.
- See a wide split Mr Cat startup/rescan splash with a named pipeline stage, visible
  percentage, progress bar, and timestamped console log. Startup separately reports software layout, plugins,
  catalog, project-file/recovery, fonts, and editor readiness. When configured, library scans report per-file
  counts and scan percentage; disabled or unconfigured scans are explicitly marked skipped. The opening and
  completion pause for 0.2–0.5 seconds, ordinary fast lines are paced 0.05–0.1 seconds apart, and real scans
  report immediately without artificial per-line delay.

## Is FFmpeg required?

**Yes.** Cat Clip Composer is the editor and project manager; FFmpeg and FFprobe are its media engine. The
application uses them to inspect source files, generate thumbnails and contact sheets, render project
previews, composite effects and overlays, mix audio, and export the finished video. Windows playback in the
preview controls does not replace that processing engine.

Normal builds and portable packages already contain a pinned Windows x64 FFmpeg/FFprobe shared runtime, so
most users should not install FFmpeg separately. The default `ffmpeg.exe` preference resolves to
`thirdparty\ffmpeg\ffmpeg.exe` beside the application.

## Installation

### Portable application

Cat Clip Composer uses a portable, one-folder Windows package rather than an installer. Check the
[latest GitHub Release](https://github.com/propiro/CatClipComposer/releases/latest) for a Windows x64
package. If no release asset is listed yet, use the source-build instructions below.

When a portable package is available:

1. Download `CatClipComposer-v<version>-win-x64.zip`, not GitHub's automatically generated **Source code**
   archives.
2. Optionally verify the download with the adjacent `.sha256` checksum file.
3. Extract the complete `CatClipComposer` folder to a writable location.
4. Keep `version_<version>` plus the `thirdparty`, `plugins`, `fonts`, and `docs` folders beside
   `CatClipComposer.exe`. The marker filename should match the version in the Release name.
5. Run `CatClipComposer.exe`. The normal self-contained package does not need a separate .NET installation.

Do not copy only the executable: the application also needs the bundled plugin and FFmpeg files.

The application is not currently code-signed. Windows SmartScreen may therefore show an unknown-publisher
warning on first launch. Only continue when the archive came from this repository's Releases page and its
SHA-256 matches the published checksum; do not disable Windows security globally.

### Build from source

Prerequisites:

- Windows 10 22H2 or later, x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Git](https://git-scm.com/download/win) and [Git LFS](https://git-lfs.com/)

Clone the repository and hydrate the FFmpeg binaries stored through Git LFS:

```powershell
git lfs install
git clone https://github.com/propiro/CatClipComposer.git
Set-Location .\CatClipComposer
git lfs pull
git lfs ls-files
```

Build and run:

```powershell
dotnet build .\CatClipComposer\CatClipComposer.sln --configuration Release --nologo
dotnet run --project .\CatClipComposer\CatClipComposer.csproj --configuration Release
dotnet run --project .\CatClipComposer.Cli\CatClipComposer.Cli.csproj --configuration Release -- --version
dotnet run --project .\CatClipComposer.Cli\CatClipComposer.Cli.csproj --configuration Release -- --help
```

Create the self-contained portable Windows x64 package under `publish\CatClipComposer`:

```powershell
.\scripts\Publish-Portable.ps1
```

See [portable deployment](docs/DEPLOYMENT.md) for framework-dependent publishing, package validation, and
safe replacement of an existing portable folder.

Maintainers publish the validated portable ZIP and its SHA-256 file as assets on a `v<version>` GitHub
Release. Generated build output is never committed to the source branch.

## Using a separately downloaded FFmpeg

A separate download is optional. It is useful when testing another compatible build or restoring a missing
local tool folder.

- [Official FFmpeg download page](https://ffmpeg.org/download.html) — FFmpeg publishes source and links to
  third-party providers of ready-to-run Windows executables.
- [BtbN FFmpeg Windows builds](https://github.com/BtbN/FFmpeg-Builds/releases) — the provider used for Cat
  Clip Composer's audited bundle.
- [Exact bundled BtbN release](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-08-06-13-39)
  — the reproducible build currently carried by this repository.

For the closest match to the bundled runtime, download a Windows x64 archive whose name contains
`win64-lgpl-shared-8.1`. Extract the complete archive and keep `ffmpeg.exe`, `ffprobe.exe`, and all supplied
DLLs together. In Cat Clip Composer, open **Preferences**, find **FFmpeg executable**, select that
`ffmpeg.exe`, and save. Its matching `ffprobe.exe` must be in the same folder.

The build must provide the `drawtext` filter and the native `mpeg4` and `aac` encoders. The optional
Media Foundation preset also needs `h264_mf`. A full compatibility check is:

```powershell
& 'C:\Tools\ffmpeg\bin\ffmpeg.exe' -version
& 'C:\Tools\ffmpeg\bin\ffmpeg.exe' -filters  | Select-String 'drawtext'
& 'C:\Tools\ffmpeg\bin\ffmpeg.exe' -encoders | Select-String 'mpeg4|aac|h264_mf'
& 'C:\Tools\ffmpeg\bin\ffprobe.exe' -version
```

Replace the example path with the location you extracted.

On the first run:

1. Open **Preferences**.
2. Add the folders containing source clips.
3. Choose the output and editable-project folders. Leave FFmpeg at its default to use the bundle.
4. Save Preferences; startup rescanning is enabled by default.
5. Expand **Project settings** at the bottom-left of Project Preview to choose the timeline target and output preset.
6. Drag clips to the timeline, add any still screens or timed effects, and select **Export**.

To add another lane, select **+ Track** in Layers / Used Clips, choose Video, Overlay, Audio, Progress,
Background, or Effects, and name it. Select that track header before adding an effect or layer item. Media
cards can be dropped directly onto any Video lane. Click a workspace panel and press Space to focus or
restore Content Browser, Layers / Used Clips, or Project Timeline.

## Configuration and local application data

Configuration is stored beside the executable:

```text
<executable directory>\CatClipComposer.ini
```

The executable directory must be writable when Preferences are saved. The catalog and generated cache remain outside the repository:

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
CatClipComposer.Plugins.BuiltIn/ Built-in dynamically discovered source/effect modules
```

## License notes

FFmpeg is free and open-source software: no purchase or subscription is required to download and use it.
“Free” does not mean public domain or free of license conditions. FFmpeg is normally LGPL software, while
enabling optional GPL components changes the resulting binary to the GPL; a build made with
`--enable-nonfree` is not redistributable. See FFmpeg's [license details](https://ffmpeg.org/doxygen/trunk/md_LICENSE.html)
and [legal/compliance guidance](https://ffmpeg.org/legal.html).

Cat Clip Composer's pinned FFmpeg build is the shared LGPL v3 variant. It reports neither `--enable-gpl` nor
`--enable-nonfree`, and its replaceable DLLs, exact license, corresponding source location, build
configuration, and hashes ship under `thirdparty\ffmpeg`. Cat Clip Composer invokes `ffmpeg.exe` and
`ffprobe.exe` as external programs and does not require `libx264` or another GPL component for normal use.

You may select another compatible FFmpeg for local use. If you redistribute Cat Clip Composer together with
a different FFmpeg build, inspect that build's `ffmpeg -version` output and comply with its actual license and
the licenses of its enabled libraries. In particular, do not redistribute a `nonfree` build. This summary is
not legal advice; preserve [the complete third-party notices](THIRD_PARTY_NOTICES.md) when distributing the
application.

## Next refinements

- Add capability detection and additional non-GPL hardware encoder presets.
- Add trimming without turning the application into a general-purpose editor.
