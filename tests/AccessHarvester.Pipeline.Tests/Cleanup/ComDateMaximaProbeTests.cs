using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.Cleanup;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Cleanup;

/// <summary>
/// Hermetic tests for <see cref="ComDateMaximaProbe"/>. Validates the R2 Issue 1
/// graceful-degradation contract: when the COM date sampler throws (Access not
/// installed, .accdb file lock, OpenCurrentDatabase failure, etc.) the probe
/// returns a result with Primary=null + degradation note instead of bubbling
/// the exception. The CLI consumes this result and continues with the DAO
/// fallback per strategy §3.2 / §8.
/// </summary>
public class ComDateMaximaProbeTests
{
    private static IrDatabase EmptyDb() => new()
    {
        Name = "Test",
        Forms = [], Reports = [], Queries = [], Tables = [],
        Relationships = [], Macros = [], Modules = [],
    };

    private sealed class FakeSampler : IDateMaximaSampler
    {
        private readonly IReadOnlyList<SampledDateMaximum> _samples;
        public FakeSampler(IReadOnlyList<SampledDateMaximum> samples) => _samples = samples;
        public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db) => _samples;
    }

    private sealed class ThrowingSampler : IDateMaximaSampler
    {
        private readonly string _message;
        public ThrowingSampler(string message) => _message = message;
        public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db)
            => throw new InvalidOperationException(_message);
    }

    private sealed class ThrowingDisposableSampler : IDateMaximaSampler, IDisposable
    {
        public bool Disposed { get; private set; }
        public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db)
            => throw new InvalidOperationException("simulated COM failure");
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Probe_Success_Wraps_Cached_Samples_And_Sets_ComAvailable()
    {
        var samples = new List<SampledDateMaximum>
        {
            new("tblX", new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), "com"),
        };
        var result = ComDateMaximaProbe.Probe(() => new FakeSampler(samples), EmptyDb());

        Assert.True(result.ComAvailable);
        Assert.Null(result.DegradationNote);
        Assert.Null(result.OriginalErrorMessage);
        Assert.NotNull(result.Primary);
        // Second call should return the cached samples without re-invoking the
        // factory — verify by calling Sample on the returned wrapper.
        var replayed = result.Primary!.Sample(EmptyDb());
        Assert.Single(replayed);
        Assert.Equal("tblX", replayed[0].TableName);
    }

    [Fact]
    public void Probe_When_Factory_Throws_Returns_Degradation_Note()
    {
        // Simulates AccessComSession constructor throwing (Access not installed,
        // file lock during ctor, etc).
        var result = ComDateMaximaProbe.Probe(
            () => throw new InvalidOperationException("Microsoft Access is not installed or registered."),
            EmptyDb());

        Assert.False(result.ComAvailable);
        Assert.Null(result.Primary);
        Assert.NotNull(result.DegradationNote);
        Assert.Contains("COM date sampling unavailable", result.DegradationNote);
        Assert.Contains("Microsoft Access is not installed", result.DegradationNote);
        Assert.Equal("Microsoft Access is not installed or registered.", result.OriginalErrorMessage);
    }

    [Fact]
    public void Probe_When_Sample_Throws_Returns_Degradation_Note()
    {
        // Simulates OpenCurrentDatabase / CurrentDb() throwing inside Sample().
        var result = ComDateMaximaProbe.Probe(
            () => new ThrowingSampler("OpenCurrentDatabase failed: file is locked by another process."),
            EmptyDb());

        Assert.False(result.ComAvailable);
        Assert.Null(result.Primary);
        Assert.NotNull(result.DegradationNote);
        Assert.Contains("OpenCurrentDatabase failed", result.DegradationNote);
        Assert.Contains("Per-table date signals fell back to DAO LastUpdated",
            result.DegradationNote);
    }

    [Fact]
    public void Probe_When_Sample_Throws_Disposes_Sampler_Instance()
    {
        // Best-effort dispose path: if Sample throws and the sampler is
        // IDisposable, the probe must dispose it before returning.
        var throwing = new ThrowingDisposableSampler();
        var result = ComDateMaximaProbe.Probe(() => throwing, EmptyDb());

        Assert.False(result.ComAvailable);
        Assert.True(throwing.Disposed);
    }

    [Fact]
    public void Probe_Null_Factory_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => ComDateMaximaProbe.Probe(null!, EmptyDb()));
    }
}
