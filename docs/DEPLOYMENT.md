# Portable one-folder deployment

Last reviewed: 2026-08-06

`scripts\Publish-Portable.ps1` publishes the WPF editor, headless CLI, managed/native SQLite dependencies, optional self-contained .NET runtime, default INI, documentation, notices, and a separate `thirdparty` tool boundary into one folder.

## Publish

Framework-dependent (requires the .NET 8 Desktop Runtime on the target):

```powershell
.\scripts\Publish-Portable.ps1 -SelfContained $false
```

Self-contained win-x64 (default; includes the .NET runtime):

```powershell
.\scripts\Publish-Portable.ps1
```

Complete an audited FFmpeg tool folder at publish time:

```powershell
.\scripts\Publish-Portable.ps1 `
  -FfmpegDirectory "D:\AuditedTools\ffmpeg"
```

The supplied directory must contain `ffmpeg.exe` and `ffprobe.exe` from one build. Known license/source notice filenames are copied, and `BUILD_INFO.txt` records `ffmpeg -version`/build configuration. If no notice is found, the script warns; that warning must be resolved before release.

## Layout

```text
CatClipComposer/
├── CatClipComposer.exe
├── CatClipComposer.Cli.exe
├── CatClipComposer.ini
├── *.dll and runtime files
├── docs/
└── thirdparty/
    └── ffmpeg/
        ├── ffmpeg.exe
        ├── ffprobe.exe
        ├── BUILD_INFO.txt
        └── exact license/source notices
```

When the INI retains `FfmpegPath=ffmpeg.exe`, both executables first look for `thirdparty\ffmpeg\ffmpeg.exe` beside themselves, then fall back to `PATH`. No machine-wide install or script path rewrite is required.

## Release gate

One-folder capability does not make an arbitrary FFmpeg build redistributable. Before publishing:

1. Inspect `BUILD_INFO.txt` for `--enable-gpl` and `--enable-nonfree`.
2. Reject `--enable-nonfree` for distribution.
3. Prefer an LGPL-compatible build for the normal package; keep GPL builds/user selection outside the normal distribution unless the whole distribution plan has been reviewed for GPL compliance.
4. Include the exact FFmpeg distributor's license, source offer/link, and notices.
5. Preserve `THIRD_PARTY_NOTICES.md` and applicable .NET runtime notices.
6. Run the published CLI help/config and one render using the packaged tool path.

The 2026-08-06 smoke published both framework-dependent and 154 MB self-contained folders, ran the published CLI, copied FFmpeg/FFprobe into `thirdparty`, and rendered a project through automatic packaged-tool discovery. The test FFmpeg was not selected as a release binary; exact-binary licensing remains a release gate.
