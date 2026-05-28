using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// Effective configuration for one <c>classify</c> run. Persisted into
/// <c>classification.json</c> so the editor and sanitizer can see exactly
/// which vocabularies fired which heuristics, and so a reviewer can
/// reproduce the scan.
/// </summary>
public sealed record ScanConfig(
    IReadOnlyList<string> SystemColumnPatterns,
    IReadOnlyList<string> PiiNamePatterns,
    IReadOnlyList<string> PiiValuePatterns,
    int ValueSampleSize)
{
    /// <summary>The defaults used when the CLI is invoked without an override.</summary>
    public static ScanConfig Default() => new(
        SystemColumnPatterns: PiiDictionary.DefaultSystemColumnPatterns,
        PiiNamePatterns:      PiiDictionary.DefaultPiiNamePatterns,
        PiiValuePatterns:     PiiDictionary.DefaultPiiValuePatternIds,
        ValueSampleSize:      200);
}
