using Xunit;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Session 2 of `harvester-finish-canonical-parse-success-report`:
// sentinel test for the diff-failure message. The live gate
// (ParseSuccessReportTests) only emits the failure message when the
// committed golden disagrees with a fresh generation — which means a
// maintainer encounters it once, under stress, while debugging an
// unexpected count shift. The message has to stay human-readable
// under that load. These tests construct a hand-built golden/generated
// pair with a known, single-character divergence and assert that the
// message ParseSuccessReportDiff.Build emits is the one the human
// would actually see: it names the fixture, points at the offset/line
// of the divergence, shows both surrounding snippets, and tells the
// maintainer how to regenerate.
//
// The diff helper is exercised here so it is not the parallel logic
// from a second copy — both this test and the live gate consume the
// same builder.
public sealed class ParseSuccessReportDiffTests
{
    [Fact]
    public void Build_returns_null_when_strings_are_byte_equal()
    {
        var same = "{\n  \"fixture\": \"HomeHealthCare.accdb\"\n}\n";
        var result = ParseSuccessReportDiff.Build(
            committed: same,
            generated: same,
            fixtureName: "HomeHealthCare.accdb",
            outPath: "samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json");

        Assert.Null(result);
    }

    [Fact]
    public void Build_emits_actionable_message_when_one_count_changes()
    {
        // Hand-built minimal report pair that diverges only at the
        // VBA totalModules count (32 vs. 31). The mutation is in
        // exactly one place so the divergence-offset / line / snippet
        // story is unambiguous.
        const string committed =
            "{\n"
            + "  \"fixture\": \"HomeHealthCare.accdb\",\n"
            + "  \"schemaVersion\": 1,\n"
            + "  \"summary\": {\n"
            + "    \"vbaModules\": {\n"
            + "      \"totalModules\": 32,\n"
            + "      \"parseOk\": 32\n"
            + "    }\n"
            + "  }\n"
            + "}\n";

        const string generated =
            "{\n"
            + "  \"fixture\": \"HomeHealthCare.accdb\",\n"
            + "  \"schemaVersion\": 1,\n"
            + "  \"summary\": {\n"
            + "    \"vbaModules\": {\n"
            + "      \"totalModules\": 31,\n"
            + "      \"parseOk\": 32\n"
            + "    }\n"
            + "  }\n"
            + "}\n";

        const string outPath =
            "samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json";

        var msg = ParseSuccessReportDiff.Build(
            committed: committed,
            generated: generated,
            fixtureName: "HomeHealthCare.accdb",
            outPath: outPath);

        Assert.NotNull(msg);

        // The message has to identify the fixture and the output path
        // — without them, a maintainer dropped into a CI log cannot
        // tell which golden is wrong or where it lives.
        Assert.Contains("HomeHealthCare.accdb", msg);
        Assert.Contains(outPath, msg);

        // The divergence sits on the totalModules line. With six lines
        // of preceding content, the first mismatch is on line 6 of
        // both strings (1-based) — that is what the human sees in the
        // "(line N)" portion of the message.
        Assert.Contains("(line 6)", msg);

        // Both the committed and generated values must be quoted, so
        // the maintainer can tell which side moved. The snippet window
        // around the offset captures the "totalModules" key and the
        // disagreeing digit.
        Assert.Contains("committed: <<", msg);
        Assert.Contains("generated: <<", msg);
        Assert.Contains("32", msg);
        Assert.Contains("31", msg);

        // Regeneration affordance — the message has to tell the
        // maintainer how to update the golden when the count shift is
        // intentional.
        Assert.Contains(ParseSuccessReportDiff.UpdateEnvVar, msg);
        Assert.Contains("dotnet test --filter ParseSuccessReportTests", msg);
    }

    [Fact]
    public void Snippet_collapses_embedded_newlines_for_single_line_display()
    {
        // The Snippet helper renders LF as the literal two-character
        // sequence "\n" so the diff message stays single-line per
        // committed/generated side. Without that, a divergence near a
        // line boundary would wrap mid-message and obscure the
        // surrounding context.
        var divergent = "abc\ndef\nghi";
        var snippet = ParseSuccessReportDiff.Snippet(divergent, offset: 4);

        Assert.DoesNotContain("\n", snippet);
        Assert.Contains("\\n", snippet);
    }
}
