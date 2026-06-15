using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>
/// Folds COM-side degradation notes into a <see cref="CleanupFindings"/>'s
/// <see cref="CleanupFindings.DegradationNotes"/>. Extracted from the
/// <c>identify-cleanup</c> command (Set 014) so the fold is hermetically
/// testable independent of the COM path.
/// </summary>
public static class CleanupDegradationNotes
{
    /// <summary>
    /// Returns <paramref name="findings"/> with COM-side degradation notes folded in.
    /// The whole-COM-failure note (<paramref name="comDegradationNote"/>) is prepended
    /// only when the DAO fallback actually had data to use
    /// (<c>DateSignalSource != "unavailable"</c>) — otherwise the analyser's own
    /// "Date sampling unavailable" note is more accurate and the probe message would
    /// contradict it. Per-column sampler-swallow notes
    /// (<paramref name="samplerDegradations"/>, O-1-5) are always surfaced when present:
    /// they describe a real read failure regardless of the resolved source. Order is
    /// [com-failure?] + sampler + existing. When nothing applies, the original
    /// instance is returned unchanged.
    /// </summary>
    public static CleanupFindings Fold(CleanupFindings findings, string? comDegradationNote,
        IReadOnlyList<string> samplerDegradations)
    {
        bool hasComNote = comDegradationNote is not null
            && findings.DateSignalSource != "unavailable";
        if (!hasComNote && samplerDegradations.Count == 0)
            return findings;

        var augmented = new List<string>();
        if (hasComNote) augmented.Add(comDegradationNote!);
        augmented.AddRange(samplerDegradations);
        augmented.AddRange(findings.DegradationNotes);
        return findings with { DegradationNotes = augmented };
    }
}
