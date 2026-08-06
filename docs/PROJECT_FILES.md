# Project files and crash recovery

Last reviewed: 2026-08-06

Cat Clip Composer stores editable work as versioned UTF-8 JSON with the `.ccproject` extension. Project files contain references and editing metadata; they do not copy or embed source videos, images, audio, fonts, cached previews, or final exports.

## Normal projects

Use **New**, **Open**, and **Save** in the main toolbar. New projects contain five stable track types:

1. Video
2. Overlays
3. Audio
4. Progress
5. Effects

The video track round-trips ordered source clips and still images. The layer panel adds/edits/removes timed text, image, audio, and progress items; clip effects edit fit/fill/stretch/animated-blur, fades, and volume. A shared Core mapper projects the complete enabled track model into both GUI and headless renders.

Each project has a GUID, name, creation/modification UTC timestamps, output settings, ordered tracks, and stable item GUIDs. Source references use absolute paths and optional catalog media IDs. When loading, the GUI resolves catalog media by ID first and path second; missing source paths remain represented so they can be diagnosed or replaced in a later editing pass.

Normal saves are atomic: JSON is written to a unique temporary file in the destination directory, flushed, and moved over the selected `.ccproject` only after serialization succeeds. Files with a schema version newer than the application supports are rejected instead of guessed at.

## Recovery autosave

Every timeline mutation synchronizes the video track and writes an atomic recovery document at:

```text
<MetadataFolder>\recovery\autosave.ccproject
```

On startup the GUI loads this recovery document automatically when present and reports the recovered project in the status line. Creating a new project clears the prior recovery before saving the new empty state. A normal project save also refreshes recovery, so a crash after later unsaved edits can restore the most recent timeline state without overwriting the user's named project file.

The recovery document preserves the normal project path when one exists. Its own recovery location is never mistaken for the normal save destination.

## Export acceptance and history

Catalog usage is not updated merely by adding a clip to a project, autosaving, or saving a `.ccproject`. Only a successful final export records a render job and increments the included source clips' use counts. New history rows include the accepted project name and normal project-file path when available; older history rows remain valid as unnamed/legacy exports.

## Headless inspection

Create an empty project:

```powershell
CatClipComposer.Cli.exe project --create `
  --project-file "D:\Cat Projects\Example.ccproject" `
  --project-name "Example"
```

Inspect it as JSON:

```powershell
CatClipComposer.Cli.exe project `
  --project-file "D:\Cat Projects\Example.ccproject" `
  --json
```

Creation refuses to replace an existing file unless `--overwrite` is explicit.

Render the saved project's enabled tracks and project output settings without WPF:

```powershell
CatClipComposer.Cli.exe render `
  --project-file "D:\Cat Projects\Example.ccproject" `
  --output "Example.mp4"
```

`--project-file` is intentionally not combined with ad-hoc `--clip`/`--screen` arguments; choose one source of timeline truth.
