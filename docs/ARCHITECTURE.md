# Architecture

Last reviewed: 2026-08-16

## Repository modules

```text
CatClipComposer.Core
    Domain models, formatting utilities, and service contracts

CatClipComposer.Infrastructure
    INI/project stores, SQLite, filesystem paths, FFprobe/FFmpeg, GitHub update adapter, and service composition

CatClipComposer
    WPF application, compact dock workspace, presentation models, and desktop-specific interaction

CatClipComposer.Cli
    Headless command dispatch, text/JSON output, and process exit codes

CatClipComposer.Plugins.BuiltIn
    First-party dynamically loaded source and video-effect modules
```

All modules remain in this repository. Git submodules are prohibited.

## Dependency direction

```text
WPF -----+
         +--> Core <-- Infrastructure
CLI -----+          (implements Core contracts)

Built-in plugins --> Core plugin contracts
```

The executable modules may reference Core and Infrastructure for composition. Core must not reference WPF, SQLite, FFmpeg process details, or CLI concerns.

## Runtime data flow

### Catalog scan

1. An executable loads `CatClipComposer.ini` through `ISettingsStore`.
2. `IMediaScanner` enumerates configured folders and accepted extensions.
3. `IMediaProbe` invokes FFprobe and parses duration, dimensions, and audio stream presence.
4. Focused thumbnail/contact-sheet generators invoke FFmpeg and write keyed JPEG cache files.
5. `IMediaCatalog` upserts paths and technical/search metadata into SQLite while preserving user tags and the
   explicit seen/unseen state. Existing catalogs migrate as seen; newly discovered rows start unseen.
6. Recovery/save synchronization replaces each project GUID's set of catalog media references. Those references
   drive green current-project and yellow other-project browser badges but remain separate from successful-export
   usage history. Only completed renders increment `use_count` and appear in the detailed Usage view.

### Composition and render

1. The GUI synchronizes timeline items into the versioned project; the CLI can load that same project or create ad-hoc ordered segments.
2. `ProjectRenderMapper` projects enabled Background, Video, Overlay, Audio, Progress, and Effects track
   items into one renderer plan without WPF/CLI duplication. Track order is top-to-bottom in the editor;
   the bottom Video track supplies the base and higher visual tracks are composited over it in reverse order.
3. Project output dimensions/FPS/encoder/quality/bitrates are copied into export requests, with narrow
   command-line overrides. Final export retains all project settings. Temporary WPF previews select the
   Media Foundation H.264 compatibility encoder for stable Windows playback and may apply a 10–100% even
   canvas scale without changing the saved output profile.
4. `ICompositionExporter` owns the shared GUI/CLI export transaction.
5. `IVideoRenderer` validates inputs and produces a normalized layered filter graph.
6. FFmpeg renders to a unique partial path.
7. A successful render atomically replaces the selected output.
8. `ICompositionExporter` records the render job and ordered media IDs through `IMediaCatalog`.
9. Project Preview sends the same layered plan directly to `IVideoRenderer` in metadata storage, deliberately
   bypassing `ICompositionExporter` so previewing never changes completed-project usage history.
10. An optional preview range trims the final composited video and final mixed audio together, then resets both
    timestamps to zero. The WPF transport retains the project-time offset so seeking and frame stepping still
    move the global timeline playhead correctly.
11. After every plugin and overlay stage, the filter graph normalizes the final canvas to the requested width,
    height, square-pixel aspect, and encoder pixel format. This prevents a blur or zoom stage from leaving an
    odd-sized frame that Media Foundation H.264 rejects as an invalid media type.
12. Still-image overlay inputs are trimmed and timestamped to their declared project interval before
    composition. Once that interval ends, the overlay passes the underlying stream through instead of asking
    FFmpeg to repeat an unbounded image source, which keeps Background effects and image overlays composable.
13. `ProjectRenderMapper` retains visual track order on filter effects and overlays. The filter graph interleaves
    those operations from the bottom track upward instead of flattening every filter ahead of every overlay.
14. Text/image/moving overlay transforms use normalized center coordinates plus uniform scale and rotation. FFmpeg
    applies those values at final-output resolution; schema-5 and older items retain their preset placement
    until a user edits or directly manipulates the overlay.
15. Text/image/GIF/video fade values become alpha fades on each transparent overlay stream at its absolute project
    interval. They do not fade the composed video or reuse audio/source fade semantics. Text stroke uses a crisp
    drawtext border and, when smoothness is nonzero, a separate alpha-blurred stroke-only layer beneath it.
    Renderer text files normalize Unicode and omit unresolved combining marks so one unsupported glyph cannot
    suppress an otherwise valid multiline overlay; trailing line breaks are removed.
16. Frame, active Range, and All each expose LQ/HQ actions over the same render path. Range falls back
    to a short slice at the playhead when no range exists; a frame result is loaded and paused instead of playing.
17. A successful prerender atomically records a content/source/app fingerprint and global-time coverage beside
    its uniquely named MP4. Startup/Open restore all current-fingerprint chunks. During an editing session the
    catalog retains older chunks, splits overlapping coverage to a yellow stale state, and keeps non-overlapping
    green coverage seekable; a replacement render overwrites only its interval. Presentation remembers coverage
    by semantic fingerprint so an exact undo/manual revert can restore green without accepting changed content.
    The WPF shell serializes requested renders and reports the queued count while the view model owns parsing,
    engine-start, frame-progress, and scope status. Range selection never evicts
    cached media. The disk cache is bounded to the newest 80 project chunks.
18. LQ previews use the same mapped timeline and ordered filter graph on the scaled canvas. Source fitting,
    overlay geometry, text, margins, progress height, and blur radius scale together. Selected image overlays
    may use Lanczos instead of the default bilinear preview scaler; whole-frame effects still execute at the
    temporary canvas size. Cache metadata records resolution, preservation mode, and the object that received
    the optional higher-quality scaler. Preference/selection-only changes do not invalidate still-useful video;
    the UI identifies its recorded percentage and applies new choices on the next prerender. Image-overlay
    preview scaling applies the resolution factor after resolving the source/cap width, so sub-480-pixel PNGs
    retain the same relative geometry as HQ output.
19. Project-preview transport uses the active chunk's declared project-time interval rather than trusting a
    platform-reported media duration. The timer and MediaEnded path both pause, reset to the chunk start, and
    update the play/pause state, preventing playback from escaping a selected range.
20. MediaElement source switches clear the preceding URI and set only the latest requested chunk on a later
    dispatcher turn. A failure for the still-current source is reopened once without a modal interruption;
    only a repeated current-source failure is reported as unavailable.
21. Moving overlay inputs use FFmpeg input looping, trim to the timeline block, preserve animation frames, and
    pass through the same alpha, fade, scale, rotation, position, track-order, and preview-quality stages as
    still overlays. Their audio stream is intentionally not mixed because they are visual Overlay-track items.

### Timeline and parameter editing

1. `MainViewModel` calculates exact landing ranges for both drag preview and commit, preserving the pointer's
   offset within the selected block group so a drop cannot jump by a block width.
2. Timeline lanes expose transient landing-preview geometry. WPF owns pointer capture and edge thumbs; the
   view model owns snapped movement and non-primary timed-item resizing. Resize preview derives time from the
   pointer's absolute displacement since capture, avoiding nonlinear accumulation of relative Thumb deltas.
3. Frame/grid snapping always applies. A workspace checkbox additionally aligns either moving edge to primary
   source-clip starts and ends; those clip boundaries receive a bounded visual priority zone before grid snap.
4. Shared WPF range and numeric controls keep validation consistent across layers, clip effects, and plugins.
   One compact range canvas moves or resizes Start/End together; effect-value sliders and arrow buttons use
   sensible UI bounds and snap steps, while finite manual text entry may exceed those convenience bounds when
   the renderer's hard safety limits permit it.
   Time arrow buttons retain the configured normal snap; Ctrl uses 0.5 seconds and Shift uses 1 second.
5. Effect frame preview clones the in-memory project, replaces only the working native overlay/progress item or
   plugin effect, renders a 0.1-second H.264 slice at the selected playhead, and never saves the candidate or
   records export history. The editor
   owns cancellation/debounce and a snapped non-modal preview window. The window paints immediately, reports
   preparation phases over the first quarter, and maps process progress monotonically over the remainder.
6. `ProjectPreviewOverlayCanvas` maps the project frame into the MediaElement's actual letterboxed viewport,
   displays active text/image/moving content with selection handles, and owns pointer capture for move, uniform-scale,
   and rotation gestures. `MainViewModel` owns a transactional draft: live movement updates the proxy without
   creating one history entry per mouse move; OK/Enter captures one project change, while Cancel/Escape restores
   every original placement field and releases any pending pointer interaction before redrawing. Item-ID
   selection remains shared with the timeline and Project Layers Data panel.
   A schema-8 item lock disables gesture initiation while preserving shared selection and form-based editing;
   schema 9 opacity flows through the same shared mapper to FFmpeg alpha/color controls. Once a render exists,
   unchanged items contribute only hit-testing/selection chrome over the MediaElement. A stale item paints one
   exact-alpha live proxy; if its transform changed, a snapshot of the rendered transform supplies the crossed
   stale-location marker until the next successful prerender.
7. Timeline lane projection preserves chronological layout while assigning insertion order as explicit WPF
   z-order, so the newest overlapping block receives pointer input. Complete non-source effects can be cloned
   through one JSON-shaped project-item copy and pasted at the playhead only onto a compatible track.
8. Text presets persist domain parameters in the portable INI. The WPF editor alone regenerates recognizable
   text/font thumbnails, keeping bitmap rendering out of Core and configuration persistence.

### Plugin discovery and effect rendering

1. Startup scans `plugins` beside the executable for assemblies implementing the versioned Core plugin
   contract.
2. Each plugin assembly receives its own non-collectible `AssemblyLoadContext` and dependency resolver;
   the shared Core contract assembly stays in the default context.
3. The catalog validates API version, stable ID, metadata, compatible tracks, and unique parameter keys.
   Plugin API 2 adds preview scale plus a selected-object quality hint to the video filter context so current
   and future effects can preserve their visual units when rendering on a smaller temporary canvas.
4. Project items persist a plugin ID and string parameter dictionary rather than a concrete module type.
5. `ProjectRenderMapper` resolves and validates required source/effect modules. Background effects receive
   the current source before fit/pad; filter and overlay effects receive the composited stream at their
   declared stage.
6. Plugins execute in-process and are trusted application extensions, not a security sandbox. Only modules
   from trusted sources should be placed in the portable `plugins` folder.

### Project save and recovery

1. The presentation timeline raises a change event only for mutations, not selection.
2. `MainViewModel` synchronizes ordered video/still items into the project's Video track.
3. `IProjectStore` writes the complete versioned document atomically to recovery.
4. Normal Save writes the selected `.nya` and refreshes `autosave.nya` recovery.
5. Startup reports named, percentage-based service/configuration/layout/plugin/catalog stages; conditionally
   rescans the catalog with per-file scan progress, then reports project-file/recovery loading so media IDs and
   paths are resolved before timeline/preview synchronization.
6. A successful render carries project identity into history; editing and autosave alone never update catalog usage.
7. `ProjectUndoHistory` holds a configurable 1–256 serialized in-memory snapshots (32 by default). Every logical
   project mutation captures after synchronization with a user-visible description, undo/redo reapplies through
   the normal project projection, and the exact saved
   snapshot determines the dirty marker even when navigating backward and forward.
8. Closing first resolves unsaved project state through Save / Don't save / Cancel, then atomically persists
   window geometry, workspace splitter dimensions, preview layout/tab, focus, and expansion to the INI. After
   those operations succeed, clean shutdown removes recovery so it cannot masquerade as a new edit next launch.
9. Startup reconciles recovery with its named saved project by semantic JSON comparison that excludes schema,
   normal-file path, and modification timestamp. Equivalent legacy recovery and pristine untitled recovery are
   clean; any content difference retains the recovery and dirty marker.

### Undo navigation and optional update checks

1. `ProjectUndoHistory` owns snapshot order and exposes descriptions for every reachable undo/redo destination.
   A normal arrow restores one snapshot; choosing dropdown entry N moves N snapshots in memory, projects only the
   final state through `MainViewModel`, writes recovery once, and reloads matching prerender chunks once.
2. `IApplicationUpdateChecker` keeps GitHub/network details out of WPF. Its Infrastructure adapter performs a
   manual, anonymous, sequential check of the public latest Release, repository version file, and—when the build
   embeds a reachable commit—the current-to-main comparison.
3. Responses have byte limits and a 15-second timeout, checks are serialized/cached for five minutes, cancellation
   is propagated, remote XML prohibits DTDs, and partial endpoint failures remain visible without hiding successful
   results. No updater downloads, executes, or replaces application files.
4. Browser launch accepts only HTTPS `github.com/propiro/CatClipComposer` paths. A binary update accepts the
   preferred `CatClipComposer-v&lt;version&gt;-win-x64-light.zip` Release asset or the earlier unsuffixed full-package
   name; repository code is reported separately.

## Responsibility audit

Each component is listed separately to keep its responsibility and boundary readable in narrow editors.

- **`MainViewModel`:** WPF catalog/settings/scan/export presentation. Timeline state and the shared
  render/history transaction are delegated.
- **`CompositionExportService`:** Render and record successful output history. It is shared by GUI and CLI
  and owns neither presentation nor FFmpeg construction.
- **`JsonProjectStore`:** Validate and atomically save/load normal and recovery project documents. It owns
  no timeline, UI, catalog, or render behavior.
- **`TimelineViewModel`:** Ordered segments, selection, editing, duration, target, ruler/snap state, exact-frame
  playhead, and axis summaries. Lane projection is kept in focused timeline presentation models; `MOD-001` is closed.
- **`ProjectRenderMapper`:** Convert enabled persisted tracks/items into renderer values. It is pure Core
  code shared by GUI and CLI.
- **Core plugin contracts:** Define versioned descriptors, media categories, render stages, track
  compatibility, parameters, and video/audio/source interfaces without depending on WPF or Infrastructure.
- **`PluginCatalog` / `PluginLoadContext`:** Discover and validate module assemblies and isolate their
  private dependencies while sharing the Core API identity.
- **Built-in plugin project:** Provide one class per background, video-filter, or PNG-source module. It has
  no WPF, persistence, catalog, or process-launch responsibility.
- **`FfmpegVideoRenderer`:** Validate and coordinate temporary render output.
- **`FfmpegFilterGraphBuilder`:** Build normalization, concat, overlay, and progress filters.
- **`FfmpegRenderCommandBuilder`:** Build argument-safe FFmpeg process configuration.
- **`FfmpegCommandService`:** Project the same final render request and command builder into a displayable
  Windows command line, persist any text-overlay support files, and directly execute edited arguments only when
  the parsed executable still resolves to the configured FFmpeg binary. It never invokes a command shell; WPF
  owns output selection, confirmation, clipboard access, and the command window.
- **`FfmpegProcessRunner`:** Execute FFmpeg, cancel, collect errors, and report progress. `MOD-002` is closed.
- **`SqliteMediaCatalog`:** Media/tag/seen CRUD, project-reference replacement, and successful-export/history
  SQL behind `IMediaCatalog`.
  Schema, connection, time conversion, mapping, and history projection are delegated; `MOD-003` is closed.
- **Preview generators:** Produce a static thumbnail and evenly sampled contact sheet. A shared process
  runner removes duplicated process/cancellation behavior; images remain replaceable cache files.
- **SQLite persistence helpers:** Own one schema, connection, conversion, or row-projection task each.
- **WPF window code-behind:** Own window events, media transport, validation prompts, and dialog flow.
  Explorer launch and exception presentation are delegated; `MOD-004` is closed.
- **WPF editor controls:** Own reusable Start/End-or-duration entry, whole-timeline shortcuts, and bounded
  slider/arrow interaction without depending on persistence or FFmpeg.
- **WPF desktop helpers:** Own shell launch and consistent exception presentation only.
- **`WorkspaceLayoutController`:** Map panels to dock slots and apply temporary panel focus. Focus never
  overwrites durable settings; the window captures the runtime geometry and current focus/expansion on close.
- **Content browser:** Search cached metadata and recycle one virtualized surface across list, small-grid, and
  large-grid presentation. It does not eagerly decode video; full-width focus retains the timeline drop target.
- **`CliApplication`:** Parse invocation, initialize shared services, dispatch, and map failures to exit codes.
- **CLI command modules:** Implement config, scan, list, metadata, project render, and history behavior while
  sharing Core/Infrastructure workflows.
- **Portable publisher:** Compose either the default shared framework-dependent GUI/CLI file set or explicit
  self-contained single-file entry points, plus the extensionless version marker, INI, docs, mandatory pinned
  FFmpeg payload, plugin modules, and custom-font folder. It validates .NET runtime contracts, shared-file
  identity, marker identity, hashes, license flags, versions, and required render capabilities before publishing.
- **Application startup:** Provide the shared `ApplicationServicesFactory` composition root and coordinate the
  staged splash pipeline, saved software layout, conditional live scan, project/recovery state, and main-window
  handoff. The WPF shell alone owns its 20–40 ms ordinary-line pacing and 100–200 ms opening/completion holds;
  real scan updates bypass artificial per-line pacing. A portable INI flag is committed only after successful
  editor initialization, selecting a five-second first-launch minimum and three-second returning minimum.
  Presentation-only percentages/stage labels do not leak into Infrastructure. `BOOT-001` is closed.
- **INI configuration:** Split generic reading, application mapping, and atomic storage. `CFG-001` and
  `AUD-CFG-001` are closed.

## Architectural rules

- A class should have one primary reason to change.
- Process execution, FFmpeg argument construction, filter graph construction, and UI orchestration are separate responsibilities.
- Persistence SQL stays in Infrastructure; domain state stays in Core.
- Shared GUI/CLI behavior must live behind Core interfaces or in focused application services, never copied between executables.
- Superseded implementations are deleted in the same change that replaces them.

## Plugin design references

The contract uses the same broad boundaries seen in established systems while staying intentionally smaller:

- Microsoft's .NET plugin guidance uses a shared contract assembly, `AssemblyLoadContext`, and
  `AssemblyDependencyResolver` for plugin dependency loading:
  <https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support>
- OBS separates loadable modules from registered source/filter kinds and treats filters as processors of
  source audio/video: <https://docs.obsproject.com/plugins> and <https://docs.obsproject.com/reference-modules>
- OpenFX declares explicit effect contexts rather than one untyped callback:
  <https://openfx.readthedocs.io/en/main/Reference/ofxImageEffectContexts.html>
- MLT models processing as producers, filters, transitions, and consumers chained through a framework:
  <https://www.mltframework.org/docs/framework/>

Cat Clip Composer applies those ideas as a versioned descriptor plus separate video, audio, source, and
overlay interfaces. It does not claim binary compatibility with OBS, OpenFX, or MLT.

## Final responsibility audit conclusion

The 2026-08-06 post-MVP audit found no remaining P0/P1 responsibility violation. The larger presentation,
scanning, CLI dispatch, filter-graph, INI mapping, and catalog classes each retain one cohesive workflow and
delegate process execution, persistence projection, timeline state, executable composition, and desktop
integration to focused collaborators. The exact bundled-FFmpeg audit is closed; deferred trimming remains
the only known scoped feature gap.
