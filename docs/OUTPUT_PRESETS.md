# Project settings, output presets, and render layers

Last reviewed: 2026-08-12

Output settings belong to each `.nya`, so changing Preferences never silently changes a saved project's
delivery format. **Project settings** edits the project name, target duration, preset, dimensions, frame
rate, encoder, quality, video bitrate, and AAC audio bitrate. The Layers/Used Clips rollout shows the current
summary and opens the same editor.

## Included presets

- **YouTube 1080p:** 1920x1080 at 30 FPS, 8000 kbps video, 192 kbps audio.
- **YouTube 1080p60:** 1920x1080 at 60 FPS, 12000 kbps video, 192 kbps audio.
- **YouTube 4K:** 3840x2160 at 30 FPS, 45000 kbps video, 256 kbps audio.
- **YouTube 4K60:** 3840x2160 at 60 FPS, 68000 kbps video, 256 kbps audio.
- **YouTube Shorts:** 1080x1920 at 30 FPS, 8000 kbps video, 192 kbps audio.
- **Square:** 1080x1080 at 30 FPS, 8000 kbps video, 192 kbps audio.
- **Classic 4:3:** 1440x1080 at 30 FPS, 8000 kbps video, 192 kbps audio.
- **Custom:** Width, height, FPS, bitrates, encoder, and quality remain editable.

The 8/12 Mbps 1080p and upper-bound 45/68 Mbps 4K values follow YouTube's published SDR upload
recommendations. These are starting presets; use Custom to retain another native source rate. The renderer
accepts even dimensions from 16-7680 and frame rates from 1-240.

References:

- YouTube recommended upload encoding: <https://support.google.com/youtube/answer/1722171>
- Clipchamp export resolution: <https://support.microsoft.com/en-us/clipchamp/exporting-and-saving-a-video-in-clipchamp>
- Clipchamp MP4 format: <https://support.microsoft.com/en-us/Clipchamp/what-format-is-my-video-exported-in>
- Adobe Premiere export settings: <https://helpx.adobe.com/premiere/desktop/render-and-export/export-files/export-video.html>

## Encoder behavior

- `NativeMpeg4` uses FFmpeg's native MPEG-4 Part 2 encoder and is the non-GPL compatibility default.
- `WindowsMediaFoundationH264` uses the bundled `h264_mf` encoder. Successful use depends on Windows Media
  Foundation support.
- `Libx264Gpl` is an explicit user-supplied GPL-tool opt-in. The mandatory bundle does not include libx264.
- AAC output uses the selected project audio bitrate.

## Editable render layers

The layer panel represents Video, Overlay, Audio, Progress, and Effects tracks. Text, PNG/JPEG, music, and
progress items can be independently added, timed, edited, enabled, or removed. Video and still items expose
Fit, Fill, or Stretch plus fade-in, fade-out, and source volume. Source-derived blurred side fill is a
separate configurable module on the Background timeline.

Progress is a timeline effect, never a global preference. Each progress item can cover the whole project, a
selected source segment, or a custom range and has its own solid/segmented/tick style, color, height, and
top/bottom position. Music loops when its layer duration exceeds its source and supports volume and fades.

Text effects can use an installed Windows font family or a TTF/OTF from the configured portable font
folder. Custom-folder fonts are visibly marked in the chooser. Multiple text and image effects are applied
in timeline order. Every text/image overlay can independently fade from transparency at its start and fade
back to transparency at its end; both values are clamped to that overlay's own duration.

FFmpeg's documented `fade`, `afade`, `drawtext`, `overlay`, `gblur`, `drawgrid`, `concat`, and `amix`
filters provide the implementation: <https://ffmpeg.org/ffmpeg-filters.html>.
