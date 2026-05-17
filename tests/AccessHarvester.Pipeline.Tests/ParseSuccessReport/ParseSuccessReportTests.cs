using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Session 1 of `harvester-finish-canonical-parse-success-report`:
// the per-fixture parse-success report is committed as the interim
// golden. Default test mode diffs against the committed file;
// `UPDATE_PARSE_SUCCESS_REPORTS=1` overwrites it. Session 2 carries
// forward the same gate without changing the test contract.
public sealed class ParseSuccessReportTests
{
    private const string UpdateEnvVar = "UPDATE_PARSE_SUCCESS_REPORTS";

    [Fact]
    public void HomeHealthCare_parse_success_report_matches_committed_golden()
    {
        AssertOrUpdate("HomeHealthCare.accdb");
    }

    [SkippableFact]
    public void NorthwindDevEdition_parse_success_report_matches_committed_golden()
    {
        // Northwind YAML mirror is gitignored — present on the
        // maintainer's Windows machine after a local COM-extract pass,
        // absent on CI / fresh clones. Skip cleanly when missing so
        // the suite stays green elsewhere.
        var repoRoot = RepoRoot();
        var mirror = Path.Combine(repoRoot, "samples", "DatabaseYaml", "NorthwindDevEdition.accdb");
        Skip.IfNot(
            Directory.Exists(mirror),
            $"NorthwindDevEdition YAML mirror not present at {mirror} — " +
            "regenerate via the local COM extractor or skip.");

        AssertOrUpdate("NorthwindDevEdition.accdb");
    }

    private static void AssertOrUpdate(string fixtureName)
    {
        var repoRoot = RepoRoot();
        var mirror = Path.Combine(repoRoot, "samples", "DatabaseYaml", fixtureName);
        var report = ParseSuccessReportGenerator.Generate(fixtureName, mirror);

        // Sanity invariants — a zero module/query count means the YAML
        // mirror is empty or the loader is broken. Fail loudly either
        // way before the diff path.
        Assert.True(report.Summary.VbaModules.TotalModules > 0,
            $"{fixtureName}: VBA module count is zero — empty mirror or loader regression.");
        Assert.True(report.Summary.JetSqlQueries.TotalQueries > 0,
            $"{fixtureName}: query count is zero — empty mirror or loader regression.");

        var generatedJson = JsonSerializer.Serialize(report, SerializerOptions) + "\n";

        var outDir = Path.Combine(repoRoot, "samples", "parse-success-reports");
        var outPath = Path.Combine(outDir, $"{fixtureName}.parse-success-report.json");

        bool update = string.Equals(
            Environment.GetEnvironmentVariable(UpdateEnvVar) ?? "", "1",
            StringComparison.Ordinal);

        if (update || !File.Exists(outPath))
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(outPath, generatedJson, new System.Text.UTF8Encoding(false));
            return;
        }

        var committed = File.ReadAllText(outPath, new System.Text.UTF8Encoding(false));
        if (!string.Equals(committed, generatedJson, StringComparison.Ordinal))
        {
            // Surface the first divergence so the failure message is
            // actionable. The full diff is recoverable by re-running
            // with UPDATE_PARSE_SUCCESS_REPORTS=1 and inspecting git.
            var divergence = FindFirstDivergence(committed, generatedJson);
            throw new Xunit.Sdk.XunitException(
                $"{fixtureName}: generated parse-success report does not match committed golden at {outPath}.\n"
                + $"First divergence at offset {divergence.Offset} (line {divergence.Line}): \n"
                + $"  committed: <<{Snippet(committed, divergence.Offset)}>>\n"
                + $"  generated: <<{Snippet(generatedJson, divergence.Offset)}>>\n"
                + $"Re-run with `{UpdateEnvVar}=1 dotnet test --filter ParseSuccessReportTests` to regenerate, then commit the diff.");
        }
    }

    private record struct Divergence(int Offset, int Line);

    private static Divergence FindFirstDivergence(string a, string b)
    {
        int min = Math.Min(a.Length, b.Length);
        int line = 1;
        for (int i = 0; i < min; i++)
        {
            if (a[i] != b[i]) return new Divergence(i, line);
            if (a[i] == '\n') line++;
        }
        return new Divergence(min, line);
    }

    private static string Snippet(string s, int offset)
    {
        int start = Math.Max(0, offset - 20);
        int end = Math.Min(s.Length, offset + 40);
        return s.Substring(start, end - start).Replace("\n", "\\n");
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Prevent over-escaping of `<`, `>`, `&` in surrounding-text
        // samples; the report is consumed as a file, not embedded in
        // HTML. The serialiser writes ASCII by default; ensure that
        // text containing non-ASCII (e.g., a stray Caption glyph) is
        // encoded predictably.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Walk up from this test binary to the repo root. The root carries
    // a `.gitignore` or `samples/` directory; we anchor on the latter
    // since the repo's bare-worktree layout means `.gitignore` lives
    // inside the worktree (which may or may not be `main/`).
    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var samples = Path.Combine(here.FullName, "samples");
            var slnCandidate = Path.Combine(here.FullName, "AccessHarvester.sln");
            if (Directory.Exists(samples) && File.Exists(slnCandidate))
                return here.FullName;
            here = here.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate repo root above " + AppContext.BaseDirectory);
    }
}
