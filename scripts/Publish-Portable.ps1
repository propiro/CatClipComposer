[CmdletBinding()]
param(
    [string]$OutputPath = "publish\CatClipComposer",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$FfmpegDirectory,
    [switch]$SkipFfmpeg,
    [switch]$AllowGplFfmpeg,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}

$trimmedRepositoryRoot = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$trimmedOutput = $resolvedOutput.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$repositoryPrefix = $trimmedRepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
$outputPrefix = $trimmedOutput + [System.IO.Path]::DirectorySeparatorChar
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "publish"))
$publishPrefix = $publishRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$volumeRoot = [System.IO.Path]::GetPathRoot($resolvedOutput).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar)

if ($trimmedOutput.Equals($volumeRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $trimmedOutput.Equals($trimmedRepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $repositoryPrefix.StartsWith($outputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to publish over a filesystem or repository root: $resolvedOutput"
}

if ($outputPrefix.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
    -not $trimmedOutput.Equals($publishRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
    -not $outputPrefix.StartsWith($publishPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Repository-local publish output must be under '$publishRoot': $resolvedOutput"
}

$existingEntries = if (Test-Path -LiteralPath $resolvedOutput) {
    @(Get-ChildItem -LiteralPath $resolvedOutput -Force)
} else {
    @()
}
if ($existingEntries.Count -gt 0 -and -not $Force) {
    throw "Publish output is not empty. Re-run with -Force to replace it: $resolvedOutput"
}

if ($SkipFfmpeg -and -not [string]::IsNullOrWhiteSpace($FfmpegDirectory)) {
    throw "Use either -FfmpegDirectory or -SkipFfmpeg, not both."
}

$stagingRoot = Join-Path (
    [System.IO.Path]::GetTempPath()) (
    "CatClipComposer-Publish-" + [Guid]::NewGuid().ToString("N"))
$desktopPublish = Join-Path $stagingRoot "desktop"
$cliPublish = Join-Path $stagingRoot "cli"
$packageRoot = Join-Path $stagingRoot "package"

try {
    New-Item -ItemType Directory -Force -Path $desktopPublish,$cliPublish,$packageRoot | Out-Null
    $publishArguments = @(
        "publish",
        "--configuration", "Release",
        "--runtime", $Runtime,
        "--self-contained", $SelfContained.ToString().ToLowerInvariant(),
        "--nologo",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )
    if ($SelfContained) {
        $publishArguments += "-p:EnableCompressionInSingleFile=true"
    }

    & dotnet @publishArguments --output $desktopPublish (
        Join-Path $repositoryRoot "CatClipComposer\CatClipComposer.csproj")
    if ($LASTEXITCODE -ne 0) {
        throw "Desktop publish failed with exit code $LASTEXITCODE."
    }

    & dotnet @publishArguments --output $cliPublish (
        Join-Path $repositoryRoot "CatClipComposer.Cli\CatClipComposer.Cli.csproj")
    if ($LASTEXITCODE -ne 0) {
        throw "CLI publish failed with exit code $LASTEXITCODE."
    }

    $desktopFiles = @(Get-ChildItem -LiteralPath $desktopPublish -File)
    $cliFiles = @(Get-ChildItem -LiteralPath $cliPublish -File)
    if ($desktopFiles.Count -ne 1 -or $desktopFiles[0].Name -ne "CatClipComposer.exe") {
        throw "Desktop single-file publish produced an unexpected file layout."
    }
    if ($cliFiles.Count -ne 1 -or $cliFiles[0].Name -ne "CatClipComposer.Cli.exe") {
        throw "CLI single-file publish produced an unexpected file layout."
    }

    Copy-Item -LiteralPath $desktopFiles[0].FullName -Destination $packageRoot
    Copy-Item -LiteralPath $cliFiles[0].FullName -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "CatClipComposer.ini.example") `
        -Destination (Join-Path $packageRoot "CatClipComposer.ini")
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs") `
        -Destination $packageRoot -Recurse -Force
    $packagedReadme = Get-Content `
        -LiteralPath (Join-Path $repositoryRoot "README.md") -Raw -Encoding utf8
    $packagedReadme.Replace("(docs/", "(") | Set-Content `
        -LiteralPath (Join-Path $packageRoot "docs\README.md") -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "THIRD_PARTY_NOTICES.md") `
        -Destination (Join-Path $packageRoot "docs\THIRD_PARTY_NOTICES.md") -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "thirdparty") `
        -Destination $packageRoot -Recurse -Force

    if (-not $SkipFfmpeg) {
        $resolvedFfmpeg = if (-not [string]::IsNullOrWhiteSpace($FfmpegDirectory)) {
            [System.IO.Path]::GetFullPath($FfmpegDirectory)
        } else {
            [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "thirdparty\ffmpeg"))
        }
        $ffmpegToolDirectory = if (
            Test-Path -LiteralPath (Join-Path $resolvedFfmpeg "ffmpeg.exe")) {
            $resolvedFfmpeg
        } else {
            Join-Path $resolvedFfmpeg "bin"
        }
        $ffmpegExe = Join-Path $ffmpegToolDirectory "ffmpeg.exe"
        $ffprobeExe = Join-Path $ffmpegToolDirectory "ffprobe.exe"
        if (-not (Test-Path -LiteralPath $ffmpegExe) -or
            -not (Test-Path -LiteralPath $ffprobeExe)) {
            throw (
                "A complete portable package requires ffmpeg.exe and ffprobe.exe. " +
                "Supply -FfmpegDirectory or use -SkipFfmpeg for an application-only package.")
        }

        $ffmpegBuildLines = @(& $ffmpegExe -version 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect the selected FFmpeg executable: $ffmpegExe"
        }
        $ffmpegBuildText = $ffmpegBuildLines -join [Environment]::NewLine
        if ($ffmpegBuildText -match "--enable-nonfree") {
            throw "The selected FFmpeg build uses --enable-nonfree and cannot be packaged."
        }
        if ($ffmpegBuildText -match "--enable-gpl" -and -not $AllowGplFfmpeg) {
            throw (
                "The selected FFmpeg build uses GPL components. Choose an LGPL build or " +
                "pass -AllowGplFfmpeg for an explicit personal/opt-in package.")
        }

        $ffmpegTarget = Join-Path $packageRoot "thirdparty\ffmpeg"
        New-Item -ItemType Directory -Force -Path $ffmpegTarget | Out-Null
        Copy-Item -LiteralPath $ffmpegExe,$ffprobeExe -Destination $ffmpegTarget -Force
        $noticeCopied = $false
        $noticeDirectories = @($resolvedFfmpeg, $ffmpegToolDirectory) | Select-Object -Unique
        foreach ($noticeDirectory in $noticeDirectories) {
            foreach ($noticeName in @(
                "LICENSE", "LICENSE.txt", "COPYING.LGPLv2.1", "COPYING.GPLv3",
                "SOURCE.txt", "README.txt")) {
                $noticePath = Join-Path $noticeDirectory $noticeName
                if (Test-Path -LiteralPath $noticePath) {
                    Copy-Item -LiteralPath $noticePath -Destination $ffmpegTarget -Force
                    $noticeCopied = $true
                }
            }
        }
        $ffmpegBuildText | Set-Content `
            -LiteralPath (Join-Path $ffmpegTarget "BUILD_INFO.txt") -Encoding utf8
        if (-not $noticeCopied) {
            Write-Warning (
                "FFmpeg was copied without a nearby license/source notice. " +
                "Add the exact distributor notices before release.")
        }
    }

    if (Test-Path -LiteralPath $resolvedOutput) {
        Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null
    Get-ChildItem -LiteralPath $packageRoot -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $resolvedOutput -Recurse -Force
    }
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

Write-Host "Portable folder published to: $resolvedOutput"
