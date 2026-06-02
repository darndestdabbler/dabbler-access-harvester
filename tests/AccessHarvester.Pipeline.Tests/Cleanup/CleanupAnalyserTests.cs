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