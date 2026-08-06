# Project goals and feature status

Last reviewed: 2026-08-06

## Product goal

Cat Clip Composer is a deliberately narrow Windows application for cataloging folders of short clips, arranging selected clips on a duration-based timeline, adding simple presentation elements, and producing a YouTube-ready compilation without the complexity of a general video editor.

The application should work for personal use and remain suitable for possible commercial distribution. Its normal runtime must not depend on paid components, GPL/AGPL components, or non-redistributable FFmpeg builds.

## Scope principles

- One repository with modular feature projects and no Git submodules.
- GUI and headless workflows over shared domain and infrastructure modules.
- Durable local media catalog and export history.
- Simple assembly, presentation, and rendering features; no attempt to become a full nonlinear editor.
- Configuration stored in an INI file beside the executable.
- Maintained documentation, worklog, TODO list, and audit trail.

## Requested features

| Area | Feature | Status | Notes |
|---|---|---:|---|
| Configuration | Multiple configurable source folders | Done | Recursive scanning is optional. |
| Configuration | Output directory and target timeline duration | Done | Persisted in the executable-directory INI file. |
| Catalog | Scan MP4, WebM, AVI, and common related containers | Done | MP4, WebM, AVI, MOV, MKV, and M4V supported. |
| Catalog | Probe duration, dimensions, and audio presence | Done | External FFprobe adapter. |
| Catalog | Durable database that can be refreshed | Done | SQLite catalog with availability tracking. |
| Browser | Selectable thumbnails with duration/index/optional names | Done | Cached FFmpeg thumbnails and search. |
| Browser | Video preview or content slideshow | Partial | Windows media preview exists; FFmpeg slideshow/contact sheet remains `PREVIEW-001`. |
| Timeline | Configurable duration axis and total | Done | Progress against target duration is shown. |
| Timeline | Add, duplicate, select, remove, and reorder clips | Done | Buttons and Delete-key removal supported. |
| Screens | Splash, mid-video, and outro screens | Done | Still images can be inserted anywhere and reordered. |
| Overlays | PNG/text overlays and custom fonts | Partial | One compilation-wide image/text layer exists; independently timed overlays remain `OVERLAY-001`. |
| Progress | Per-clip or whole-video progress bars | Done | Rendered into final output. |
| Output | Landscape and portrait YouTube formats | Done | 1920×1080 and 1080×1920. |
| Output | Join selected items into a final video | Done | Safe temporary output and cancellation. |
| History | Record use time and final output for source files | Done | Export jobs and ordered source clip history. |
| History | Open prior output/source locations | Done | History browser integrates with File Explorer. |
| Configuration | INI file beside executable | Done | Atomic `CatClipComposer.ini` store with safe defaults and escaping. |
| Automation | Headless command-line mode | Not done | `CLI-001`. |
| Licensing | Default path without required GPL components | Not done | Current `libx264` default is tracked as `LIC-001`. |
| Projects | Save and reopen named timelines | Not done | `PROJECT-001`. |
| Editing | Trim and per-clip volume | Not done | Deferred narrow-editor enhancement `EDIT-001`. |

## Definition of an MVP release

The MVP release requires all P0 items in `docs/TODO.md` to be complete, a clean Release build, a clean dependency vulnerability audit, an end-to-end headless smoke test, an end-to-end GUI render smoke test, and no open critical licensing audit item.
