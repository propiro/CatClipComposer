# Portable one-folder deployment

Last reviewed: 2026-08-06

Cat Clip Composer always deploys its pinned FFmpeg runtime. Both normal builds and portable publishes place
the tools under `thirdparty\ffmpeg`, separate from the application executables and configuration.

## Publish

Create the normal self-contained Windows x64 package:

```powershell
.\scripts\Publish-Portable.ps1
```

Create a framework-dependent package that requires the .NET 8 Desktop Runtime:

```powershell
.\scripts\Publish-Portable.ps1 -SelfContained $false
```

The output folder must be empty. Use `-Force` to replace an earlier generated package explicitly. For
safety, repository-local output is accepted only beneath `publish`; filesystem roots, the repository root,
and repository parents are rejected.

There is no application-only or skip-FFmpeg publish mode. The publisher always takes the repository's pinned
payload and validates it before any package is accepted.

## Package layout

```text
CatClipComposer/
|-- CatClipComposer.exe
|-- CatClipComposer.Cli.exe
|-- CatClipComposer.ini
|-- docs/
|   |-- README.md
|   `-- THIRD_PARTY_NOTICES.md
`-- thirdparty/
    |-- README.md
    `-- ffmpeg/
        |-- ffmpeg.exe
        |-- ffprobe.exe
        |-- avcodec-62.dll and the other required shared DLLs
        |-- LICENSE.txt
        |-- SOURCE.txt
        |-- BUILD_INFO.txt
        `-- MANIFEST.sha256
```

The root contains only the GUI/CLI entry points, portable INI, documentation, and the third-party boundary.
Application assemblies, native SQLite, and the optional .NET runtime remain inside the single-file programs.
FFmpeg's shared runtime files stay together in their own folder and can be replaced with an
interface-compatible build as required by the applicable license.

When the INI keeps its default `FfmpegPath=ffmpeg.exe`, GUI and CLI resolve
`thirdparty\ffmpeg\ffmpeg.exe` beside the application. An explicit configured path remains a local user
override; the mandatory packaged payload is still included.

## Pinned FFmpeg payload

The repository carries BtbN's Windows x64 LGPL shared build:

- Version: `n8.1.2-34-g9b6c8969e0-20260806`
- Release: `autobuild-2026-08-06-13-39`
- Archive SHA-256: `97e1af03208a4582c26d5f3e670ab51af50b8d5788da78231aae218a7c917d56`
- License supplied by the distribution: LGPL v3
- GPL/nonfree flags: absent

`SOURCE.txt` contains the pinned release/archive and upstream source URLs. `BUILD_INFO.txt` records the exact
configuration and audited capabilities. `MANIFEST.sha256` records every executable, runtime DLL, and license
file copied from the archive.

The binary payload is stored with Git LFS. A checkout used for building or publishing must hydrate LFS files;
the build will otherwise lack a runnable tool payload and the publisher's hash check will fail.

## Publisher checks

Before publishing, the script verifies:

1. Required executables, records, and license files exist.
2. Every runtime/license SHA-256 matches `MANIFEST.sha256`.
3. FFmpeg and FFprobe report the pinned version.
4. FFmpeg reports neither `--enable-gpl` nor `--enable-nonfree`.
5. `drawtext`, native `mpeg4`, native `aac`, and `h264_mf` are available.
6. The copied package payload still matches its manifest.

## Updating FFmpeg

An upgrade is a reviewed dependency change, not an arbitrary file replacement. Replace all executables and
DLLs from one build, update the license/source/build/hash records, rerun the dependency and license audit,
perform scan/preview/render smokes, update component documentation, and commit the payload through Git LFS.
