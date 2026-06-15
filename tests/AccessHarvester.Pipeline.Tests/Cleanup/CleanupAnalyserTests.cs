using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Cleanup;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Cleanup;

public class CleanupAnalyserTests
{
    private static IrDatabase Db(
        IrFormOrReport[]? forms = null,
        IrQuery[]? queries = null,
        IrTable[]? tables = null,
        IrRelationship[]? relationships = null,
        IrMacro[]? macros = null,
        IrModule[]? modules = null)
        => new()
        {
            Name = "TestDb",
            Forms = forms ?? [],
            Reports = [],
            Queries = queries ?? [],
            Tables = tables ?? [],
            Relationships = relationships ?? [],
            Macros = macros ?? [],
            Modules = modules ?? [],
        };

    private static IrFormOrReport Form(string name, string? recordSource = null) => new()
    {
        Name = name,
        ObjectType = "Form",
        RecordSource = recordSource,
        Controls = [],
        MacroActions = [],
        Properties = new Dictionary<string, string>(),
    };

    private static IrTable Table(string name, DateTimeOffset? lastUpdated = null) => new()
    {
        Name = name,
        ColumnCount = 1,
        LastUpdated = lastUpdated,
        DateTimeColumnNames = lastUpdated is null ? [] : ["UpdatedAt"],
    };

    private sealed class FixedSampler(IReadOnlyList<SampledDateMaximum> samples) : IDateMaximaSampler
    {
        public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db) => samples;
    }

    [Fact]
    public void Run_Does_Not_Emit_CrossReferenceOnly_Findings()
    {
        var db = Db(forms: [Form("frmSwitchboard")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: false);

        Assert.Empty(findings.Findings);
    }

    [Fact]
    public void Run_Emits_Medium_Finding_For_NamePattern_Only()
    {
        var db = Db(tables: [Table("tblClients_OLD")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: false);

        var finding = Assert.Single(findings.Findings);
        Assert.Equal("Table", finding.ObjectType);
        Assert.Equal("tblClients_OLD", finding.ObjectName);
        Assert.Equal(ConfidenceTiers.Medium, finding.ConfidenceTier);
        Assert.Contains(finding.FiredSignals, s => s.SignalType == "NamePattern");
        Assert.DoesNotContain(finding.FiredSignals, s => s.SignalType == "CrossReferenceAbsence");
    }

    [Fact]
    public void Run_Suppresses_TimeOnly_ZeroDate_Maximum_As_NoSignal()
    {
        // O-2-6 / headline misfire: a time-only Date/Time column's MAX() presents as
        // 1899-12-30 HH:MM:SS (the OLE epoch base + fractional day). The guard must
        // treat that as "no date signal" — no ~46176-day-stale finding. The table
        // name carries no staleness pattern, so the only possible signal is the date.
        var timeOnlyMax = new DateTimeOffset(1899, 12, 30, 14, 30, 0, TimeSpan.Zero);
        var db = Db(tables: [Table("tblClientServiceNeeds")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([new SampledDateMaximum("tblClientServiceNeeds", timeOnlyMax, "com")]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: true);

        Assert.Empty(findings.Findings);
    }

    [Fact]
    public void Run_Suppresses_Exact_OleEpoch_Maximum_As_NoSignal()
    {
        // The pure zero-date (00:00:00 on the epoch base) is likewise no signal.
        // This also documents the accepted exact-epoch false-negative (014 R2): a
        // genuine record dated *exactly* 1899-12-30 would be suppressed too, but in
        // Access that value is the zero/default and is undecidable from the sentinel
        // without per-column sampler provenance — deferred to DW-16.
        var zeroDate = new DateTimeOffset(1899, 12, 30, 0, 0, 0, TimeSpan.Zero);
        var db = Db(tables: [Table("tblData")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([new SampledDateMaximum("tblData", zeroDate, "com")]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: true);

        Assert.Empty(findings.Findings);
    }

    [Fact]
    public void Run_Guard_Does_Not_Suppress_NamePattern_When_Date_Is_ZeroDate()
    {
        // The guard suppresses only the *date* signal. A zero-date maximum on a
        // table whose name still matches a staleness pattern keeps its Medium
        // name-pattern finding (with no DateSignal).
        var zeroDate = new DateTimeOffset(1899, 12, 30, 9, 0, 0, TimeSpan.Zero);
        var db = Db(tables: [Table("tblClients_OLD")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([new SampledDateMaximum("tblClients_OLD", zeroDate, "com")]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: true);

        var finding = Assert.Single(findings.Findings);
        Assert.Equal(ConfidenceTiers.Medium, finding.ConfidenceTier);
        Assert.Contains(finding.FiredSignals, s => s.SignalType == "NamePattern");
        Assert.DoesNotContain(finding.FiredSignals, s => s.SignalType == "DateSignal");
    }

    [Fact]
    public void Run_Keeps_Real_Old_Date_DateSignal_Finding()
    {
        // Regression guard: a legitimately old-but-real date (just after the epoch
        // and well before the threshold) must still produce its Medium date finding.
        // This proves the guard targets only the OLE zero-date, not old real dates.
        var realOld = new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var db = Db(tables: [Table("tblData")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([new SampledDateMaximum("tblData", realOld, "com")]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: true);

        var finding = Assert.Single(findings.Findings);
        Assert.Equal(ConfidenceTiers.Medium, finding.ConfidenceTier);
        Assert.Contains(finding.FiredSignals, s => s.SignalType == "DateSignal");
    }

    [Fact]
    public void Run_Keeps_PreEpoch_RealDate_As_Stale_Finding()
    {
        // 014 R1 Major regression pin: a genuine maximum *before* the OLE epoch base
        // (e.g. 1890-01-01) is a real, genuinely-stale date and MUST still flag. The
        // guard matches the epoch day EXACTLY (==), so a "<=" floor that would have
        // wrongly suppressed this pre-1900 date is ruled out.
        var preEpoch = new DateTimeOffset(1890, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var db = Db(tables: [Table("tblData")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([new SampledDateMaximum("tblData", preEpoch, "com")]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: true);

        var finding = Assert.Single(findings.Findings);
        Assert.Equal(ConfidenceTiers.Medium, finding.ConfidenceTier);
        Assert.Contains(finding.FiredSignals, s => s.SignalType == "DateSignal");
    }

    [Fact]
    public void Run_Emits_High_Finding_For_NamePattern_And_DateSignal()
    {
        var staleAt = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var db = Db(tables: [Table("tblArchive_2023")]);

        var findings = CleanupAnalyser.Run(
            db,
            new FixedSampler([new SampledDateMaximum("tblArchive_2023", staleAt, "dao-metadata")]),
            new DateOnly(2026, 6, 2),
            thresholdDays: 90,
            comAvailable: false);

        var finding = Assert.Single(findings.Findings);
        Assert.Equal(ConfidenceTiers.High, finding.ConfidenceTier);
        Assert.Equal(1, finding.FiredSignals.Count(s => s.SignalType == "DateSignal"));
        Assert.True(finding.FiredSignals.Count(s => s.SignalType == "NamePattern") >= 1);
        Assert.Contains(finding.FiredSignals, s => s.SignalType == "NamePattern");
        Assert.Contains(finding.FiredSignals, s => s.SignalType == "DateSignal");
    }
}