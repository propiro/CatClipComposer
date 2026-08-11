# Software stack and license policy

Last reviewed: 2026-08-11

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

Each dependency is described separately so version, purpose, license, and distribution effect remain readable.

- **C# / .NET 8:** Application language and runtime. .NET source/library packages are primarily MIT;
  Windows distributions include Microsoft terms. Accepted with official notices preserved.
- **WPF on .NET 8:** Windows GUI stack. Accepted for the Windows-only desktop application.
- **.NET console host:** Headless CLI process and exit codes. Part of the runtime; no extra package.
- **Microsoft.Data.Sqlite 8.0.29:** ADO.NET SQLite provider under MIT. Accepted.
- **SQLite via SQLitePCLRaw 2.1.12:** Embedded public-domain catalog database. Accepted.
- **SQLitePCLRaw 2.1.12:** Apache-2.0 native interop/bundle, pinned past vulnerable SQLite versions.
- **System.Text.Json from .NET 8:** Structured CLI output and project serialization. Accepted MIT library.
- **CatClipComposer.Plugins.BuiltIn assembly 0.1.16:** First-party dynamic module assembly containing
  background blur, video blur, and PNG source support. Individual module contract versions are 1.0.0 or
  1.1.0. It adds no NuGet or third-party runtime dependency and ships under `plugins`.
- **FFmpeg/FFprobe n8.1.2-34-g9b6c8969e0-20260806:** Probe, preview, filters, and encoding. The pinned
  BtbN Windows x64 shared build uses LGPL v3, contains no `--enable-gpl` or `--enable-nonfree`, and ships
  under `thirdparty\ffmpeg` with replaceable DLLs, exact license/source/build records, and hashes.
- **Bundled FFmpeg integrations:** The exact enabled set is recorded in `BUILD_INFO.txt`; it covers common
  codecs/formats, font shaping, network protocols, and hardware APIs. `SOURCE.txt` identifies the pinned BtbN
  build-script tag whose `scripts.d` files resolve each integration to an upstream repository and commit.
  The set is restricted to LGPL-compatible MIT, BSD, Apache, ISC, zlib, MPL, FTL, LGPL, and similar terms;
  the binary configuration disables GPL-only and nonfree components. These integrations remain inside the
  separately distributed FFmpeg runtime rather than becoming linked application dependencies.
- **FFmpeg native `mpeg4`:** Default MPEG-4 Part 2 encoder. It needs no external GPL library and is the
  accepted compatibility default.
- **FFmpeg `h264_mf`:** Optional Media Foundation H.264 encoder. It is present in the pinned build and needs
  no `--enable-gpl` component.
- **libx264:** Optional GPL H.264 encoder identifier retained for explicit custom-tool use. It is absent from
  the mandatory bundle and is never required or selected by default.

## Dependency rules

1. Add a dependency only when platform or existing code cannot reasonably satisfy the requirement.
2. Record version, purpose, direct/transitive status, license, source, and redistribution effect here and in third-party notices when shipped.
3. Prefer a small adapter behind a Core interface so dependencies can be replaced.
4. Audit vulnerabilities after every package change.
5. Do not suppress a known vulnerability when a patched compatible version exists.

## FFmpeg distribution boundary

The repository includes the audited shared runtime under `thirdparty\ffmpeg`. Central build targets copy it
to GUI and CLI output directories, and the portable publisher always includes it. With the default
`FfmpegPath=ffmpeg.exe`, the application resolves this packaged path directly. A user can still configure a
different executable as an explicit local override.

The pinned bundle is dynamically linked so its FFmpeg DLLs remain separate and replaceable. Its exact LGPL
v3 text, source/archive locations, build flags, and file hashes travel beside it. The publisher rejects a
missing or modified manifest file, mismatched executable version, absent required capability, or any build
that reports `--enable-gpl` or `--enable-nonfree`.

## Application versioning

`Directory.Build.props` supplies version 0.1.16 to the WPF, CLI, Core, Infrastructure, and built-in plugin
projects, including
assembly, file, and informational metadata. User-visible strings resolve from Core assembly metadata so the
main-window title/status bars and headless output cannot drift from the built components.

## User interface artwork and fonts

The startup/rescan splash embeds the user-supplied Mr Cat bee-costume photograph, lightly sharpened with the
built-in image-generation tool. It is an application asset rather than a software dependency. The project
owner remains responsible for confirming image-distribution rights for a public release.

The packaged `fonts` folder contains instructions but no third-party font by default. User-added TTF/OTF
files remain separately replaceable and subject to their own licenses; installed Windows fonts are selected
by family name and are not copied into the package.

FFmpeg documents its native `mpeg4` encoder as usable without the GPL `libxvid` wrapper and documents `h264_mf` as a Media Foundation encoder. YouTube lists MPEG4 as a supported upload format and recommends MP4/H.264/AAC for optimal uploads. These sources establish the default/optional preset boundary; a distributor must still audit the exact configured FFmpeg binary.

- FFmpeg native MPEG-4 and encoder documentation: <https://ffmpeg.org/ffmpeg-all.html>
- FFmpeg Media Foundation encoder documentation: <https://ffmpeg.org/ffmpeg-codecs.html#MediaFoundation>
- YouTube supported formats: <https://support.google.com/youtube/troubleshooter/2888402>
- YouTube recommended encoding: <https://support.google.com/youtube/answer/1722171>
