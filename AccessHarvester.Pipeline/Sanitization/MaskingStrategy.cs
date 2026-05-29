using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Per-column masking strategy applied by the sanitizer. The 7-value taxonomy
/// from <c>data-classification-plan.md</c> § 3.5 (Test-data behavior column),
/// promoted into a typed enum because <c>SanitizeCommand</c> selects a handler
/// per row based on the effective strategy.
/// </summary>
public enum MaskingStrategy
{
    MockReplace,
    Generalize,
    Redact,
    ScrubFreeText,
    Preserve,
    RemapId,
    Regenerate,
}

/// <summary>Wire-form (kebab-case) name <-> <see cref="MaskingStrategy"/> mapping.</summary>
public static class MaskingStrategyWire
{
    private static readonly Dictionary<MaskingStrategy, string> ToWireMap = new()
    {
        [MaskingStrategy.MockReplace]   = "mock-replace",
        [MaskingStrategy.Generalize]    = "generalize",
        [MaskingStrategy.Redact]        = "redact",
        [MaskingStrategy.ScrubFreeText] = "scrub-free-text",
        [MaskingStrategy.Preserve]      = "preserve",
        [MaskingStrategy.RemapId]       = "remap-id",
        [MaskingStrategy.Regenerate]    = "regenerate",
    };

    private static readonly Dictionary<string, MaskingStrategy> FromWireMap = new(StringComparer.Ordinal)
    {
        ["mock-replace"]    = MaskingStrategy.MockReplace,
        ["generalize"]      = MaskingStrategy.Generalize,
        ["redact"]          = MaskingStrategy.Redact,
        ["scrub-free-text"] = MaskingStrategy.ScrubFreeText,
        ["preserve"]        = MaskingStrategy.Preserve,
        ["remap-id"]        = MaskingStrategy.RemapId,
        ["regenerate"]      = MaskingStrategy.Regenerate,
    };

    public static string ToWire(this MaskingStrategy s) => ToWireMap[s];

    public static MaskingStrategy FromWire(string s)
    {
        if (s is null) throw new ArgumentNullException(nameof(s));
        if (!FromWireMap.TryGetValue(s, out var strat))
            throw new ArgumentException($"Unknown masking strategy: '{s}'.", nameof(s));
        return strat;
    }
}
