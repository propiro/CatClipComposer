# INI configuration

Last reviewed: 2026-08-06

## Location

The default configuration path is:

```text
<executable directory>\CatClipComposer.ini
```

The code resolves this with `AppContext.BaseDirectory`. The GUI and CLI use the same filename when their executables are deployed beside one another.

The executable directory must be writable when saving Options. This is intentionally a portable-configuration model. A future installer must either install to a user-writable application directory or explicitly arrange appropriate permissions; the application does not silently redirect configuration elsewhere.

Catalog data and generated thumbnails remain machine-local under `%LOCALAPPDATA%\CatClipComposer` and are not configuration.

## Schema

```ini
; Cat Clip Composer configuration

[Sources]
IncludeSubfolders=true
ShowFileNames=true
Folder0=C:\Videos\Cats
Folder1=D:\Incoming clips

[Output]
Folder=C:\Videos\Compositions
TargetDurationMinutes=15
Orientation=Landscape

[Tools]
FfmpegPath=C:\Tools\ffmpeg\bin\ffmpeg.exe

[Overlays]
ProgressStyle=WholeCompilation
ImagePath=C:\Artwork\watermark.png
Text=Channel name\nSecond line
FontPath=C:\Fonts\Example.ttf
TextSize=42
Position=TopRight
```

Valid enum values:

- `Orientation`: `Landscape`, `Portrait`
- `ProgressStyle`: `None`, `WholeCompilation`, `EachClip`
- `Position`: `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`, `Center`

Source folders use zero-based `FolderN` keys and are loaded in numeric order. Duplicate paths are removed case-insensitively.

## Escaping and recovery

Only the `Overlays.Text` value uses escaping:

- `\n` newline
- `\r` carriage return
- `\t` tab
- `\\` literal backslash

Paths are stored without quotes. Values may contain `=` because the reader splits on only the first equals sign.

Missing keys use documented defaults. Malformed booleans, numbers, and enums fall back safely; bounded numeric settings are clamped to their valid range. Unknown sections and keys are ignored. Saving rewrites the known schema atomically through a same-directory temporary file.

The superseded JSON configuration implementation was removed rather than retained as legacy code. Pre-release JSON settings are not migrated automatically.
