using System;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Diff-message builder shared by ParseSuccessReportTests (the live
// gate) and ParseSuccessReportDiffTests (Session 2's sentinel that
// verifies the failure message stays human-readable). Keeping the
// builder in one place means the sentinel is exercising the exact
// message the human would see at gate-fail time rather than a
// parallel implementation drifting from it.
internal static class ParseSuccessReportDiff
{
    internal const string UpdateEnvVar = "UPDATE_PARSE_SUCCESS_REPORTS";

    // Returns null when the two strings are byte-equal. Returns the
    // assembled diff-failure message otherwise.
    internal static string? Build(
        string committed,
        string generated,
        string fixtureName,
        string outPath)
    {
        if (string.Equals(committed, generated, StringComparison.Ordinal))
            return null;

        var divergence = FindFirstDivergence(committed, generated);
        return
            $"{fixtureName}: generated parse-success report does not match committed golden at {outPath}.\n"
            + $"First divergence at offset {divergence.Offset} (line {divergence.Line}): \n"
            + $"  committed: <<{Snippet(committed, divergence.Offset)}>>\n"
            + $"  generated: <<{Snippet(generated, divergence.Offset)}>>\n"
            + $"Re-run with `{UpdateEnvVar}=1 dotnet test --filter ParseSuccessReportTests` to regenerate, then commit the diff.";
    }

    internal readonly record struct Divergence(int Offset, int Line);

    internal static Divergence FindFirstDivergence(string a, string b)
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

    internal static string Snippet(string s, int offset)
    {
        int start = Math.Max(0, offset - 20);
        int end = Math.Min(s.Length, offset + 40);
        return s.Substring(start, end - start).Replace("\n", "\\n");
    }
}
