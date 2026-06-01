using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace AccessHarvester.Integration.TestSupport;

// Black-box invocation of the AccessHarvester CLI exe.
//
// Captures stdout / stderr / exit code from a real Process so tests can
// assert on the CLI's public surface (subcommand dispatch, exit codes,
// --json-status envelopes, parse output shape) without coupling to the
// in-process Console.SetOut path. The test runner (xUnit) installs its
// own Console capture that bypasses Console.SetOut, so an in-process
// invocation cannot reliably observe the CLI's writes — Process is the
// reliable boundary.
internal sealed record CliResult(int ExitCode, string StdOut, string StdErr);

internal static class CliInvoker
{
    private static readonly string ExePath = LocateExe();

    // Grace period for a lingering CLI process to exit on its own once its
    // output is materialized. The CLI's AccessComSession self-reaps the exact
    // msaccess.exe it created on Dispose (graceful Quit + doubled GC +
    // bounded exact-PID failsafe; see docs/access-com-zombie-cleanup.md), so
    // the harness lets that run to completion rather than blunt-killing the
    // CLI out from under it. Comfortably exceeds the in-CLI 5s exact-PID wait.
    private const int GracefulCliExitMs = 15_000;

    public static CliResult Run(params string[] args) => RunWithTimeout(60_000, args);

    /// <summary>
    /// Variant that lets a caller override the default 60-second per-process
    /// timeout. COM-driven subcommands against larger sample databases
    /// (e.g. <c>classify</c> against HomeHealthCare.accdb, which value-samples
    /// every non-key column across hundreds of tables) can legitimately need
    /// more wall-clock than the default.
    /// </summary>
    public static CliResult RunWithTimeout(int timeoutMs, params string[] args)
    {
        if (TryBuildReportBaselineReadyCheck(args, out var outputRoot, out var requiredRelativePaths))
            return RunUntilFilesExist(timeoutMs, outputRoot, requiredRelativePaths, args);

        var psi = CreateStartInfo(args);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start CLI process at {ExePath}");

        // Drain both streams concurrently to avoid deadlock if either fills.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(timeoutMs))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException($"CLI process did not exit within {timeoutMs}ms. args=[{string.Join(' ', args)}]");
        }
        return new CliResult(proc.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    /// <summary>
    /// Some Access-backed CLI paths can fully materialize their output tree
    /// before the process exits, then spend additional time in COM cleanup.
    /// For those cases, treat a stable set of required files as success and
    /// stop the lingering process once the artifacts are ready to read.
    /// </summary>
    public static CliResult RunUntilFilesExist(
        int timeoutMs,
        string outputRoot,
        IReadOnlyCollection<string> requiredRelativePaths,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);
        ArgumentNullException.ThrowIfNull(requiredRelativePaths);

        if (requiredRelativePaths.Count == 0)
            throw new ArgumentException("At least one required output file must be provided.", nameof(requiredRelativePaths));

        var psi = CreateStartInfo(args);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start CLI process at {ExePath}");

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        var deadline = Environment.TickCount64 + timeoutMs;
        Dictionary<string, long>? priorSizes = null;
        int stablePolls = 0;

        while (true)
        {
            if (proc.HasExited)
            {
                // The CLI exited on its own — its AccessComSession.Dispose() ran
                // and reaped the msaccess.exe it created. Nothing to kill.
                try { stdoutTask.Wait(5_000); } catch { /* best-effort */ }
                try { stderrTask.Wait(5_000); } catch { /* best-effort */ }

                return new CliResult(
                    proc.ExitCode,
                    stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty,
                    stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty);
            }

            var currentSizes = TryGetRequiredFileSizes(outputRoot, requiredRelativePaths);
            if (currentSizes is not null)
            {
                if (priorSizes is not null
                    && currentSizes.Count == priorSizes.Count
                    && currentSizes.All(pair => priorSizes.TryGetValue(pair.Key, out var priorSize)
                                           && priorSize == pair.Value))
                {
                    stablePolls++;
                    if (stablePolls >= 2)
                    {
                        // Required output is materialized and stable. Give the CLI a
                        // bounded chance to exit on its own so its AccessComSession
                        // reaps the exact msaccess.exe it created. Only force-kill the
                        // CLI *process* (managed, PID-scoped) as a last resort if it
                        // overruns — never blunt-kill it mid-cleanup, which would abort
                        // the in-CLI exact-PID reaper and orphan msaccess.
                        if (!proc.WaitForExit(GracefulCliExitMs))
                            KillCliProcessBestEffort(proc);
                        try { stdoutTask.Wait(5_000); } catch { /* best-effort */ }
                        try { stderrTask.Wait(5_000); } catch { /* best-effort */ }

                        return new CliResult(
                            proc.HasExited ? proc.ExitCode : 0,
                            stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty,
                            stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty);
                    }
                }
                else
                {
                    stablePolls = 0;
                }

                priorSizes = currentSizes;
            }
            else
            {
                priorSizes = null;
                stablePolls = 0;
            }

            if (Environment.TickCount64 >= deadline)
            {
                KillCliProcessBestEffort(proc);
                throw new TimeoutException($"CLI process did not produce stable required files within {timeoutMs}ms. args=[{string.Join(' ', args)}]");
            }

            Thread.Sleep(250);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    private static bool TryBuildReportBaselineReadyCheck(
        string[] args,
        out string outputRoot,
        out string[] requiredRelativePaths)
    {
        outputRoot = string.Empty;
        requiredRelativePaths = Array.Empty<string>();

        if (args.Length == 0
            || !string.Equals(args[0], "report-baseline", StringComparison.OrdinalIgnoreCase)
            || !args.Contains("--json-status", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        string? outRoot = TryGetOptionValue(args, "--out");
        string? dbPath = TryGetOptionValue(args, "--db");
        string? reports = TryGetOptionValue(args, "--reports");
        if (string.IsNullOrWhiteSpace(outRoot)
            || string.IsNullOrWhiteSpace(dbPath)
            || string.IsNullOrWhiteSpace(reports))
        {
            return false;
        }

        string fixtureStem = Path.GetFileNameWithoutExtension(dbPath);
        outputRoot = Path.Combine(outRoot, "AccessMetaData", fixtureStem, "reports-baseline");
        requiredRelativePaths = reports
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(reportName => $"{reportName}/aggregates.json")
            .Prepend("baseline-manifest.json")
            .ToArray();
        return requiredRelativePaths.Length > 1;
    }

    private static string? TryGetOptionValue(string[] args, string optionName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static Dictionary<string, long>? TryGetRequiredFileSizes(
        string outputRoot,
        IReadOnlyCollection<string> requiredRelativePaths)
    {
        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var relativePath in requiredRelativePaths)
        {
            string path = Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                return null;

            sizes[relativePath] = new FileInfo(path).Length;
        }

        return sizes;
    }

    // Last-resort termination of the lingering CLI *process* only. Narrowed
    // from a blunt external `taskkill /PID <pid> /T /F`: the msaccess.exe the
    // CLI spawns via COM is a DCOM-activated LocalServer, so it is NOT in this
    // process tree and an external tree-kill could never reap it anyway. Since
    // Session 1 hardened AccessComSession to self-reap that exact msaccess PID
    // on Dispose, the harness only needs to terminate the CLI process itself if
    // it overruns the graceful window — and only after that Dispose has had its
    // chance to run. The managed Process.Kill keeps this PID-scoped with no
    // external taskkill subprocess and cannot touch unrelated Access instances.
    private static void KillCliProcessBestEffort(Process proc)
    {
        try { proc.Kill(entireProcessTree: true); }
        catch { /* best-effort test cleanup */ }
    }

    private static string LocateExe()
    {
        string baseDir = AppContext.BaseDirectory;
        string exeName = OperatingSystem.IsWindows() ? "AccessHarvester.exe" : "AccessHarvester";
        string candidate = Path.Combine(baseDir, exeName);
        if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException(
            $"AccessHarvester CLI exe not found at expected test-bin location: {candidate}. " +
            $"Verify the AccessHarvester.Cli project reference copies the exe alongside the test assembly.");
    }
}
