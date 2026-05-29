<#
.SYNOPSIS
    Publishes the AccessHarvester CLI as a self-contained Windows binary
    and (by default) wraps it into a release-ready zip + SHA-256 sidecar.

.DESCRIPTION
    Runs `dotnet publish` against AccessHarvester.Cli with the publish
    profile that bakes the runtime, native deps, and single-file
    wrapper into one self-extracting executable. The output directory
    is gitignored. Re-running against the same source produces a
    byte-identical artifact (Deterministic=true is the .NET SDK default
    in .NET 10; the SDK strips the build-time PE timestamp by default).

    Outputs:
        publish/win-x64/AccessHarvester.exe
            The bare self-contained binary. Used by the integration
            tests' PackagedBinarySmokeTests, by local maintainer
            invocations, and as the input to the release bundle step.

        publish/release/AccessHarvester-<semver>-schema-v<schema>-win-x64.zip
        publish/release/AccessHarvester-<semver>-schema-v<schema>-win-x64.zip.sha256
            The release bundle a downstream consumer pulls from the
            chosen distribution channel (per the
            harvester-cli-distribution session set: GitHub Releases).
            Built deterministically — every entry's LastWriteTime is
            pinned to a fixed epoch and entries are written in
            alphabetical name order, so two consecutive publishes from
            the same source produce byte-identical zips + identical
            SHA-256.

    The bundle filename encodes BOTH the harvester semver and the
    schema version, read at publish time from the binary's --version
    output (the CLI is the runtime authority per docs/harvester/install.md).

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER RuntimeIdentifier
    Target RID. Defaults to win-x64; the harvester is Windows-only by
    COM constraint, so a non-win-* RID would be misleading.

.PARAMETER OutputDir
    Override the bare-binary publish destination. Defaults to
    `publish/<rid>/` under the repo root. The release bundle always
    lands at `publish/release/`.

.PARAMETER SkipBundle
    Skip the zip + SHA-256 step. Use for fast iteration when you only
    need the bare exe (e.g., running smoke tests locally).

.EXAMPLE
    pwsh tools/publish/publish.ps1

.EXAMPLE
    pwsh tools/publish/publish.ps1 -SkipBundle
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDir = "",
    [switch]$SkipBundle
)

$ErrorActionPreference = "Stop"

# Resolve repo root: this script lives at <repo>/tools/publish/publish.ps1
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$cliProject = Join-Path $repoRoot "AccessHarvester.Cli\AccessHarvester.Cli.csproj"

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "publish\$RuntimeIdentifier"
}

if (Test-Path $OutputDir) {
    Write-Host "[publish] Clearing prior output at $OutputDir"
    Remove-Item -Recurse -Force $OutputDir
}

Write-Host "[publish] Project       : $cliProject"
Write-Host "[publish] Configuration : $Configuration"
Write-Host "[publish] Runtime       : $RuntimeIdentifier"
Write-Host "[publish] Output        : $OutputDir"
Write-Host ""

& dotnet publish $cliProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $OutputDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $OutputDir "AccessHarvester.exe"
if (-not (Test-Path $exe)) {
    throw "Expected artifact not found at $exe"
}

$size = (Get-Item $exe).Length
Write-Host ""
Write-Host "[publish] OK -> $exe ($([math]::Round($size / 1MB, 1)) MB)"

if ($SkipBundle) {
    Write-Host "[publish] -SkipBundle set; skipping release bundle step."
    return
}

# ------------------------------------------------------------------
# Release bundle: deterministic zip + SHA-256 sidecar
# ------------------------------------------------------------------

Write-Host ""
Write-Host "[bundle] Starting release-bundle step"

# Pull semver + schema version from the binary's --version output. The
# CLI is the runtime authority per docs/harvester/install.md; reading
# from the binary (rather than the csproj) guarantees the filename and
# the runtime --version agree.
$versionOutput = & $exe --version
if ($LASTEXITCODE -ne 0) {
    throw "Reading --version from $exe failed with exit code $LASTEXITCODE"
}
if ($versionOutput -notmatch 'AccessHarvester\s+(?<semver>\S+)\s+\(schema\s+v(?<schema>\S+)\)') {
    throw "Could not parse --version output: '$versionOutput'"
}
$harvesterVersion = $matches['semver']
$schemaVersion = $matches['schema']

$bundleStem = "AccessHarvester-$harvesterVersion-schema-v$schemaVersion-$RuntimeIdentifier"
$releaseDir = Join-Path $repoRoot "publish\release"

if (Test-Path $releaseDir) {
    Write-Host "[bundle] Clearing prior release output at $releaseDir"
    Remove-Item -Recurse -Force $releaseDir
}
New-Item -ItemType Directory -Path $releaseDir | Out-Null

# Stage the bundle contents in a temp subdirectory so the zip's entry
# names mirror what a consumer sees on unzip — exe + notes + license
# pointer sit at the archive root, not under a leading folder.
$stagingDir = Join-Path $releaseDir "_staging"
New-Item -ItemType Directory -Path $stagingDir | Out-Null

Copy-Item $exe (Join-Path $stagingDir "AccessHarvester.exe")

$releaseNotesPath = Join-Path $stagingDir "RELEASE-NOTES.md"
$releaseNotesContent = @"
# AccessHarvester $harvesterVersion (schema v$schemaVersion)

Self-contained Windows distribution of the Dabbler Access Harvester
CLI. Single-file ``AccessHarvester.exe`` carries its own .NET runtime,
native SQLite dependencies, and managed payload.

Per-release notes are populated at tag time by the maintainer (or by
the GitHub Actions release workflow when CI is configured). This file
shipping with placeholder content means the bundle was produced by a
manual maintainer publish rather than a tagged-release CI run.

See ``docs/harvester/install.md`` in the source repository for the
canonical install + verification procedure.

Source: https://github.com/darndestdabbler/dabbler-access-harvester
"@
Set-Content -Path $releaseNotesPath -Value $releaseNotesContent -Encoding UTF8 -NoNewline

$licensePath = Join-Path $stagingDir "LICENSE.txt"
$licenseContent = @"
The Dabbler Access Harvester is distributed by darndestdabbler.

This bundle ships a binary built from the source at:
    https://github.com/darndestdabbler/dabbler-access-harvester

For the current license terms applicable to this binary, consult the
source repository at the commit corresponding to this release's tag.
The bundle filename encodes the released semver
(AccessHarvester-<semver>-schema-v<schema>-win-x64.zip); the matching
git tag is v<semver>.
"@
Set-Content -Path $licensePath -Value $licenseContent -Encoding UTF8 -NoNewline

# Build the zip deterministically. The .NET runtime ships
# System.IO.Compression so no external dependency is required; the
# manual entry-build path is the only reliable way to pin
# LastWriteTime (which the high-level ZipFile.CreateFromDirectory
# helper inherits from the source files' mtime, defeating
# determinism after a dotnet publish that re-touches every output).
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zipPath = Join-Path $releaseDir "$bundleStem.zip"
$fixedEpoch = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)

$zip = [System.IO.Compression.ZipFile]::Open(
    $zipPath,
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $stagedFiles = Get-ChildItem $stagingDir -Recurse -File | Sort-Object -Property Name
    foreach ($f in $stagedFiles) {
        $entryName = $f.Name
        $entry = $zip.CreateEntry($entryName,
            [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $fixedEpoch
        $writeStream = $entry.Open()
        try {
            $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
            $writeStream.Write($bytes, 0, $bytes.Length)
        } finally {
            $writeStream.Dispose()
        }
    }
} finally {
    $zip.Dispose()
}

Remove-Item -Recurse -Force $stagingDir

$zipSize = (Get-Item $zipPath).Length
Write-Host "[bundle] Zip   -> $zipPath ($([math]::Round($zipSize / 1MB, 1)) MB)"

# SHA-256 sidecar in the standard `<hash>  <filename>` two-space format
# that `sha256sum -c` and Get-FileHash both consume cleanly. Single line,
# LF terminator for cross-platform tolerance.
$hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash.ToLowerInvariant()
$sidecarPath = "$zipPath.sha256"
$sidecarLine = "$hash  $(Split-Path $zipPath -Leaf)`n"
[System.IO.File]::WriteAllText($sidecarPath, $sidecarLine, [System.Text.UTF8Encoding]::new($false))

Write-Host "[bundle] SHA256 -> $sidecarPath"
Write-Host "[bundle]   $hash"
Write-Host ""
Write-Host "[bundle] OK"
