# Software stack and license policy

Last reviewed: 2026-08-06

## Desired license status

Cat Clip Composer is personal software that may later be distributed commercially. Normal application use must not require paid libraries, GPL/AGPL components, or an FFmpeg build that cannot be redistributed. Permissive open-source and public-domain components are acceptable.

Preferred licenses:

- Public domain / CC0 for code-equivalent assets
- MIT
- BSD-2-Clause / BSD-3-Clause
- Apache-2.0
- Other permissive licenses after review

Restricted by default:

- GPL and AGPL dependencies that would impose reciprocal distribution obligations on Cat Clip Composer
- `--enable-nonfree` FFmpeg builds, which FFmpeg marks as non-redistributable
- Paid or royalty-bearing runtime libraries
- Network services required for core local functionality

LGPL external tools or dynamically linked libraries may be acceptable after a distribution-compliance review. FFmpeg remains an independently configured executable rather than linked application code.

## Project stack

| Component | Version | Purpose | License/status | Distribution decision |
|---|---:|---|---|---|
| C# / .NET | 8 | Runtime and application language | .NET source/library packages primarily MIT; Windows distributions have Microsoft terms | Accepted; preserve official notices when publishing. |
| WPF | .NET 8 | Windows GUI | Part of .NET Windows desktop stack | Accepted for Windows-only application. |
| .NET console host | .NET 8 | Headless CLI process and exit codes | Part of .NET runtime stack | Accepted; no added package. |
| Microsoft.Data.Sqlite | 8.0.29 | ADO.NET SQLite provider | MIT | Accepted. |
| SQLite | Via SQLitePCLRaw 2.1.12 | Embedded catalog database | Public domain | Accepted. |
| SQLitePCLRaw | 2.1.12 | Native SQLite interop/bundle | Apache-2.0 | Accepted; explicitly pinned past vulnerable native SQLite versions. |
| System.Text.Json | .NET 8 | Structured CLI output and internal serialization where needed | MIT as a .NET library package | Accepted. |
| FFmpeg / FFprobe | User supplied or publisher-supplied audited pair | Probe, thumbnails, filters, encoding | LGPL-2.1-or-later by default; optional GPL parts change build status | Separate executable under `thirdparty`; default features must work with an LGPL-compatible build. |
| FFmpeg native `mpeg4` | Selected FFmpeg build | Default MPEG-4 Part 2 encoder | Native FFmpeg encoder, available without external GPL library | Accepted non-GPL compatibility default. YouTube accepts MPEG4 uploads but recommends H.264. |
| FFmpeg `h264_mf` | Selected Windows FFmpeg build | Preferred H.264 encoder | Media Foundation wrapper documented by FFmpeg; no `--enable-gpl` dependency | Accepted optional non-GPL Windows preset; availability is build-dependent. |
| libx264 | Selected FFmpeg build | Optional H.264 encoder | GPL when enabled in FFmpeg | Allowed only as an explicit `Libx264Gpl` user opt-in; never required/default. |

## Dependency rules

1. Add a dependency only when platform or existing code cannot reasonably satisfy the requirement.
2. Record version, purpose, direct/transitive status, license, source, and redistribution effect here and in third-party notices when shipped.
3. Prefer a small adapter behind a Core interface so dependencies can be replaced.
4. Audit vulnerabilities after every package change.
5. Do not suppress a known vulnerability when a patched compatible version exists.

## FFmpeg distribution boundary

The repository does not include FFmpeg binaries. Users configure `ffmpeg.exe`; `ffprobe.exe` is located beside it, under the portable `thirdparty\ffmpeg` boundary, or on `PATH`. The publisher records `ffmpeg -version` as `BUILD_INFO.txt`. Release review must distinguish:

- LGPL-compatible builds without `--enable-gpl` or `--enable-nonfree`;
- GPL builds, which may be used personally as an explicit opt-in but are not required;
- nonfree builds, which are not an accepted redistributable dependency.

## Application versioning

`Directory.Build.props` supplies version 0.1.3 to the WPF, CLI, Core, and Infrastructure projects, including assembly, file, and informational metadata. User-visible version strings are resolved from the Core assembly metadata so the main-window title/status bars and headless output cannot drift from the built components.

FFmpeg documents its native `mpeg4` encoder as usable without the GPL `libxvid` wrapper and documents `h264_mf` as a Media Foundation encoder. YouTube lists MPEG4 as a supported upload format and recommends MP4/H.264/AAC for optimal uploads. These sources establish the default/optional preset boundary; a distributor must still audit the exact configured FFmpeg binary.

- FFmpeg native MPEG-4 and encoder documentation: <https://ffmpeg.org/ffmpeg-all.html>
- FFmpeg Media Foundation encoder documentation: <https://ffmpeg.org/ffmpeg-codecs.html#MediaFoundation>
- YouTube supported formats: <https://support.google.com/youtube/troubleshooter/2888402>
- YouTube recommended encoding: <https://support.google.com/youtube/answer/1722171>
