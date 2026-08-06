# Portable one-folder deployment

Last reviewed: 2026-08-06

`scripts\Publish-Portable.ps1` publishes the WPF editor, headless CLI, managed/native SQLite dependencies, optional self-contained .NET runtime, default INI, documentation, notices, and a separate `thirdparty` tool boundary into one folder. Managed dependencies and, for self-contained builds, the .NET runtime are bundled into the two application executables instead of being scattered through the package root.

## Publish

Complete self-contained win-x64 package with an audited LGPL-compatible FFmpeg build:

```powershell
.\scripts\Publish-Portable.ps1 `
  -FfmpegDirectory "D:\AuditedTools\ffmpeg"
```

Framework-dependent package (requires the .NET 8 Desktop Runtime on the target):

```powershell
.\scripts\Publish-Portable.ps1 `
  -SelfContained $false `
  -FfmpegDirectory "D:\AuditedTools\ffmpeg"
```

An application-only package may deliberately omit FFmpeg:

```powershell
.\scripts\Publish-Portable.ps1 -SkipFfmpeg
```

Without `-SkipFfmpeg`, the supplied directory—or the repository's `thirdparty\ffmpeg` folder—must contain `ffmpeg.exe` and `ffprobe.exe` from one build, either directly or in a `bin` child. The publisher rejects `--enable-nonfree` builds and rejects GPL-enabled builds unless `-AllowGplFfmpeg` is explicitly supplied for a personal/opt-in package. Known license/source notice filenames are copied from the distribution/tool folder, and `BUILD_INFO.txt` records `ffmpeg -version`/build configuration. A missing notice produces a warning that must be resolved before release.

The output folder must be empty. Use `-Force` to explicitly replace a prior generated package. For safety, repository-local outputs are accepted only beneath `publish`; filesystem roots, repository roots, and repository parents are rejected.

## Layout

```text
CatClipComposer/
|-- CatClipComposer.exe
|-- CatClipComposer.Cli.exe
|-- CatClipComposer.ini
|-- docs/
|   |-- README.md
|   `-- THIRD_PARTY_NOTICES.md
`-- thirdparty/
    `-- ffmpeg/
        |-- ffmpeg.exe
        |-- ffprobe.exe
        |-- BUILD_INFO.txt
        `-- exact license/source notices
```

The root contains only the two executable entry points, the portable INI, and organized documentation/tool folders. Application assemblies, native SQLite, and the optional runtime live inside the single-file executables. When the INI retains `FfmpegPath=ffmpeg.exe`, both programs first look for `thirdparty\ffmpeg\ffmpeg.exe` beside themselves, then fall back to `PATH` for an explicitly application-only package.

## Release gate

One-folder capability does not make an arbitrary FFmpeg build redistributable. Before publishing:

1. Inspect `BUILD_INFO.txt` for `--enable-gpl` and `--enable-nonfree`.
2. Reject `--enable-nonfree` for distribution.
3. Prefer an LGPL-compatible build for the normal package; keep GPL builds/user selection outside the normal distribution unless the whole distribution plan has been reviewed for GPL compliance.
4. Include the exact FFmpeg distributor's license, source offer/link, and notices.
5. Preserve `docs\THIRD_PARTY_NOTICES.md` and applicable .NET runtime notices.
6. Run the published CLI help/config and one render using the packaged tool path.

The 2026-08-06 smoke published both framework-dependent and self-contained packages, ran the published CLI, copied FFmpeg/FFprobe into `thirdparty`, and rendered a project through automatic packaged-tool discovery. A later packaging smoke verified that the application/runtime payload is limited to the two root executables. The test FFmpeg was not selected as a release binary; exact-binary licensing remains a release gate.
