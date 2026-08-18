# Worklog

This is an append-only record of material project work. Newest entries go first. Corrections should be added as new notes rather than rewriting historical results.

## 2026-08-19 — v0.1.36 text fidelity and application-native export workflow

- Replaced the text gizmo's stretched WPF `TextBlock` measurement with the selected typeface's supported glyph
  set and painted outline bounds. Unsupported fallback-only characters no longer inflate or appear in the editor
  when FFmpeg cannot paint them. The timeline-selected overlay also receives an explicit topmost preview z-index.
- Replaced Export's Windows save picker with a Cat Clip Composer directory/file chooser. It remembers the last
  selected export folder, falls back to `CCC_output` one level above the first library source, navigates drives and
  folders, creates folders, previews the exact MP4 path, and requires inline approval before replacing a file.
- Added a separate modal export-progress window with destination/output summary, determinate percentage, processed
  media time, elapsed time, detailed preparation/FFmpeg/replacement/history stages, a timestamped activity log,
  cancellation, failure details, and output reveal. The shared renderer now reports those stages without adding a
  WPF dependency to Core or Infrastructure.
- Removed the redundant destination picker from **FFmpeg command...**. It now opens the existing editable command
  window directly with a unique proposed path, while normal Export remains the safe/history-recorded path.
- Advanced all source/component metadata and the visible marker to v0.1.36. No public tag or binary Release was
  created pending manual acceptance. No dependency or redistributed payload changed.
- Verification: repeated zero-warning/error Release builds, a 39-key/21-file XAML resource audit, explicit
  `LastExportFolder` INI load plus CLI exposure, static confirmation that Export/command paths no longer construct
  a save picker, v0.1.36 CLI/marker checks, and installed-runtime GUI startup to the versioned main window.

## 2026-08-18 — v0.1.35 clean-root Light and Full packaging

- Replaced the v0.1.33-era framework-dependent multi-file merge with single-file publishing for both GUI and
  CLI in the default Light package. Application assemblies and native SQLite are now bundled into each small
  apphost while .NET 8 remains external; explicit Full mode also embeds .NET.
- Marked FFmpeg, fonts, and the visible version marker as external single-file assets. Plugins continue to be
  copied after publishing. This prevents replaceable LGPL FFmpeg files from being duplicated inside either EXE
  and leaves exactly four root files: GUI, CLI, portable INI, and `version_0.1.35`.
- Added publisher and GitHub workflow gates that reject any root DLL, dependency/runtime JSON, native SQLite file,
  or unexpected directory. Advanced all source/component metadata to v0.1.35; no public tag or binary Release
  was created pending manual acceptance.
- Verification: zero-warning/error Release build and 39-key/19-file XAML audit; Light and Full clean-root
  publishes; installed-runtime GUI startup; CLI/version/plugin/SQLite smokes; isolated missing-.NET host output
  with Microsoft's download link; and a real one-second 1920x1080 MPEG-4/AAC render through external FFmpeg.
  Light measured 145.52 MiB unpacked with 5.66/2.85 MB entry points; Full measured 244.42 MiB, down from the prior
  358.29 MiB full layout because FFmpeg is no longer redundantly embedded. No dependency changed.

## 2026-08-16 — v0.1.34 final FFmpeg command inspector

- Added **FFmpeg command...** under Project Settings. It asks for the intended MP4 destination, builds the exact
  current final project graph through the shared render mapper and command builder, and opens an editable command
  field with **COPY TO CLIPBOARD**, **EXECUTE FFMPEG**, and Close actions.
- Direct execution parses the edited Windows command line without PowerShell or `cmd.exe`, requires the executable
  to remain the configured FFmpeg binary, passes arguments through `ProcessStartInfo.ArgumentList`, captures
  bounded output, supports cancellation, and warns about overwrite access and its export-history/safe-replacement
  bypass. Generated text-overlay files remain in the metadata command-assets folder so copied commands stay valid.
- Advanced the source/component marker to v0.1.34. Verification covered clean Release builds, WPF command-window
  construction, static XAML resources, and a real one-second FFmpeg generation/edit/execute smoke that rendered
  text and rejected a substituted non-FFmpeg executable. No dependency or redistributed payload changed.

## 2026-08-16 — Installation choices and factual video FAQ

- Reworked the public installation guidance into direct question-and-answer choices: use Light with the .NET 8
  Desktop Runtime x64, or use an available Full/self-contained package when a separate runtime installation is
  undesirable. Clarified that the plain .NET Runtime is insufficient for WPF, the SDK is unnecessary for users,
  v0.1.32 is the current unsuffixed Full asset, and future releases default to Light.
- Added a question-and-answer FAQ confirming that non-cat footage is welcome and that Mr. Cat is indeed that
  beautiful. Additional answers accurately cover accepted containers versus codecs, source-file safety, deferred
  trimming, output shapes, layered prerendering, FFmpeg, H.264/default encoders, local operation, supported Windows
  platforms, and the project's experimental pre-1.0 status.
- This was a documentation-only change: application behavior, component version, application/dependency binaries,
  and the existing v0.1.32 binary Release did not change. The usual local light package was republished so its
  bundled documentation matches the repository.

## 2026-08-16 — v0.1.33 light-by-default Windows packaging

- Made framework-dependent Windows x64 publication the default while retaining the prior compressed
  self-contained layout behind explicit `-SelfContained $true`. The usual `publish\CatClipComposer` folder now
  contains the v0.1.33 light build; the public v0.1.32 full Release and immutable tag were not changed.
- The light publisher validates GUI/CLI apphosts, dependency/runtime configuration files, .NET 8 Desktop/Core
  framework contracts, version markers, plugin output, FFmpeg payload, and merged-file hashes. The Windows-specific
  SQLite provider is shared only when its strong assembly identity matches the CLI-resolved provider.
- Changed future tagged assets to `CatClipComposer-v<version>-win-x64-light.zip`, added explicit .NET 8 Desktop
  Runtime installation/release guidance, and kept the About update checker compatible with both new light names
  and earlier unsuffixed full archives.
- Verification: repeated zero-warning/error Release builds; 39-key/18-file XAML resource gate; light CLI version
  and fresh SQLite-catalog smokes; hidden light WPF startup; forced missing-runtime apphost output with Microsoft's
  x64 download URL; explicit full-package publication; and package/version/layout inspection. The measured light
  package is 143.02 MiB unpacked and 61.29 MiB zipped versus 358.29 MiB and 267.41 MiB for the full mode. No NuGet,
  FFmpeg, plugin, font, or other redistributed dependency changed.

## 2026-08-15 — v0.1.32 public source and Windows release

- Pushed the two pending source commits to public `main` after refreshing GitHub authentication, then created the
  immutable annotated `v0.1.32` tag at `f0b48fe347d873267a8c431ab75b5ce286b3ca5b`.
- The tag-only Windows workflow completed successfully: LFS checkout, .NET setup, tag/version validation, portable
  build/package validation, and GitHub Release publication all passed.
- Published `CatClipComposer-v0.1.32-win-x64.zip` (282,417,319 bytes) and its adjacent SHA-256 file. The public
  checksum is `15ec7a8b73e4bb715a1e214dd04675bc390ae57d5bd81942544a0236ef3ce1eb`.

## 2026-08-14 — v0.1.32 text geometry, overlap selection, About image, and tag shortcuts

- Diagnosed the supplied multiline Spaceport text exactly: its Windows CRLF pairs were interpreted as two line
  advances by FFmpeg but one by WPF. Shared Core normalization now converts every line ending to LF, preserves
  valid composed text, removes only unattached combining marks, trims trailing blank lines, and feeds both the
  drawtext file and project-view proxy.
- Made the active timeline-selected overlay the final WPF preview visual, giving that explicit selection priority
  for click/drag hit testing when objects overlap without changing FFmpeg track compositing order.
- Changed the resizable About artwork from crop/fill to aspect-preserving fit so the complete Mr. Cat photograph
  remains visible. Added deterministic ten-most-used library tag buttons to both bulk and metadata tag windows;
  buttons append without erasing in-progress text or duplicating a tag with different casing.
- Replaced the personal author email on all six unpushed commits with the account-ID GitHub `noreply` identity and
  set that identity locally for future commits. Already-public history was left intact rather than force-rewritten.
- Verification: clean Release builds after each product-code batch; the screenshot text/newline, selected-overlay
  ordering, tag-frequency, typed-tag preservation, and duplicate suppression smoke passes; all 18 XAML files resolve
  39 static resources; analyzers, vulnerability, Gitleaks history/diff, version/marker, and in-place portable publish
  gates pass. No dependency or redistributed third-party binary changed.

## 2026-08-14 — v0.1.31 history navigation, About/update visibility, and code audit

- Split both toolbar history controls into a one-step arrow and adjacent dropdown. Dynamic action lists expose
  every reachable undo/redo destination; a selected entry performs one atomic N-step snapshot transition, one
  recovery save, and one fingerprint-matching prerender-cache reload.
- Added a themed About window using the existing Mr. Cat splash resource, literal entirely-vibecoded disclosure,
  application/build revision details, and links to the public project.
- Added a Core update-check contract and Infrastructure GitHub adapter. Manual checks separately compare the exact
  packaged Windows ZIP, repository version, and reachable current-to-main revision; requests are sequential,
  bounded, timed out, cancellation-aware, five-minute cached, anonymous, and non-installing.
- Audited dependencies, tracked/history/working-diff secrets, analyzers, process/path/network/SQL boundaries, XAML,
  and generated-package rules. Hardened project URL allow-listing, remote XML parsing and partial-response handling,
  update cancellation/rate protection, invalid custom-font/browser launch errors, and SQLite migration visibility.
- Added a tag-only Windows release workflow using immutable-SHA-pinned official checkout/.NET actions, hydrated LFS,
  the repository's existing publisher and package gates, minimum Release permission, and GitHub CLI publication of
  the complete ZIP/checksum. The tag remains intentionally unpushed until the manual checklist succeeds.
- Verification: clean Release builds after every code batch; reflection smoke proves C/B/A history ordering plus
  atomic Undo 2/Redo 2; live GitHub smoke detects repository v0.1.26 and packaged v0.1.18 from a known-old build;
  vulnerability, analyzer, Gitleaks history/diff, XAML resource, whitespace, CLI version, and in-place publish checks.

## 2026-08-14 — v0.1.30 prerender, cross-track editing, and text reliability pass

- Relabeled the Project Preview toolbar as a bordered PRERENDER group with FRAME/RANGE/ALL LQ/HQ actions.
  Serialized concurrent prerender requests, exposed the queue count in the bottom status bar, and reported exact
  frame/range/all scope plus parsing, FFmpeg startup, and live frame-progress stages.
- Indexed rendered coverage by semantic project fingerprint. A moved/edited overlap becomes yellow immediately;
  an exact undo or manual revert restores only matching green intervals and reloads matching cached chunks.
- Removed the model-layer prohibition on compatible cross-track non-source moves. Timeline ghosts follow valid
  target lanes, WPF Escape cancels drag/drop, one undo restores membership/timing, and Ctrl+C/Ctrl+V plus empty-
  lane paste actions make the effect clipboard discoverable across Timeline and Project Layers.
- Made the undo stack configurable from 1–256 entries (32 default), added named reverse-chronological action
  records, and turned History into a modeless toggle with Actions, Exports, and Logs/Crashes. Clip inspection now
  includes technical, catalog-date, project-reference, disk-location, tag, and completed-export data.
- Advanced projects to schema 11 with text stroke enable/color/width/smoothness and matching portable presets.
  Effect-editor frame renders bypass only the working text candidate's fades. Diagnosed the reported hidden top
  text against the supplied project: trailing blank lines/free-standing combining marks made FFmpeg silently drop
  the block. Normalized render text now retains valid lines, and a real project slice renders the top track again.
- Verification: clean Release build, static XAML resource audit, `git diff --check`, CLI config/version smokes,
  exact-project real FFmpeg text/fade/stroke renders, and in-place portable publish validation. No dependency
  changed, so no new vulnerability audit was required.

## 2026-08-14 — v0.1.29 effect clipboard and catalog-state pass

- Preserved lane chronology while assigning insertion-order z-index, so a later-added block wins selection in
  an overlap. Added complete effect/overlay/audio/progress copy and compatible-track paste at the playhead from
  timeline/Project Layers menus and Ctrl+C/Ctrl+V.
- Added portable text-overlay presets for text, font, transform, opacity, and fades. The editor regenerates
  compact text/font thumbnails at runtime and deliberately keeps project timing out of presets.
- Added Project Preview wheel zoom, timeline wheel panning, and Ctrl/Shift 0.5/1-second time-button modifiers.
- Migrated the catalog with seen state and per-project media references. Green current-project, yellow
  other-project, and blue unseen corners update independently from successful-export use history; clips become
  seen when deliberately previewed or through the context action.
- Added persisted Content Browser sorting by name, newest file date, duration, or custom tag, including CLI
  configuration/list visibility. Advanced all component metadata and the extensionless marker to 0.1.29.
- Verification: clean Release solution build, static XAML resource audit, `git diff --check`, and an isolated
  fresh SQLite/CLI list smoke. No dependency changed, so no new vulnerability audit was required.

## 2026-08-14 — v0.1.28 stale-coverage and moving-overlay pass

- Made direct overlay manipulation fully transactional: the draft now retains preset placement as well as
  normalized transform values, and Cancel/Escape releases any pending pointer interaction before restoring and
  redrawing the exact saved state.
- Replaced all-or-nothing preview invalidation with interval coverage. The timeline ruler paints current cached
  spans green and overlapping changed spans yellow; range-scoped overlay/effect edits retain unaffected chunks,
  primary-source mutations compare before/after items to stale their enclosing changed span, while output and
  visual track-order changes conservatively stale all covered time.
- Coalesced rapid MediaElement source swaps onto the dispatcher and transparently reopens a still-current chunk
  once before showing a playback error, removing the observed one-off codec modal during fast needle movement.
- Native layer insertion now uses the playhead when no selected Video range supplies start/end timing. Added
  schema-10 GIF/video overlays across the Effects catalog, empty-lane menu, editor, transform/lock gizmo,
  persistence, shared mapper, and FFmpeg graph; moving inputs loop and retain motion through the block while
  applying still-overlay position, scale, rotation, opacity, fades, track order, and LQ/HQ scaling.
- Advanced all component metadata and the extensionless marker to 0.1.28. Verification covered clean Release
  builds, XAML resources, interval split/replacement logic, project schema/CLI inspection, and real two-second
  MP4 and animated-GIF overlay renders at 320×180/30 fps with differing frame hashes proving retained motion.

## 2026-08-14 — v0.1.27 chunked prerender and bounded-transport pass

- Corrected reduced-resolution image overlay sizing so the preview factor applies after resolving the source
  width and 480-pixel cap; small PNG/JPG overlays now occupy the same relative canvas area in LQ and HQ.
- Replaced the asymmetric prerender toolbar with compact FRAME, PREVIEW, and ALL groups, each offering LQ/HQ;
  PREVIEW uses the selected range and falls back to the current frame.
- Retained every matching project-fingerprint prerender as a reusable time chunk, restored matching chunks across
  sessions, merged their coverage feedback, and switched cached media automatically from ruler/lane/block seeks.
  Range selection now pauses without unloading; actual timeline-content changes clear the session catalog and
  discard an in-flight result if its captured fingerprint no longer matches.
- Bound playback to the active chunk's declared global interval. Timer and natural completion both pause, reset
  the needle to the chunk start, and synchronize the transport icon instead of allowing hidden continued play.
- Advanced all component metadata and the extensionless marker to 0.1.27. Verification covered clean Release
  builds, XAML resources, an in-process chunk selection/replacement smoke, and a real FFmpeg 80-pixel PNG/JPG
  overlay check proving 80 px at HQ and 20 px at 25% LQ.

## 2026-08-14 — v0.1.26 preview-quality and editor-feedback pass

- Corrected the remaining scrollbar compression by separating horizontal width from vertical height and gave
  both thumb orientations a usable minimum. Replaced WPF's native menu icon gutter with a six-pixel detail rail.
- Removed all editor-only opacity reduction from stale overlay content. Unchanged rendered selections now paint
  only chrome; moved overlays show an exact-alpha live proxy and a crossed notice at the old rendered transform.
- Reorganized native/plugin item editors into consistent content/module, timeline, transform, and adjustment
  sections. Native PNG/text settings can prerender unsaved changes over the real project background through the
  same snapped, progress-reporting frame companion as plugin effects.
- Added persisted 10/25/50/75/90/100% LQ preview resolution, selected-image Lanczos scaling, explicit Frame LQ
  and Frame HQ actions, scaled effect geometry/radii, and cache metadata for preview settings/selection.
- Added a high-contrast dark tooltip theme, 500 ms delay, focused descriptions on the new controls, and baseline
  hover help for standard interactive controls. Advanced all component metadata and the marker to 0.1.26.
- Verification covered clean Release builds, static XAML resources, CLI config output, and a real 25% FFmpeg
  render combining Background Blur, Video Blur, opaque transformed PNG, text, and progress at 80×46 for a
  320×180 project; the direct/transitive NuGet vulnerability audit was clear. Commit: recorded by the commit
  containing this entry.

## 2026-08-13 — v0.1.25 overlay fidelity and editor-shell polish

- Replaced effect-frame's long nearly-empty wait with an immediately painted five-percent bar, named project
  preparation stages, a monotonic preparation ticker, mapped FFmpeg progress, and persistent elapsed timing.
- Removed the image renderer's hidden 90% alpha. Added schema-9 explicit 0–100% opacity for text/image overlays,
  including editor/live image feedback, Core mapping, validation, and real FFmpeg alpha/color application.
- Replaced black emoji lock buttons with transparent gray vector open/closed locks; locked state adds a gray body
  fill while unlocked remains an outline. Project-view overlay double-click now opens that item's editor.
- Added List/Small/Large/Extra large browser cycling with a bounded 420 px default and portable INI/CLI support.
  Space now consumes previously focused panel buttons and expands Browser/Layers/Timeline as requested.
- Bound the custom scrollbar tracks to their scrollbar viewport/value properties, restoring proportional thumbs;
  capped/ellipsized context-menu labels remove the excessive blank width.
- Advanced metadata and the extensionless marker to 0.1.25. Verification covered clean full solution builds,
  XAML resource checks, schema/config round-trips, and real 100% versus 50% image-overlay renders.

## 2026-08-12 — v0.1.24 persistent prerenders and timeline safety

- Retained the latest successful project prerender in metadata storage and restored it on startup or normal
  project Open only when a project/app/source-file fingerprint still matches. Preview filenames are unique so
  Windows playback locks cannot corrupt replacement, while superseded files are removed best-effort.
- Reworked timed Background blur as an absolute-time choice between complete normal and blurred compositions,
  restoring the effect consistently in Frame, selected-range, and All prerenders. The portable capability gate
  now requires the bundled FFmpeg `blend` filter.
- Connected ruler, empty-lane, and block clicks to pause and seek inside available prerender coverage; corrected
  autoplay-pending consumption so the project play/pause button follows the actual MediaElement state.
- Made clip-boundary candidates take priority over nearby grid candidates while moving/resizing timed blocks.
  Effect-frame feedback is now determinate and parses current plus historical FFmpeg progress timestamps.
- Added persisted schema-8 text/image transform locking with visible lock buttons on timeline blocks and Project
  Layers Data, context actions, and read-only selection in Project Preview while locked.
- Advanced application/component metadata and the extensionless marker to 0.1.24. Repeated complete Release
  builds passed with zero warnings/errors; the user's real 179-second Background-blur project rendered through
  the bundled FFmpeg at reduced smoke-test resolution and produced a decodable output with blurred side fill.

## 2026-08-12 — v0.1.23 bundled-preview compatibility and browser/timeline polish

- Replaced Background lightness's unavailable `eq` dependency with a bounded `lutyuv` luma offset supported by
  the bundled LGPL FFmpeg; -100/0/+100 retain their black/unchanged/white semantics.
- Reworked Content Browser mouse selection so Ctrl explicitly toggles cards and Shift selects from the first
  anchor through the clicked card, while retaining multi-card drag and double-click insertion.
- Persisted timeline time zoom and track height in `[Workspace]`, renamed the existing horizontal fit control to
  **Fit width**, and based it on the live scroll viewport. Removed misleading scrollbar arrow end caps.
- Added an immediate, non-locking source preview to the PNG/image overlay editor with missing/invalid feedback.
- Advanced application/component metadata and the extensionless marker to 0.1.23. Verification included clean
  Release builds, settings/XAML checks, direct lightness endpoint probes, and a real saved-project render through
  the bundled FFmpeg producing a one-second 320x180 MPEG-4/AAC output without `eq`.

## 2026-08-12 — v0.1.22 effect discovery, progress workflow, and timeline-state polish

- Confirmed the user's splash timing and window/workspace restoration acceptance, then made Content Browser
  Ctrl/Shift selection explicit and added an alphabetically grouped Effects tab with native/plugin entries for
  every compatible timeline. Renamed the right panel to Project Layers Data and removed its old add-effect row.
- Changed new-project track order to Overlays, Video, Progress, Background, Audio. Progress creation now inherits
  selected-clip timing/naming, remembers visual defaults in the portable INI, and supports copy/paste style.
- Added block enable/disable without removal, darkened/grayed disabled presentation, and shared render exclusion.
  Corrected mixed Video-lane effect editing, movement, resizing, removal, and state changes.
- Made empty-lane clicks select the project frame and add native compatible items, applied clip-boundary snapping
  to range and block edges, kept source-only clip boundaries authoritative, widened/iconized preview transport,
  synchronized autoplay state after MediaOpened, and added disk/recent choices to Open.
- Mapped Background lightness -100..100 to FFmpeg `eq` brightness -1..1 and documented calculation ranges.
  The effect-frame companion now opens at editor width above it where possible and reports render progress plus
  elapsed time.
- Verification included repeated zero-warning Release builds, XAML-resource and source invariants, CLI creation
  of the exact five-track project, settings parsing for recent/progress defaults, and an isolated real WPF start
  to the versioned editor followed by exit code 0. The user separately confirmed splash/layout preservation.

## 2026-08-12 — v0.1.21 first-launch splash duration

- Added portable `Startup.FirstStartupCompleted` state, defaulting false and saved atomically only after the editor
  initializes successfully.
- Applied a five-second splash minimum to the first successful launch in an installation and an approximately
  three-second minimum thereafter; manual refresh retains only its short boundary holds.
- Exposed the first-startup state through human and JSON CLI configuration output and documented its INI schema.
- Advanced application/component metadata and the checked-in extensionless marker to 0.1.21.
- Full Release builds passed without warnings/errors. Real WPF observation measured 5.138 seconds of first splash
  visibility and 3.266 seconds returning, confirmed the persisted false-to-true transition, and closed cleanly.

## 2026-08-12 — v0.1.20 approximately three-second startup

- Reduced ordinary synthetic startup gaps to 20–40 ms and opening/completion holds to 100–200 ms.
- Kept configured startup scanning and manual library-refresh diagnostics immediate so genuine work, rather than
  decorative timing, determines longer splash durations.
- Advanced application/component metadata and the checked-in extensionless marker to 0.1.20.
- A full Release build passed without warnings or errors. Three real v0.1.20 WPF launches reached the editor in
  2.415, 2.474, and 2.775 seconds and closed cleanly; an earlier colder pass measured approximately 2.9 seconds.

## 2026-08-12 — v0.1.19 shorter splash pacing

- Removed the unconditional five-second minimum from startup and manual library-refresh splashes.
- Reduced artificial gaps between ordinary fast startup lines from 500–750 ms to 50–100 ms, while preserving
  immediate live scan reporting.
- Added separate randomized 200–500 ms opening and completion holds so the first and last states remain readable.
- Advanced application/component metadata and the checked-in extensionless marker to 0.1.19.
- Verified two warning-free full Release builds, the v0.1.19 CLI and marker, a real WPF startup reaching the
  versioned editor in about 4.1 seconds and closing cleanly, and a UI-Automation pass observing the staged log
  reach 17 visible lines before successful editor handoff.

## 2026-08-12 — v0.1.18 staged startup diagnostics

- Expanded the wide split Mr Cat splash with a named stage, numeric percentage, progress bar, and timestamped
  console-style messages; widened the diagnostics area and exposed horizontal scrolling for long file details.
- Split startup into service, configuration, font, software-layout, editor-workspace, plugin, catalog,
  project-file/recovery, and completion stages. Empty/disabled scan work is reported explicitly.
- When startup scanning is selected and source folders exist, reported enumeration plus per-file scan counts,
  scan percentage, finalization, and result totals inside the overall startup range. Manual refresh uses the same
  stage/percentage presentation.
- Real UI Automation verified scan-disabled, scan-enabled empty-library, and generated saved-project startup
  branches without retaining test settings/data; the saved project displayed its filename and clean-load result.
- The full FFmpeg-aware portable publisher passed with exactly one current marker, v0.1.18 CLI output, and no
  packaged private-path or credential match.
- Advanced all application/component metadata and the checked-in marker to 0.1.18.
- Published GitHub Release v0.1.18 with the validated 280,258,157-byte Windows x64 ZIP and separately downloadable
  checksum; public metadata and checksum content match the local archive.

## 2026-08-12 — v0.1.17 extensionless version marker

- Added checked-in `version_0.1.17` with a short changelist and copied it beside both executable outputs.
- Made executable builds reject a marker that is missing, duplicated, or out of sync with central version
  metadata, and remove stale `version_*` files from reused build/publish directories.
- Extended portable publishing to require byte-identical GUI/CLI markers and accept exactly one current marker
  in the package root. Advanced all application/component metadata to 0.1.17.
- Verified expected rejection of a deliberately mismatched version, automatic removal of planted stale markers,
  a warning-free Release build, XAML resource audit, fresh full portable publish, packaged CLI version, and real
  WPF v0.1.17 title. Package text contained no private-path or credential match.
- Published GitHub Release v0.1.17 with the validated 280,254,521-byte Windows x64 ZIP and separately downloadable
  checksum; public metadata and checksum content match the local archive.

## 2026-08-12 — v0.1.16 session-state correction

- Corrected clean recovery being unconditionally classified as an unsaved edit on every startup. Clean shutdown
  now clears recovery, and startup reconciles stale recovery against the named saved project while preserving
  genuinely changed crash-recovery content.
- Added all runtime workspace defaults to the packaged INI template and advanced every application/component
  to visible version 0.1.16.
- An isolated real WPF smoke moved the v0.1.16 main window to 111,99 at 1366x822, closed without a prompt,
  verified the exact INI values, reopened at the exact geometry, and closed cleanly again. A schema-6 saved
  project plus equivalent schema-7 recovery opened without an asterisk and deleted recovery; a changed recovery
  retained both its asterisk and recovery file.
- Published GitHub Release v0.1.16 with the validated 280,252,855-byte self-contained Windows x64 ZIP and its
  separately downloadable SHA-256 file; public download metadata and checksum content were verified.

## 2026-08-12 — Session restoration, undo, contextual prerender, and transactional overlays

- Persisted normal/maximized window geometry, workspace splitter sizes, preview split/tab state, focused panel,
  and expanded panel in `CatClipComposer.ini`; off-screen/default positioning remains safe.
- Corrected nonlinear timed-block edge resizing by measuring the pointer once from drag start. Added left-click
  compatible-effect menus on empty lanes, bounded project undo/redo with toolbar/keyboard actions, an asterisk
  dirty marker, and a literal Save / Don't save / Cancel close dialog.
- Changed the main prerender action to the active range or current frame and added explicit Frame and All actions.
  Project-frame prerenders pause after loading rather than playing a whole composition.
- Made Project Preview overlay manipulation transactional with on-canvas OK/Cancel and Enter/Escape, while live
  movement avoids flooding undo history. Added schema-7 text/image alpha fade-in/out fields and FFmpeg rendering.
- Passed the Release build and 17-file XAML audit. Workspace INI values parsed exactly; schema 6 still loaded;
  a real two-second 320x180 MPEG-4/AAC render visually confirmed transformed text/image overlays transparent at
  both edges and opaque in the middle. No dependency changed.

## 2026-08-12 — Direct Project Preview overlay manipulation

- Added active text/image content gizmos over the correctly letterboxed Project Preview frame. Clicking selects
  the matching timeline/layer item; dragging moves it, the corner handle scales it, and the upper handle rotates it.
- Added normalized X/Y, uniform scale, and rotation fields to schema 6, overlay dialogs, layer/timeline summaries,
  shared render mapping, and FFmpeg text/image composition while retaining preset placement for older projects.
- Passed the Release solution build and XAML resource audit. Real 320x180 MPEG-4/AAC smokes rendered transformed
  text plus the Mr. Cat image and a legacy schema-3 preset-overlay project; FFprobe confirmed two-second video.

## 2026-08-12 — Mr. Cat splash-screen disclosure

- Added a prominent README note that the software includes a photo of Mr. Cat as its splash screen.
- Recorded the completed documentation item and its documentation audit; no application or release files changed.

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
