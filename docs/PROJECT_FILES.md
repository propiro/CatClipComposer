# Project files and crash recovery

Last reviewed: 2026-08-12

Cat Clip Composer stores editable work as versioned UTF-8 JSON with the `.nya` extension. Project files
contain references and editing metadata; they do not copy or embed source videos, images, audio, fonts,
cached previews, or final exports.

## Normal projects

Use **New**, **Open**, and **Save** in the main toolbar. **Open** offers disk browsing and existing recent
projects. New projects contain five default tracks in top-to-bottom visual/editor order:

1. Overlays
2. Video
3. Progress
4. Background
5. Audio

The bottommost Video track is the sequential base composition. Video and other visual tracks above it are
composited from bottom to top and can contain timed visual layers plus source audio. Project Layers Data can
create/remove, collapse, reorder, and color-code tracks and edit/remove timed items.
The Content Browser's grouped Effects tab adds text, image, audio, progress, and compatible plugin effects.
Clip effects control fit/fill/stretch, fades, and volume. Any block can remain on the timeline while disabled;
the shared Core mapper excludes disabled blocks from both GUI and headless renders.

The desktop editor keeps up to 100 in-memory undo snapshots. Ctrl+Z/Ctrl+Y and the toolbar arrows move through
those logical project changes; recovery autosave follows the restored state. The title and project label show
an asterisk whenever the current snapshot differs from the last normal save. Undo history is session-only and
is not embedded into `.nya` or recovery files.

Schema version 9 adds explicit 0–100% text/image overlay opacity. Existing projects load at 100%, matching their
intended visual setting while removing the renderer's former hidden 90% PNG attenuation. Schema version 8 adds
the persisted `isTransformLocked` flag for text/image overlays. Schema version 7 adds
optional per-text/image-overlay fade-in and fade-out seconds. These alpha fades are
bounded to the item's duration and remain independent from source/audio fades. Schema version 6 added
backward-compatible text/image overlay transforms: normalized X/Y center coordinates,
uniform scale, rotation in degrees, and a flag distinguishing direct placement from the five legacy presets.
Schema version 5 records human-scale Background blur lightness percentages and normalized hue angles. Loading
schema 4 or older converts the former -1..1 lightness convention to -100..100 percent and wraps negative hue
angles into 0..360 degrees. Schema version 4 added optional track/item color codes. Schema version 3 added the project background color,
Background timeline, multiple named tracks, and
versioned plugin IDs/parameter dictionaries. Schema version 2 added the target duration, timeline ruler and
snap modes, installed/custom font selection, and per-effect progress style, color, size, and position. Each project also carries a GUID, name,
creation/modification UTC timestamps, output settings, ordered tracks, and stable item GUIDs. Older JSON projects
remain readable; saving them writes schema 9. The normal Open dialog prefers `.nya`.
An older per-clip `BlurBackground` fit value is migrated to Fit plus an equivalent built-in Background blur
module block at the same time range, preserving the visual intent without retaining the hard-coded renderer.

The built-in Background blur effect reads the active source below the current project time and exposes
saturation, lightness, hue, zoom, and Gaussian blur parameters. Plugin module IDs are saved in `.nya`; a
missing or incompatible required module produces an explicit render error instead of silently changing the
project.

Background lightness is a human percentage mapped linearly to a bounded luma offset: -100 is
black, 0 leaves brightness unchanged, and +100 is white. Saturation uses 0 for grayscale, 1 unchanged, and 3
as the slider maximum; hue rotates through 0..360 degrees.

Source references use absolute paths and optional catalog media IDs. Loading resolves catalog media by ID
first and path second. Missing source paths stay represented so they can be diagnosed or replaced later.

Normal saves are atomic: JSON is written to a unique temporary file in the destination directory, flushed,
and moved over the selected `.nya` only after serialization succeeds. Files with a schema version newer than
the application supports are rejected rather than guessed at.

## Recovery autosave

Timeline mutations write an atomic recovery document at:

```text
<MetadataFolder>\recovery\autosave.nya
```

Startup loads recovery after the catalog so source media can be resolved. Creating a new project clears the
previous recovery before writing the new empty state. A normal save also refreshes recovery, so later
unsaved timeline changes can be restored without overwriting the named project.

Recovery exists only for crash protection. A successful normal close clears it after the project decision and
workspace save complete. On startup, older recovery left by a previous version is compared with its named saved
project while ignoring schema/path/modification metadata; identical content is restored as clean and the stale
recovery is removed. A semantically changed recovery remains dirty. A pristine untitled recovery is likewise
treated as clean, while renamed, reconfigured, or populated untitled projects remain recoverable edits.

Closing a dirty project opens a dedicated Save / Don't save / Cancel dialog. A cancelled file dialog or failed
save keeps the editor open. Window/workspace preferences are saved only after that project decision permits the
close; they remain application INI values rather than project data.

The recovery document preserves the normal project path when one exists. Its own recovery location is never
treated as the normal save destination.

## Prerender cache

The most recent successful Frame, selected-range, or All project prerender is retained under:

```text
<MetadataFolder>\project-previews
```

An atomic JSON entry records its file, project-time coverage, and fingerprint. The fingerprint includes semantic
project content, Cat Clip Composer version, and referenced source/font file size and modification time. Startup
and normal project Open restore only an exact match; project edits, app updates, changed/missing source files, or
missing preview video reject it. These MP4/JSON files are disposable feedback caches, not part of `.nya`, and a
new successful prerender replaces the current metadata reference and removes older project previews best-effort.

## Export acceptance and history

Catalog usage is not updated by adding a clip, autosaving, or saving a `.nya`. Only a successful final
export records a render job and increments the included source clips' use counts. History includes the
accepted project name and normal project path when available.

## Headless inspection

```powershell
CatClipComposer.Cli.exe project --create `
  --project-file "D:\Cat Projects\Example.nya" `
  --project-name "Example"

CatClipComposer.Cli.exe project `
  --project-file "D:\Cat Projects\Example.nya" `
  --json

CatClipComposer.Cli.exe render `
  --project-file "D:\Cat Projects\Example.nya" `
  --output "Example.mp4"
```

Creation refuses to replace an existing file unless `--overwrite` is explicit. `--project-file` is not
combined with ad-hoc `--clip` or `--screen` arguments; choose one source of timeline truth.
