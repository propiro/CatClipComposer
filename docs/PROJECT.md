# Project goals and feature status

Last reviewed: 2026-08-14

## Product goal

Cat Clip Composer is an entirely vibecoded experiment intended to test how good—or bad—software created
without manually touching the code can become.

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
  realized, supports explicit Ctrl-toggle and first-anchor Shift-range selection plus mass tag editing, and
  drags one or many clips into a selected Video timeline.
- **Selectable browser presentation — Done.** One header control cycles through a thumbnail list, small grid,
  large grid, and extra-large grid; Preferences stores separate bounded thumbnail sizes for all grid modes.
- **Full-width browser focus — Done.** A left-edge arrow expands the browser while preserving the timeline
  drop target; toggling back restores the saved dock layout.
- **Video/contact-sheet preview — Done.** Muted-by-default playback has play/pause, seek, mute, and volume
  controls and is backed by a configurable cached FFmpeg contact sheet (12 slides by default).
- **Tags and project-use metadata — Done.** Tags are normalized and searchable; successful exports add
  named-project usage history.

### Workspace and visual design

- **Main editor panels — Done.** Browser, preview, Project Layers Data, and timeline occupy four resizable
  slots. Clip and Project Preview can be joined as tabs or split into resizable side-by-side viewports.
- **Repositionable docking — Done.** Every panel can swap into left, center, right, or bottom and persists
  its slot in the INI.
- **Session workspace restoration — Done.** Window position/size/maximized state, splitter dimensions,
  joined/split previews, preview divider, selected preview tab, focused panel, and expanded panel persist in
  the portable INI and recover safely when a monitor layout changes.
- **Complete Visual Studio designer layout — Done.** Default coordinates live in XAML; runtime settings can
  replace them.
- **Compact monochrome theme — Done.** Dark surfaces, warm neutral colors, square controls, reduced spacing,
  readable text, distinct disabled states, and dark custom scrollbars are applied consistently.
- **Startup and scan feedback — Done.** The wide split Mr Cat splash reports named stages, a visible percentage,
  progress bar, and timestamped console diagnostics. Software layout, plugins, catalog, project/recovery, fonts,
  and editor readiness are separate messages. Opening/completion holds last 100–200 ms, while ordinary fast
  lines use 20–40 ms gaps. The first successful installation launch has a five-second minimum and persists its
  completion in the portable INI; returning launches have an approximately three-second minimum. Configured
  startup scans add immediate live per-file counts/percentages; skipped scans say why. Manual rescans use the
  same foreground, cancelable presentation.

### Timeline and presentation

- **Configurable duration axis and total — Done.** The target is project-specific; scalable lanes, timeline
  zoom, frame/time/both rulers, and frame/0.1/0.5/1-second snapping support precise placement. Time zoom and
  track height use discrete minus/value/plus controls rather than narrow sliders.
- **Add, select, remove, and reorder clips — Done.** Blocks drag to snapped interval or neighboring-block
  positions, Ctrl supports multi-selection, and selected-video controls plus Delete are available.
- **Dynamic timelines — Done.** New projects default to Overlays, Video, Progress, Background, then Audio;
  those track kinds plus optional legacy Effects tracks can be added, named, resized, collapsed, color-coded,
  vertically sorted, removed when empty, and focused with
  Space from anywhere inside the panel; horizontal/vertical fit controls are available. Visual tracks render
  bottom-to-top so the topmost track is the topmost composite.
- **Frame/range selection and dual previews — Done.** Clicking or dragging the ruler selects an exact frame;
  Shift/Ctrl drag paints a frame-snapped range, Mark start/end creates one from the playhead, and either edge
  remains draggable. Clip Preview handles raw library media, including optional autoplay for Video-block
  double-clicks. The bordered Prerender group gives Frame, active Range, and All explicit LQ/HQ actions; Range defaults to
  the current frame when no range exists. A six-stop 10–100% temporary-resolution control uses the same
  composition/effect graph on a smaller canvas without changing export settings, while optional selected-image
  scaling uses a higher-quality scaler. Project preview maps local playback back onto project time and uses
  Windows-compatible H.264 without recording export history. All valid chunks for the current
  project/source/app fingerprint are restored between sessions; timeline clicks switch between their coverage
  without range-selection changes unloading them. A thin ruler line shows current cached intervals in green and
  only changed overlaps in yellow. Rapid chunk switches are coalesced and retried once if Windows reports a
  transient source failure. Exact semantic undo/reverts restore matching green coverage. A serialized prerender
  queue remains visible in the bottom status bar together with scope/frame totals and parsing/engine/render stages.
  Playback stops and resets at the active chunk boundary.
  Project Settings is a compact rollout at the bottom-left of Project Preview rather
  than consuming Project Layers Data space.
- **Contextual timeline preview and effects — Done.** The playhead menu renders from the selected frame and
  marks range boundaries; the selected-range menu renders only that interval. Left-clicking empty lanes,
  plus track-header and item menus, lists only compatible plugin effects. Timeline selection mirrors Layers /
  Used Clips, and a yellow media-block edge marks content outside the current rendered-preview coverage.
- **Splash, mid-video, and outro screens — Done.** Still images can be inserted and reordered anywhere.
- **PNG/text/GIF/video overlays and custom fonts — Done.** Multiple timed elements are editable using installed system
  fonts or visibly marked TTF/OTF files from the portable custom-font folder. Active overlays expose their
  content and move/scale/rotate gizmos over Project Preview; clicking one synchronizes timeline and Layers /
  Used Clips selection; double-clicking it opens the item's preferences. Visible gray transparent per-block lock
  controls prevent project-view transform gestures while retaining selection and form editing. OK/Enter commits
  the draft and Cancel/Escape restores it. The same editor exposes transform, explicit 0–100% opacity, and
  optional fade-in from and fade-out to transparency. GIF/video items loop moving content through their block
  duration using the same controls. With no selected Video range, new native overlays begin at the playhead.
  Live stale proxies retain that exact opacity; moved
  content leaves a crossed notice over its old prerendered location until the frame is refreshed. The active
  timeline selection is hit-tested first where preview objects overlap. WPF proxies and FFmpeg share normalized
  LF line breaks and render-safe text content, so multiline object bounds match the prerendered layout.
  Text appearance/transform settings, including optional stroke color/width/smoothness, can be saved as portable
  presets and selected from generated text/font thumbnails without inheriting an old timeline interval. Effect
  frame previews intentionally bypass only the candidate text's fade so appearance remains judgeable.
- **Progress bars — Done.** Progress is an independent timeline effect with whole-project, source-segment,
  or custom timing and per-item style, color, size, and position. Adding one inherits the selected clip range,
  names a single selection `PROGRESS <clip>`, remembers accepted visual defaults, and supports style copy/paste.
- **Editable effects/layers — Done.** Video, overlays, progress, audio, fades, volume, fit/fill/stretch, and
  timed modules project through the shared renderer. A Background module fills unused frame space from the
  active source with configurable saturation, lightness, hue, zoom, and Gaussian blur. Editors default to
  Start/End, optionally accept duration, provide whole-timeline shortcuts, inherit multi-selected Video ranges,
  and combine bounded sliders/arrows with unrestricted finite manual values.
- **Predictable timed-block editing — Done.** Dragging preserves the pointer's grab offset and shows the exact
  snapped landing interval. Left/right handles resize non-primary timed blocks, and optional clip-range
  snapping aligns starts or ends to source-clip boundaries. Resize movement uses one absolute pointer delta,
  so the handle cannot accelerate as WPF reports repeated relative deltas. Track names drag vertically to reorder the stack;
  Video track names bring Project Preview forward and timed effect/overlay blocks open their editor on double-click.
  A later-added overlapping block remains on top for selection, full effect blocks copy/paste at the playhead or
  clicked empty compatible lane, and effect/overlay/audio/progress blocks drag across compatible timelines,
  time arrows accept Ctrl/Shift step modifiers, and the preview/timeline wheel gestures zoom or pan in context.
- **Compact range and effect-frame editing — Done.** Start/End values share one miniature draggable timeline.
  Native overlay and plugin-effect dialogs group content/module, timing, transform, and appearance/adjustment
  controls consistently. They can render unsaved settings over the selected real project frame in a same-width
  companion above the editor, show staged preparation plus live FFmpeg progress and elapsed time, and optionally
  refresh plugin effects after a short debounce.
- **Effect discovery and block state — Done.** The Content Browser has a grouped Effects tab with native and
  plugin entries repeated under every compatible timeline category. Timeline blocks can be disabled without
  deletion; disabled blocks remain editable but render darkened/grayed and are excluded from output.
- **Catalog state, tags, and sorting — Done.** Durable blue unseen, green current-project, and yellow other-project
  corners summarize clip state without conflating project references with completed-export history. Browser
  sorting persists across name, newest source date, duration, and custom-tag modes. Single/mass tag and metadata
  editors keep current typed content while quick buttons add the library's ten most-used tags without duplicates.
- **Track-ordered filter composition — Done.** Video filter effects and overlays interleave bottom-to-top, so
  an overlay below Video blur is filtered and the same overlay above it stays sharp.
- **Extensible plugin modules — Done.** Versioned media/stage/track contracts, isolated dependency loading,
  persisted parameters, and a portable `plugins` folder support first-party and trusted future modules.

### Output and history

- **Landscape and portrait output — Done.** YouTube 1080p, 4K, Shorts, and custom frame sizes are available.
- **Resolution, aspect, codec, quality, and FPS presets — Done.** Seven common presets plus custom settings
  persist per project.
- **Safe final compilation — Done.** Rendering uses a temporary output and supports cancellation.
- **Successful-export usage history — Done.** Completed jobs record ordered source clips and final output.
- **History browsing — Done.** A modeless toggle surface combines newest-first project actions, exports, and
  log/crash files; prior outputs and source locations open in File Explorer. Clip inspection adds technical,
  catalog-date, saved/recovered-project-reference, and completed-export details.
- **Selectable undo/redo destinations — Done.** The main arrows move one step; adjacent dropdowns list every
  retained action and restore the selected earlier/later moment as one recovery/cache refresh operation.
- **About and update visibility — Done.** A top-right `?` opens build/experiment details and the complete Mr. Cat image,
  fitted without cropping across the supported resizable window.
  Its explicit, non-installing GitHub check distinguishes newer repository code from a newer packaged Windows ZIP.

### Projects and automation

- **Named editable projects — Done.** Versioned schema-11 `.nya` JSON uses stable track/item IDs, optional
  track/item colors, text stroke/image/GIF/video overlay fades/transforms, background color, and plugin metadata.
- **Undo/redo and dirty-state protection — Done.** Ctrl+Z/Ctrl+Y and toolbar arrows restore a configurable
  1–256 project-edit stack (32 by default). Exact undo/revert fingerprints also restore green prerender coverage;
  snapshots, the project/title shows an asterisk away from the last save point, and closing offers Save,
  Don't save, or Cancel without losing the window on a failed/cancelled save.
- **Crash recovery — Done.** Every timeline mutation writes an atomic recovery file under metadata storage.
- **Headless operation — Done.** Config, catalog metadata, project rendering, and history commands support
  JSON and stable exit codes.

### Licensing, deployment, and versioning

- **Non-GPL default rendering — Done.** Native MPEG-4 and Media Foundation H.264 need no GPL component;
  libx264 is only an explicit user-supplied-tool opt-in.
- **One-folder deployment — Done.** Normal GUI/CLI releases share a compact framework-dependent file set and
  require the .NET 8 Desktop Runtime; full single-file self-contained publication remains explicit. The pinned
  LGPL FFmpeg shared runtime, its DLLs, license, source record, build information, and hashes are always under
  `thirdparty`.
- **Visible package version marker — Done.** Every executable output carries one extensionless
  `version_<version>` file with a short changelist; build and publish reject a missing, stale, or duplicate marker.
- **Public binary release — Done.** GitHub Release v0.1.32 provides the self-contained Windows x64 folder as
  a versioned ZIP with an adjacent SHA-256 checksum and no programming environment requirement.
- **Light release policy — Done.** Future tagged releases default to a much smaller multi-file package requiring
  the free .NET 8 Desktop Runtime x64; its native apphost supplies the missing-runtime download prompt. Full
  packaging remains available only through an explicit publisher switch.
- **Shared user-visible version — Done.** Version 0.1.33 metadata drives every component, the window title and
  status bar, and headless output.

### Deferred editing scope

- **Trim and per-clip volume — Partial.** Per-clip volume is complete. Trimming remains deferred as
  `EDIT-001` so the application stays deliberately narrower than a general editor.

## Definition of an MVP release

The MVP release requires all P0 items in `docs/TODO.md` to be complete, a clean Release build, a clean
dependency vulnerability audit, end-to-end headless and GUI render smokes, and no open critical licensing
audit item.
