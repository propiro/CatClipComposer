# Project goals and feature status

Last reviewed: 2026-08-07

## Product goal

Cat Clip Composer is a deliberately narrow Windows application for cataloging folders of short clips,
arranging selected clips on a duration-based timeline, adding simple presentation elements, and producing
a YouTube-ready compilation without the complexity of a general video editor.

The application should work for personal use and remain suitable for possible commercial distribution.
Its normal runtime must not depend on paid components, GPL/AGPL components, or a non-redistributable
FFmpeg build.

## Scope principles

- One repository with modular feature projects and no Git submodules.
- GUI and headless workflows over shared domain and infrastructure modules.
- Durable local media catalog and export history.
- Simple assembly, presentation, and rendering features; no attempt to become a full nonlinear editor.
- Configuration stored in an INI file beside the executable.
- Maintained documentation, worklog, TODO list, and audit trail.

## Requested features

Feature status is grouped by area instead of placed in a wide table so it remains readable in source form.

### Configuration

- **Multiple source folders — Done.** Recursive scanning is optional.
- **Application folders and project-specific editing settings — Done.** Preferences persist library/tool
  folders in the executable-directory INI; each project owns its target and output settings.
- **INI beside the executable — Done.** The store uses atomic replacement, safe defaults, and escaping.

### Catalog and browser

- **Common video containers — Done.** MP4, WebM, AVI, MOV, MKV, and M4V are supported.
- **Duration, dimensions, and audio probing — Done.** The bundled FFprobe adapter performs the probe.
- **Durable refreshable database — Done.** SQLite tracks catalog entries and availability.
- **Selectable thumbnails and search — Done.** Cached thumbnails support name, path, and tag search.
- **Large-library safety and drag/drop — Done.** A recycled tile grid loads cached previews only when
  realized, supports extended selection and mass tag editing, and drags one or many clips into a selected Video timeline.
- **Full-width browser focus — Done.** A left-edge arrow expands the browser while preserving the timeline
  drop target; toggling back restores the saved dock layout.
- **Video/contact-sheet preview — Done.** Muted-by-default playback has play/pause, seek, mute, and volume
  controls and is backed by a configurable cached FFmpeg contact sheet (12 slides by default).
- **Tags and project-use metadata — Done.** Tags are normalized and searchable; successful exports add
  named-project usage history.

### Workspace and visual design

- **Main editor panels — Done.** Browser, preview, layers/used clips, and timeline occupy four resizable
  slots.
- **Repositionable docking — Done.** Every panel can swap into left, center, right, or bottom and persists
  its slot in the INI.
- **Complete Visual Studio designer layout — Done.** Default coordinates live in XAML; runtime settings can
  replace them.
- **Compact monochrome theme — Done.** Dark surfaces, warm neutral colors, square controls, reduced spacing,
  readable text, distinct disabled states, and dark custom scrollbars are applied consistently.
- **Startup and scan feedback — Done.** The sharpened Mr Cat splash reports startup/library progress and a
  scrolling diagnostic log for at least five seconds; manual rescans use the same foreground, cancelable
  presentation.

### Timeline and presentation

- **Configurable duration axis and total — Done.** The target is project-specific; scalable lanes, timeline
  zoom, frame/time/both rulers, and frame/0.1/0.5/1-second snapping support precise placement.
- **Add, select, remove, and reorder clips — Done.** Blocks drag to snapped interval or neighboring-block
  positions, Ctrl supports multi-selection, and selected-video controls plus Delete are available.
- **Dynamic timelines — Done.** Background, Video, Overlay, Audio, Progress, and Effects tracks can be
  added, named, resized, collapsed, color-coded, vertically sorted, removed when empty, and focused with
  Space from anywhere inside the panel; horizontal/vertical fit controls are available. Visual tracks render
  bottom-to-top so the topmost track is the topmost composite.
- **Frame/range selection and dual previews — Done.** Clicking or dragging the ruler selects an exact frame;
  Shift/Ctrl drag paints a frame-snapped range and preview playback stops at its end. Clip Preview handles raw
  library media, including Video-block double-clicks. Project Preview uses the normal layered renderer with a
  Windows-compatible H.264 preview override to create a seekable composition without recording export history.
- **Splash, mid-video, and outro screens — Done.** Still images can be inserted and reordered anywhere.
- **PNG/text overlays and custom fonts — Done.** Multiple timed elements are editable using installed system
  fonts or visibly marked TTF/OTF files from the portable custom-font folder.
- **Progress bars — Done.** Progress is an independent timeline effect with whole-project, source-segment,
  or custom timing and per-item style, color, size, and position.
- **Editable effects/layers — Done.** Video, overlays, progress, audio, fades, volume, fit/fill/stretch, and
  timed modules project through the shared renderer. A Background module fills unused frame space from the
  active source with configurable saturation, lightness, hue, zoom, and Gaussian blur.
- **Extensible plugin modules — Done.** Versioned media/stage/track contracts, isolated dependency loading,
  persisted parameters, and a portable `plugins` folder support first-party and trusted future modules.

### Output and history

- **Landscape and portrait output — Done.** YouTube 1080p, 4K, Shorts, and custom frame sizes are available.
- **Resolution, aspect, codec, quality, and FPS presets — Done.** Seven common presets plus custom settings
  persist per project.
- **Safe final compilation — Done.** Rendering uses a temporary output and supports cancellation.
- **Successful-export usage history — Done.** Completed jobs record ordered source clips and final output.
- **History browsing — Done.** Prior outputs and source locations open in File Explorer.

### Projects and automation

- **Named editable projects — Done.** Versioned schema-4 `.nya` JSON uses stable track/item IDs, optional
  track/item colors, background color, and plugin metadata.
- **Crash recovery — Done.** Every timeline mutation writes an atomic recovery file under metadata storage.
- **Headless operation — Done.** Config, catalog metadata, project rendering, and history commands support
  JSON and stable exit codes.

### Licensing, deployment, and versioning

- **Non-GPL default rendering — Done.** Native MPEG-4 and Media Foundation H.264 need no GPL component;
  libx264 is only an explicit user-supplied-tool opt-in.
- **One-folder deployment — Done.** GUI and CLI are single-file applications; the pinned LGPL FFmpeg shared
  runtime, its DLLs, license, source record, build information, and hashes are always under `thirdparty`.
- **Shared user-visible version — Done.** Version 0.1.10 metadata drives every component, the window title and
  status bar, and headless output.

### Deferred editing scope

- **Trim and per-clip volume — Partial.** Per-clip volume is complete. Trimming remains deferred as
  `EDIT-001` so the application stays deliberately narrower than a general editor.

## Definition of an MVP release

The MVP release requires all P0 items in `docs/TODO.md` to be complete, a clean Release build, a clean
dependency vulnerability audit, end-to-end headless and GUI render smokes, and no open critical licensing
audit item.
