using System.Collections.Generic;
using AccessHarvester.Pipeline.Cleanup;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Cleanup;

/// <summary>
/// Hermetic pins for <see cref="CleanupDegradationNotes.Fold"/> (Set 014, O-1-5 /
/// R1 Minor). Proves the CLI note-fold is behavior-equivalent to the legacy
/// logic when there are no sampler degradations, and that per-column sampler
/// swallow notes are surfaced in the correct order regardless of source.
/// </summary>
public class CleanupDegradationNotesTests
{
    private static CleanupFindings Findings(string dateSignalSource, params string[] existing) => new()
    {
        Database = "Db",
        GeneratedAt = "2026-01-01T00:00:00Z",
        HarvestDate = "2026-01-01",
        DateSignalSource = dateSignalSource,
        ThresholdDays = 90,
        DegradationNotes = existing,
        Findings = [],
    };

    [Fact]
    public void Fold_No_Notes_Returns_Same_Instance_Unchanged()
    {
        var f = Findings("dao-metadata", "pre-existing note");
        var result = CleanupDegradationNotes.Fold(f, comDegradationNote: null, samplerDegradations: []);

        Assert.Same(f, result);
    }

    [Fact]
    public void Fold_ComNote_Prepended_When_Source_Not_Unavailable()
    {
        // Legacy behavior: COM-failure note prepended ahead of existing notes.
        var f = Findings("dao-metadata", "analyser note");
        var result = CleanupDegradationNotes.Fold(f, "COM date sampling unavailable: boom", []);

        Assert.Equal(["COM date sampling unavailable: boom", "analyser note"], result.DegradationNotes);
    }

    [Fact]
    public void Fold_ComNote_Suppressed_When_Source_Unavailable()
    {
        // Legacy behavior preserved: when the DAO fallback had no data either, the
        // analyser's own "unavailable" note stands alone — the probe note would
        // contradict it, so it is not added.
        var f = Findings("unavailable", "Date sampling unavailable: ...");
        var result = CleanupDegradationNotes.Fold(f, "COM date sampling unavailable: boom", []);

        Assert.Same(f, result);
        Assert.Equal(["Date sampling unavailable: ..."], result.DegradationNotes);
    }

    [Fact]
    public void Fold_Sampler_Degradations_Always_Surfaced_Even_When_Source_Unavailable()
    {
        // Per-column swallow notes describe a real read failure and are surfaced
        // regardless of the resolved source (here: unavailable, so no com note).
        var f = Findings("unavailable", "analyser note");
        var sampler = new List<string> { "Date sampling failed for [tblA].[T]: boom" };
        var result = CleanupDegradationNotes.Fold(f, comDegradationNote: null, samplerDegradations: sampler);

        Assert.Equal(["Date sampling failed for [tblA].[T]: boom", "analyser note"], result.DegradationNotes);
    }

    [Fact]
    public void Fold_Order_Is_Com_Then_Sampler_Then_Existing()
    {
        var f = Findings("com", "existing note");
        var sampler = new List<string> { "sampler note 1", "sampler note 2" };
        var result = CleanupDegradationNotes.Fold(f, "com failure note", sampler);

        Assert.Equal(
            ["com failure note", "sampler note 1", "sampler note 2", "existing note"],
            result.DegradationNotes);
    }
}
