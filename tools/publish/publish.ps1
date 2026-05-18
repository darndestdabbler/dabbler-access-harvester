<#
.SYNOPSIS
    Publishes the AccessHarvester CLI as a self-contained Windows binary.

.DESCRIPTION
    Runs `dotnet publish` against AccessHarvester.Cli with the publish
    profile that bakes the runtime, native deps, and single-file
    wrapper into one self-extracting executable. The output directory
    is gitignored. Re-running against the same source produces a
    byte-identical artifact (Deterministic=true is the .NET SDK default
    in .NET 10; the SDK strips the build-time PE timestamp by default).

    The artifact lives at:
        publish/win-x64/AccessHarvester.exe

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER RuntimeIdentifier
    Target RID. Defaults to win-x64; the harvester is Windows-only by
    COM constraint, so a non-win-* RID would be misleading.

.PARAMETER OutputDir
    Override the publish destination. Defaults to
    `publish/<rid>/` under the repo root.

.EXAMPLE
    pwsh tools/publish/publish.ps1

.EXAMPLE
    pwsh tools/publish/publish.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDir = ""
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
