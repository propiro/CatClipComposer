# INI configuration

Last reviewed: 2026-08-14

## Location

Application preferences are stored beside the executable:

```text
<executable directory>\CatClipComposer.ini
```

The GUI and CLI resolve this through `AppContext.BaseDirectory`. The CLI also accepts `--config <file>`
and `--data <folder>` overrides for isolated automation; these do not rewrite the normal INI.

The executable directory must be writable when Preferences are saved. This is an intentional portable
configuration model. Catalog data and generated previews default to `%LOCALAPPDATA%\CatClipComposer`, but
`Library.MetadataFolder` can relocate them. Existing metadata is not moved automatically.

## Application-preference schema

```ini
; Cat Clip Composer configuration

[Startup]
FirstStartupCompleted=false

[RecentProjects]
Project0=D:\Projects\Cats.nya

[ProgressDefaults]
Style=Solid
Position=Bottom
Color=#C8C0B2
Height=10

[TextOverlayPresets]
; PresetN values are application-generated Base64 JSON records.

[Sources]
IncludeSubfolders=true
ShowFileNames=true
RescanLibraryOnStartup=true
Folder0=C:\Videos\Cats
Folder1=D:\Incoming clips

[Library]
MetadataFolder=C:\Users\Example\AppData\Local\CatClipComposer
PreviewSlideCount=12
BrowserViewMode=SmallGrid
BrowserSortMode=Name
SmallThumbnailSize=120
LargeThumbnailSize=220
ExtraLargeThumbnailSize=420

[Preview]
QualityPercent=50
PreserveSelectedObjectQuality=true

[Editing]
UndoHistoryCapacity=32

[Output]
Folder=C:\Videos\Compositions
ProjectFolder=C:\Videos\CatClipComposer Projects

[Tools]
FfmpegPath=ffmpeg.exe
CustomFontFolder=fonts

[Workspace]
ContentBrowserDock=Left
PreviewDock=Center
LayersDock=Right
TimelineDock=Bottom
WindowWidth=1440
WindowHeight=900
WindowLeft=-1
WindowTop=-1
WindowMaximized=false
WorkspaceLeftWidth=310
WorkspaceRightWidth=270
WorkspaceBottomHeight=270
TimelinePixelsPerSecond=8
TimelineTrackHeight=64
PreviewsSplit=false
PreviewSplitRatio=0.5
ActivePreviewTab=0
ActiveWorkspacePanel=ContentBrowser
ExpandedWorkspacePanel=
```

Preferences intentionally contain long-lived application behavior: library folders and scanning, metadata
and preview storage, export/project default folders, external tools, custom fonts, and workspace docking.
Resolution, frame rate, encoder, quality, target duration, ruler/snap behavior, and render effects belong to
each `.nya` project and can be changed from **Project settings** or the timeline.

`PreviewSlideCount` is clamped to 1-24 and defaults to 12. `BrowserViewMode` accepts `List`, `SmallGrid`,
`LargeGrid`, or `ExtraLargeGrid` and defaults to `SmallGrid`. `SmallThumbnailSize` is clamped to 80-200 pixels;
`LargeThumbnailSize` is clamped to 140-360 pixels and kept at least 20 pixels larger than the small size;
`ExtraLargeThumbnailSize` is clamped to 240-640 and kept at least 40 pixels larger than Large.
These browser preferences affect cached-image presentation only and never cause source videos to be loaded.
`BrowserSortMode` accepts `Name`, `Date`, `Length`, or `Tag` and defaults to `Name`; Date places the newest
source-file modification first, while untagged clips sort after named tags in Tag mode.

`Preview.QualityPercent` controls the temporary LQ preview canvas and accepts the six UI stops `10`, `25`,
`50`, `75`, `90`, or `100`; another finite integer is normalized to the nearest stop. It does not change the
project's final output dimensions or export quality. `Preview.PreserveSelectedObjectQuality` defaults to true
and asks the renderer to use higher-quality scaling for a selected image overlay where the filter graph can do
so safely. Whole-frame effects still run on the smaller preview canvas. A previous valid cached prerender can
still be displayed after either preference changes; its recorded percentage is shown, and the next prerender
uses the new choice. `config` exposes both values in text and JSON output.

`Editing.UndoHistoryCapacity` controls the in-memory project-edit snapshot stack. It defaults to `32` and is
clamped to `1`–`256`. Lowering it trims the oldest undo/action entries immediately; it does not change project
files or the persistent crash-recovery snapshot.

`Startup.FirstStartupCompleted` defaults to false. After the editor initializes successfully, the application
atomically saves it as true. False or missing values select the five-second first-launch splash minimum; true
selects the approximately three-second returning-launch minimum. Failed initialization does not advance it.

`RecentProjects.ProjectN` keeps at most ten distinct project paths, newest first. The Open button exposes these
as a recent-project menu and ignores entries whose files no longer exist. `ProgressDefaults` remembers the last
accepted progress-bar style, top/bottom position, color, and 2-100 px height for the next progress item; malformed
colors and sizes return to safe defaults.

`TextOverlayPresets.PresetN` stores up to 100 named text-overlay parameter sets in numeric order. Values are
application-generated Base64-encoded JSON so multiline text and paths cannot break INI parsing. Presets contain
text, font identity, stroke enable/color/width/smoothness, transform, opacity, and fades, but deliberately exclude timeline start/end. The editor
regenerates each text/font thumbnail at runtime; no thumbnail cache or binary image is embedded in the INI.

`RescanLibraryOnStartup` defaults to true; startup skips scanning when no source folder is configured. The splash
reports the skip explicitly when this setting is false or no folder exists. When enabled with configured folders,
it shows live per-file counts and a scan percentage inside the overall startup percentage range. Source
folders use zero-based `FolderN` keys, load in numeric order, and are deduplicated case-insensitively.

`MetadataFolder` contains `catalog.db`, cached `thumbnails`, cached contact-sheet `previews`, and crash
`recovery`. `Output.Folder` is the default final-compilation destination. `Output.ProjectFolder` is the
default location for editable `.nya` projects.

`CustomFontFolder` defaults to the portable `fonts` subfolder beside the executable. TTF and OTF files there
appear alongside installed Windows font families in text-layer editors and are marked `CUSTOM FOLDER`.
Preferences provides both Browse and Open folder actions.

The default `FfmpegPath=ffmpeg.exe` resolves to the mandatory bundled
`<executable directory>\thirdparty\ffmpeg\ffmpeg.exe`; it does not depend on `PATH`. An explicit path is a
local override and its matching `ffprobe.exe` must sit beside it. If the bundle is missing, Preferences
offers a button to open the current compatible Windows LGPL-build download page.

Workspace dock values are `Left`, `Center`, `Right`, and `Bottom`. All four values must be unique. Moving a
panel into an occupied slot swaps the panels and saves the layout; invalid or duplicate values recover to
browser-left, preview-center, layers-right, and timeline-bottom.

The remaining Workspace values are captured when the main window closes. Window width/height are clamped to
the application's minimum size; a position is reused only when it still intersects the Windows virtual screen.
`WindowLeft=-1` and `WindowTop=-1` mean use normal centered first-run placement. The three workspace sizes
preserve the left, right, and bottom splitters. `TimelinePixelsPerSecond` (0.1–240) and
`TimelineTrackHeight` (28–110 px) restore the two project-timeline zoom axes; **Fit width** sets the time zoom
to fit the complete project duration into the current viewport. `PreviewsSplit` and `PreviewSplitRatio` restore joined tabs or
side-by-side previews and their divider. `ActivePreviewTab` is 0 for Clip Preview or 1 for Project Preview.
`ActiveWorkspacePanel` and optional `ExpandedWorkspacePanel` accept `ContentBrowser`, `Preview`, `Layers`, or
`Timeline`. Invalid numeric values and ratios are normalized before use.

## Parsing and recovery

Paths are stored without quotes and may contain `=` because the reader splits only on the first equals
sign. Missing keys use documented defaults. Malformed booleans and numbers fall back safely, bounded
numeric settings are clamped, and unknown sections or keys are ignored. Saving rewrites the known schema
atomically through a same-directory temporary file.

The superseded JSON configuration implementation was removed. Pre-release JSON settings are not migrated
automatically. Old project-specific INI keys are ignored because those values now live in `.nya` projects.
