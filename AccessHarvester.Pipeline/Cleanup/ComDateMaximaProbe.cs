using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>
/// Result of probing the COM date sampler — see <see cref="ComDateMaximaProbe"/>.
/// Exposed as a separate type so the CLI and hermetic tests share the same
/// shape.
/// </summary>
public sealed class ComProbeResult
{
    /// <summary>Wrapped pre-sampled primary sampler when the probe succeeded; null otherwise.</summary>
    public IDateMaximaSampler? Primary { get; init; }
    /// <summary>True iff the COM probe completed without throwing.</summary>
    public bool ComAvailable { get; init; }
    /// <summary>Human-readable degradation note for inclusion in object-cleanup.json. Null on success.</summary>
    public string? DegradationNote { get; init; }
    /// <summary>Original exception message for stderr logging. Null on success.</summary>
    public string? OriginalErrorMessage { get; init; }
    /// <summary>Disposable wrapper on the sampler (when one was instantiated). Caller owns disposal.</summary>
    public IDisposable? DisposableSampler { get; init; }
}

/// <summary>
/// Probes a COM date-maxima sampler upfront and degrades gracefully on failure.
/// Per strategy §3.2 / §8: "The fallback path also activates automatically when
/// COM extraction throws or the host platform is non-Windows — `--no-com` is a
/// discipline knob, not a correctness gate."
/// </summary>
public static class ComDateMaximaProbe
{
    /// <summary>
    /// Invokes <paramref name="samplerFactory"/> and then <c>Sample(db)</c> on
    /// the produced sampler, both inside a try/catch. On success returns a
    /// pre-sampled wrapper so downstream code does not re-invoke COM. On any
    /// failure (Access not installed, .accdb file lock, COM activation
    /// failure, OpenCurrentDatabase exception) returns a result with
    /// <c>Primary=null</c> and a degradation note.
    /// </summary>
    public static ComProbeResult Probe(Func<IDateMaximaSampler> samplerFactory, IrDatabase db)
    {
        ArgumentNullException.ThrowIfNull(samplerFactory);
        ArgumentNullException.ThrowIfNull(db);

        IDateMaximaSampler? sampler = null;
        try
        {
            sampler = samplerFactory();
            var probed = sampler.Sample(db);
            return new ComProbeResult
            {
                Primary = new PreSampledSampler(probed),
                ComAvailable = true,
                DegradationNote = null,
                OriginalErrorMessage = null,
                DisposableSampler = sampler as IDisposable,
            };
        }
        catch (Exception comEx)
        {
            try { (sampler as IDisposable)?.Dispose(); } catch { }
            return new ComProbeResult
            {
                Primary = null,
                ComAvailable = false,
                DegradationNote =
                    $"COM date sampling unavailable: {comEx.Message}. Per-table date signals fell back to DAO LastUpdated.",
                OriginalErrorMessage = comEx.Message,
                DisposableSampler = null,
            };
        }
    }

    /// <summary>
    /// Wraps an already-obtained list of <see cref="SampledDateMaximum"/> so
    /// the analyser can consume it as an <see cref="IDateMaximaSampler"/>
    /// without re-invoking COM.
    /// </summary>
    internal sealed class PreSampledSampler : IDateMaximaSampler
    {
        private readonly IReadOnlyList<SampledDateMaximum> _cached;
        public PreSampledSampler(IReadOnlyList<SampledDateMaximum> cached) => _cached = cached;
        public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db) => _cached;
    }
}
