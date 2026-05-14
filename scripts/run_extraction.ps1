<#
.SYNOPSIS
    Drives AccessObjectExtractor.accdb's RunExtractionRelatively procedure
    via COM, so an .accdb in the repo root gets extracted to a YAML tree
    without anyone having to launch Access by hand.

.DESCRIPTION
    Spawns Access.Application as a COM server, opens the extractor .accdb,
    invokes the public sub by name (Application.Run), and tears the COM
    session down cleanly so no .laccdb lock file is left behind.

    Cleanup order matters here -- skipping CloseCurrentDatabase is the
    usual cause of a leftover .laccdb after Quit:
        1. CloseCurrentDatabase   release the open .accdb's lock
        2. Quit(acQuitSaveNone=2) exit the Access.Application process
        3. FinalReleaseComObject  drop ALL outstanding RCW references
        4. $null + GC.Collect     reap the underlying server

.PARAMETER TargetDb
    File name of the .accdb to extract.  Resolved relative to the repo
    root by the extractor itself (CurrentProject.Path).  Defaults to
    HomeHealthCare.accdb.

.PARAMETER OutputFolder
    Folder name under the repo root where the per-database YAML tree is
    written.  Defaults to DatabaseYaml; the extractor creates a
    <OutputFolder>\<TargetDb>\ subfolder for the actual files.

.PARAMETER Extractor
    File name of the extractor .accdb (the one whose modYamlDocumenter
    holds RunExtractionRelatively).  Defaults to AccessObjectExtractor.accdb.

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run_extraction.ps1

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run_extraction.ps1 `
        -TargetDb 'OtherDatabase.accdb' -OutputFolder 'DatabaseYaml'
#>
[CmdletBinding()]
param(
    [string]$TargetDb     = 'HomeHealthCare.accdb',
    [string]$OutputFolder = 'DatabaseYaml',
    [string]$Extractor    = 'AccessObjectExtractor.accdb'
)

$ErrorActionPreference = 'Stop'

# Repo root = parent of scripts/.  Using $PSScriptRoot keeps this
# portable -- no hardcoded user paths.
$repo          = Split-Path -Parent $PSScriptRoot
$extractorPath = Join-Path $repo $Extractor
$targetDbPath  = Join-Path $repo $TargetDb

$startTime = Get-Date
Write-Output "Start: $startTime"
Write-Output "Repo:      $repo"
Write-Output "Extractor: $extractorPath"
Write-Output "Target DB: $targetDbPath"
Write-Output "Output:    $repo\$OutputFolder\$TargetDb\"

if (-not (Test-Path $extractorPath)) {
    Write-Output "ERROR: Extractor not found at $extractorPath"
    exit 2
}
if (-not (Test-Path $targetDbPath)) {
    Write-Output "ERROR: Target database not found at $targetDbPath"
    exit 2
}

Write-Output "Spawning Access.Application via COM..."

$app = $null
$script:failed = $false
try {
    $app = New-Object -ComObject Access.Application
    $app.Visible     = $false
    $app.UserControl = $false
    $app.OpenCurrentDatabase($extractorPath)
    # OpenCurrentDatabase can flip Visible back on -- especially when the
    # opened .accdb has an AutoExec macro or a startup form that touches
    # Application.  Re-assert headless mode after the open so the window
    # stays hidden for the whole run.
    $app.Visible     = $false
    $app.UserControl = $false
    Write-Output "Opened: $extractorPath"

    Write-Output "Calling RunExtractionRelatively('$TargetDb', '$OutputFolder')..."
    # ByRef params on the VBA side -- PowerShell needs [ref] wrappers.
    $td = $TargetDb
    $of = $OutputFolder
    $app.Run('RunExtractionRelatively', [ref]$td, [ref]$of)
    # The procedure itself spawns and quits an inner Access.Application,
    # but in case anything on the outer app got toggled, pin Visible
    # off one more time before cleanup.
    $app.Visible     = $false
    $app.UserControl = $false
    Write-Output "Procedure returned."
}
catch {
    Write-Output "ERROR: $($_.Exception.Message)"
    Write-Output $_.ScriptStackTrace
    $script:failed = $true
}
finally {
    if ($app) {
        try { $app.CloseCurrentDatabase() } catch { Write-Output "CloseCurrentDatabase error: $($_.Exception.Message)" }
        try { $app.Quit(2) }                catch { Write-Output "Quit error: $($_.Exception.Message)" }
        try { [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($app) | Out-Null } catch {}
        $app = $null
    }
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
}

$lockFiles = Get-ChildItem -Path $repo -Filter '*.laccdb' -ErrorAction SilentlyContinue
if ($lockFiles) {
    Write-Output "WARN: lock file(s) still present after cleanup:"
    $lockFiles | ForEach-Object { Write-Output "  $($_.FullName)" }
} else {
    Write-Output "OK: no .laccdb lock files remain in $repo"
}

$endTime = Get-Date
Write-Output "End: $endTime"
Write-Output ("Elapsed: {0:N1} seconds" -f ($endTime - $startTime).TotalSeconds)

if ($script:failed) { exit 1 } else { exit 0 }
