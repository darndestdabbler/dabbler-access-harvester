# PreToolUse guard for the Bash and PowerShell tools.
#
# Enforces the CLAUDE.md / lessons-learned rule: run `dotnet build` and
# `dotnet test` SEQUENTIALLY IN THE FOREGROUND. Backgrounding them, or
# launching one while another is still alive, races against the shared
# worktree on Windows and spawns COM-RPC-wedged `testhost` zombies that
# hold the build DLL locks until the machine is rebooted (observed in
# harvester-report-baseline-capture S3, 2026-05-30).
#
# Blocks (exit code 2 -> fed back to the model as a denial) when a
# `dotnet build|test|run` invocation is either:
#   1. launched with run_in_background == true, or
#   2. launched while a `testhost.exe` for THIS repo is already running.
#
# Fails OPEN on any parse/error path so the guard can never itself wedge
# the agent. `dotnet build-server` is explicitly exempt.

$ErrorActionPreference = 'Stop'

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
    $payload = $raw | ConvertFrom-Json
} catch { exit 0 }

$cmd = [string]$payload.tool_input.command
if ([string]::IsNullOrWhiteSpace($cmd)) { exit 0 }

# Only police dotnet build/test/run; never trip on `dotnet build-server`.
if ($cmd -notmatch '(?i)\bdotnet\s+(test|run|build)\b') { exit 0 }
if ($cmd -match '(?i)\bdotnet\s+build-server\b') { exit 0 }

# Case 1: backgrounding is never allowed for these.
if ($payload.tool_input.run_in_background -eq $true) {
    [Console]::Error.WriteLine("BLOCKED: Never background dotnet build/test/run - CLAUDE.md requires running them sequentially in the foreground. Backgrounding races against the shared worktree and spawns COM-RPC-wedged testhost zombies that lock the build until reboot.")
    exit 2
}

# Case 2: a testhost for this repo is already alive (active run or a
# wedged zombie). Scope by command line so an unrelated VS Code / other
# project testhost does not false-positive.
try {
    $live = Get-CimInstance Win32_Process -Filter "Name='testhost.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match 'AccessHarvester' }
} catch { $live = $null }

if ($live) {
    $ids = (@($live).ForEach({ $_.ProcessId }) -join ', ')
    [Console]::Error.WriteLine("BLOCKED: A dotnet test/build for this repo is already in flight or a stale testhost is lingering (PID(s): $ids) - run sequentially. Clear stale testhost first (Stop-Process -Name testhost -Force); if it is wedged on COM RPC it needs a reboot.")
    exit 2
}

exit 0
