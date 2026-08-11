# Audit log

## AUDIT-2026-08-11-003 — Public install and FFmpeg documentation audit

Scope: public installation/build instructions and accurate FFmpeg requirement, download, and license
guidance.

Findings and remediation:

- The README described the bundled runtime and build commands but did not provide a complete clone/LFS flow,
  portable installation path, or a direct answer about whether FFmpeg is operationally required.
- The public repository currently has no packaged release asset. The installation section now directs users
  to Releases when an asset exists and otherwise gives a complete source-build path without claiming that an
  installer is available.
- The separate-tool instructions now identify the recommended Windows x64 LGPL shared variant, require the
  matching FFprobe and DLL set, name the required filter/encoders, and show local validation commands.
- License wording was checked against FFmpeg's official license and legal pages: FFmpeg is free/open-source,
  optional GPL components change the binary's license, and `--enable-nonfree` produces an unredistributable
  binary. The bundled manifest remains the audited LGPL v3 build with no GPL/nonfree flags.
- The stack inventory's stale application/plugin version references were corrected to the central 0.1.15
  version. Dependencies and shipped binaries did not change.

Verification: the documented Git LFS inventory listed the complete FFmpeg executable/DLL payload; the CLI
reported v0.1.15; and the bundled binary reported the pinned build, `drawtext`, `mpeg4`, `aac`, and `h264_mf`
with no GPL/nonfree configure flags. The required Release solution build completed with zero warnings and
zero errors; all added relative documentation targets exist; and `git diff --check` passed. Dependencies did
not change.

## AUDIT-2026-08-11-002 — Timeline capture, render-order, and frame-preview audit

Scope: reported resize/drag offsets, compact range interaction, track/item gestures, filter/overlay stack order,
and selected-frame feedback while editing plugin effects.

Findings and remediation:

- `DragStarted` selected an item by rebuilding `TimelineLanes`, removing the Thumb that WPF had just captured.
  Selection now occurs on committed resize. Dragging likewise measures its local grab point before any selection
  refresh can detach the source Border.
- Separate Start and End sliders did not communicate one interval. A compact shared canvas now has a draggable
  range body and independent boundary handles while retaining exact text/arrow entry.
- Render mapping discarded track order by applying all filter plugins before all overlays. Both values now carry
  track order into a single bottom-to-top visual operation sequence.
- Effect editing had no feedback until the complete project preview was rendered. A cloned project substitutes
  only the working item and renders a short H.264 slice at the playhead into a snapped companion window; automatic
  refresh is debounced and cancels obsolete renders.

Verification: Release builds passed after every code batch; the 16-file XAML resource audit resolved all 36
keys; the UI smoke exercised the real resize event sequence, mini range track, track reordering, render-order
mapping, and frame-preview controls. Two real 640x360 compositions showed a blurred image below Video blur and
a sharp image above it. The recovered photo-overlay plus Background blur case still rendered for two seconds
through native MPEG-4 and Media Foundation H.264, and its working Background effect rendered as an unsaved
0.1-second selected-frame preview. Dependencies did not change.

## AUDIT-2026-08-11-001 — Timed-layer interaction and overlay/blur render audit

Scope: effect movement/resizing, range and parameter entry, selected-clip defaults, and the reported JPEG
overlay plus Background blur preview failure.

Findings and remediation:

- A move was derived from the pointer itself instead of the original grab position, and WPF showed no landing
  state. Preview and commit now share one view-model calculation that preserves the group grab offset and
  exposes the exact snapped interval in the target lane.
- Timed blocks had no edge interaction. Non-primary items now expose independently draggable left/right
  handles; the view model validates and persists the resized range. Primary Video edges remain `EDIT-001`
  source trimming rather than silently changing source content.
- Range dialogs conflated end time with duration and repeated inconsistent numeric text boxes. Shared controls
  now use Start/End by default, optionally enter duration, provide whole-timeline bounds, initialize from all
  selected Video items, and pair bounded slider/arrow controls with unrestricted finite manual text values.
- The infinite looped still-image input combined `shortest`/EOF repetition with a post-Background stream,
  causing FFmpeg to reject the filter graph and write no packets. Still overlays are now trimmed, rebased to
  their project start, and pass through the underlying stream after their own end.

Verification: the Release solution built with zero warnings/errors; all 36 XAML resource keys resolved; the
UI construction smoke passed range editors, numeric caps/manual values, exact move previews, edge resizing,
clip-boundary snapping, browser modes, and preview layout. The two-second recovered overlay/blur case rendered
MPEG-4/AAC and H.264/AAC at 1920x1080, SAR 1:1, exactly 2.000 seconds, and H.264 decoded cleanly. The complete
89.53546-second cloned recovery composition rendered through Media Foundation H.264 and decoded cleanly.
The user's recovery/project files were never modified. The NuGet vulnerability audit reported no known
vulnerable direct or transitive packages; dependencies did not change.

## AUDIT-2026-08-07-024 — Dynamic timeline and plugin-module pass

Scope: requested grid browser, panel focus, dynamic tracks, background color/blur, plugin architecture,
refresh choices, splash timing, snapping, dragging, multi-selection, and fit controls; `AUD-PLUGIN-001`.

Findings:

- The catalog now uses a custom recycling virtualizing wrap panel with fixed-size preview cards. It realizes
  visible rows only and continues to bind cache image paths rather than decoding every source video.
- Content Browser, Layers/Used Clips, and Project Timeline record pointer focus and toggle temporary focus
  with Space. Text boxes, combo boxes, buttons, and sliders retain normal Space behavior.
- Schema 3 adds background color, a Background track, additional named tracks, and stable plugin IDs plus
  parameter dictionaries. The first Video timeline remains the sequential base; later Video timelines map
  to timed full-frame visual/audio layers.
- Timeline blocks use stable IDs for Ctrl multi-selection. Dragging within a lane preserves group offsets,
  snaps to the configured ruler interval or nearby start/end edges, and base-video dragging reorders at clip
  boundaries. Fit controls calculate time zoom and lane height from the visible surface.
- The module API is owned by Core and describes version, media kinds, render stage, compatible track kinds,
  and typed parameters. Infrastructure loads each trusted assembly through its own dependency resolver while
  sharing the Core contract identity. WPF only handles module selection/parameter editing; render mapping and
  compatibility enforcement are shared with CLI.
- The built-in module assembly provides source-derived Background blur with saturation/lightness/hue/zoom/
  Gaussian-blur controls, timed composited Video blur, and PNG still-source handling. It is copied into build
  outputs and required under the portable package's `plugins` folder.
- Manual Refresh now distinguishes metadata refresh from forced cache regeneration and requests source setup
  only when none exists. Startup and manual refresh splashes stay foreground for at least three seconds.
- Plugin modules are in-process trusted code, not sandboxed. Deployment documentation now states this
  boundary and advises accepting modules only from trusted sources.

Verification:

- Release solution builds passed with zero warnings and errors after every code batch.
- Built CLI `config --json` loaded and reported three module IDs, versions, stages, and media categories.
- The audited bundled FFmpeg initially exposed the absence of `eq`; the Background module was corrected to
  use the bundle's `hue` saturation/brightness controls and the smoke was rerun.
- A real two-second 320x180 native MPEG-4/AAC export from a 90x160 source passed with source-derived blurred
  side fill, all background color controls, timed video blur, a second Video timeline, text, segmented
  progress, and mixed audio. FFprobe confirmed 320x180 video plus audio for exactly two seconds; sampled
  frames showed the unblurred and timed-blur states.
- The self-contained portable publisher completed. Its root contains only GUI/CLI executables and INI;
  `plugins` contains the built-in DLL/dependency record, and the audited FFmpeg payload remains under
  `thirdparty`. The published CLI reported version 0.1.6, discovered all modules, and rendered the same
  project through the packaged plugin and FFmpeg paths.
- A catalog scan found four synthetic videos and generated eight cached images; catalog-only refresh changed
  none of those cache files, while forced-preview refresh rebuilt all four thumbnails and four contact sheets
  and reported four refreshed entries without errors.
- The full Release build passed with zero warnings/errors; the NuGet audit reported no known vulnerable
  direct or transitive package; `git diff --check` passed.

Result: requested functionality is implemented; schema advanced to 3, application/component version advanced
to 0.1.6, and `AUD-PLUGIN-001` is closed.

## AUDIT-2026-08-06-023 — Project settings and editor interaction pass

Scope: requested Preferences/project split, dark ComboBoxes, portable fonts, progress effects, preview
transport, precise timeline lanes, `.nya` files, startup scanning/splash, and default Preferences sizing;
`AUD-UX-003`.

Findings:

- The former application settings mixed durable folder/tool preferences with values that change per project.
  Project target and output choices now persist in schema-2 `.nya` files and are exposed through a default
  right-side rollout; legacy compilation-wide progress/overlay settings were removed rather than retained.
- Preferences now opens at 760x850, uses automatic scrolling only when needed, defaults contact sheets to 12,
  rescans on startup, manages a portable custom-font folder, and offers compatible FFmpeg-download guidance
  only when the mandatory bundle is unavailable.
- The custom ComboBox template paints selection and popup surfaces dark and recycles item containers. Text
  effects distinguish installed font families from custom TTF/OTF files, while both feed the shared renderer.
- Progress is represented only as an editable timeline effect with independent timing, style, color, height,
  and position. Five lanes, scalable height/time zoom, ruler modes, snapping, and selected-video controls make
  timeline placement explicit without stretching one video lane over the whole panel.
- Preview playback is muted by default and exposes transport, seek, mute, volume, and elapsed/total feedback.
- Startup and manual rescans report work through the sharpened Mr Cat splash instead of appearing empty; the
  manual scan can be cancelled. No final foreground launch was performed after the user clarified that an
  earlier test window had merely been closed accidentally.

Verification:

- Release solution build passed with zero warnings and errors after the final source changes.
- A headless schema/settings smoke confirmed 12-slide and startup-rescan defaults, `.nya` recovery, schema-2
  persistence, and project-effect round trips.
- The mandatory bundled FFmpeg rendered a two-second MPEG-4/AAC sample containing installed-system-font text
  and a segmented top progress effect; a sampled frame showed both correctly.
- Final package, CLI, vulnerability, and repository-integrity checks are recorded with the containing commit.

Result: requested behavior is implemented; application/component version advanced to 0.1.5 and
`AUD-UX-003` is closed.

## AUDIT-2026-08-06-022 — Mandatory FFmpeg bundle and documentation readability

Scope: requested always-present FFmpeg tools, exact-binary release gate, and unreadable Markdown tables;
`DEPLOY-003`, `AUD-RELEASE-FFMPEG-001`, and `AUD-DOC-002`.

Findings and verification:

- The machine's previously used Gyan `2026-01-14-git-6c878f8b82-full_build` reports `--enable-gpl`,
  libx264, libx265, and other GPL components. It remains suitable for local tests but was rejected as the
  mandatory normal-distribution payload.
- The selected replacement is BtbN Windows x64 LGPL shared FFmpeg
  `n8.1.2-34-g9b6c8969e0-20260806`, release `autobuild-2026-08-06-13-39`. The downloaded archive SHA-256
  matched the distributor manifest: `97e1af03208a4582c26d5f3e670ab51af50b8d5788da78231aae218a7c917d56`.
- Runtime inspection confirmed `drawtext`, native `mpeg4`, native AAC, and `h264_mf`; neither
  `--enable-gpl` nor `--enable-nonfree` is present.
- Executables, required shared DLLs, LGPL v3 text, source/archive record, exact build flags, and file hashes
  now live together under `thirdparty\ffmpeg`. Binary files are Git LFS objects.
- GUI and CLI build output automatically receives the payload. The portable publisher no longer supports
  omitting or replacing it and validates manifest hashes, version pairing, license flags, and capabilities.
- Dense prose tables were removed from the documentation. Stable TODO IDs, priorities, statuses, acceptance
  criteria, stack details, feature status, architecture boundaries, CLI options, and output presets now use
  headings and short lists readable in raw Markdown.
- Required Release builds passed with zero warnings/errors, and the dependency re-audit reported no known
  vulnerable NuGet packages.
- The approximately 373 MB self-contained package retained only the two application executables and INI
  at its root, reported application version 0.1.4, and matched every FFmpeg manifest hash.
- A published-CLI smoke used the packaged FFmpeg path to scan a synthetic clip, generate both static and
  contact-sheet previews, and render a two-second 1920x1080/30 native MPEG-4 plus AAC output. FFprobe confirmed
  the streams/duration, and completed-project usage history contained the render.

Result: exact binary redistribution selection is complete; `DEPLOY-003`, `AUD-RELEASE-FFMPEG-001`, and
`AUD-DOC-002` are closed; application/component version advanced to 0.1.4.

## AUDIT-2026-08-06-021 — Full-width content browser focus

Scope: requested left-side browser expansion control; `BROWSER-002` and `AUD-BROWSER-002`.

Findings and verification:

- The former browser header control only hid the panel body; it did not create more catalog space and removed the useful list instead.
- A left-edge direction arrow now applies a temporary browser-focus layout across all five workspace columns while leaving the full-width timeline visible beneath it as the drag/drop target.
- Preview and Layers/Used Clips are hidden only during focus. Toggling back reapplies the unchanged persisted dock assignments, so default and custom layouts restore without a settings mutation.
- The arrow direction, tooltip, and UI Automation name reflect the current action. Recycling virtualization and existing catalog-to-timeline drag data remain unchanged.
- Clean Release builds passed with zero warnings/errors. UI Automation invoked expand and restore in the built application; 1440x900 captures confirmed the full-width and restored layouts, timeline availability, and readable arrow.

Result: `BROWSER-002` and `AUD-BROWSER-002` closed; application/component version advanced to 0.1.3.

## AUDIT-2026-08-06-020 — Visual Studio designer workspace

Scope: reported empty/incomplete `MainWindow` XAML designer; `WORKSPACE-002` and `AUD-DESIGNER-001`.

Findings and verification:

- The panels were declared in XAML, but none had initial `Grid.Row`/`Grid.Column` values. Visual Studio therefore placed all four in row 0/column 0 and showed only the last overlapping panel.
- Content Browser, Preview, Layers/Used Clips, and Timeline now declare the same default positions and margins used by `ApplicationSettings` and `WorkspaceLayoutController`.
- Runtime docking remains dynamic and persisted; the controller continues to replace the initial XAML values when settings select a different layout.
- Release compilation, explicit coordinate audit, default startup, and a custom saved-layout startup passed without changing runtime docking behavior.

Result: `WORKSPACE-002` and `AUD-DESIGNER-001` closed; application/component version advanced to 0.1.2.

## AUDIT-2026-08-06-019 — Button and small-text readability

Scope: reported unreadable main-window text, especially button labels; `UI-002` and `AUD-UX-002`.

Findings and verification:

- The primary button style used a light fill while WPF string content did not reliably receive its intended dark foreground, producing light-on-light labels such as Export and Add clip.
- Primary actions now use a dark warm-neutral fill and an explicit light foreground on the template visual tree. Normal and disabled templates also own readable foreground/background pairs instead of reducing the whole control to 45% opacity.
- Muted/faint palette values were raised while remaining achromatic, header controls moved to 11 px, and the main workspace no longer uses 8-9 px labels.
- A clean Release build and two 1440x900 runtime captures verified the failure and the correction. The final window retained its compact geometry without clipping.

Result: `UI-002` and `AUD-UX-002` closed; application/component version advanced to 0.1.1.

## AUDIT-2026-08-06-018 — Recovered feature audit, versioning, and compact deployment

Scope: the full prior user request, `VERSION-001`, `DEPLOY-002`, `AUD-VERSION-001`, and `AUD-PORTABLE-002`.

Findings and verification:

- The prior four feature commits contain working implementations for the compact warm-monochrome UI, persisted four-slot docking, virtualized drag/drop browser, SQLite metadata plus file previews, versioned projects/recovery, export-only usage history, layered FFmpeg effects, and output profiles.
- Explicit software/component versioning had been missed; shared 0.1.0 assembly/file/informational metadata now drives the main title/status bars and CLI text/JSON output.
- The old publisher left hundreds of managed/runtime files in the root. Single-file publishing now produces only the GUI and CLI executables there, with the INI and organized `docs`/`thirdparty` folders.
- A complete tool pair is required unless the publisher is explicitly asked for an application-only package. Nonfree builds are rejected; GPL builds require a named opt-in; exact notices and build information remain part of the release gate.
- Release build completed with zero warnings/errors and the dependency audit reported no known vulnerable packages. CLI and portable-package smokes passed.

Result: the missed version requirement and tidy binary layout are closed. `AUD-RELEASE-FFMPEG-001` remains open until an exact LGPL-compatible redistribution binary and its notices are selected.

## AUDIT-2026-08-06-017 — Portable deployment

Scope: `DEPLOY-001`, runtime completeness, tool boundary, and redistributable-build guardrails.

Verification:

- Framework-dependent publish contained GUI/CLI, managed assemblies, native SQLite, INI, docs, notices, and `thirdparty` layout; published JSON help ran.
- Self-contained win-x64 publish contained 260 root runtime/application files totaling 154,208,032 bytes; published JSON help ran without `dotnet` invocation.
- Publisher copied `ffmpeg.exe`/`ffprobe.exe`, recorded build information, and automatic `thirdparty\ffmpeg` discovery completed a layered render.
- The available test FFmpeg was used only for smoke verification and is not approved as the commercial/public release binary.

Result: deployment mechanics passed and `AUD-PORTABLE-001` closed. Exact binary license/source-notice approval remains open as `AUD-RELEASE-FFMPEG-001`.

## AUDIT-2026-08-06-016 — Layered render and output settings

Scope: `LAYERS-001`, `FX-001`, `OVERLAY-001`, and `OUTPUT-001`.

Verification:

- Saved five-track project mapped through shared Core code into both WPF and CLI renderer requests.
- Real portrait source rendered with animated blurred background, 0.5/0.75-second clip fades, 70% source volume, timed text, timed PNG, custom progress, and looped/faded music.
- Initial smoke exposed a non-terminating looped still overlay; adding framesync `shortest=1` fixed it and the rerun completed in normal time.
- FFprobe reported exactly 6.000 seconds, 640×360, 24/1 fps, native `mpeg4` video, and AAC audio.
- A four-frame visual sheet showed the changing blurred background, dark fade endpoints, correctly timed text, and correctly timed PNG.
- Release build passed with zero warnings/errors.

Result: `LAYERS-001`, `FX-001`, `OVERLAY-001`, `OUTPUT-001`, and `AUD-FX-001` closed.

## AUDIT-2026-08-06-015 — Catalog metadata and preview cache

Scope: `CATMETA-001`, `PREVIEW-001`, and successful-export usage semantics.

Findings and verification:

- SQLite remains appropriate for stable IDs, multiple changing roots, user-editable tags, availability, and normalized project/export joins; static/contact-sheet JPEGs remain ordinary replaceable cache files.
- The schema migration is additive and preserves existing rows/tags during scanner upserts.
- A generated six-second 640×360 MP4 scanned successfully with audio metadata, one static thumbnail, and an 800×90 five-frame contact sheet.
- Tags normalized to `orange cat; indoor; favorite`, persisted, and are included in GUI filtering/headless output.
- Per-clip usage returned zero before rendering; after a successful export it returned exactly one row with project name, `.ccproject` path, output path, UTC date, and occurrence count.
- Release solution build passed with zero warnings/errors.

Result: `CATMETA-001`, `PREVIEW-001`, and `AUD-CATMETA-001` closed.

This is an append-only audit trail. Each audit records scope, findings, action IDs, and evidence. Closing an item requires a later closure entry; do not erase the original finding.

## AUDIT-2026-08-06-016 — Project persistence and recovery closure

Scope: `PROJECT-001` and `AUD-PROJECT-001`; foundation portion of `LAYERS-001`.

Findings:

- `.ccproject` is versioned JSON with required identity, output settings, five typed tracks, and stable item IDs; media is referenced rather than embedded.
- The item schema covers the requested timing, fit, fade, volume, overlay, and progress data without coupling JSON to WPF view models.
- `JsonProjectStore` owns validation and serialized atomic writes behind `IProjectStore`; timeline mapping remains in presentation.
- Recovery writes to the configured metadata folder on timeline mutations and preserves the normal project path.
- Normal project and final-output folders are separate preferences. A live metadata/database relocation requires restart and never silently moves the existing database.
- Usage counts/project-use history change only inside the successful export transaction. Additive nullable project columns preserve legacy rows.

Verification:

- Release build passed with zero warnings/errors.
- CLI created and reloaded a schema-1 project with the same GUID, five tracks, and 1920x1080 output; implicit overwrite returned exit code `2`.
- Project-enabled GUI startup passed after schema migration.

Result: `PROJECT-001` and `AUD-PROJECT-001` closed; `LAYERS-001` remains in progress for editing and render projection.

## AUDIT-2026-08-06-015 — Workspace visual and scale audit

Scope: `UI-001`, `WORKSPACE-001`, `BROWSER-001`, and `AUD-UX-001`.

Findings:

- The original white client area came from relying on an implicit `Window` style for derived window types. Every window and root surface now explicitly uses the dark window brush.
- The replacement palette contains warm neutral gray/beige accents and no blue accent brush; button templates use one-pixel corners and reduced padding.
- Browser, preview, layers/used-clips, and timeline are peers in one four-slot grid with user-resizable splitters.
- Dock commands swap the requested panel with the occupied slot, guaranteeing exactly one panel per slot; normalized values persist in `[Workspace]`.
- The catalog uses `VirtualizingStackPanel` recycling instead of `WrapPanel`, so source videos are never opened merely to populate the list and cached thumbnails bind only for realized rows.
- Drag/drop transfers one selected catalog view model and adds its durable media record to the timeline.

Verification:

- Release build passed with zero warnings/errors.
- A captured 1440x900 main-window image was visually inspected: all client surfaces are dark, density is reduced, corners are square, and all four panels are visible.
- Configuration/CLI projection includes all four workspace slots.

Result: `UI-001`, `WORKSPACE-001`, `BROWSER-001`, and `AUD-UX-001` closed.

## AUDIT-2026-08-06-014 — Architecture and documentation closure

Scope: `AUD-ARCH-001` and `AUD-DOC-001` after all planned responsibility refactors.

Architecture findings:

- Core contains models, utilities, contracts, and the shared export application service; it has no WPF, CLI, SQLite, or FFmpeg process dependency.
- Infrastructure has focused INI, SQLite, scan/probe/thumbnail, render construction/execution, and composition modules behind Core contracts.
- WPF owns desktop presentation and delegates timeline state, durable workflows, persistence, rendering, and repeated desktop interactions.
- CLI owns parsing, command validation/dispatch, text/JSON projection, and exit-code mapping while sharing application services with WPF.
- Larger classes were reviewed by responsibility. Their remaining size follows cohesive workflow/format construction rather than unrelated ownership or GUI/CLI duplication.

Documentation findings:

- `PROJECT.md` covers every requested product area and distinguishes Done, Partial, Not done, and Deferred behavior.
- `TODO.md` has stable IDs and acceptance evidence for configuration, CLI, licensing, bootstrapping, modularity, product work, and audits.
- README indexes project, architecture, stack/licenses, configuration, headless, TODO, worklog, audit, and third-party documents.
- INI location/schema, CLI commands/JSON/exit codes, dependency versions/licenses, GPL opt-in boundary, work history, and audit evidence match the code.
- Remaining open work is limited to timed multiple overlays (`OVERLAY-001`), fallback preview (`PREVIEW-001`), and named project persistence (`PROJECT-001`); trim/volume remains explicitly deferred (`EDIT-001`).

Verification: final Release build, dependency vulnerability audit, CLI JSON help, prior isolated headless/catalog/render smokes, hidden GUI startup, `git diff --check`, and Git status review passed.

Result: `AUD-ARCH-001` and `AUD-DOC-001` closed.

## AUDIT-2026-08-06-013 — WPF desktop interaction closure

Scope: `MOD-004`.

Findings:

- `DesktopShell` is the single File Explorer launch implementation and normalizes its selected file path.
- `DesktopDialogs` consistently presents startup and owned-window exception details.
- Window code-behind retains only window-specific event flow and contextual validation/information prompts.

Verification: Release build and hidden GUI startup smoke passed.

Result: `MOD-004` closed.

## AUDIT-2026-08-06-012 — SQLite responsibility closure

Scope: `MOD-003`.

Findings:

- `SqliteMediaCatalog` retains only `IMediaCatalog` operation and transaction coordination.
- Connection-string creation, schema initialization, invariant UTC conversion, media parameter/row mapping, and export-history aggregation have one focused internal class each.
- The database schema, SQL semantics, and public Core interface did not change.
- Export-history order is projected as one-based for both GUI and CLI; the CLI's redundant display offset was removed.

Verification: Release build and direct schema/upsert/query/availability/export/history mapping smoke tests passed.

Result: `MOD-003` closed.

## AUDIT-2026-08-06-011 — Headless automation closure

Scope: `CLI-001`, `BOOT-001`, and `AUD-CLI-001`.

Implementation findings:

- The console executable consumes `ApplicationServicesFactory` and the same INI, SQLite, scanner, renderer, and `CompositionExportService` contracts as WPF.
- Config, scan, list, render, and history are separate command modules; WPF is never loaded by the CLI.
- `--json` emits one stdout document and suppresses progress; non-JSON progress goes to stderr.
- Existing render outputs require `--overwrite`; ordered `--clip`/`--screen` options preserve segment order.
- Documented exit codes distinguish invalid arguments, configuration, partial scans, execution failure, and cancellation.

Verification:

- Release solution build passed with zero warnings/errors.
- Help, config, empty list/history, JSON parsing, invalid render arguments/configuration (`2`/`3`), partial-scan warnings (`4`), and FFmpeg launch failure (`5`) passed against an isolated data folder.
- Rendering to an existing destination without `--overwrite` was rejected with exit code `2` and left the file untouched.
- A real native-MPEG-4 source was scanned through FFprobe/FFmpeg; list returned its durable ID.
- CLI rendered an ordered still plus catalog clip with overlay/progress settings; FFprobe verified codec `mpeg4` and 1920x1080 dimensions.
- History recorded one export with the correct media ID and the catalog usage count advanced to one.
- The full four-project NuGet audit reported no known vulnerable packages from the configured sources.

Result: `CLI-001`, `BOOT-001`, and `AUD-CLI-001` closed.

## AUDIT-2026-08-06-010 — Shared export workflow review

Scope: GUI/CLI duplication risk before `CLI-001`.

Findings:

- `CompositionExportService` now owns the renderer call and successful export-history write.
- WPF presentation owns busy state and catalog refresh only; it no longer duplicates the durable export transaction.
- The composition root exposes the application workflow rather than its lower-level renderer dependency.

Result: shared workflow accepted; `BOOT-001` remains in progress until CLI consumption is verified.

## AUDIT-2026-08-06-009 — Default encoder license closure

Scope: `LIC-001` and `AUD-LIC-001`.

Implementation findings:

- `NativeMpeg4` is the application and INI default and emits `-c:v mpeg4`; FFmpeg documents this as its native MPEG-4 Part 2 encoder without requiring the GPL libxvid wrapper.
- `WindowsMediaFoundationH264` emits `-c:v h264_mf` with Media Foundation quality/archive options and uses the safer `nv12` pixel format documented by FFmpeg.
- `Libx264Gpl` is never implicit and is labeled as GPL in enum, UI, INI, README, stack inventory, and third-party notices.
- No preset uses `--enable-nonfree` components.

Verification:

- Installed FFmpeg encoder inventory exposed native `mpeg4` and `h264_mf`.
- A smoke input was generated with native `mpeg4` rather than libx264.
- Default render completed and FFprobe returned video codec `mpeg4`.
- Media Foundation render completed and FFprobe returned video codec `h264`.
- Release build passed with zero warnings/errors.

Limit: the locally installed FFmpeg distribution itself was configured with GPL components. Cat Clip Composer did not invoke them in the two verified presets. Every commercial release must separately inspect the exact external FFmpeg build with `ffmpeg -version`; this standing release check does not reopen the application-code finding.

Result: `LIC-001` and `AUD-LIC-001` closed.

## AUDIT-2026-08-06-008 — FFmpeg module responsibility closure

Scope: `MOD-002`.

Findings:

- Render request validation and atomic-output coordination remain in `FfmpegVideoRenderer`.
- Filter graph construction has no process-launch responsibility.
- Command construction uses `ProcessStartInfo.ArgumentList` and has no execution responsibility.
- Process execution exclusively owns start errors, cancellation, progress parsing, exit code, and standard error collection.
- Temporary cleanup is a narrow shared helper.

Verification: Release build passed; a real mixed video/still render with audio, text, PNG overlay, per-clip progress, and 1920×1080 output passed.

Result: `MOD-002` closed. Encoder licensing remains separately open under `LIC-001`.

## AUDIT-2026-08-06-007 — Timeline responsibility closure

Scope: `MOD-001`.

Findings:

- `MainViewModel` no longer owns timeline collection mutation, selection, ordering, target calculations, axis labels, or render projection.
- `TimelineViewModel` has one cohesive state-management responsibility and exposes a read-only collection.
- WPF bindings now address the timeline module directly.

Verification: Release build passed; a direct temporary harness passed add/insert/move/reindex/summary/target/projection/remove/clear checks; GUI startup smoke will run with the final verification set.

Result: `MOD-001` closed.

## AUDIT-2026-08-06-006 — Composition-root progress review

Scope: `BOOT-001` and GUI/CLI construction duplication risk.

Findings:

- Added one Infrastructure composition root returning Core interface types.
- WPF startup now consumes the factory and contains no concrete scanner, probe, thumbnail, catalog, or renderer construction.
- The factory accepts optional data/configuration paths for deterministic CLI and test use.

Result: design accepted; `BOOT-001` remains in progress until CLI consumption is verified.

## AUDIT-2026-08-06-005 — INI configuration closure

Scope: `CFG-001` and `AUD-CFG-001`.

Implementation findings:

- Configuration resolves to `CatClipComposer.ini` under `AppContext.BaseDirectory`.
- Parsing, application mapping/normalization, and atomic file replacement are separate responsibilities.
- The JSON settings class and all references to `settings.json` were removed.
- Saving to a protected executable directory produces an explicit configuration error instead of silently redirecting.

Verification:

- Release solution build: passed with zero warnings and errors.
- Temporary round-trip harness: passed for folders containing `=`, folder ordering, booleans, doubles, enums, all overlay fields, newline/backslash text escaping, missing files, malformed values, defaults, and clamping.
- Temporary test artifacts were removed after the run.

Result: `CFG-001` and `AUD-CFG-001` closed.

## AUDIT-2026-08-06-004 — Documentation and TODO baseline

Scope: requested product features, documentation requirements, and durable task tracking.

Findings:

- The original README documented current behavior but did not provide a stable requested/done/not-done matrix.
- No worklog, architecture record, durable TODO IDs, or append-only audit record existed.
- Added `PROJECT.md`, `ARCHITECTURE.md`, `STACK_AND_LICENSES.md`, `TODO.md`, `WORKLOG.md`, and this audit log.

Actions: `AUD-DOC-001` remains open until the P0 implementation changes are reflected and cross-checked.

## AUDIT-2026-08-06-003 — Architecture baseline

Scope: project boundaries and class responsibilities.

Findings:

- Core/Infrastructure/WPF project boundaries are directionally correct.
- `MainViewModel` combines catalog, scanning, settings, timeline, and rendering responsibilities (`MOD-001`).
- `FfmpegVideoRenderer` combines filter construction and process execution (`MOD-002`).
- `SqliteMediaCatalog` combines schema, mapping, commands, and history (`MOD-003`).
- GUI and planned CLI need shared service composition (`BOOT-001`).
- Repeated desktop helpers exist in WPF code-behind (`MOD-004`).

Action: complete P0 modular work, then perform `AUD-ARCH-001`.

## AUDIT-2026-08-06-002 — License baseline

Scope: runtime libraries and FFmpeg requirements.

Findings:

- .NET, Microsoft.Data.Sqlite, SQLite, and SQLitePCLRaw have acceptable current license status for the desired product direction.
- FFmpeg is correctly external rather than linked or bundled.
- The renderer currently hardcodes `libx264`, which requires a GPL-enabled FFmpeg build and violates the desired default-license policy.

Critical action: `LIC-001` / `AUD-LIC-001` must close before an MVP release.

## AUDIT-2026-08-06-001 — Dependency vulnerability baseline

Scope: all direct and transitive NuGet packages.

Evidence:

```text
dotnet list .\CatClipComposer\CatClipComposer.sln package --vulnerable --include-transitive
```

Initial finding: `SQLitePCLRaw.lib.e_sqlite3 2.1.6` was reported with high-severity advisory `GHSA-2m69-gcr7-jv3q` / `CVE-2025-6965`.

Remediation: explicitly pinned `SQLitePCLRaw.bundle_e_sqlite3 2.1.12`, rebuilt, reran the application startup smoke test, and reran the audit.

Result: zero known vulnerable packages from configured sources. `AUD-DEP-001` closed.

## AUDIT-2026-08-07-005 — Preview, timeline stack, and close-safety audit

Scope: latest browser, timeline, composition preview, persistence, theming, and shutdown requests.

Findings:

- The reported 0.1.5 window came from `publish\CatClipComposer`, while the preceding 0.1.6 package had been
  placed in a differently named sibling folder. The publisher already defaults to the documented path; this
  release explicitly replaces and verifies that exact folder. Forced updates now preserve any existing
  `CatClipComposer.ini` byte-for-byte while replacing generated application files.
- Preview renders must not count as accepted projects. The new path calls `IVideoRenderer` rather than
  `ICompositionExporter`, retaining identical project mapping while avoiding catalog history mutation.
- The editor displays tracks top-to-bottom. The renderer now chooses the lowest visible Video track as its
  base and applies visual tracks in reverse list order, giving the top row final compositing priority.
- Dirty state is independent from recovery autosave. Normal save clears it; mutations set it; closing uses a
  default-Yes three-way prompt and cannot continue after a failed or cancelled save.
- Schema 4 only adds optional validated `#RRGGBB` color strings and remains an additive project migration.

Verification: Release build, CLI/version and project-schema smokes, portable-publisher guards, exact output
layout/version inspection, and `git diff --check` are recorded with the release commit. Dependencies did not
change, so a new package vulnerability audit was not required.

## AUDIT-2026-08-07-006 — Mixed-aspect Project Preview failure

Scope: FFmpeg concat rejection reported from the v0.1.7 Project Preview workflow.

Finding: input normalization applied `setsar=1` before scale. FFmpeg's scale filter then recalculated sample
aspect ratio to preserve display aspect for sources with different stored geometry, producing otherwise equal
1920x1080 frames with incompatible SAR values at concat.

Remediation: apply a final `setsar=1` after every base-segment and timed-video scale pipeline. The earlier
normalization remains useful for plugin input, while the final reset establishes concat's actual invariant.

Verification: a copied catalog rendered the exact reported clips (catalog IDs 10, 11, and 5) without touching
the real history database. FFprobe reported MPEG-4 1920x1080, square-pixel SAR 1:1, AAC audio, and 70.804 seconds.
The corrected portable package is versioned 0.1.8 so it is visibly distinguishable from the reported build.

## AUDIT-2026-08-07-007 — Windows Project Preview playback and range interaction

Scope: jittered v0.1.8 Project Preview playback, timeline-to-source preview routing, and ruler range selection.

Findings:

- The affected 1920x1080 preview contained 2,267 monotonically ordered video frames at 30 fps, with a largest
  timestamp gap of one frame. Bundled FFmpeg decoded it fully without warnings, and distributed frame samples
  showed coherent sequential clip content.
- Its MPEG-4 Advanced Simple Profile stream used B-frames. This isolated the visible jitter to the Windows
  MediaElement decoder path rather than the renderer's frame order or concat timing.
- The same real three-clip composition rendered through `h264_mf` as H.264 Constrained Baseline with no
  B-frames, constant 30 fps, 70.800 seconds of video, 70.804 seconds overall, and a clean full decode.
- Ruler input previously tracked only a single playhead and ignored modifiers; timeline blocks exposed no
  source path to WPF interaction code.

Remediation: temporary Project Preview requests override only the encoder with Media Foundation H.264. Final
exports retain project settings. Timeline lane items now expose their source for Video-block double-click,
and timeline state owns a normalized visual range used by ruler input and bounded preview playback.

Verification: Release solution build passed with zero warnings/errors. The real-source H.264 render and full
decode passed. Dependencies did not change, so a package vulnerability audit was not required.

## AUDIT-2026-08-07-008 — v0.1.9 main-window construction failure

Scope: the reported `StaticResourceExtension` startup exception immediately after the v0.1.9 splash.

Finding: the new range label referenced `MainTextBrush`, while the application theme declares `TextBrush`.
WPF markup compilation did not reject the unresolved runtime resource lookup, so the Release build passed but
`MainWindow.InitializeComponent()` threw during application startup.

Remediation: use the declared theme resource and run a repository-wide simple `StaticResource` definition
check at the start of every portable publication.

Verification: all 34 keys referenced across 15 XAML files resolve, the Release solution builds with zero
warnings/errors, and a hidden startup smoke remained alive after the splash with a nonzero main-window handle.
The smoke loaded an existing dirty recovery project, so its hidden close request correctly waited on the
unsaved-project dialog; the isolated test process was then terminated. Dependencies did not change.

## AUDIT-2026-08-07-009 — Range-only render and editor interaction audit

Scope: requested splash pacing, preview transport/layout, selected-range rendering/editing, autoplay, and
Used Clips transform/effect access.

Findings and remediation:

- Playback-only range stopping still rendered the entire composition and could not map a trimmed file back to
  project time. `RenderRequest` now carries an optional bounded output range; the filter graph trims final video
  and mixed audio together and resets timestamps, while WPF owns the global timeline offset.
- Range state exposed geometry but not editable boundaries. Two themed thumbs now clamp independently to at
  least one frame, and Mark start/end derive a valid interval from the playhead.
- Used Clips selection did not always synchronize the primary timeline clip, and the Clip FX button could act
  on stale selection. Selection now synchronizes by item ID; transform/effect entry points operate on the
  selected row and plugin-effect timing inherits its interval.
- Startup reports were emitted in a burst before the five-second hold. A paced queue fills only sub-500–750 ms
  gaps on fast startup; known configured rescans bypass it entirely.

Verification: Release solution builds passed with zero warnings/errors. A real three-source range crossing a
concat boundary rendered exactly 10.000 seconds of H.264/AAC from project time 45–55 seconds, both streams
started at zero, used constant 30 fps with no B-frames, decoded cleanly, and sampled coherently. A two-second
full render also passed. Hidden UI Automation observed paced fast-startup log transitions and the updated XAML
resource audit passed. Dependencies did not change, so a vulnerability audit was not required.

## AUDIT-2026-08-07-010 — Preview layout and compact-control audit

Scope: joined/split preview behavior, Clip Preview discoverability, timeline sizing controls, Project Settings
placement, and Layers / Used Clips expander styling.

Findings and remediation:

- A single tab host made simultaneous clip/composition comparison impossible. Split mode now reparents the same
  pane instances into resizable left/right hosts, and Join restores them to their original tabs.
- Autoplay previously shared a wrapping transport row and could disappear at narrower widths. It now occupies a
  fixed location beside Add this clip. Video-block double-click has a post-selection handler so a refreshed block
  still activates Clip Preview and uses the current autoplay state.
- Narrow sliders made small zoom/height changes difficult. Discrete controls now expose the current pixels-per-
  second and pixel-height values between accessible decrement/increment buttons.
- Project Settings consumed scarce Used Clips height, and native Expander glyphs did not match the dark theme.
  Settings now lives in the Project Preview footer; both its rollout and track groups use one square-triangle
  expander template.

Verification: the Release solution built with zero warnings/errors; the XAML resource audit resolved 36 keys
across 15 files; and a non-visible WPF construction smoke exercised Split, Join, pane reparenting, autoplay
visibility, settings placement, and the themed expander resource. Dependencies did not change, so a package
vulnerability audit was not required.

## AUDIT-2026-08-07-011 — Context preview, browser modes, and blur-render audit

Scope: playhead/range context actions, browser presentation choices, selection synchronization, stale-preview
feedback, compatible effect discovery, and the reported Background blur Media Foundation failure.

Findings and remediation:

- The blur plugin's scale expression could round a 1920x1080 composition to 1920x1081. Media Foundation H.264
  rejected the odd final height with `MF_E_INVALIDMEDIATYPE`. The renderer now normalizes the fully composed
  stream to exact requested dimensions, SAR 1:1, and encoder pixel format after plugin and overlay stages.
- Preview rendering had only footer entry points and no explicit coverage state. Context menus now derive the
  playhead or selected interval, and media blocks outside the successful render interval receive a subdued
  yellow edge and explanatory tooltip.
- The browser had one presentation even though its virtualization could support several. The same recycling
  panel now switches among list, small-grid, and large-grid modes without loading source videos; bounded sizes
  round-trip through INI and headless config.
- Timeline selection and effect discovery were uneven across editor surfaces. Stable item IDs now synchronize
  timeline and Used Clips selection; lane, track, and item menus resolve track compatibility before listing
  modules, and item actions inherit the source interval.

Verification: Release builds passed after each code batch. The exact recovered 1920x1080 blur project rendered
through Media Foundation H.264, reported H.264/AAC, yuv420p, SAR 1:1, and decoded cleanly. A 45-55 second
selected range rendered exactly 10.000 seconds. A non-visible WPF smoke exercised all three browser modes,
selection synchronization, preview-coverage chrome, and default Preferences height. Dependencies did not
change, so a package vulnerability audit was not required.
