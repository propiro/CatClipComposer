# Third-party deployment boundary

This folder contains runtime components kept separate from Cat Clip Composer's own executables.

`ffmpeg\` contains the mandatory pinned FFmpeg/FFprobe Windows x64 shared runtime. Its executables and DLLs
are tracked with Git LFS and copied automatically into GUI output, CLI output, and every portable package.

The accompanying files are part of the deployable payload:

- `LICENSE.txt` — exact LGPL v3 text supplied by the selected distribution.
- `SOURCE.txt` — distributor release, archive hash, and upstream source locations.
- `BUILD_INFO.txt` — version, configuration flags, library versions, and required capability audit.
- `MANIFEST.sha256` — hashes for every copied executable, DLL, and license file.

The default INI value `FfmpegPath=ffmpeg.exe` resolves to `thirdparty\ffmpeg\ffmpeg.exe`. FFprobe and all
shared DLLs must stay beside it. Do not replace only one file; upgrades require a complete one-build payload,
updated records, license/dependency audit, and render verification.
