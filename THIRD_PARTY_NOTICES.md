# Third-party components

This file is an engineering inventory, not legal advice. Preserve the exact notices and source records that
ship beside third-party binaries.

## .NET and WPF

Cat Clip Composer targets .NET 8 and WPF. .NET source and library packages are primarily provided under the MIT License; Windows runtime distributions also carry Microsoft’s applicable distribution terms and third-party notices.

- Project: <https://github.com/dotnet/runtime>
- License information: <https://github.com/dotnet/core/blob/main/license-information.md>

## Microsoft.Data.Sqlite 8.0.29

Lightweight ADO.NET provider maintained by Microsoft under the MIT License.

- Package: <https://www.nuget.org/packages/Microsoft.Data.Sqlite/8.0.29>
- Source: <https://github.com/dotnet/efcore>

## SQLite

The SQLite engine bundled through `Microsoft.Data.Sqlite` is dedicated to the public domain and may be used for commercial or private purposes.

- Project and status: <https://sqlite.org/about.html>
- Copyright statement: <https://sqlite.org/copyright.html>

## SQLitePCLRaw 2.1.12

The `Microsoft.Data.Sqlite` package brings in SQLitePCLRaw packages under the Apache License 2.0. The bundle is explicitly pinned to 2.1.12 so the application uses a SQLite version containing the fix for CVE-2025-6965 rather than the older vulnerable transitive version. NuGet package manifests are authoritative for the exact resolved packages.

- Source: <https://github.com/ericsink/SQLitePCL.raw>
- License: <https://www.apache.org/licenses/LICENSE-2.0>

## FFmpeg and FFprobe

Cat Clip Composer bundles BtbN's Windows x64 shared FFmpeg/FFprobe runtime
`n8.1.2-34-g9b6c8969e0-20260806` under `thirdparty\ffmpeg`. The exact distribution uses LGPL v3 and reports
neither `--enable-gpl` nor `--enable-nonfree`.

The distributed `LICENSE.txt`, `SOURCE.txt`, `BUILD_INFO.txt`, and `MANIFEST.sha256` files identify the
license, pinned binary archive and SHA-256, upstream source, exact configuration, and runtime hashes. The
shared DLLs remain separate and replaceable by interface-compatible builds.

The build configuration also lists its enabled LGPL-compatible codec, format, font, protocol, and hardware
integrations. `SOURCE.txt` records that set and links the exact BtbN build-script tag; its `scripts.d` files
pin each integration's upstream repository and commit. Those projects retain their respective MIT, BSD,
Apache, ISC, zlib, MPL, FTL, LGPL, and similar notices and source terms. GPL-only and nonfree integrations
are disabled in the bundled variant.

The default renderer uses native `mpeg4`; the bundle also provides native AAC, `drawtext`, and Windows
Media Foundation `h264_mf`. The bundled build deliberately disables `libx264`, `libx265`, and other GPL-only
components. The application's `libx264` option therefore requires an explicit user-supplied GPL build and is
never the normal packaged path.

- Official legal and compliance information: <https://ffmpeg.org/legal.html>
- FFmpeg license details: <https://ffmpeg.org/doxygen/trunk/md_LICENSE.html>
- Binary build source and release: <https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-08-06-13-39>
- Pinned FFmpeg source revision: <https://github.com/FFmpeg/FFmpeg/tree/9b6c8969e0>

No paid runtime library is required by Cat Clip Composer. Open-source and public-domain components still retain their respective attribution, source-availability, redistribution, and other license requirements.

The Mr Cat splash photograph is a user-supplied application asset, not a third-party software component.
Custom TTF/OTF files placed in the portable `fonts` folder are not bundled dependencies by default; anyone
redistributing added fonts must follow each font's license.
