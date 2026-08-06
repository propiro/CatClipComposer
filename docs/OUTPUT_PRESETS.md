# Output presets and render layers

Last reviewed: 2026-08-06

Output settings belong to each `.ccproject`, so changing Preferences does not silently rewrite a saved project's delivery format. The **Output** dialog applies a preset and still exposes editable width, height, frame rate, encoder, quality, video bitrate, and AAC audio bitrate.

## Included presets

| Preset | Frame | FPS | Video / audio kbps | Intended use |
|---|---:|---:|---:|---|
| YouTube 1080p | 1920×1080 | 30 | 8000 / 192 | Standard 16:9 upload |
| YouTube 1080p60 | 1920×1080 | 60 | 12000 / 192 | High-frame-rate 16:9 upload |
| YouTube 4K | 3840×2160 | 30 | 45000 / 256 | Standard-frame-rate UHD |
| YouTube 4K60 | 3840×2160 | 60 | 68000 / 256 | High-frame-rate UHD |
| YouTube Shorts | 1080×1920 | 30 | 8000 / 192 | Vertical 9:16 |
| Square | 1080×1080 | 30 | 8000 / 192 | Square 1:1 |
| Classic 4:3 | 1440×1080 | 30 | 8000 / 192 | Classic 4:3 |
| Custom | editable | editable | editable | Unlisted delivery requirement |

The 8/12 Mbps 1080p and upper-bound 45/68 Mbps 4K values follow YouTube's published SDR recommendations for standard/high frame rates. YouTube recommends MP4, AAC-LC or Opus, H.264, native source frame rate, progressive scan, and common rates including 24/25/30/48/50/60. Clipchamp's current export choices provide a second editor reference for 480p/720p/1080p/4K MP4 delivery, while Adobe documents H.264 and Match Source/Adaptive High Bitrate as common online-export paths.

Primary references:

- YouTube recommended upload encoding: <https://support.google.com/youtube/answer/1722171>
- Clipchamp export resolution: <https://support.microsoft.com/en-us/clipchamp/exporting-and-saving-a-video-in-clipchamp>
- Clipchamp MP4/30-fps format: <https://support.microsoft.com/en-us/Clipchamp/what-format-is-my-video-exported-in>
- Adobe Premiere export settings: <https://helpx.adobe.com/premiere/desktop/render-and-export/export-files/export-video.html>

These are starting presets, not a claim that every source should be converted to 30 or 60 fps. Use Custom to retain another native source rate. The renderer validates even dimensions from 16–7680 and frame rates from 1–240.

## Encoder and quality behavior

- `NativeMpeg4` uses FFmpeg's native MPEG-4 Part 2 encoder, selected target bitrate, and a quality-derived quantizer range. It is the compatibility/default path because it does not require a GPL library.
- `WindowsMediaFoundationH264` uses `h264_mf` quality control when the selected Windows FFmpeg build exposes it. H.264 is the preferred YouTube upload codec, but availability depends on the OS/build.
- `Libx264Gpl` maps quality to CRF and is an explicit GPL-build opt-in. It is never required or selected automatically.
- AAC output uses the selected audio bitrate. Encoder availability and the exact FFmpeg build remain release-audit inputs.

## Editable render layers

The layer panel projects the saved Video, Overlay, Audio, Progress, and Effects tracks. Text, PNG/JPEG, music, and progress items can be added, edited, timed, or removed. Video/still items expose Fit, Fill, Stretch, or animated Blur Background plus fade-in, fade-out, and source volume.

Progress can cover the whole project, each video segment through the global progress preference, or an arbitrary custom range. Music loops when its selected layer duration exceeds the source and supports volume plus fade-in/out. Multiple text/image layers are applied in time order. FFmpeg's documented `fade`, `afade`, `drawtext`, `overlay`, `gblur`, `concat`, and `amix` filters provide the implementation: <https://ffmpeg.org/ffmpeg-filters.html>.
