# Third-party components

This file is an engineering inventory, not legal advice. Verify the exact notices included by the chosen deployment and FFmpeg build before publishing binaries.

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

FFmpeg is not bundled by this repository. The user configures external `ffmpeg.exe` and `ffprobe.exe` executables.

FFmpeg is LGPL 2.1-or-later by default. Enabling optional GPL components makes the corresponding build GPL. Builds configured with `--enable-nonfree` are not redistributable. Consult the configuration printed by `ffmpeg -version` and follow the obligations of the exact binary distribution.

The current MP4 renderer uses `libx264`; FFmpeg identifies that library as a GPL component, so a GPL-enabled FFmpeg build is required for export.

- Official legal and compliance information: <https://ffmpeg.org/legal.html>
- FFmpeg license details: <https://ffmpeg.org/doxygen/trunk/md_LICENSE.html>

No paid runtime library is required by Cat Clip Composer. Open-source and public-domain components still retain their respective attribution, source-availability, redistribution, and other license requirements.
