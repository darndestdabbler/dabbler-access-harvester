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
                KillProcessTreeBestEffort(proc.Id);
                try { proc.WaitForExit(5_000); } catch { /* best-effort */ }
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
                        KillProcessTreeBestEffort(proc.Id);
                        try { proc.WaitForExit(5_000); } catch { /* best-effort */ }
                        try { stdoutTask.Wait(5_000); } catch { /* best-effort */ }
                        try { stderrTask.Wait(5_000); } catch { /* best-effort */ }

                        return new CliResult(
                            0,
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
                KillProcessTreeBestEffort(proc.Id);
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

    private static void KillProcessTreeBestEffort(int pid)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add("/PID");
                psi.ArgumentList.Add(pid.ToString());
                psi.ArgumentList.Add("/T");
                psi.ArgumentList.Add("/F");

                using var killer = Process.Start(psi);
                try { killer?.WaitForExit(5_000); } catch { /* best-effort */ }
                return;
            }

            using var proc = Process.GetProcessById(pid);
            proc.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort test cleanup.
        }
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
