[CmdletBinding()]
param(
    [string]$OutputPath = "publish\CatClipComposer",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$FfmpegDirectory
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}

New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null
$publishArguments = @(
    "publish",
    "--configuration", "Release",
    "--runtime", $Runtime,
    "--self-contained", $SelfContained.ToString().ToLowerInvariant(),
    "--output", $resolvedOutput
)

& dotnet @publishArguments (Join-Path $repositoryRoot "CatClipComposer\CatClipComposer.csproj")
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed with exit code $LASTEXITCODE." }

& dotnet @publishArguments (Join-Path $repositoryRoot "CatClipComposer.Cli\CatClipComposer.Cli.csproj")
if ($LASTEXITCODE -ne 0) { throw "CLI publish failed with exit code $LASTEXITCODE." }

$deployedConfig = Join-Path $resolvedOutput "CatClipComposer.ini"
if (-not (Test-Path -LiteralPath $deployedConfig)) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "CatClipComposer.ini.example") -Destination $deployedConfig
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot "README.md") -Destination $resolvedOutput -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "THIRD_PARTY_NOTICES.md") -Destination $resolvedOutput -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs") -Destination $resolvedOutput -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "thirdparty") -Destination $resolvedOutput -Recurse -Force

if (-not [string]::IsNullOrWhiteSpace($FfmpegDirectory)) {
    $resolvedFfmpeg = [System.IO.Path]::GetFullPath($FfmpegDirectory)
    $ffmpegExe = Join-Path $resolvedFfmpeg "ffmpeg.exe"
    $ffprobeExe = Join-Path $resolvedFfmpeg "ffprobe.exe"
    if (-not (Test-Path -LiteralPath $ffmpegExe) -or -not (Test-Path -LiteralPath $ffprobeExe)) {
        throw "The FFmpeg directory must contain both ffmpeg.exe and ffprobe.exe: $resolvedFfmpeg"
    }

    $ffmpegTarget = Join-Path $resolvedOutput "thirdparty\ffmpeg"
    New-Item -ItemType Directory -Force -Path $ffmpegTarget | Out-Null
    Copy-Item -LiteralPath $ffmpegExe,$ffprobeExe -Destination $ffmpegTarget -Force
    $noticeCopied = $false
    foreach ($noticeName in @("LICENSE.txt", "COPYING.LGPLv2.1", "COPYING.GPLv3", "SOURCE.txt")) {
        $noticePath = Join-Path $resolvedFfmpeg $noticeName
        if (Test-Path -LiteralPath $noticePath) {
            Copy-Item -LiteralPath $noticePath -Destination $ffmpegTarget -Force
            $noticeCopied = $true
        }
    }
    & $ffmpegExe -version | Set-Content -LiteralPath (Join-Path $ffmpegTarget "BUILD_INFO.txt") -Encoding utf8
    if (-not $noticeCopied) {
        Write-Warning "FFmpeg executables were copied, but no license/source notice was found beside them. Add the exact distributor notices before release."
    }
}

Write-Host "Portable folder published to: $resolvedOutput"
