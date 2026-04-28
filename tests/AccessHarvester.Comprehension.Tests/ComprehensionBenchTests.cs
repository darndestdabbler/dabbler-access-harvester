using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace AccessHarvester.Comprehension.Tests;

/// <summary>
/// Layer C — AI comprehension test bench. Runs the harvester pipeline against
/// the gold-standard DBs and shells out to <c>tools/comprehension-bench/run.py</c>
/// which routes each curated question through the AI router and asserts the
/// graduated rubric (low ≥ 0.85, medium ≥ 0.65, high ≥ 0.40).
/// </summary>
/// <remarks>
/// Cost-gated by the <c>HARVESTER_COMPREHENSION_BENCH</c> environment variable.
/// Plain <c>dotnet test</c> skips this fact by default — the bench routes paid
/// API calls per question and is flaky against provider-rate-limit conditions.
/// CI's slow lane sets the env var; the local dev loop does not.
/// </remarks>
public class ComprehensionBenchTests
{
    private static readonly string[] GoldStandardDbs =
    {
        "NorthwindDevEdition",
        "HomeHealthCare",
    };

    [SkippableFact]
    public void Bench_GraduatedRubricPasses_AcrossGoldStandardDbs()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable("HARVESTER_COMPREHENSION_BENCH") == "1",
            "Set HARVESTER_COMPREHENSION_BENCH=1 to run the AI-comprehension bench.");

        Skip.IfNot(OperatingSystem.IsWindows(),
            "Pipeline-prep requires Microsoft Access (Windows COM).");

        Skip.IfNot(IsAccessAvailable(),
            "Microsoft Access is not registered on this host.");

        string repoRoot = LocateRepoRoot();
        string samplesDir = Path.Combine(repoRoot, "samples");

        // Verify per-DB skip preconditions BEFORE creating outRoot so a skip
        // on a missing fixture or curated questions.yaml does not leak temp
        // directories or trigger the failure-diagnostic preservation path.
        foreach (var db in GoldStandardDbs)
        {
            string accdbPath = Path.Combine(samplesDir, $"{db}.accdb");
            Skip.IfNot(File.Exists(accdbPath), $"Sample DB not present at {accdbPath}.");

            string questionsPath = Path.Combine(repoRoot,
                "docs", "test-cases", "comprehension", db, "questions.yaml");
            Skip.IfNot(File.Exists(questionsPath),
                $"Curated questions.yaml not present at {questionsPath}. " +
                $"Run draft_questions.py and curate before promoting to questions.yaml.");
        }

        string outRoot = Path.Combine(Path.GetTempPath(), $"ae-comprehension-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outRoot);

        bool failed = false;
        try
        {
            foreach (var db in GoldStandardDbs)
            {
                string accdbPath = Path.Combine(samplesDir, $"{db}.accdb");
                string questionsPath = Path.Combine(repoRoot,
                    "docs", "test-cases", "comprehension", db, "questions.yaml");

                RunHarvesterPipeline(repoRoot, accdbPath, outRoot);

                string irRoot = Path.Combine(outRoot, "AccessMetaData", db);
                Assert.True(Directory.Exists(irRoot),
                    $"Expected IR directory at {irRoot} after pipeline run.");

                string scoresPath = Path.Combine(outRoot, $"{db}.scores.jsonl");
                int benchExitCode = RunBench(repoRoot, db, irRoot, questionsPath, scoresPath);
                if (benchExitCode != 0)
                {
                    failed = true;
                    Assert.True(benchExitCode == 0,
                        $"Bench failed for {db} (exit={benchExitCode}). " +
                        $"PRESERVED scores at {scoresPath} (outRoot kept for diagnosis: {outRoot}).");
                }
            }
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            // Only preserve outRoot when an actual failure occurred. Skips
            // unwound before this point. Cleanup can be forced via env var.
            bool forceCleanup =
                Environment.GetEnvironmentVariable("HARVESTER_COMPREHENSION_BENCH_CLEANUP") == "1";
            if ((!failed || forceCleanup) && Directory.Exists(outRoot))
            {
                try { Directory.Delete(outRoot, recursive: true); }
                catch { /* best-effort; the temp path is GUID-scoped so leaks are isolated. */ }
            }
            else if (failed)
            {
                Console.Out.WriteLine($"[Comprehension.Tests] PRESERVED outRoot for diagnosis: {outRoot}");
            }
        }
    }

    private static bool IsAccessAvailable()
    {
        if (!OperatingSystem.IsWindows()) return false;
#pragma warning disable CA1416
        return Type.GetTypeFromProgID("Access.Application", throwOnError: false) is not null;
#pragma warning restore CA1416
    }

    private static void RunHarvesterPipeline(string repoRoot, string accdbPath, string outRoot)
    {
        string exeName = OperatingSystem.IsWindows() ? "AccessHarvester.exe" : "AccessHarvester";
        string exePath = Path.Combine(AppContext.BaseDirectory, exeName);
        Assert.True(File.Exists(exePath), $"CLI exe not found at {exePath}.");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repoRoot,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--db");
        psi.ArgumentList.Add(accdbPath);
        psi.ArgumentList.Add("--out");
        psi.ArgumentList.Add(outRoot);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start harvester pipeline at {exePath}");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(300_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Pipeline did not exit within 5 minutes for {accdbPath}.");
        }
        Assert.True(proc.ExitCode == 0,
            $"Pipeline failed (exit={proc.ExitCode}). stderr:\n{stderrTask.Result}\nstdout tail:\n{Tail(stdoutTask.Result)}");
    }

    private static int RunBench(string repoRoot, string db, string irRoot, string questionsPath, string scoresPath)
    {
        string python = Path.Combine(repoRoot, ".venv", "Scripts",
            OperatingSystem.IsWindows() ? "python.exe" : "python");
        Assert.True(File.Exists(python),
            $"Python venv interpreter not found at {python}. Bootstrap with `python -m venv .venv` from {repoRoot}.");

        string runner = Path.Combine(repoRoot, "tools", "comprehension-bench", "run.py");
        Assert.True(File.Exists(runner), $"Bench runner not found at {runner}.");

        var psi = new ProcessStartInfo
        {
            FileName = python,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repoRoot,
        };
        psi.ArgumentList.Add(runner);
        psi.ArgumentList.Add("--db"); psi.ArgumentList.Add(db);
        psi.ArgumentList.Add("--ir-root"); psi.ArgumentList.Add(irRoot);
        psi.ArgumentList.Add("--questions"); psi.ArgumentList.Add(questionsPath);
        psi.ArgumentList.Add("--out"); psi.ArgumentList.Add(scoresPath);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start bench runner at {python} {runner}");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(30 * 60 * 1000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Bench runner did not exit within 30 minutes for {db}.");
        }

        // Forward summary so the test runner output captures bench progress.
        if (!string.IsNullOrWhiteSpace(stdoutTask.Result))
            Console.Out.WriteLine(stdoutTask.Result);
        if (!string.IsNullOrWhiteSpace(stderrTask.Result))
            Console.Error.WriteLine(stderrTask.Result);
        return proc.ExitCode;
    }

    private static string LocateRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string solution = Path.Combine(dir, "AccessHarvester.sln");
            if (File.Exists(solution)) return dir;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"AccessHarvester.sln not found walking up from {AppContext.BaseDirectory}.");
    }

    private static string Tail(string text, int maxLines = 50)
    {
        var lines = text.Split('\n');
        if (lines.Length <= maxLines) return text;
        return string.Join('\n', lines.Skip(lines.Length - maxLines));
    }
}
