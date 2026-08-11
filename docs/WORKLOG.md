# Worklog

This is an append-only record of material project work. Newest entries go first. Corrections should be added as new notes rather than rewriting historical results.

## 2026-08-12 — Explicit vibecoding disclosure

- Made the public README open with the requested verbatim statement that Cat Clip Composer is entirely
  vibecoded and that the project is an experiment in creating software without manually touching code.
- Mirrored the experiment's positioning in the project goals and recorded the completed documentation item.

## 2026-08-11 — Public v0.1.15 Windows release

- Published `v0.1.15` from commit `6eb6c21` with the 280,213,770-byte self-contained Windows x64 ZIP and its
  separate lowercase SHA-256 file; generated executables remain outside the source branch.
- GitHub's stored asset digest, the downloaded checksum, and an independently downloaded ZIP all matched
  SHA-256 `5aeffc0121ae8ff06f49b16a23da6bbbd2ccdd596f67c80d983fd406fc0cf1a9`.
- A fresh extraction reported Cat Clip Composer v0.1.15 through the packaged CLI, and all ten files covered
  by the bundled FFmpeg manifest passed their individual SHA-256 checks.

## 2026-08-11 — Portable GitHub Release preparation

- Prepared a complete self-contained Windows x64 ZIP and SHA-256 checksum through the established portable
  publisher after validating the central and packaged CLI versions.
- Kept generated executables out of the source branch and documented version-tagged GitHub Release assets as
  the public distribution boundary.
- Updated public installation and deployment guidance to distinguish the executable asset from GitHub's
  source archives and disclose the current lack of code signing and possible SmartScreen prompt.

## 2026-08-11 — Public installation and FFmpeg guidance

- Reworked the public README into a usable installation page with honest portable-release status, complete
  Git LFS clone and Release-build commands, first-run guidance, and the supported portable publisher.
- Explained why FFmpeg and FFprobe are required, when the bundled runtime is sufficient, how to select a
  separately downloaded compatible build, and how to check its required filters and encoders.
- Linked the official FFmpeg downloads, BtbN Windows builds, the exact pinned build, and upstream license and
  compliance pages. Clarified that FFmpeg is free/open-source but LGPL, GPL, and nonfree builds have different
  redistribution conditions.
- Corrected the stale central and built-in-plugin version references in the stack inventory to 0.1.15.

## 2026-08-11 — Direct timeline gestures and selected-frame effect preview

- Corrected two WPF event-order bugs: resize selection rebuilt and destroyed the captured Thumb, while drag
  grab coordinates were read from a lane visual after that visual had been replaced.
- Added vertical track-name drag/drop, Video-track double-click routing to Project Preview, and effect/overlay
  block double-click routing to the appropriate editor.
- Replaced separate Start and End sliders with one miniature timeline whose body moves the interval and whose
  handles resize either boundary; exact numeric and optional-duration entry remain available.
- Retained track order through render mapping and interleaved Video filter effects with overlays bottom-to-top.
  A real image/blur smoke visually confirmed blurred-below and sharp-above behavior.
- Added a snapped effect-frame companion window. It renders a cloned, unsaved effect candidate at the selected
  playhead on demand or after a debounced edit, with cancellation and no recovery/history mutation.
- Released the completed portion as visible application/component version 0.1.15. The distinct raw-source
  Background-module contribution preference remains open pending semantic confirmation in `RENDER-BG-002`.

## 2026-08-11 — Predictable effect timing and still-overlay render correction

- Preserved the exact pointer grab offset while dragging timeline blocks and added a translucent preview of
  the range that the move will commit.
- Added left/right resize handles to every non-primary timed block and an enabled-by-default option to snap
  moving or resized edges to primary source-clip boundaries.
- Standardized layer, plugin, and clip-effect numeric editing with bounded sliders, decrement/increment
  buttons, and exact finite manual entry. Range editors now default to Start/End, optionally accept duration,
  offer one-click zero/last-clip bounds, and inherit the outer range of selected Video blocks.
- Changed Background blur lightness to a human-scale percentage, normalized hue to 0–360 degrees, and added
  schema-5 migration for older saved parameter values.
- Reproduced the reported image-overlay plus Background blur failure from a cloned recovery project. Bounded
  and timestamped still inputs now stop repeating after their item interval instead of producing FFmpeg's
  invalid-argument failure when composed after a Background effect.
- Released the work as visible application/component version 0.1.14; verification and exact-folder portable
  publication are recorded by the commit containing this entry.

## 2026-08-07 — Grid browser, dynamic timelines, and plugin effects

- Replaced the catalog row list with a recycling virtualized tile grid and retained full-width browser focus
  plus direct drag/drop to a selected Video lane.
- Added Space-key focus toggling for the browser, layers, and timeline panels; added dynamic named track
  creation/removal and project background color.
- Added Ctrl multi-selection, draggable timeline blocks, interval/neighbor-edge snapping, and horizontal and
  vertical fit controls. Browser drops can insert into the base Video sequence or create timed layers on an
  additional Video track.
- Added a versioned Core plugin API with media categories, render stages, compatible timelines, parameter
  descriptors, isolated assembly dependency loading, diagnostics, and `.nya` plugin persistence.
- Added a separately loaded built-in module project containing configurable source-derived Background blur,
  timed Video blur, and PNG splash-screen source modules. Removed the old hard-coded blur-background render
  branch from normal editing.
- Added catalog-only versus forced-preview refresh choices, a CLI `--regenerate-previews` option, and a
  foreground startup/rescan splash with a three-second minimum.
- Updated the publisher to require/copy `plugins`, advanced the project schema to 3, and advanced all
  application components to 0.1.6.
- Verification: Release build and self-contained portable publish passed; CLI module discovery found all
  three modules; the NuGet audit found no known vulnerabilities; a bundled-FFmpeg smoke
  rendered a two-second 320x180 MPEG-4/AAC project from a vertical source with background blur/color
  controls, timed video blur, second Video lane, text, progress, and audio. The published CLI repeated the
  render through its packaged plugin and FFmpeg folders.
- Closed: `PLUGIN-001`, `UX-PANEL-003`, `CAT-REFRESH-002`, and `AUD-PLUGIN-001`; expanded acceptance for
  `BROWSER-001`, `LAYERS-001`, `FX-001`, `PROJECT-001`, `UX-TIMELINE-002`, and `UX-SPLASH-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Project-centered editing, precision timeline, and startup feedback

- Reorganized the visible Preferences around application-level folders, library scanning, previews, tools,
  fonts, and docking; moved target/output choices into project settings and added their right-panel rollout.
- Made Preferences open at 760x850 so normal content fits without a vertical scrollbar; shrinking the window
  still enables scrolling.
- Added default-on startup rescanning, 12-slide contact sheets, bundled-FFmpeg missing guidance, and a
  packaged `fonts` folder with installed/custom font selection and visible custom markers.
- Changed normal project/recovery names to `.nya`/`autosave.nya`, advanced the project schema to version 2,
  and retained atomic saves plus readable schema-1 loading.
- Added muted preview transport, five scalable timeline lanes, time zoom, track height, time/frame ruler
  modes, snapping, direct selected-clip controls, and individually styled progress timeline effects.
- Added the user-supplied Mr Cat startup/rescan splash with a lightly sharpened image, progress, diagnostics,
  and cancel support for manual scans.
- Reworked ComboBox templates to retain the dark theme and recycle long font lists.
- Advanced all application components to 0.1.5.
- Verification: recorded by the audit entry and final commit containing this work.
- Closed: `UX-PROJECT-002`, `UX-TIMELINE-002`, `UX-PREVIEW-002`, `UX-FONT-001`, `UX-SPLASH-001`, and
  `AUD-UX-003`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Mandatory audited FFmpeg bundle and readable documentation

- Audited the previously used Gyan full build and rejected it for mandatory distribution because its own
  configuration enables GPL components.
- Added pinned BtbN FFmpeg `n8.1.2-34-g9b6c8969e0-20260806` Windows x64 LGPL shared executables and DLLs
  under `thirdparty\ffmpeg`, tracked through Git LFS.
- Added the distributor license, pinned archive/upstream source record, build configuration/capability record,
  and SHA-256 manifest beside the runtime.
- Made GUI/CLI builds copy the bundle, removed application-only/alternate-tool publish modes, made default
  discovery resolve the bundle directly, and added publisher integrity, license-flag, version, and capability
  checks.
- Replaced prose-heavy Markdown tables across project, TODO, architecture, stack, output, and headless docs
  with readable headings and lists.
- Advanced all application components to 0.1.4.
- Verified clean Release builds, zero known vulnerable NuGet packages, build-output payload copies, an
  approximately 373 MB self-contained package, exact manifest hashes, published CLI version output, catalog scan,
  both preview types, a real two-second 1920x1080/30 MPEG-4 plus AAC render, and export usage history.
- Commit: recorded by the commit containing this entry.
- Closed: `DEPLOY-003`, `AUD-RELEASE-FFMPEG-001`, and `AUD-DOC-002`.

## 2026-08-06 — Full-width content browser focus

- Replaced the browser body hide/show control with a left-edge direction arrow that expands the content browser across the complete workspace width.
- Kept the full-width timeline visible while browsing so virtualized catalog rows remain draggable directly onto the project.
- Made browser focus temporary: collapsing restores every panel's persisted dock assignment, including custom layouts, without rewriting settings.
- Added state-specific tooltips and UI Automation names, and advanced all application components to 0.1.3.
- Verified with clean Release builds plus a 1440x900 live expand/restore capture driven through UI Automation.
- Closed: `BROWSER-002` and `AUD-BROWSER-002`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Complete XAML designer workspace

- Diagnosed the apparently empty Visual Studio designer as four XAML panels overlapping in the default grid cell until runtime docking code executed.
- Declared the default left/center/right/bottom grid coordinates, spans, and gutters directly on the four panels in `MainWindow.xaml`.
- Kept persisted docking unchanged: `WorkspaceLayoutController` still overrides the XAML defaults before the window is displayed.
- Added design namespaces explicitly and advanced all application components to 0.1.2.
- Verified by Release build, XAML coordinate audit, default runtime startup, and saved-layout override smoke.
- Closed: `WORKSPACE-002` and `AUD-DESIGNER-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — UI text readability correction

- Reproduced the reported unreadable button labels in a 1440x900 runtime capture.
- Removed the light-filled primary action treatment and used a darker warm-neutral surface with an explicit high-contrast text color in the template visual tree.
- Added readable disabled button surfaces/text, strengthened secondary/tertiary neutral colors, enabled layout rounding, and raised the smallest main-workspace labels from 8-9 px to 10 px.
- Preserved the compact one-pixel-corner layout and blue-free palette while bumping every application component to 0.1.1.
- Verified with clean Release builds, application startup, title/version inspection, and a second 1440x900 runtime capture with no clipping.
- Closed: `UI-002` and `AUD-UX-002`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Versioned and compact portable package

- Audited the recovered implementation against the complete dockable-editor, catalog, metadata, project/recovery, layered-render, output-profile, and portability request.
- Added shared 0.1.0 assembly/file/informational metadata for every project and exposed it in the main-window title/status bars plus CLI text/JSON output.
- Changed portable publishing to validated single-file GUI/CLI executables so managed/native runtime files no longer clutter the package root.
- Made a complete FFmpeg pair the normal package requirement, retained an explicit application-only escape hatch, and added nonfree/GPL/license/build-info guards.
- Verified by Release build, CLI version/help checks, assembly metadata inspection, package layout checks, published CLI execution, and FFmpeg packaging guard smokes.
- Closed: `VERSION-001`, `DEPLOY-002`, `AUD-VERSION-001`, and `AUD-PORTABLE-002`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Editable render layers, output presets, and portable publish

- Connected five persisted project tracks to one shared Core render mapper consumed by WPF and the headless CLI.
- Added layer controls to add/edit/remove timed text, PNG/JPEG, looped music with volume/fades, and whole/custom progress ranges.
- Added per-video/still Fit, Fill, Stretch, animated Blur Background, fade-in/out, and source volume controls.
- Added seven common YouTube/social output presets plus validated custom dimensions, FPS, encoder, quality, and video/audio bitrates saved per project.
- Expanded FFmpeg normalization/mixing for timed layers and fixed looped-image framesync termination found during the real render smoke.
- Added a one-folder publisher for GUI, CLI, runtime, config, docs, and an explicit `thirdparty\ffmpeg` boundary with automatic tool discovery/build-info capture.
- Verified a 6.000-second 640×360/24 MPEG-4 + AAC output with blur, fades, text, PNG, progress, and music; inspected sampled frames. Published framework-dependent and 154 MB self-contained folders; published CLI and packaged-tool rendering passed.
- Closed: `LAYERS-001`, `FX-001`, `OUTPUT-001`, `OVERLAY-001`, `DEPLOY-001`, `AUD-FX-001`, `AUD-PORTABLE-001`.
- Open release gate: `AUD-RELEASE-FFMPEG-001` for the exact redistributed binary/notices.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Catalog metadata and lightweight content previews

- Kept SQLite as the searchable mutable catalog because multiple changing source roots, tags, availability, stable media IDs, and project/export joins are relational state; kept JPEG previews as replaceable files rather than database blobs.
- Added an additive migration for normalized user tags and contact-sheet paths while preserving legacy catalog rows.
- Added configurable evenly sampled FFmpeg contact sheets, shared preview-process handling, and the codec-independent preview strip in the GUI.
- Added tag search/editing and per-clip completed-project usage with project name/path, date, output, and occurrence count.
- Added headless `tag`/`usage`, richer `list` JSON, and optional render project identity.
- Synthetic scan produced a static thumbnail and 800×90 five-slide sheet; tags survived normalization/rescan; usage was empty before export and populated only after a successful named-project render.
- Closed: `CATMETA-001`, `PREVIEW-001`, `AUD-CATMETA-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Versioned project files and recovery

- Added a versioned `.ccproject` schema with stable project/track/item IDs and Video, Overlay, Audio, Progress, and Effects tracks.
- Added persisted output settings and item fields for timing, fit, fades, volume, text/font/position, and progress ranges in preparation for the layer editor.
- Added atomic normal project save/load plus automatic atomic recovery on every timeline mutation.
- Added GUI New/Open/Save and automatic startup recovery, and a headless project create/inspect command.
- Separated configured metadata, project, and final-output folders; metadata changes take effect on restart without moving data implicitly.
- Added project name/path to successful export history via an additive SQLite migration; editing and autosave do not increment usage.
- Project store smoke preserved schema version, GUID, five tracks, 1920x1080 output, and overwrite exit code `2`; GUI startup passed.
- Closed: `PROJECT-001`, `AUD-PROJECT-001`; `LAYERS-001` is in progress.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Compact dockable editor workspace

- Replaced the green/blue-adjacent, spacious card treatment with a warm monochrome workstation palette, compact square controls, tight gutters, and dark Windows title-bar requests.
- Explicitly painted every derived WPF window and root surface, removing the white client-area leak caused by relying on an implicit base `Window` style.
- Rebuilt the main window as four resizable slots for content browser, preview, layers/used clips, and project timeline.
- Added persisted panel swapping among left/center/right/bottom slots and an expandable browser body.
- Replaced the non-virtualizing thumbnail wrap panel with recycled virtualized rows that bind only cached preview images for realized items.
- Added content-browser drag/drop into the project timeline.
- Closed: `UI-001`, `WORKSPACE-001`, `BROWSER-001`, `AUD-UX-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Final architecture and documentation audit

- Cross-checked repository modules and larger classes against the single-responsibility and GUI/CLI sharing rules.
- Cross-checked the requested feature matrix, completed work, partial work, deferred work, stable TODO IDs, configuration/CLI references, stack inventory, and third-party notices against the implementation.
- Confirmed all P0 implementation/audit work is done; remaining open work is explicitly documented product scope.
- Closed: `AUD-ARCH-001`, `AUD-DOC-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — WPF desktop helper extraction

- Replaced duplicate File Explorer launch code with one path-normalizing desktop shell helper.
- Centralized startup and owned-window exception presentation without abstracting window-specific validation/dialog flow.
- Closed: `MOD-004`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — SQLite persistence responsibility split

- Reduced the catalog adapter to media/history operations behind `IMediaCatalog`.
- Extracted focused connection creation, schema initialization, invariant UTC conversion, media parameter/row mapping, and export-history aggregation.
- Kept the existing schema and Core interface unchanged.
- Corrected headless text history to display the already one-based stored projection order without adding a second offset.
- Closed: `MOD-003`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Headless CLI

- Added a separate console executable in the same solution and repository.
- Added config inspection, scan, catalog list, ordered render, and export-history commands.
- Added optional configuration/data isolation, human-readable output, one-document JSON output, stderr progress, stable exit codes, Ctrl+C cancellation, and explicit overwrite protection.
- Reused shared service composition and the GUI's composition export/history workflow.
- Passed help/config/list/history JSON smoke checks, exit codes `2`, `3`, `4`, and `5`, overwrite protection, and a real FFmpeg scan/render/history/use-count workflow with an ordered still plus catalog clip.
- FFprobe verified native `mpeg4` at 1920x1080 for the CLI-rendered output.
- Closed: `CLI-001`, `BOOT-001`, `AUD-CLI-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Shared composition export workflow

- Added a Core application service that owns the render-and-record-history transaction.
- Migrated WPF export onto the shared service so the CLI can reuse identical successful-export bookkeeping.
- Kept FFmpeg rendering, catalog persistence, executable composition, and presentation behind separate responsibilities.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Non-GPL default encoder policy

- Added persisted encoder presets with an explicit license boundary.
- Made FFmpeg's native `mpeg4` encoder the default compatibility preset.
- Added Windows Media Foundation `h264_mf` as the preferred non-GPL H.264 option.
- Retained `libx264` only as a UI/INI-labeled `Libx264Gpl` opt-in.
- Adjusted renderer pixel format and arguments per encoder rather than sharing incompatible options.
- Generated input without libx264, rendered with both non-GPL presets, and used FFprobe to verify `mpeg4` and `h264` output codecs.
- Closed: `LIC-001`, `AUD-LIC-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — FFmpeg rendering module split

- Replaced the multi-purpose FFmpeg renderer with a small render coordinator.
- Extracted pure filter-graph construction, argument-safe command construction, process execution/progress/cancellation, and temporary-file cleanup.
- Moved the renderer into an explicit `Rendering` feature namespace and updated shared service composition.
- Passed a mixed video/still-image render with audio, text, PNG overlay, and per-clip progress after the split.
- Closed: `MOD-002`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Timeline presentation module extraction

- Extracted ordered segments, selection, insert/move/remove/clear operations, target duration, axis labels, summary values, and render-segment projection from `MainViewModel`.
- Bound WPF timeline controls directly to the focused `TimelineViewModel`.
- Exposed timeline clips as a read-only observable collection so external code cannot bypass ordering rules.
- Passed a direct timeline smoke test covering add, insert, move, reindex, duration/progress, target changes, render order, remove, and clear.
- Closed: `MOD-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Shared application composition root

- Added a focused `ApplicationServicesFactory` that constructs paths, INI settings, SQLite, scanning, probing, thumbnails, and rendering services.
- Moved WPF startup onto the shared factory and removed duplicated manual construction from `App`.
- Prepared the same composition root for headless CLI consumption.
- `BOOT-001` is in progress until the CLI uses the factory.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Executable-directory INI configuration

- Replaced the JSON settings implementation with focused INI reader, mapper, and atomic store classes.
- Set the default path to `CatClipComposer.ini` under `AppContext.BaseDirectory`.
- Documented the complete schema, enum values, escaping, defaults, clamping, and writable-directory behavior.
- Added a direct smoke test for round trips, multiline/backslash escaping, ordered folders, missing files, malformed values, and bounded values.
- Deleted the superseded JSON store; no legacy configuration path remains.
- Closed: `CFG-001`, `AUD-CFG-001`.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Repository policy and documentation system

- Parsed the owner’s workflow, modularity, configuration, headless, licensing, documentation, audit, and commit requirements into repository-scoped `AGENTS.md`.
- Added project goals and requested/completed/incomplete feature tracking.
- Added architecture boundaries and an initial responsibility audit.
- Added software stack, dependency, and desired-license policy documentation.
- Added stable engineering and audit TODO identifiers.
- Added this worklog and an append-only audit log.
- Commit: recorded by the commit containing this entry.

## 2026-08-06 — Initial functional MVP

- Implemented WPF catalog browsing, FFprobe scanning, FFmpeg thumbnails, SQLite persistence, timeline assembly, still screens, overlays, progress bars, portrait/landscape rendering, cancellation, and export history.
- Added initial README and third-party notices.
- Pinned SQLitePCLRaw 2.1.12 after a package audit found a high-severity issue in the older transitive native SQLite bundle.
- Verified Release build, application startup, scanning, thumbnails, mixed audio/silent input, overlays, progress styles, still screens, both output orientations, and history writes.
- Commit: `bde6480` (`feat: build the initial clip composer MVP`).

## 2026-08-07 — Layered preview and timeline interaction release

- Raised the portable application and shared component metadata to 0.1.7 and project persistence to schema 4.
- Split the center viewer into muted Clip Preview and rendered Project Preview tabs; the top PREVIEW action
  uses the shared renderer without writing completed-export history and supports seek/frame stepping.
- Added a frame-snapped playhead that follows ruler click/drag and Project Preview playback.
- Added visible Add track entry points, contextual browser/track/item/timeline actions, collapsible groups,
  track sorting, and persisted track/item color codes.
- Defined editor order as top-to-bottom and render order as bottom-to-top, with the bottommost Video track as
  the base composition.
- Fixed the expanded virtualized grid width, enabled multi-selection, mass tags, and multi-clip drag/drop,
  and made Space resolve focus anywhere inside eligible panels.
- Added dark scrollbar and preview-tab styling plus Save/Don't save/Cancel close protection whose default
  Save path refuses to close on cancellation or failure.
- Increased the Mr Cat startup/rescan minimum display to five seconds.
- Made forced in-place portable publishing preserve the existing executable-directory INI byte-for-byte.
- Verification and exact-folder portable publication are recorded by the commit containing this entry.

## 2026-08-07 — Mixed-aspect preview concat correction

- Reproduced the Project Preview failure with the same three catalog clips selected by the user.
- Confirmed FFmpeg received equal 1920x1080 frame sizes but different post-scale sample-aspect ratios.
- Reset sample aspect ratio after final scale/pad/crop/background processing for base segments and timed
  video layers, ensuring concat receives identical square-pixel streams.
- Rendered the three real sources through a copied catalog so testing did not alter real project-use history;
  FFprobe reported MPEG-4 1920x1080, SAR 1:1, AAC, and 70.804 seconds.
- Released the correction as visible application/component version 0.1.8.
- Commit: recorded by the commit containing this entry.

## 2026-08-07 — Stable Windows preview playback and timeline ranges

- Verified the reported jittered preview had monotonic 30 fps timestamps, decoded without FFmpeg warnings,
  and produced clean sequential sampled frames; the affected stream was MPEG-4 Advanced Simple Profile with
  B-frames, leaving Windows/WPF decoding as the incompatible boundary.
- Kept the project's chosen encoder for final export but made temporary Project Preview files use Windows
  Media Foundation H.264 Constrained Baseline without B-frames.
- Added Video-block double-click routing to the muted Clip Preview tab, including direct source fallback when
  the catalog card is unavailable.
- Added visible frame-snapped Shift/Ctrl ruler ranges, modifier-click extension, normal-click clearing, range-
  bounded Project Preview playback, and range-aware frame stepping.
- Released the correction as visible application/component version 0.1.9.
- Commit: recorded by the commit containing this entry.

## 2026-08-07 — XAML resource startup correction

- Corrected the timeline range label's undefined `MainTextBrush` reference to the theme's declared
  `TextBrush`, which had caused v0.1.9 to fail while constructing the main window.
- Added a repository XAML `StaticResource` audit and made it a required portable-publisher guard.
- Passed the complete resource audit, Release build, and a hidden startup smoke that reached the main window
  and remained alive beyond the five-second splash.
- Released the startup correction as visible application/component version 0.1.10.
- Commit: recorded by the commit containing this entry.

## 2026-08-07 — Range-only preview and editor transport pass

- Moved the render action into the center-bottom of Project Preview and retained a stronger accent than its
  transport controls.
- Consolidated both preview transports into stateful play/pause and speaker/muted-speaker buttons; added an
  Autoplay clips checkbox for timeline Video-block double-clicks.
- Added optional final video/audio range trimming to the shared renderer. WPF keeps the original timeline
  offset while the temporary file uses zero-based timestamps.
- Added draggable range boundary handles plus Mark start/end actions and stale-preview invalidation.
- Made Used Clips selection synchronize to timeline blocks, exposed Transform / FX by button, double-click,
  and context action, and prefilled new plugin effects from the selected item's start/duration.
- Paced only fast startup log lines by 500–750 ms; configured startup rescans and manual refreshes remain live.
- Released the work as visible application/component version 0.1.11.
- Commit: recorded by the commit containing this entry.

## 2026-08-07 — Split preview and compact timeline controls

- Added a header-level Split/Join action that moves the existing Clip and Project Preview panes between
  joined tabs and resizable left/right viewports without duplicating media state or controls.
- Moved the autoplay checkbox beside Add this clip so it remains visible, and routed Video timeline-block
  double-click through a dedicated post-selection event that activates Clip Preview before loading the source.
- Replaced the narrow time-zoom and track-height sliders with minus/value/plus controls and readable live values.
- Moved the Project Settings rollout from Layers / Used Clips to the bottom-left of Project Preview.
- Replaced native white expander glyphs with themed square buttons and up/down triangles for Project Settings
  and track groups.
- Raised the visible application/component version to 0.1.12; verification and exact-folder publication are
  recorded by the commit containing this entry.

## 2026-08-07 — Contextual preview, browser modes, and strict-canvas correction

- Reproduced the reported background-blur preview failure and traced it to a plugin-stage 1920x1081 frame
  reaching Media Foundation H.264. Final composition now restores exact project dimensions, SAR 1:1, and the
  encoder pixel format after every plugin and overlay stage.
- Added playhead actions for Preview from here and range marking, plus Preview range on the active ruler
  selection. Successful previews record their covered interval; newly changed or uncovered media blocks use
  a restrained yellow edge until rendered again.
- Added recycled thumbnail-list, small-grid, and large-grid Content Browser modes with portable Preferences
  for bounded small/large sizes and matching headless config output.
- Synchronized timeline and Layers / Used Clips selection and exposed compatible plugin actions from empty
  timeline lanes, track headers, and individual items.
- Raised the visible application/component version to 0.1.13; verification and exact-folder publication are
  recorded by the commit containing this entry.
