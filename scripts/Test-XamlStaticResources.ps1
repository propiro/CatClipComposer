[CmdletBinding()]
param(
    [string]$ProjectPath = "CatClipComposer"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedProject = if ([System.IO.Path]::IsPathRooted($ProjectPath)) {
    [System.IO.Path]::GetFullPath($ProjectPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ProjectPath))
}

if (-not [System.IO.Directory]::Exists($resolvedProject)) {
    throw "XAML project directory does not exist: $resolvedProject"
}

$xamlFiles = @(Get-ChildItem -LiteralPath $resolvedProject -Filter "*.xaml" -File)
if ($xamlFiles.Count -eq 0) {
    throw "No XAML files found under: $resolvedProject"
}

$definitions = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
foreach ($file in $xamlFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($content, 'x:Key="([^"]+)"')) {
        [void]$definitions.Add($match.Groups[1].Value)
    }
}

$missing = [System.Collections.Generic.List[string]]::new()
foreach ($file in $xamlFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($content, '\{StaticResource\s+([^,}\s]+)')) {
        $key = $match.Groups[1].Value
        if (-not $key.StartsWith("{") -and -not $definitions.Contains($key)) {
            $missing.Add("$($file.Name): $key")
        }
    }
}

if ($missing.Count -gt 0) {
    throw "Undefined XAML StaticResource references:$([Environment]::NewLine)$($missing -join [Environment]::NewLine)"
}

Write-Output "XAML StaticResource audit passed: $($definitions.Count) keys across $($xamlFiles.Count) files."
