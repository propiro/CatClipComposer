# TODO register

Last audited: 2026-08-07

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

### `TIMELINE-002` — Add an exact-frame playhead and visual track stack controls

- Priority/status: P0 / Done
- Acceptance: The ruler selects a frame by click/drag; tracks collapse, reorder, and accept optional colors;
  blocks accept optional colors; and the topmost visual track composites above lower tracks.

### `TIMELINE-003` — Add frame-snapped range selection

- Priority/status: P0 / Done
- Acceptance: Shift/Ctrl drag on the ruler paints a visible frame-snapped range, modifier-click extends from
  the current frame, normal clicks clear the range, and Project Preview playback stops at the selected end.

### `UX-SAFETY-001` — Protect unsaved projects and finish application handoff polish

- Priority/status: P0 / Done
- Acceptance: Closing a dirty window defaults to Save and offers Don't save/Cancel; failed/cancelled saving
  keeps the process open; the startup/rescan splash lasts at least five seconds; dark scrollbar templates
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
- Acceptance: Six default track types, additional named tracks, background color, and requested item fields
  persist; add/edit/remove controls and the shared GUI/CLI renderer projection pass.

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

### `DEPLOY-002` — Keep application DLL/runtime clutter out of the portable root

- Priority/status: P1 / Done
- Acceptance: Single-file publishes leave only two entry-point executables and the INI beside organized
  `docs` and `thirdparty` folders.

### `DEPLOY-003` — Always ship an audited FFmpeg runtime with the application

- Priority/status: P0 / Done
- Acceptance: A pinned LGPL v3 shared FFmpeg/FFprobe payload, DLLs, license, source record, build flags,
  and hashes live under `thirdparty\ffmpeg`; GUI/CLI builds and every portable publish copy it.

### `OVERLAY-001` — Support multiple image/text overlays with individual timing

- Priority/status: P2 / Done
- Acceptance: The layer editor controls timing and placement for multiple elements; timed render passed.

### `PREVIEW-001` — Add an FFmpeg contact-sheet/slideshow fallback preview

- Priority/status: P2 / Done
- Acceptance: Configurable cached contact sheets appear below Windows playback and remain available for
  unsupported codecs.

### `PROJECT-001` — Save and reopen named timelines with crash recovery

- Priority/status: P2 / Done
- Acceptance: Versioned schema-4 multi-track `.nya` documents and atomic `autosave.nya` recovery round-trip
  background/module metadata plus optional track/item colors without embedding media; GUI and CLI checks pass.

### `UX-PROJECT-002` — Separate durable Preferences from frequently changed project settings

- Priority/status: P0 / Done
- Acceptance: Preferences now contain application folders, scanning, previews, tools, fonts, and workspace
  behavior; project target/output settings live in `.nya` and are visible from the Layers rollout.

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
- Acceptance: The foreground Mr Cat splash displays for at least five seconds with progress plus capped
  scrolling diagnostics during startup and manual rescans; Preferences defaults tall enough to avoid a
  scrollbar until the user shrinks it.

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
- Evidence: CLI create/load preserved schema, GUID, six default tracks, background/plugin metadata, and
  output; overwrite returned 2; GUI startup and additive SQLite migration passed.

### `AUD-UX-003` — Verify project settings, timeline precision, preview, fonts, and splash

- Priority/status: P0 / Done
- Evidence: Release build, schema/INI round-trip, system-font/progress render, image sampling, and source audit
  confirm the requested settings split and editor behavior without launching another foreground GUI test.

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
