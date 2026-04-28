using System;
using System.Diagnostics;
using System.IO;

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

    public static CliResult Run(params string[] args)
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

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start CLI process at {ExePath}");

        // Drain both streams concurrently to avoid deadlock if either fills.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(60_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException($"CLI process did not exit within 60s. args=[{string.Join(' ', args)}]");
        }
        return new CliResult(proc.ExitCode, stdoutTask.Result, stderrTask.Result);
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
