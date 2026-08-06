# Third-party deployment boundary

The portable publisher copies external runtime tools under this folder so their files, licenses, and origin remain distinct from Cat Clip Composer.

Expected layout for a complete portable package:

```text
thirdparty/
└── ffmpeg/
    ├── ffmpeg.exe
    ├── ffprobe.exe
    ├── BUILD_INFO.txt
    └── exact distributor license/source notices
```

Cat Clip Composer automatically discovers `thirdparty\ffmpeg\ffmpeg.exe` when the INI uses the default `ffmpeg.exe` value. `ffprobe.exe` must be from the same build and sit beside it.

Do not distribute an arbitrary FFmpeg download without auditing its `ffmpeg -buildconf` output and accompanying license. FFmpeg is LGPL 2.1-or-later by default, but a build containing GPL components becomes GPL. The application defaults to the native MPEG-4 encoder, offers Windows Media Foundation H.264 when the build supports it, and labels libx264 as explicit GPL opt-in.

The repository intentionally contains no third-party executable. Pass an audited FFmpeg directory to `scripts\Publish-Portable.ps1` when preparing a complete distribution; `-SkipFfmpeg` creates an explicitly application-only package instead.
