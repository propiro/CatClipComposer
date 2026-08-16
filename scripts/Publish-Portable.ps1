[CmdletBinding()]
param(
    [string]$OutputPath = "publish\CatClipComposer",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $false,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
& (Join-Path $PSScriptRoot "Test-XamlStaticResources.ps1")
[xml]$versionProperties = Get-Content `
    -LiteralPath (Join-Path $repositoryRoot "Directory.Build.props") `
    -Raw -Encoding utf8
$applicationVersion = [string]$versionProperties.Project.PropertyGroup.Version
$versionMarkerName = "version_$applicationVersion"
$versionMarkerSource = Join-Path $repositoryRoot $versionMarkerName
$repositoryVersionMarkers = @(Get-ChildItem -LiteralPath $repositoryRoot -File -Filter "version_*")
if ($repositoryVersionMarkers.Count -ne 1 -or
    $repositoryVersionMarkers[0].Name -cne $versionMarkerName) {
    throw "The repository must contain exactly one version marker named '$versionMarkerName'."
}
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
$existingConfigurationPath = Join-Path $resolvedOutput "CatClipComposer.ini"
$preservedConfiguration = if ($Force -and (Test-Path -LiteralPath $existingConfigurationPath)) {
    [System.IO.File]::ReadAllBytes($existingConfigurationPath)
} else {
    $null
}

function Assert-FfmpegPayload([string]$Directory, [bool]$InspectCapabilities) {
    $manifestPath = Join-Path $Directory "MANIFEST.sha256"
    foreach ($requiredName in @(
        "ffmpeg.exe", "ffprobe.exe", "LICENSE.txt", "SOURCE.txt",
        "BUILD_INFO.txt", "MANIFEST.sha256")) {
        if (-not (Test-Path -LiteralPath (Join-Path $Directory $requiredName))) {
            throw "Bundled FFmpeg is incomplete; missing '$requiredName' under '$Directory'."
        }
    }

    $manifestLines = @(Get-Content -LiteralPath $manifestPath -Encoding utf8 |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($manifestLines.Count -eq 0) {
        throw "Bundled FFmpeg manifest is empty: $manifestPath"
    }

    foreach ($line in $manifestLines) {
        if ($line -notmatch "^(?<hash>[0-9a-fA-F]{64})  (?<name>[^\\/]+)$") {
            throw "Invalid bundled FFmpeg manifest entry: $line"
        }

        $filePath = Join-Path $Directory $Matches.name
        if (-not (Test-Path -LiteralPath $filePath)) {
            throw "Bundled FFmpeg manifest file is missing: $filePath"
        }

        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
        if (-not $actualHash.Equals($Matches.hash, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Bundled FFmpeg hash mismatch: $filePath"
        }
    }

    if (-not $InspectCapabilities) {
        return
    }

    $ffmpegExe = Join-Path $Directory "ffmpeg.exe"
    $ffprobeExe = Join-Path $Directory "ffprobe.exe"
    $ffmpegBuildLines = @(& $ffmpegExe -version 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect bundled FFmpeg: $ffmpegExe"
    }
    $ffmpegBuildText = $ffmpegBuildLines -join [Environment]::NewLine
    if ($ffmpegBuildText -match "--enable-gpl" -or
        $ffmpegBuildText -match "--enable-nonfree") {
        throw "The mandatory FFmpeg bundle must not enable GPL or nonfree components."
    }

    $recordedVersion = Get-Content `
        -LiteralPath (Join-Path $Directory "BUILD_INFO.txt") `
        -Encoding utf8 -TotalCount 1
    if (-not $ffmpegBuildLines[0].StartsWith(
        $recordedVersion,
        [System.StringComparison]::Ordinal)) {
        throw "Bundled FFmpeg does not match BUILD_INFO.txt."
    }

    $ffprobeBuildLines = @(& $ffprobeExe -version 2>&1)
    $versionToken = $recordedVersion -replace "^ffmpeg version ", ""
    if ($LASTEXITCODE -ne 0 -or
        $ffprobeBuildLines[0] -notmatch [regex]::Escape($versionToken)) {
        throw "Bundled FFprobe does not match the pinned FFmpeg version."
    }

    $filters = @(& $ffmpegExe -hide_banner -filters 2>&1)
    $encoders = @(& $ffmpegExe -hide_banner -encoders 2>&1)
    foreach ($requiredPattern in @(
        "\bdrawtext\b", "\bhue\b", "\blutyuv\b", "\bgblur\b", "\bblend\b",
        "\bmpeg4\b", "\baac\b", "\bh264_mf\b")) {
        if (-not [bool]($filters -match $requiredPattern) -and
            -not [bool]($encoders -match $requiredPattern)) {
            throw "Bundled FFmpeg lacks a required capability matching '$requiredPattern'."
        }
    }
}

function Assert-FrameworkDependentPublish(
    [string]$Directory,
    [string]$ApplicationName,
    [string]$RequiredFramework) {
    foreach ($requiredName in @(
        "$ApplicationName.exe",
        "$ApplicationName.dll",
        "$ApplicationName.deps.json",
        "$ApplicationName.runtimeconfig.json")) {
        if (-not (Test-Path -LiteralPath (Join-Path $Directory $requiredName))) {
            throw "Framework-dependent publish is missing '$requiredName' under '$Directory'."
        }
    }

    $runtimeConfigurationPath = Join-Path $Directory "$ApplicationName.runtimeconfig.json"
    $runtimeConfiguration = Get-Content -LiteralPath $runtimeConfigurationPath -Raw -Encoding utf8 |
        ConvertFrom-Json
    $runtimeOptions = $runtimeConfiguration.runtimeOptions
    if ($null -eq $runtimeOptions) {
        throw "Framework-dependent publish has no runtimeOptions: $runtimeConfigurationPath"
    }
    if ($null -ne $runtimeOptions.includedFrameworks) {
        throw "Framework-dependent publish unexpectedly embeds .NET frameworks: $runtimeConfigurationPath"
    }

    $frameworks = @()
    if ($null -ne $runtimeOptions.framework) {
        $frameworks += $runtimeOptions.framework
    }
    if ($null -ne $runtimeOptions.frameworks) {
        $frameworks += @($runtimeOptions.frameworks)
    }
    $matchingFramework = @($frameworks | Where-Object {
        $_.name -ceq $RequiredFramework -and
        ([string]$_.version).StartsWith("8.", [System.StringComparison]::Ordinal)
    })
    if ($matchingFramework.Count -ne 1) {
        throw "Framework-dependent publish must require $RequiredFramework 8.x: $runtimeConfigurationPath"
    }
}

function Copy-MergedFrameworkDependentFiles([string]$Source, [string]$Destination) {
    $excludedTopLevelDirectories = @("docs", "fonts", "plugins", "thirdparty")
    $sourcePrefix = [System.IO.Path]::GetFullPath($Source).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File) {
        if (-not $file.FullName.StartsWith(
            $sourcePrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Publish file escaped its source directory: $($file.FullName)"
        }
        $relativePath = $file.FullName.Substring($sourcePrefix.Length)
        $topLevelName = $relativePath.Split(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.StringSplitOptions]::RemoveEmptyEntries)[0]
        if ($topLevelName -in $excludedTopLevelDirectories -or
            $file.Name -like "version_*") {
            continue
        }

        $destinationPath = Join-Path $Destination $relativePath
        if (Test-Path -LiteralPath $destinationPath) {
            $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $destinationHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
            if ($sourceHash -ne $destinationHash) {
                $isSharedWindowsSqliteProvider =
                    $relativePath -ceq "SQLitePCLRaw.provider.e_sqlite3.dll" -and
                    [Reflection.AssemblyName]::GetAssemblyName($file.FullName).FullName -ceq
                    [Reflection.AssemblyName]::GetAssemblyName($destinationPath).FullName
                if (-not $isSharedWindowsSqliteProvider) {
                    throw "GUI and CLI publish outputs conflict at '$relativePath'."
                }
            }
            continue
        }

        $destinationDirectory = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destinationPath
    }
}

$ffmpegSource = Join-Path $repositoryRoot "thirdparty\ffmpeg"
Assert-FfmpegPayload $ffmpegSource $true

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
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )
    if ($SelfContained) {
        $publishArguments += @(
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:EnableCompressionInSingleFile=true")
    } else {
        $publishArguments += "-p:PublishSingleFile=false"
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

    $desktopExe = Join-Path $desktopPublish "CatClipComposer.exe"
    $cliExe = Join-Path $cliPublish "CatClipComposer.Cli.exe"
    $desktopVersionMarker = Join-Path $desktopPublish $versionMarkerName
    $cliVersionMarker = Join-Path $cliPublish $versionMarkerName
    if ($SelfContained) {
        $desktopFiles = @(Get-ChildItem -LiteralPath $desktopPublish -File)
        $cliFiles = @(Get-ChildItem -LiteralPath $cliPublish -File)
        $expectedDesktopNames = @("CatClipComposer.exe", $versionMarkerName)
        $expectedCliNames = @("CatClipComposer.Cli.exe", $versionMarkerName)
        if ($desktopFiles.Count -ne 2 -or
            @($desktopFiles | Where-Object { $_.Name -notin $expectedDesktopNames }).Count -ne 0) {
            throw "Desktop single-file publish produced an unexpected file layout."
        }
        if ($cliFiles.Count -ne 2 -or
            @($cliFiles | Where-Object { $_.Name -notin $expectedCliNames }).Count -ne 0) {
            throw "CLI single-file publish produced an unexpected file layout."
        }
    } else {
        Assert-FrameworkDependentPublish $desktopPublish "CatClipComposer" "Microsoft.WindowsDesktop.App"
        Assert-FrameworkDependentPublish $cliPublish "CatClipComposer.Cli" "Microsoft.NETCore.App"
    }
    $sourceMarkerHash = (Get-FileHash -LiteralPath $versionMarkerSource -Algorithm SHA256).Hash
    foreach ($publishedMarker in @($desktopVersionMarker, $cliVersionMarker)) {
        if (-not (Test-Path -LiteralPath $publishedMarker) -or
            (Get-FileHash -LiteralPath $publishedMarker -Algorithm SHA256).Hash -ne $sourceMarkerHash) {
            throw "Published version marker is missing or changed: $publishedMarker"
        }
    }

    if ($SelfContained) {
        Copy-Item -LiteralPath $desktopExe -Destination $packageRoot
        Copy-Item -LiteralPath $cliExe -Destination $packageRoot
    } else {
        Copy-MergedFrameworkDependentFiles $desktopPublish $packageRoot
        Copy-MergedFrameworkDependentFiles $cliPublish $packageRoot
    }
    Copy-Item -LiteralPath $desktopVersionMarker -Destination $packageRoot
    $pluginPublish = Join-Path $desktopPublish "plugins"
    $builtInPlugin = Join-Path $pluginPublish "CatClipComposer.Plugins.BuiltIn.dll"
    if (-not (Test-Path -LiteralPath $builtInPlugin)) {
        throw "Desktop publish did not include the built-in plugin module: $builtInPlugin"
    }
    Copy-Item -LiteralPath $pluginPublish -Destination $packageRoot -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "CatClipComposer.ini.example") `
        -Destination (Join-Path $packageRoot "CatClipComposer.ini")
    if ($null -ne $preservedConfiguration) {
        [System.IO.File]::WriteAllBytes(
            (Join-Path $packageRoot "CatClipComposer.ini"),
            $preservedConfiguration)
    }
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
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "fonts") `
        -Destination $packageRoot -Recurse -Force
    Assert-FfmpegPayload (Join-Path $packageRoot "thirdparty\ffmpeg") $false
    $packagedVersionMarkers = @(Get-ChildItem -LiteralPath $packageRoot -File -Filter "version_*")
    if ($packagedVersionMarkers.Count -ne 1 -or
        $packagedVersionMarkers[0].Name -cne $versionMarkerName -or
        (Get-FileHash -LiteralPath $packagedVersionMarkers[0].FullName -Algorithm SHA256).Hash -ne
            $sourceMarkerHash) {
        throw "Portable package version marker does not match $versionMarkerName."
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

$packageFlavor = if ($SelfContained) { "full self-contained" } else { "light .NET 8-dependent" }
Write-Host "Portable folder published ($packageFlavor) to: $resolvedOutput"
