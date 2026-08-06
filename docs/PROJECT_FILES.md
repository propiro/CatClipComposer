# Project files and crash recovery

Last reviewed: 2026-08-06

Cat Clip Composer stores editable work as versioned UTF-8 JSON with the `.nya` extension. Project files
contain references and editing metadata; they do not copy or embed source videos, images, audio, fonts,
cached previews, or final exports.

## Normal projects

Use **New**, **Open**, and **Save** in the main toolbar. New projects contain five stable track types:

1. Video
2. Overlays
3. Audio
4. Progress
5. Effects

The video track round-trips ordered source clips and still images. The Layers/Used Clips panel adds, edits,
and removes timed text, image, audio, and progress effects. Clip effects control fit/fill/stretch/animated
blur, fades, and volume. A shared Core mapper projects the enabled track model into both GUI and headless
renders.

Schema version 2 adds the project target duration, timeline ruler and snap modes, installed/custom font
selection, and per-effect progress style, color, size, and position. Each project also carries a GUID, name,
creation/modification UTC timestamps, output settings, ordered tracks, and stable item GUIDs. Older schema-1
JSON projects remain readable; saving them writes schema 2. The normal Open dialog prefers `.nya`.

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

The recovery document preserves the normal project path when one exists. Its own recovery location is never
treated as the normal save destination.

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
