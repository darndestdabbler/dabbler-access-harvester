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
    // The diff-message text is built by ParseSuccessReportDiff so the
    // Session 2 sentinel test can exercise the same builder.
    private const string UpdateEnvVar = ParseSuccessReportDiff.UpdateEnvVar;

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
        var diff = ParseSuccessReportDiff.Build(committed, generatedJson, fixtureName, outPath);
        if (diff is not null)
            throw new Xunit.Sdk.XunitException(diff);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        // Pin the indented-write newline to LF. With WriteIndented=true
        // the default NewLine is Environment.NewLine — CRLF on Windows,
        // LF on Linux/macOS — which would make the committed golden
        // platform-specific. The downstream AI consumer reads the file
        // as bytes; LF is the canonical choice and matches the .gitattributes
        // eol=lf rule on *.json.
        NewLine = "\n",
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
