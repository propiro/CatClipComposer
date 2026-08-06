# INI configuration

Last reviewed: 2026-08-06

## Location

The default configuration path is:

```text
<executable directory>\CatClipComposer.ini
```

The code resolves this with `AppContext.BaseDirectory`. The GUI and CLI use the same filename when their executables are deployed beside one another.

The CLI also accepts `--config <file>` and `--data <folder>` overrides for isolated automation. These overrides select locations for that process only and do not change the INI. See `docs/HEADLESS.md`.

The executable directory must be writable when saving Options. This is intentionally a portable-configuration model. A future installer must either install to a user-writable application directory or explicitly arrange appropriate permissions; the application does not silently redirect configuration elsewhere.

Catalog data and generated previews default to `%LOCALAPPDATA%\CatClipComposer`; `Library.MetadataFolder` can relocate them and is itself configuration.

## Schema

```ini
; Cat Clip Composer configuration

[Sources]
IncludeSubfolders=true
ShowFileNames=true
Folder0=C:\Videos\Cats
Folder1=D:\Incoming clips

[Library]
MetadataFolder=C:\Users\Example\AppData\Local\CatClipComposer
PreviewSlideCount=4

[Output]
Folder=C:\Videos\Compositions
ProjectFolder=C:\Videos\CatClipComposer Projects
TargetDurationMinutes=15
Orientation=Landscape
VideoEncoder=NativeMpeg4

[Tools]
FfmpegPath=ffmpeg.exe

[Overlays]
ProgressStyle=WholeCompilation
ImagePath=C:\Artwork\watermark.png
Text=Channel name\nSecond line
FontPath=C:\Fonts\Example.ttf
TextSize=42
Position=TopRight

[Workspace]
ContentBrowserDock=Left
PreviewDock=Center
LayersDock=Right
TimelineDock=Bottom
```

Valid enum values:

- `Orientation`: `Landscape`, `Portrait`
- `VideoEncoder`: `NativeMpeg4`, `WindowsMediaFoundationH264`, `Libx264Gpl`
- `ProgressStyle`: `None`, `WholeCompilation`, `EachClip`
- `Position`: `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`, `Center`
- Workspace dock values: `Left`, `Center`, `Right`, `Bottom`

`MetadataFolder` contains `catalog.db`, cached `thumbnails`, cached contact-sheet `previews`, and crash `recovery`. The folder is selected during service composition, so a changed value takes effect after restart; existing data is not moved implicitly. `PreviewSlideCount` is clamped to 1-12.

`Output.Folder` is for accepted final compilations. `Output.ProjectFolder` is the default location for editable `.ccproject` files. Keeping these separate makes it possible to replace media/text and export a revised compilation later without treating an MP4 as the project source.

`NativeMpeg4` is the non-GPL compatibility default. `WindowsMediaFoundationH264` uses FFmpeg's Media Foundation wrapper and is the preferred non-GPL YouTube preset when supported by the selected Windows FFmpeg build. `Libx264Gpl` is an optional GPL-dependent preset and is never selected implicitly.

The default `FfmpegPath=ffmpeg.exe` resolves to the mandatory bundled
`<executable directory>\thirdparty\ffmpeg\ffmpeg.exe`; it does not depend on `PATH`. Set an explicit path only
to override the bundle locally. The matching `ffprobe.exe` must be beside an override.

Source folders use zero-based `FolderN` keys and are loaded in numeric order. Duplicate paths are removed case-insensitively.

The four workspace dock values must be unique. Moving a panel into an occupied slot swaps the two panels and saves the new layout. Missing, malformed, or duplicate values recover to browser-left, preview-center, layers-right, and timeline-bottom.

## Escaping and recovery

Only the `Overlays.Text` value uses escaping:

- `\n` newline
- `\r` carriage return
- `\t` tab
- `\\` literal backslash

Paths are stored without quotes. Values may contain `=` because the reader splits on only the first equals sign.

Missing keys use documented defaults. Malformed booleans, numbers, and enums fall back safely; bounded numeric settings are clamped to their valid range. Unknown sections and keys are ignored. Saving rewrites the known schema atomically through a same-directory temporary file.

The superseded JSON configuration implementation was removed rather than retained as legacy code. Pre-release JSON settings are not migrated automatically.
