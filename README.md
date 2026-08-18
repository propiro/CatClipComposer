# Cat Clip Composer

Cat Clip Composer is ENTIRELY vibecoded. This is an experiment to see how good (or bad!) vibecoding without manual touching code can get.

Cat Clip Composer is a focused Windows desktop application for building YouTube-ready compilations from folders of short video clips. It catalogs clips once, lets you assemble a simple ordered timeline, and renders the result through FFmpeg.

The software includes a photo of Mr. Cat as its splash screen.

Current source application and component version: **0.1.36**. The latest published v0.1.32 Windows package
remains the full self-contained build; future release packages default to the smaller light format below.

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
  Its header cycles between a thumbnail list, small grid, large grid, and extra-large grid; card sizes are configurable in
  Preferences, and single or mass tag edits are available from the context menu. Both tag editors preserve
  entered text while offering quick buttons for the ten most-used library tags. Sort by name, newest file
  date, duration, or custom tag. Green, yellow, and blue thumbnail corners identify clips used in the current
  project, referenced by another saved/recovered project, or newly imported and not yet previewed.
- Preview a selected library clip—or double-click its Video timeline block—in muted Clip Preview, with a
  permanently visible autoplay checkbox. Clip and Project Preview can remain joined as tabs or split into
  resizable left/right viewports. A bordered Prerender group gives Frame, Range, and All explicit LQ/HQ actions;
  Range falls back to the current frame when no range exists. Multiple valid prerender chunks remain
  seekable from the timeline and are restored between sessions. A
  Preview Settings rollout offers six temporary-resolution stops from 10% through 100% plus higher-quality
  scaling for the selected image overlay. Temporary previews use Windows-compatible H.264, and no preview
  action records an export or changes final export settings.
- Add clips more than once, reorder them, remove them, and compare the total against a project-specific target duration.
- Add a still image anywhere on the timeline. Put it first for a splash screen, between videos for a mid-roll, or last for an outro.
- Add, edit, time, disable/enable, and remove multiple PNG/JPG, text, music, and individually styled progress
  effects from the Content Browser's grouped Effects tab; choose installed Windows or visibly marked
  portable-folder fonts. Copy/paste complete effect blocks at the playhead or an empty compatible lane, and
  drag non-source blocks between compatible timelines. Text overlays support configurable outline color,
  width, and smoothness; their portable INI presets include recognizable text/font thumbnails.
- Select visible text or image overlays directly in Project Preview, then drag, scale, or rotate their
  on-canvas gizmos. OK/Enter commits a transform and Cancel/Escape restores it. Preview selection stays
  synchronized with the timeline and Project Layers Data panel; the timeline-selected item wins hit testing
  where objects overlap, and multiline text proxies share renderer-normalized line breaks. Exact X/Y, scale,
  rotation, placement
  presets, and transparency fade-in/out remain editable in the overlay dialog. A moved live proxy uses the
  exact configured opacity; the old prerendered location receives a crossed **MOVED CONTENT** notice until
  the frame is prerendered again.
- Time effects with Start/End fields (or optional duration entry), set them to the whole timeline in one click,
  adjust the interval on one compact mini timeline, and use bounded sliders/arrows for effect values while
  retaining exact manual numeric entry.
- Set clip Fit, Fill, or Stretch plus fade-in/out and source volume; add the configurable blur-content
  background module on the Background timeline when vertical media should fill a horizontal project.
- Render solid, segmented, or tick progress effects over the complete project, selected clips, or a custom
  range; accepted visual styles become defaults and can be copied/pasted between progress blocks.
- Choose YouTube 1080p/4K/Shorts, square, classic 4:3, or custom resolution/FPS/bitrate/quality with configurable MPEG-4/H.264 video and AAC audio.
- Safely render to a temporary file before replacing the selected destination. Export uses an in-application
  directory/file chooser followed by a dedicated progress window with destination, current stage, FFmpeg media
  time, elapsed time, percentage, cancellation, and a timestamped activity log.
- Open **Project settings > FFmpeg command...** to inspect the complete generated command immediately with a
  unique proposed MP4 path in the current export directory. The window shows the command in an editable field
  and can copy the edited command or execute it through the configured
  FFmpeg executable, with an explicit confirmation and without invoking a command shell.
- Show render progress, support cancellation, and record which source clips were used in every completed output.
- Toggle a modeless History surface for reverse-chronological actions, completed exports, and log/crash files;
  inspect any clip's location, technical properties, catalog dates, project references, and completed-project use.
- Create, save, reopen, automatically recover, undo, and redo versioned `.nya` project timelines with a
  configurable history depth (32 project edits by default). Each undo/redo arrow performs one step; its adjacent
  dropdown can jump atomically to any currently retained earlier/later history moment. Unsaved
  projects show an asterisk and closing offers literal Save, Don't save, and Cancel choices.
- Open About from the top-right `?` button to see build/experiment details and the complete, uncropped Mr. Cat splash image. Its
  manual update checker separately reports newer public repository code and newer downloadable Windows Release
  ZIPs; it never downloads or installs anything, sends no credentials, and only opens allow-listed project pages.
- Run config, scan/list, tag/usage, project, layered render, and history workflows headlessly with text or JSON output and stable exit codes.
- Work in a compact, high-contrast monochrome four-panel editor workspace. Panel docking, window geometry,
  splitter sizes, preview split/join state, active preview tab, focused panel, and expanded panel persist.
- Add, remove, collapse, color-code, and vertically reorder named timelines. New projects start with Overlays,
  Video, Progress, Background, and Audio in that top-to-bottom order. The topmost visual track is
  composited above visual tracks below it; Ctrl-select/drag, snapping, frame playhead selection, Shift/Ctrl
  range selection, draggable range edges, Mark start/end controls, fit controls, and usable minus/value/plus
  controls for time zoom and track height are available. Timed-block dragging preserves the original grab
  point and previews the exact landing range; either edge resizes non-primary timed blocks, with optional
  snapping to source-clip boundaries. Drag track names vertically to reorder the render stack; double-click a
  Video track name to bring Project Preview forward, or double-click a timed effect/overlay to edit it. When
  blocks overlap on one lane, the later-added block is visually and interactively on top. Mouse-wheel over
  Project Preview zooms the image; mouse-wheel over the timeline pans backward/forward.
- Right-click the playhead to preview from that frame or mark either range edge; right-click a selected ruler
  range to preview only that interval. Left-clicking an empty compatible lane opens its add-effect menu;
  track headers and compatible items expose the same filtered effect actions.
- Select or double-click an item under Project Layers Data to edit its scaling mode, fades, and volume, or add
  a plugin effect pre-timed to that item's exact project interval. Timeline and Used Clips selections remain
  synchronized, and a muted-yellow block edge identifies media not covered by the current rendered preview.
- Browse large catalogs through a recycled virtualized grid, expand the browser across the workspace while
  keeping the timeline available, and drag thumbnail cards onto a chosen Video timeline.
- Load versioned effect/source modules from the portable `plugins` folder. The built-in module assembly
  supplies blur-content background, timed video blur, and PNG splash-screen functionality.
- Compose overlays and Video blur according to visual track order, and preview the selected timeline frame in
  a same-width window above native overlay or plugin-effect editors with the actual project background,
  progress, elapsed time, and manual or debounced refresh. Editors consistently group content, timeline,
  transform, and appearance/effect adjustment sections.
- Show the shared semantic version in the main title/status bars and through the headless `--version` option;
  place the extensionless `version_<version>` marker beside both executables so an unpacked build is visibly
  distinguishable without launching it.
- Render saved layered projects headlessly and publish the GUI/CLI as clean single-file entry points by default.
  Light executables use the installed .NET 8 Desktop Runtime; an explicit Full mode embeds .NET. Both formats
  keep application DLL/JSON clutter out of the root while leaving the pinned audited FFmpeg runtime under
  `thirdparty`, built-in modules under `plugins`, and portable custom fonts under `fonts`.
- See a wide split Mr Cat startup/rescan splash with a named pipeline stage, visible
  percentage, progress bar, and timestamped console log. Startup separately reports software layout, plugins,
  catalog, project-file/recovery, fonts, and editor readiness. When configured, library scans report per-file
  counts and scan percentage; disabled or unconfigured scans are explicitly marked skipped. The opening and
  completion pause for 0.1–0.2 seconds, ordinary fast lines are paced 0.02–0.04 seconds apart, and real scans
  report immediately without artificial per-line delay. The first successful launch in an installation stays
  visible for at least five seconds; its completion is persisted in the portable INI, and later startups remain
  visible for at least approximately three seconds.

## Installation

### Which installation type should I use?

**Q: I already have the Microsoft .NET 8 Desktop Runtime x64. Which package should I use?**

**A:** Use the **Light** package. It is the normal choice for future releases, contains all Cat Clip Composer
features, and is much smaller because it uses the .NET 8 Desktop Runtime already installed in Windows. Look for
an asset named `CatClipComposer-v<version>-win-x64-light.zip`.

**Q: I do not have .NET 8, or I do not want to install it separately. Which package should I use?**

**A:** Use a **Full** or **self-contained** package. It carries its own .NET runtime and does not require a
separate .NET installation, but the download is substantially larger. The current v0.1.32 full package is named
`CatClipComposer-v0.1.32-win-x64.zip`. Future releases default to Light; when a newer Full package is offered,
it will be identified as Full/self-contained in its asset name and Release notes.

**Q: Is the ordinary “.NET Runtime 8” enough for the Light package?**

**A:** No. Cat Clip Composer uses WPF, so the Light package specifically needs the **.NET 8 Desktop Runtime
x64**. The Desktop Runtime includes the ordinary .NET Runtime. The .NET 8 SDK also includes it, but end users do
not need the SDK or a programming environment. To check an existing installation, run `dotnet --list-runtimes`
and look for `Microsoft.WindowsDesktop.App 8.x` under the x64 .NET installation.

**Q: What happens if I accidentally download Light without the required runtime?**

**A:** The native .NET application host displays a Windows prompt with a Microsoft download link before Cat
Clip Composer starts. Install **.NET Desktop Runtime 8 for Windows x64** from Microsoft's
[.NET 8 download page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), then run the same extracted
`CatClipComposer.exe` again.

### How do I install either portable package?

Cat Clip Composer uses a portable, one-folder Windows package rather than an installer. Check the
[latest GitHub Release](https://github.com/propiro/CatClipComposer/releases/latest) for a Windows x64
package. GitHub's automatically generated **Source code** archives are not runnable application packages.

**Q: How do I install the downloaded ZIP?**

**A:**

1. Download the Light package if .NET 8 Desktop Runtime x64 is installed, or a Full package if you do not want
   a separate runtime installation.
2. Optionally verify the download with the adjacent `.sha256` checksum file.
3. Extract the complete `CatClipComposer` folder to a writable location.
4. Keep `version_<version>` plus the `thirdparty`, `plugins`, `fonts`, and `docs` folders beside
   `CatClipComposer.exe`. The marker filename should match the version in the Release name.
5. Run `CatClipComposer.exe`.

The portable root intentionally contains only the GUI/CLI executables, INI, and visible version marker. Do not
copy only an executable: the application also needs the bundled `plugins` and `thirdparty` contents.

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

Create the normal light Windows x64 package under `publish\CatClipComposer`:

```powershell
.\scripts\Publish-Portable.ps1
```

Create a full self-contained package only when explicitly required:

```powershell
.\scripts\Publish-Portable.ps1 -SelfContained $true
```

See [portable deployment](docs/DEPLOYMENT.md) for runtime behavior, package validation, and safe replacement
of an existing portable folder.

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

To add another lane, select **+ Track** in Project Layers Data, choose Video, Overlay, Audio, Progress,
Background, or Effects, and name it. Select that track header before adding an effect or layer item. Media
cards can be dropped directly onto any Video lane. Click a workspace panel and press Space to focus or
restore Content Browser, Project Layers Data, or Project Timeline.

## Frequently asked questions

### Can Cat Clip Composer be used for videos that do not contain cats?

**A:** Yes. The name reflects the project's origin, not a content restriction. Cat Clip Composer can catalog,
arrange, preview, and render any compatible local video clips. Dogs, birds, reptiles, holiday footage, memes,
tutorial fragments, and entirely cat-free compilations are all acceptable.

### Is Mr. Cat really that beautiful?

**A:** Yes. Mr. Cat is indeed that beautiful. His photograph appears on the splash screen and in the About window,
where the application takes care to show the complete, uncropped cat.

### Which video files can I import?

**A:** The catalog accepts MP4, WebM, AVI, MOV, MKV, and M4V containers. Actual decoding also depends on the codecs
inside the file and the capabilities of the bundled FFmpeg build; a supported filename extension cannot rescue
a damaged file or guarantee support for every unusual codec.

### Does Cat Clip Composer modify my original video files?

**A:** No. Scanning reads source metadata and creates separate catalog/thumbnail data. Projects are stored as `.nya`
documents, previews are cached separately, and export writes a new output through a temporary file before replacing
an explicitly selected existing destination. Cat Clip Composer does not trim or rewrite source files in place.

### Can I trim clips like in a full nonlinear video editor?

**A:** Not currently. Source trimming remains a deferred feature. The present editor focuses on arranging complete
clips and adding timed video/image/text overlays, progress graphics, background blur, fades, music, per-clip
volume, fit/fill/stretch behavior, and other supported effects. It is deliberately narrower than Premiere,
DaVinci Resolve, or another general-purpose nonlinear editor.

### Can I make both horizontal and vertical videos?

**A:** Yes. Project presets include YouTube 1080p, 4K, and vertical Shorts, plus square, classic 4:3, and custom
resolution/FPS/bitrate/quality settings. Fit, Fill, Stretch, and the blurred-background effect help adapt source
clips to a different output shape.

### Why do I sometimes need to prerender before seeing the project video?

**A:** Clip Preview can play an individual source file directly. Project Preview must first ask FFmpeg to composite the
selected frame, range, or full timeline with its tracks and effects. Frame, Range, and All each provide LQ/HQ
prerender choices, and valid rendered chunks are cached so they can be revisited until an affected edit makes them
stale.

### Does Cat Clip Composer require FFmpeg?

**A:** Yes. FFmpeg and FFprobe inspect media, generate thumbnails, render previews, composite effects, mix audio, and
export the finished video. Normal Light and Full packages already include the project's pinned LGPL-compatible
FFmpeg/FFprobe bundle under `thirdparty\ffmpeg`, so users normally do not install it separately. Preferences can
point to another compatible local build when needed.

### How can I inspect, copy, or run the complete final FFmpeg command?

**A:** Expand **Project settings** below Project Preview and select **FFmpeg command...**. The editable command
window opens directly with a unique proposed MP4 path; edit that path or any FFmpeg arguments there, then use
**COPY TO CLIPBOARD** or **EXECUTE FFMPEG**. Direct execution
is restricted to the configured FFmpeg executable and does not invoke PowerShell or `cmd.exe`. It deliberately
bypasses Cat Clip Composer's temporary-output replacement and export-history transaction, so review edited paths
and arguments carefully; the confirmation also warns that the generated `-y` argument permits output overwrite.

### Can Cat Clip Composer export H.264 video?

**A:** Yes, through the bundled Windows Media Foundation `h264_mf` encoder option. The non-GPL compatibility default is
native MPEG-4 video with AAC audio. `libx264` is not part of the mandatory package and is only available as an
explicit opt-in when the user supplies a compatible FFmpeg build with the appropriate license obligations.

### Does Cat Clip Composer upload my clips or require an account?

**A:** No. Cataloging, project editing, preview rendering, and export run locally and require no Cat Clip Composer
account or cloud service. The About window has an optional manual GitHub update check; it reports available code
or binary versions but does not download or install them and sends no credentials.

### Which operating systems are supported?

**A:** The current application is for Windows x64, with Windows 10 22H2 or later as the documented baseline. There is no
supported macOS, Linux, ARM64, or 32-bit Windows package at present.

### Is Cat Clip Composer production-ready?

**A:** Treat it as a pre-1.0, entirely vibecoded experiment. It has project recovery, undo/redo, safe temporary-output
replacement, audits, and real render tests, but it is still evolving quickly. Keep backups of important project
files and verify a completed export before deleting or archiving any source material.

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
