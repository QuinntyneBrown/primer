#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds Primer from source when needed and installs it as a .NET global tool.

.DESCRIPTION
    Packs the Primer CLI from this repository and installs it, so the `primer`
    command on this machine is the code in this working tree rather than whatever
    was published to NuGet.

    The build is skipped when nothing has changed: if a package already exists in
    the artifacts directory and no source file is newer than it, that package is
    reused. Pass -Force to rebuild regardless.

    Each build is stamped with a unique prerelease version
    (0.1.0-dev.<timestamp>). A fixed version would be served from the NuGet cache
    on the second install, silently leaving the previous build in place.

.PARAMETER Configuration
    The build configuration to pack. Defaults to Release, which is what the
    published tool is built with.

.PARAMETER Force
    Rebuild and repack even when the existing package is current.

.PARAMETER SkipBuild
    Install the newest package already in the artifacts directory without
    building. Fails if there is none.

.PARAMETER ToolPath
    Install into this directory instead of installing globally. Useful for trying
    a build without changing the `primer` command already on PATH.

.EXAMPLE
    ./eng/scripts/Install-Primer.ps1

    Builds if needed and installs or upgrades the global tool.

.EXAMPLE
    ./eng/scripts/Install-Primer.ps1 -Force

    Rebuilds from scratch and reinstalls.

.EXAMPLE
    ./eng/scripts/Install-Primer.ps1 -ToolPath ./.tools

    Installs into ./.tools rather than onto PATH.
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $Force,

    [switch] $SkipBuild,

    [string] $ToolPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Locate the repository, so the script works from any working directory.
# ---------------------------------------------------------------------------

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$project = Join-Path $repositoryRoot 'src' 'Primer' 'Primer.csproj'
$artifacts = Join-Path $repositoryRoot 'artifacts'
$packageId = 'Primer'

if (-not (Test-Path $project)) {
    throw "Could not find $project. Run this script from inside the primer repository."
}

function Write-Step {
    param([string] $Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is required but `dotnet` was not found on PATH. See https://dotnet.microsoft.com/download.'
}

# global.json pins the SDK, so this reports the version the build will actually use.
$sdkVersion = (& dotnet --version).Trim()
Write-Step "Using .NET SDK $sdkVersion"

# ---------------------------------------------------------------------------
# Decide whether a build is needed
# ---------------------------------------------------------------------------

function Get-NewestPackage {
    if (-not (Test-Path $artifacts)) { return $null }

    return Get-ChildItem -Path $artifacts -Filter "$packageId.*.nupkg" -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Test-PackageIsCurrent {
    param($Package)

    if ($null -eq $Package) { return $false }

    # Anything the packed output depends on. Test sources are deliberately excluded:
    # they cannot change the tool that gets installed.
    $inputs = @(
        Get-ChildItem -Path (Join-Path $repositoryRoot 'src') -Recurse -File -Include '*.cs', '*.csproj', '*.json' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        Get-ChildItem -Path $repositoryRoot -File -Include 'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'README.md' -ErrorAction SilentlyContinue
    )

    $newestInput = $inputs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1

    if ($null -eq $newestInput) { return $true }

    return $newestInput.LastWriteTimeUtc -le $Package.LastWriteTimeUtc
}

$package = Get-NewestPackage

if ($SkipBuild) {
    if ($null -eq $package) {
        throw "-SkipBuild was given but no package was found in $artifacts. Run without -SkipBuild first."
    }

    Write-Step "Skipping build; using $($package.Name)"
}
elseif (-not $Force -and (Test-PackageIsCurrent $package)) {
    Write-Step "Sources unchanged since $($package.Name) was packed; reusing it"
}
else {
    # A unique prerelease version per build, so the install never resolves a
    # cached package from an earlier run.
    $version = '0.1.0-dev.{0}' -f (Get-Date -Format 'yyyyMMddHHmmss')

    Write-Step "Packing $packageId $version ($Configuration)"

    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

    & dotnet pack $project `
        --configuration $Configuration `
        --output $artifacts `
        -p:Version=$version `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE."
    }

    $package = Get-NewestPackage

    if ($null -eq $package) {
        throw "Pack reported success but no package was found in $artifacts."
    }
}

# The version is the part of the file name between the id and the extension.
$packageVersion = $package.BaseName.Substring($packageId.Length + 1)

# ---------------------------------------------------------------------------
# Install
# ---------------------------------------------------------------------------

# Typed as an array on purpose: PowerShell unwraps a single-element array returned
# from `if`, and splatting the resulting string passes it one character at a time.
[string[]] $scope = if ($ToolPath) { '--tool-path', $ToolPath } else { '--global' }
$scopeLabel = if ($ToolPath) { "into $ToolPath" } else { 'globally' }

# Installing over an existing tool fails, and `update` will not move to a
# prerelease it considers older. Removing first makes the outcome deterministic.
$installed = & dotnet tool list @scope 2>$null | Select-String -Pattern "^\s*$packageId\s" -Quiet

if ($installed) {
    Write-Step "Removing the previously installed $packageId"
    & dotnet tool uninstall @scope $packageId | Out-Null
}

Write-Step "Installing $packageId $packageVersion $scopeLabel"

& dotnet tool install @scope `
    --add-source $artifacts `
    --version $packageVersion `
    --ignore-failed-sources `
    $packageId

if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool install failed with exit code $LASTEXITCODE."
}

# ---------------------------------------------------------------------------
# Verify the installed tool actually runs
# ---------------------------------------------------------------------------

$executable = if ($ToolPath) {
    Join-Path $ToolPath ($IsWindows ? 'primer.exe' : 'primer')
}
else {
    'primer'
}

$reported = & $executable --version 2>&1

if ($LASTEXITCODE -ne 0) {
    throw "The tool installed but `primer --version` failed: $reported"
}

Write-Step "Installed: primer $reported"

if (-not $ToolPath) {
    Write-Host ''
    Write-Host 'Try it:' -ForegroundColor Green
    Write-Host '  primer --help'
    Write-Host '  primer init --dry-run'
    Write-Host ''
    Write-Host 'If `primer` is not found, the global tools directory is not on PATH:' -ForegroundColor Yellow
    Write-Host '  $HOME/.dotnet/tools'
}
