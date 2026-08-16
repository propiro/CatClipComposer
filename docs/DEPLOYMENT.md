# Portable one-folder deployment

Last reviewed: 2026-08-16

Cat Clip Composer always deploys its pinned FFmpeg runtime. Both normal builds and portable publishes place
the tools under `thirdparty\ffmpeg`, separate from the application executables and configuration.

## Publish

Create the normal light Windows x64 package. It is framework-dependent, shares managed dependencies between
the GUI and CLI, and requires the Microsoft .NET 8 Desktop Runtime x64 on the destination computer:

```powershell
.\scripts\Publish-Portable.ps1
```

Create a full self-contained package only when a release is explicitly meant to carry its own .NET runtime:

```powershell
.\scripts\Publish-Portable.ps1 -SelfContained $true
```

The light package still has native Windows `CatClipComposer.exe` and `CatClipComposer.Cli.exe` apphosts. When
.NET 8 is present, they start normally. If the required runtime is absent, the native .NET host reports the
missing `Microsoft.WindowsDesktop.App` 8.x framework and supplies the appropriate download link before managed
WPF code runs. Install **.NET Desktop Runtime 8, Windows x64** from
<https://dotnet.microsoft.com/en-us/download/dotnet/8.0>; the plain .NET Runtime alone does not contain WPF.

The output folder must be empty. Use `-Force` to replace an earlier generated package explicitly; if that
folder already contains `CatClipComposer.ini`, its exact bytes are carried into the replacement package so
portable preferences and source-folder choices survive an application update. For
safety, repository-local output is accepted only beneath `publish`; filesystem roots, the repository root,
and repository parents are rejected.

There is no application-only or skip-FFmpeg publish mode. The publisher always takes the repository's pinned
payload and validates it before any package is accepted.

## GitHub Releases

Publish the validated light Windows x64 output as GitHub Release assets rather than committing
generated executables to the source branch. Each public release contains:

- `CatClipComposer-v<version>-win-x64-light.zip`
- `CatClipComposer-v<version>-win-x64-light.zip.sha256`

The already-published v0.1.32 unsuffixed ZIP remains the full self-contained package. A future full package
must be deliberately built with `-SelfContained $true` and identified as full in its asset name and notes;
the automated tag workflow defaults to light.

The ZIP contains the complete `CatClipComposer` folder. GitHub's automatically generated source archives are
source checkouts, not end-user application packages.

After the version change, automated verification, commit/push, and the user's manual acceptance checklist, create
and push an annotated tag that exactly matches `Directory.Build.props`:

```powershell
git tag -a v0.1.34 -m "Cat Clip Composer v0.1.34"
git push origin v0.1.34
```

Replace `0.1.34` with the central version. Do not reuse or move a published version tag; increment the
application version for another release. `.github/workflows/release.yml` then runs on `windows-latest`, hydrates
Git LFS, validates the tag/version and exact FFmpeg payload, calls the same portable publisher used locally,
checks the CLI/marker, creates the complete ZIP plus lowercase SHA-256 file, and publishes both through the
preinstalled GitHub CLI using the workflow's short-lived `GITHUB_TOKEN`. The two official actions are pinned to
immutable commit SHAs and persisted checkout credentials are disabled.

Do not push the tag merely to test packaging: a matching tag intentionally creates the public downloadable
Release. If the workflow fails, fix the cause, increment the version, and use a new tag rather than moving a
published release tag.

The application is not code-signed yet, so Windows can show an unknown-publisher SmartScreen prompt. Release
notes and the public README disclose this and direct users to verify the SHA-256 asset rather than disabling
Windows security.

## Package layout

```text
CatClipComposer/
|-- CatClipComposer.exe
|-- CatClipComposer.dll
|-- CatClipComposer.deps.json
|-- CatClipComposer.runtimeconfig.json
|-- CatClipComposer.Cli.exe
|-- CatClipComposer.Cli.dll
|-- CatClipComposer.Cli.deps.json
|-- CatClipComposer.Cli.runtimeconfig.json
|-- shared application and SQLite DLLs
|-- version_<version>
|-- CatClipComposer.ini
|-- fonts/
|   `-- README.txt
|-- plugins/
|   `-- CatClipComposer.Plugins.BuiltIn.dll
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

The light root contains the GUI/CLI apphosts, their small application assemblies/runtime metadata, shared
application and native SQLite dependencies, the extensionless `version_<version>` marker, and portable INI,
plus organized `fonts`, `plugins`, `docs`, and `thirdparty` subfolders. These root files are one application
unit; copying only an EXE will not work. The marker contains a short changelist, and its changing filename lets
users verify an extracted build was actually replaced without launching it. A full build instead bundles the
application assemblies, SQLite dependency, and .NET runtime inside its two single-file executables.
Plugin assemblies remain replaceable under `plugins`; the publisher requires the built-in module assembly.
FFmpeg's shared runtime files stay together in their own folder and can be replaced with an
interface-compatible build as required by the applicable license.

The portable `fonts` folder is copied into every GUI build and package. Users can add TTF/OTF files there;
font files are not embedded in `.nya` documents and remain subject to their own redistribution licenses.

When the INI keeps its default `FfmpegPath=ffmpeg.exe`, GUI and CLI resolve
`thirdparty\ffmpeg\ffmpeg.exe` beside the application. An explicit configured path remains a local user
override; the mandatory packaged payload is still included.

## Using another FFmpeg locally

The packaged runtime is the recommended and reproducible default. A user may override it under
**Preferences > FFmpeg executable** with another compatible local build. Keep that build's `ffmpeg.exe`,
matching `ffprobe.exe`, and any shared DLLs together; selecting an executable does not copy its files into
the Cat Clip Composer package.

FFmpeg's [official download page](https://ffmpeg.org/download.html) links Windows binary providers. The
[BtbN release list](https://github.com/BtbN/FFmpeg-Builds/releases) is the source used for this project's
pinned payload; a `win64-lgpl-shared-8.1` archive is the closest replacement. Custom builds need `drawtext`,
native `mpeg4`, and native `aac`; the Media Foundation H.264 preset also needs `h264_mf`.

A local override does not alter the license of the repository's pinned payload. Anyone redistributing a
different build must audit that binary's reported configuration and satisfy its actual terms. FFmpeg's
[license details](https://ffmpeg.org/doxygen/trunk/md_LICENSE.html) explain the LGPL/GPL build distinction,
and its [legal guidance](https://ffmpeg.org/legal.html) states that `--enable-nonfree` builds are not
redistributable.

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

1. Exactly one checked-in extensionless version marker matches `Directory.Build.props`.
2. GUI and CLI publish outputs contain byte-identical copies beside their executables, with no stale marker.
3. Required FFmpeg executables, records, and license files exist.
4. Every runtime/license SHA-256 matches `MANIFEST.sha256`.
5. FFmpeg and FFprobe report the pinned version.
6. FFmpeg reports neither `--enable-gpl` nor `--enable-nonfree`.
7. `drawtext`, native `mpeg4`, native `aac`, and `h264_mf` are available.
8. The copied package payload still matches its manifest.
9. The portable custom-font folder is included separately from the application executables.
10. The built-in plugin module assembly is included under `plugins`.
11. A light publish contains native GUI/CLI apphosts plus `.deps.json` and `.runtimeconfig.json` files.
12. The GUI runtime contract requires `Microsoft.WindowsDesktop.App` 8.x, the CLI requires
    `Microsoft.NETCore.App` 8.x, and neither runtime configuration embeds a self-contained framework.

## Updating FFmpeg

An upgrade is a reviewed dependency change, not an arbitrary file replacement. Replace all executables and
DLLs from one build, update the license/source/build/hash records, rerun the dependency and license audit,
perform scan/preview/render smokes, update component documentation, and commit the payload through Git LFS.
