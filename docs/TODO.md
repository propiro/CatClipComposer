# TODO register

Last audited: 2026-08-12

Statuses are `Open`, `In progress`, `Blocked`, `Done`, and `Deferred`.

Each item is presented as a short section instead of a wide table so the source remains readable in
Visual Studio, terminals, narrow windows, and rendered Markdown.

## Product and engineering TODOs

### `CFG-001` — Replace JSON settings with `CatClipComposer.ini` beside the executable

- Priority/status: P0 / Done
- Acceptance: INI round-trip and malformed-input smokes passed; the JSON store was removed and the
  schema is documented.

### `CLI-001` — Add a headless CLI project in this repository

- Priority/status: P0 / Done
- Acceptance: Config, scan/list, tag/usage, project, layered render, and history commands passed
  text/JSON, exit-code, and end-to-end render smokes; Release builds pass.

### `LIC-001` — Remove required `libx264`/GPL encoding from the default render path

- Priority/status: P0 / Done
- Acceptance: Native `mpeg4` is the default, `h264_mf` is a non-GPL option, and `libx264` remains an
  explicit GPL opt-in for user-supplied tools only.

### `DOC-INSTALL-001` — Document installation, source builds, and FFmpeg use

- Priority/status: P0 / Done
- Acceptance: The public README explains that FFmpeg/FFprobe are required media tools, distinguishes the
  included audited runtime from a local override, gives clone/LFS/build/publish instructions, links official
  and pinned downloads, and accurately summarizes LGPL/GPL/nonfree distribution boundaries.

### `DOC-PROVENANCE-001` — Disclose the entirely vibecoded project origin

- Priority/status: P0 / Done
- Acceptance: The README's first prose sentence explicitly states that Cat Clip Composer is entirely
  vibecoded and describes the experiment, while the project goals preserve that positioning.

### `DOC-SPLASH-001` — Disclose the Mr. Cat splash-screen photo

- Priority/status: P0 / Done
- Acceptance: The public README states that the software includes a photo of Mr. Cat as its splash screen.

### `BOOT-001` — Share service composition between GUI and CLI

- Priority/status: P0 / Done
- Acceptance: Both executables consume `ApplicationServicesFactory`; render/history transactions are
  shared through `ICompositionExporter`.

### `MOD-001` — Split `MainViewModel` orchestration and timeline state

- Priority/status: P1 / Done
- Acceptance: `TimelineViewModel` owns editing, ordering, selection, summaries, axis values, and render
  segment projection; the direct smoke passed.

### `MOD-002` — Split FFmpeg filter/argument construction from process execution

- Priority/status: P1 / Done
- Acceptance: Coordinator, filter builder, command builder, process runner, and cleanup helper are
  separate; the mixed-input render smoke passed.

### `MOD-003` — Split SQLite schema creation and row mapping from catalog operations

- Priority/status: P2 / Done
- Acceptance: Focused schema, connection, UTC, media mapper, and history reader classes preserve the
  Core catalog interface and schema.

### `MOD-004` — Extract repeated WPF desktop interaction helpers

- Priority/status: P2 / Done
- Acceptance: Focused helpers own Explorer launch and consistent exception presentation across
  application windows.

### `UI-001` — Replace the leaking/high-padding theme with a compact monochrome editor design

- Priority/status: P0 / Done
- Acceptance: Derived windows paint dark client surfaces; title bars request dark mode; controls use
  warm neutral colors and zero-to-one-pixel corner radii; the screenshot was reviewed.

### `UI-002` — Correct low-contrast and undersized text

- Priority/status: P0 / Done
- Acceptance: Button templates apply explicit foregrounds, disabled states remain readable, and the
  smallest main-workspace labels are at least 10 px; a 1440x900 screenshot was reviewed.

### `WORKSPACE-001` — Add resizable and repositionable main panels

- Priority/status: P0 / Done
- Acceptance: Four dock slots use splitters; every panel can swap slots; unique layout persists in INI.

### `WORKSPACE-002` — Make the complete workspace visible in the Visual Studio designer

- Priority/status: P1 / Done
- Acceptance: All panels have default XAML grid coordinates and margins matching application defaults;
  runtime preferences continue to override them.

### `WORKSPACE-003` — Restore the complete runtime workspace between sessions

- Priority/status: P0 / Done
- Acceptance: Normal/maximized window bounds, three workspace splitter sizes, preview join/split and divider,
  active preview tab, focused panel, and optional expanded panel round-trip through the executable-directory
  INI; missing/off-screen geometry falls back safely. An isolated real-window close/reopen returns the exact
  saved position and dimensions.

### `BROWSER-001` — Make the content browser safe for very large libraries and support drag/drop

- Priority/status: P0 / Done
- Acceptance: A recycling virtualized grid binds only realized cached thumbnails, and cards drag onto a
  chosen Video timeline without opening source video files.

### `BROWSER-002` — Expand the browser without losing the timeline drop target

- Priority/status: P0 / Done
- Acceptance: A left-edge direction arrow switches between full-width browser focus and the saved dock
  layout; runtime expand/restore smokes passed.

### `BROWSER-003` — Add reliable grid sizing, multi-selection, and mass metadata actions

- Priority/status: P0 / Done
- Acceptance: The virtualized wrap panel derives the real viewport width, expanded browsing produces
  multiple columns, extended selection supports mass tag replacement and multi-clip drag/drop, and browser
  context actions operate on the clicked selection.

### `BROWSER-004` — Offer list and configurable grid presentations

- Priority/status: P1 / Done
- Acceptance: One browser-header action cycles thumbnail list, small grid, and large grid without changing
  catalog loading behavior; bounded small/large sizes persist in the portable INI and appear in CLI config.

### `PROJECT-PREVIEW-001` — Separate source preview from layered project preview

- Priority/status: P0 / Done
- Acceptance: Clip Preview remains muted by default; the centered PREVIEW action renders the shared layered
  plan to temporary metadata storage without export-history writes and Project Preview supports seek and
  single-frame stepping.

### `PROJECT-PREVIEW-002` — Stabilize Windows playback and connect timeline selection

- Priority/status: P0 / Done
- Acceptance: Temporary Project Preview uses Media Foundation H.264 rather than the project's final-export
  encoder; a real mixed-source render decodes cleanly with constant frame timestamps; double-clicking a Video
  block opens its source in muted Clip Preview.

### `PROJECT-PREVIEW-003` — Render only the selected project interval

- Priority/status: P0 / Done
- Acceptance: An active timeline range trims final composited video and mixed audio to that interval; output
  timestamps restart at zero, WPF maps playback to global project time, and full preview remains unchanged.

### `PROJECT-PREVIEW-004` — Add contextual preview entry points and coverage feedback

- Priority/status: P0 / Done
- Acceptance: The playhead menu previews from its global time and marks range boundaries; a selected-range
  menu previews that interval; media outside the latest preview coverage has a restrained yellow edge.

### `TIMELINE-002` — Add an exact-frame playhead and visual track stack controls

- Priority/status: P0 / Done
- Acceptance: The ruler selects a frame by click/drag; tracks collapse, reorder, and accept optional colors;
  blocks accept optional colors; and the topmost visual track composites above lower tracks.

### `TIMELINE-003` — Add frame-snapped range selection

- Priority/status: P0 / Done
- Acceptance: Shift/Ctrl drag on the ruler paints a visible frame-snapped range, modifier-click extends from
  the current frame, normal clicks clear the range, and Project Preview playback stops at the selected end.

### `TIMELINE-004` — Make range boundaries directly editable

- Priority/status: P0 / Done
- Acceptance: Start/end handles drag independently without crossing, Mark start/end use the current frame,
  and changing a range invalidates a stale rendered preview.

### `TIMELINE-005` — Synchronize selection and expose compatible lane effects

- Priority/status: P0 / Done
- Acceptance: Selecting a timeline item highlights its Layers / Used Clips row; left-clicking empty lanes plus
  track-header/item actions offer only plugins compatible with their resolved target track and preserve the
  clicked or selected timing.

### `TIMELINE-006` — Make timed-block dragging and resizing predictable

- Priority/status: P0 / Done
- Acceptance: Dragging preserves the pointer grab offset and shows the exact landing interval before drop;
  left/right handles resize non-primary timed blocks; an enabled-by-default checkbox additionally snaps either
  block edge to primary source-clip starts and ends.

### `TIMELINE-007` — Correct live manipulation and add direct track/item gestures

- Priority/status: P0 / Done
- Acceptance: Resize capture survives lane selection updates; drag grab coordinates are measured before lane
  projection changes; track names drag vertically; Video track double-click brings Project Preview forward;
  timed effect and overlay double-click opens the matching editor.

### `TIMELINE-008` — Keep edge resizing linear

- Priority/status: P0 / Done
- Acceptance: Each resize preview is derived from the pointer's displacement from DragStarted rather than a
  sum of repeated relative Thumb deltas; 50, 100, and 200 pixels therefore map linearly at every zoom level,
  with the existing frame/clip snapping applied once to the resulting edge.

### `UX-RANGE-001` — Standardize effect ranges and numeric adjustment

- Priority/status: P1 / Done
- Acceptance: Editors show Start/End by default with optional duration entry and whole-timeline shortcuts;
  new effects inherit the enclosing range of all selected Video items; numeric sliders/arrows use documented
  convenience bounds while exact finite manual entry remains possible beyond them.

### `UX-RANGE-002` — Replace independent time sliders with one compact range timeline

- Priority/status: P1 / Done
- Acceptance: Start/End text and arrow controls remain exact, while one miniature track moves the full interval
  or resizes either boundary using the project's snap increment.

### `PROJECT-PREVIEW-005` — Preview the selected frame while editing an effect

- Priority/status: P1 / Done
- Acceptance: The effect editor opens a companion frame window snapped to its side; Preview renders an unsaved
  candidate at the playhead, Auto refresh debounces changes, stale renders cancel, and no recovery/history state
  is changed by the candidate.

### `PROJECT-PREVIEW-006` — Add contextual frame/range/all prerender actions

- Priority/status: P0 / Done
- Acceptance: Prerender Preview renders the active timeline range or, without one, a short slice at the current
  frame and pauses on it; adjacent Frame and All controls force their named scope, and none records export history.

### `UX-OVERLAY-001` — Directly manipulate positioned overlays in Project Preview

- Priority/status: P0 / Done
- Acceptance: Active text/image overlays show selectable content gizmos over the correctly letterboxed project
  frame; dragging moves, a corner handle scales, and a rotation handle rotates. Selection synchronizes by item ID
  with the timeline and Layers / Used Clips, transform values appear in the overlay editor, schema-5 and older
  placement remains compatible, and GUI/CLI FFmpeg renders apply the same persisted transform.

### `UX-OVERLAY-002` — Make preview transforms transactional and add overlay alpha fades

- Priority/status: P0 / Done
- Acceptance: A preview click activates move/scale/rotation controls and adjacent OK/Cancel; Enter/Escape are
  equivalent, live movement does not flood undo history, cancellation restores the draft, and text/image editors
  persist per-item fade-in/out that real GUI/CLI renders apply to transparency.

### `UX-HISTORY-001` — Add project undo/redo and visible dirty state

- Priority/status: P0 / Done
- Acceptance: Ctrl+Z/Ctrl+Y and toolbar arrows navigate a bounded project history; undoing back to and redoing
  onto the save point updates the title/project asterisk; closing dirty work offers literal Save, Don't save,
  and Cancel choices and never closes after a cancelled or failed save. Clean shutdown clears crash recovery;
  equivalent legacy recovery opens clean, while semantically changed recovery remains dirty.

### `RENDER-ORDER-001` — Apply filter effects and overlays in visual track order

- Priority/status: P0 / Done
- Acceptance: Bottom-to-top filter/overlay operations retain their project track order; a real image below
  Video blur renders blurred, while moving its track above the effect renders the image sharp.

### `BROWSER-005` — Make multi-selection modifiers explicit and expose a grouped effect catalog

- Priority/status: P0 / Done
- Acceptance: Ctrl-click toggles individual clips, Shift-click selects from the first selection anchor, and
  Add selected clips consumes the full selection. A separate Content Browser Effects tab groups native and
  plugin entries alphabetically by compatible timeline, repeating plugins where appropriate.

### `TIMELINE-009` — Add default semantic track order and persistent block disable state

- Priority/status: P0 / Done
- Acceptance: New projects open with Overlays, Video, Progress, Background, and Audio from top to bottom;
  obsolete default Effects tracks are no longer created. Any block can be disabled/enabled without deletion,
  stays editable, renders darkened/grayed while disabled, and the shared GUI/CLI mapper excludes it.

### `PROGRESS-001` — Create progress blocks from selected clips and reuse their visual style

- Priority/status: P0 / Done
- Acceptance: Progress is addable from its lane and the Effects tab; one selected clip creates
  `PROGRESS <clipname>`, multiple clips define the enclosing interval, accepted visuals become portable INI
  defaults, and timeline context actions copy/paste style without changing timing.

### `PROJECT-PREVIEW-007` — Show effect-preview work and synchronize transport state

- Priority/status: P0 / Done
- Acceptance: The effect-frame companion is the editor's width and opens above it where the work area allows;
  indeterminate/determinate progress and elapsed render time remain visible. Range/all autoplay changes the
  project play button to Pause only after media opens, and clicking an empty timeline lane selects that frame.

### `PROJECT-OPEN-001` — Offer disk and recent-project choices from Open

- Priority/status: P1 / Done
- Acceptance: Open presents a disk command plus up to ten newest distinct existing `.nya` paths; normal open
  and save update the portable INI list without storing project content.

### `FX-LIGHTNESS-001` — Give Background lightness literal endpoint behavior

- Priority/status: P0 / Done
- Acceptance: The -100..100 slider maps linearly to FFmpeg `eq` brightness -1..1, with -100 yielding black,
  0 unchanged, and +100 white; the editor documents lightness, saturation, and hue calculation ranges.

### `RENDER-BG-002` — Define optional overlay contribution to Blur content background

- Priority/status: P1 / Open
- Acceptance: Confirm whether “exclude from bg blur” targets the raw-source side-fill Background module or the
  track-ordered Video blur adjustment layer, then persist and render the default-on overlay preference without
  duplicating or unexpectedly dimming the visible overlay.

### `RENDER-OVERLAY-002` — Compose still overlays safely after Background effects

- Priority/status: P0 / Done
- Acceptance: Image sources are bounded and timestamped to their item interval, stop repeating afterward, and
  the recovered real photo-overlay plus Background blur project renders through both native MPEG-4 and Media
  Foundation H.264; the full H.264 output decodes cleanly.

### `RENDER-CANVAS-001` — Normalize the post-effect canvas for strict encoders

- Priority/status: P0 / Done
- Acceptance: Final plugin/overlay output is forced to the requested even dimensions, square pixels, and
  encoder pixel format; the reported 1920x1080 background-blur project renders and decodes with H.264 MF.

### `UX-PREVIEW-003` — Consolidate preview transport controls

- Priority/status: P1 / Done
- Acceptance: Clip and Project Preview each use one stateful play/pause button and an icon mute toggle; Project
  Preview owns the centered accented render action, and Clip Preview offers timeline-double-click autoplay.

### `UX-PREVIEW-004` — Split/join previews and expose compact editor controls

- Priority/status: P1 / Done
- Acceptance: Clip and Project Preview switch between joined tabs and resizable side-by-side viewports;
  Video-block double-click activates Clip Preview; autoplay remains visible; Project Settings lives in the
  Project Preview footer; track groups use themed square triangle expanders; timeline zoom and height use
  minus/value/plus controls.

### `UX-LAYERS-001` — Expose per-item transforms and effects from Used Clips

- Priority/status: P1 / Done
- Acceptance: Used Clips selection synchronizes with the timeline; its button, double-click, and context action
  open fit/fill/stretch, fades, and volume; a plugin effect can inherit the selected item's start and duration.

### `UX-SPLASH-002` — Pace fast startup logs without delaying real scans

- Priority/status: P1 / Done
- Acceptance: Opening and completion holds each last 100–200 ms; ordinary fast startup-only lines appear
  20–40 ms apart; the first successful installation launch lasts at least five seconds and persists completion,
  while later normal startups last at least approximately three seconds. Configured startup rescans and manual
  refresh diagnostics remain immediate.

### `UX-SPLASH-003` — Expose a detailed staged startup pipeline

- Priority/status: P0 / Done
- Acceptance: The wide split splash shows a stage label, percentage, progress bar, and timestamped diagnostics;
  software layout, plugins, catalog, project/recovery, fonts, and editor synchronization are distinct messages.
  Enabled startup scanning reports per-file counts and percentage, while disabled/unconfigured scanning reports
  an explicit skip reason. Real UI Automation verifies both conditional branches and the layout is inspected.

### `UX-SAFETY-001` — Protect unsaved projects and finish application handoff polish

- Priority/status: P0 / Done
- Acceptance: Closing a dirty window defaults to Save and offers Don't save/Cancel; failed/cancelled saving
  keeps the process open; the startup/rescan splash uses short boundary holds; dark scrollbar templates
  match the editor theme; and the release is published over the documented launch folder.

### `RENDER-SAR-001` — Normalize scaled clip aspect metadata before concatenation

- Priority/status: P0 / Done
- Acceptance: Every base segment and timed video layer resets to square pixels after its final scale,
  pad, crop, or background-module stage. The exact three real catalog clips from the reported failure render
  together at 1920x1080 with `sample_aspect_ratio=1:1` and 70.804 seconds of output.

### `CATMETA-001` — Add editable tags, previews, and named-project usage details

- Priority/status: P1 / Done
- Acceptance: Additive SQLite migration, five-frame preview generation, tag editing/filtering, and
  project history queries passed.

### `LAYERS-001` — Persist and edit a project layer/track model

- Priority/status: P1 / Done
- Acceptance: Five semantic default tracks plus load-compatible optional legacy Effects tracks, additional
  named tracks, background color, and requested item fields persist; add/edit/remove controls and the shared
  GUI/CLI renderer projection pass.

### `FX-001` — Add timed fades, overlays, music, progress ranges, and fit modes

- Priority/status: P1 / Done
- Acceptance: Controls and real renders cover text, PNG, progress, music, fades, volume, fit modes, timed
  video blur, and configurable source-derived background blur without a GPL-only filter.

### `PLUGIN-001` — Load extensible media/effect modules from the portable application

- Priority/status: P0 / Done
- Acceptance: A versioned Core contract describes media categories, render stages, track compatibility,
  and parameters; isolated assembly contexts discover modules under `plugins`; `.nya` stores module IDs and
  values; missing/incompatible modules fail explicitly; three built-in modules and a real render pass.

### `OUTPUT-001` — Add common output presets and custom output values

- Priority/status: P1 / Done
- Acceptance: Officially sourced presets and validated custom 640x360/24 settings reached FFmpeg and
  FFprobe.

### `DEPLOY-001` — Produce a one-folder deployment with a tidy `thirdparty` boundary

- Priority/status: P1 / Done
- Acceptance: Framework-dependent and self-contained folders ran; bundled FFmpeg tools were discovered
  and rendered from `thirdparty`.

### `VERSION-001` — Expose one application/component version to users

- Priority/status: P1 / Done
- Acceptance: All projects share central assembly/file metadata; the main title/status bars and CLI
  version output resolve it from Core.

### `VERSION-MARKER-001` — Put a visibly changing version file beside every executable

- Priority/status: P0 / Done
- Acceptance: Exactly one extensionless `version_<version>` file matches central version metadata, is copied
  beside GUI and CLI build/publish outputs, contains a short changelist, and replaces stale markers. Builds and
  portable publishing fail for a missing, duplicate, stale, or mismatched copied marker.

### `DEPLOY-002` — Keep application DLL/runtime clutter out of the portable root

- Priority/status: P1 / Done
- Acceptance: Single-file publishes leave only two entry-point executables and the INI beside organized
  `docs` and `thirdparty` folders.

### `DEPLOY-003` — Always ship an audited FFmpeg runtime with the application

- Priority/status: P0 / Done
- Acceptance: A pinned LGPL v3 shared FFmpeg/FFprobe payload, DLLs, license, source record, build flags,
  and hashes live under `thirdparty\ffmpeg`; GUI/CLI builds and every portable publish copy it.

### `DEPLOY-004` — Publish an installable GitHub Release for non-developers

- Priority/status: P0 / Done
- Acceptance: A versioned self-contained Windows x64 ZIP and SHA-256 file are attached to a public GitHub
  Release after the CLI version, package layout, and FFmpeg payload pass validation; the Release includes
  direct installation and unsigned-binary guidance.

### `OVERLAY-001` — Support multiple image/text overlays with individual timing

- Priority/status: P2 / Done
- Acceptance: The layer editor controls timing and placement for multiple elements; timed render passed.

### `PREVIEW-001` — Add an FFmpeg contact-sheet/slideshow fallback preview

- Priority/status: P2 / Done
- Acceptance: Configurable cached contact sheets appear below Windows playback and remain available for
  unsupported codecs.

### `PROJECT-001` — Save and reopen named timelines with crash recovery

- Priority/status: P2 / Done
- Acceptance: Versioned schema-7 multi-track `.nya` documents and atomic `autosave.nya` recovery round-trip
  background/module metadata, optional track/item colors, and overlay transforms/fades without embedding media;
  older schema projects remain readable and GUI/CLI checks pass.

### `UX-PROJECT-002` — Separate durable Preferences from frequently changed project settings

- Priority/status: P0 / Done
- Acceptance: Preferences now contain application folders, scanning, previews, tools, fonts, and workspace
  behavior; project target/output settings live in `.nya` and are visible from the Project Preview rollout.

### `UX-TIMELINE-002` — Add precise scalable timeline editing

- Priority/status: P0 / Done
- Acceptance: Dynamic independently visible lanes, vertical scrolling, track-height/time zoom, fit controls,
  time/frame ruler modes, interval/block-edge snapping, drag movement, Ctrl multi-selection, and selected-video
  controls are present.

### `UX-PREVIEW-002` — Add muted preview transport controls

- Priority/status: P1 / Done
- Acceptance: Playback starts muted and exposes play/pause, seek, mute, volume, and time feedback.

### `UX-FONT-001` — Support portable-folder and installed system fonts

- Priority/status: P1 / Done
- Acceptance: The default portable `fonts` folder is browsable/openable, custom TTF/OTF choices are marked,
  installed font families remain selectable, and both paths reach FFmpeg text rendering.

### `UX-SPLASH-001` — Report startup and rescan progress without an empty or frozen window

- Priority/status: P1 / Done
- Acceptance: The foreground Mr Cat splash displays progress plus capped scrolling diagnostics during startup
  and manual rescans, with a five-second first-successful-launch minimum, approximately three-second returning
  minimum, and short manual-refresh boundary holds; Preferences defaults tall enough to avoid a scrollbar until
  the user shrinks it.

### `UX-PANEL-003` — Focus the selected editing panel from the keyboard

- Priority/status: P1 / Done
- Acceptance: Clicking Content Browser, Layers/Used Clips, or Project Timeline selects it; Space toggles its
  focused layout without stealing Space while editing text, choices, buttons, or sliders.

### `CAT-REFRESH-002` — Separate metadata refresh from preview regeneration

- Priority/status: P1 / Done
- Acceptance: Manual Refresh asks between catalog-only and forced thumbnail/contact-sheet regeneration,
  while source-folder setup is requested only when no folder is configured; CLI exposes the forced mode.

### `EDIT-001` — Add trim-in/out and per-clip volume

- Priority/status: P3 / Deferred
- Acceptance: Narrow controls work without expanding into general nonlinear-editor scope. Per-clip
  volume is done; trimming remains deferred.

## Audit TODOs

### `AUD-LIC-001` — Verify the default FFmpeg command uses no GPL/nonfree component

- Priority/status: P0 / Done
- Evidence: Native and Media Foundation encoder smokes passed. The mandatory bundled build has neither
  `--enable-gpl` nor `--enable-nonfree`.

### `AUD-CLI-001` — Verify headless commands are deterministic and automation-safe

- Priority/status: P0 / Done
- Evidence: Help/config/list/history JSON, exit codes 2/3/4/5, overwrite safety, real scan/render/history,
  use count, codec, and dimensions were verified.

### `AUD-CFG-001` — Verify INI parsing and writable-location behavior

- Priority/status: P0 / Done
- Evidence: Round-trip/malformed-input smokes passed; atomic same-directory writes and explicit permission
  errors are implemented.

### `AUD-DEP-001` — Audit NuGet dependencies for known vulnerabilities

- Priority/status: P1 / Done
- Evidence: The 2026-08-06 re-audit after adding the external FFmpeg payload reports zero known vulnerable
  NuGet packages; SQLitePCLRaw remains pinned to 2.1.12.

### `AUD-ARCH-001` — Re-audit class responsibilities after P0 refactors

- Priority/status: P1 / Done
- Evidence: Architecture responsibility entries reflect final GUI, CLI, rendering, persistence,
  configuration, packaging, and desktop boundaries.

### `AUD-DOC-001` — Check requested/done/not-done documentation against code

- Priority/status: P1 / Done
- Evidence: Project scope and the TODO register were cross-checked against implementation; open, partial,
  and deferred work is explicit.

### `AUD-DOC-002` — Make documentation readable without wide Markdown tables

- Priority/status: P0 / Done
- Evidence: Prose-heavy tables were replaced by headings and short lists; raw Markdown remains readable
  in narrow editors and terminals.

### `AUD-UX-001` — Verify theme, density, docking, virtualization, and drag/drop

- Priority/status: P0 / Done
- Evidence: Release build and a captured window show no white client surface; XAML/code audit confirms
  recycling and dock persistence.

### `AUD-UX-002` — Verify text contrast and compact layout retention

- Priority/status: P0 / Done
- Evidence: A 1440x900 capture shows readable primary, ordinary, header, and disabled buttons plus brighter
  small text without clipping.

### `AUD-DESIGNER-001` — Verify design-time layout does not depend on code-behind

- Priority/status: P1 / Done
- Evidence: XAML declares one non-overlapping panel per default slot; Release build and runtime default and
  custom layout smokes pass.

### `AUD-BROWSER-002` — Verify full-width browser focus and restoration

- Priority/status: P0 / Done
- Evidence: Runtime captures and UI Automation invocation verified both layouts, timeline availability,
  and state-specific accessible names.

### `AUD-PROJECT-001` — Verify project versioning, recovery identity, and overwrite safety

- Priority/status: P0 / Done
- Evidence: CLI create/load preserved schema, GUID, the then-current default tracks, background/plugin metadata, and
  output; overwrite returned 2; GUI startup and additive SQLite migration passed.

### `AUD-UX-003` — Verify project settings, timeline precision, preview, fonts, and splash

- Priority/status: P0 / Done
- Evidence: Release build, schema/INI round-trip, system-font/progress render, image sampling, and source audit
  confirm the requested settings split and editor behavior without launching another foreground GUI test.

### `AUD-STARTUP-001` — Reject undefined XAML resources before portable publication

- Priority/status: P0 / Done
- Evidence: The v0.1.9 startup failure was reproduced as an undefined `MainTextBrush` reference, corrected to
  the declared `TextBrush`, and the publisher now audits every simple `StaticResource` reference before build.

### `AUD-CATMETA-001` — Verify metadata migration, previews, tags, and usage semantics

- Priority/status: P1 / Done
- Evidence: A synthetic six-second MP4 produced an 800x90 five-frame sheet; tags survived rescan and usage
  appeared only after a successful named-project export.

### `AUD-FX-001` — Verify render layers, timing, output settings, and codecs

- Priority/status: P1 / Done
- Evidence: A real project rendered 640x360/24 MPEG-4 plus AAC for exactly six seconds; sampled frames and
  audio confirmed the requested effects.

### `AUD-PLUGIN-001` — Verify plugin loading, compatibility, persistence, and bundled rendering

- Priority/status: P0 / Done
- Evidence: CLI discovery reported all three built-in module IDs/versions; schema-3 persisted their parameters;
  compatibility checks reject incorrect tracks; the bundled FFmpeg rendered vertical source content with
  background blur/color controls, timed video blur, a second Video lane, text, progress, and mixed audio.

### `AUD-PORTABLE-001` — Verify one-folder publish and packaged-tool discovery

- Priority/status: P1 / Done
- Evidence: Framework-dependent and self-contained publishes ran; a layered render succeeded through
  automatic `thirdparty` discovery.

### `AUD-VERSION-001` — Verify shared and user-visible version reporting

- Priority/status: P1 / Done
- Evidence: Release assemblies, title/status bindings, and CLI text/JSON output report the central version
  without initializing data.

### `AUD-PORTABLE-002` — Verify compact root layout and package guards

- Priority/status: P1 / Done
- Evidence: Single-file root checks, published CLI execution, FFmpeg integrity/license checks, and notice
  copying pass.

### `AUD-RELEASE-FFMPEG-001` — Audit the exact bundled FFmpeg binary and notices

- Priority/status: P0 / Done
- Evidence: Pinned BtbN FFmpeg n8.1.2-34-g9b6c8969e0 LGPL shared runtime has no GPL/nonfree flags; archive
  and runtime hashes, exact LGPL v3 license, source URLs, replaceable DLLs, and build flags ship together.
