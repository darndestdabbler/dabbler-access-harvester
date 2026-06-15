using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>Date-maxima sampler — see strategy doc §3.2.</summary>
public interface IDateMaximaSampler
{
    /// <summary>Returns one sampled max DateTime per non-excluded table that has data.</summary>
    IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db);
}

/// <summary>Per-table sampled maximum DateTime value with its source.</summary>
public sealed record SampledDateMaximum(string TableName, DateTimeOffset MaxValue, string Source);

/// <summary>
/// Optional capability a sampler may implement to surface per-column read
/// degradations (Set 014, O-1-5). A sampler that swallowed an individual
/// per-column failure exposes it here so the failure becomes a visible
/// degradation note rather than being indistinguishable from "column has no
/// date data". <see cref="ComDateMaximaProbe"/> reads this after sampling and
/// threads the messages onto <see cref="ComProbeResult.SamplerDegradations"/>.
/// </summary>
public interface ISamplerDegradationSource
{
    /// <summary>Per-column degradation messages accumulated during the last <c>Sample</c>.</summary>
    IReadOnlyList<string> Degradations { get; }
}

/// <summary>Fallback sampler — reads IR table LastUpdated (DAO metadata).</summary>
public sealed class IrLastUpdatedSampler : IDateMaximaSampler
{
    /// <inheritdoc/>
    public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db)
    {
        var results = new List<SampledDateMaximum>();
        foreach (var t in db.Tables)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Table", t.Name)) continue;
            if (!string.IsNullOrWhiteSpace(t.Connect)) continue;
            if (t.LastUpdated is null) continue;
            results.Add(new SampledDateMaximum(t.Name, t.LastUpdated.Value, "dao-metadata"));
        }
        return results;
    }
}
