using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.Pipeline.Cleanup;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Cleanup;

public class DateMaximaSamplerTests
{
    private static IrDatabase Db(params IrTable[] tables) => new()
    {
        Name = "Test", Forms = [], Reports = [], Queries = [], Tables = tables,
        Relationships = [], Macros = [], Modules = [],
    };

    [Fact]
    public void IrLastUpdatedSampler_Returns_Tables_With_LastUpdated()
    {
        var db = Db(new IrTable
        {
            Name = "tblClients",
            ColumnCount = 1,
            LastUpdated = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });
        var sampler = new IrLastUpdatedSampler();
        var result = sampler.Sample(db);
        Assert.Single(result);
        Assert.Equal("tblClients", result[0].TableName);
        Assert.Equal("dao-metadata", result[0].Source);
    }

    [Fact]
    public void IrLastUpdatedSampler_Excludes_Linked_Tables()
    {
        var db = Db(new IrTable
        {
            Name = "tblLinked",
            ColumnCount = 1,
            LastUpdated = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Connect = "ODBC;DSN=SomeServer",
        });
        var sampler = new IrLastUpdatedSampler();
        Assert.Empty(sampler.Sample(db));
    }

    [Fact]
    public void IrLastUpdatedSampler_Excludes_System_Tables()
    {
        var db = Db(
            new IrTable
            {
                Name = "MSysObjects",
                ColumnCount = 1,
                LastUpdated = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new IrTable
            {
                Name = "USysSettings",
                ColumnCount = 1,
                LastUpdated = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });
        var sampler = new IrLastUpdatedSampler();
        Assert.Empty(sampler.Sample(db));
    }

    [Fact]
    public void IrLastUpdatedSampler_Excludes_Tables_Without_LastUpdated()
    {
        var db = Db(new IrTable { Name = "tblBlank", ColumnCount = 1 });
        var sampler = new IrLastUpdatedSampler();
        Assert.Empty(sampler.Sample(db));
    }

    [Fact]
    public void Analyser_Produces_Mixed_Source_Per_Table_When_Primary_Sampler_Is_COM()
    {
        // Strategy §3.2 / §5: per-table source. A table with DateTime columns AND a
        // primary (COM) sample uses 'com'; a table without DateTime columns falls
        // back to DAO LastUpdated.
        var harvest = new DateOnly(2026, 5, 28);
        var dtWith = new IrTable
        {
            Name = "tblWithDates",
            ColumnCount = 1,
            DateTimeColumnNames = ["EnrollmentDate"],
            LastUpdated = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var dtWithout = new IrTable
        {
            Name = "tblNoDates",
            ColumnCount = 1,
            DateTimeColumnNames = [],
            LastUpdated = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var db = new IrDatabase
        {
            Name = "Test",
            Forms = [], Reports = [], Queries = [],
            Tables = [dtWith, dtWithout],
            Relationships = [], Macros = [], Modules = [],
        };

        var primary = new StubDateMaximaSampler(new[]
        {
            new SampledDateMaximum("tblWithDates",
                new DateTimeOffset(2022, 3, 15, 0, 0, 0, TimeSpan.Zero), "com"),
        });
        var fallback = new IrLastUpdatedSampler();

        var findings = CleanupAnalyser.Run(db, fallback, harvest,
            thresholdDays: 90, comAvailable: true, primaryDateSampler: primary);

        Assert.Equal("com", findings.DateSignalSource);
        var withDateSig = findings.Findings
            .Where(f => f.ObjectName == "tblWithDates")
            .SelectMany(f => f.FiredSignals)
            .OfType<DateSignal>()
            .Single();
        Assert.Equal("com", withDateSig.Source);

        var withoutDateSig = findings.Findings
            .Where(f => f.ObjectName == "tblNoDates")
            .SelectMany(f => f.FiredSignals)
            .OfType<DateSignal>()
            .Single();
        Assert.Equal("dao-metadata", withoutDateSig.Source);
    }

    private sealed class StubDateMaximaSampler : IDateMaximaSampler
    {
        private readonly IReadOnlyList<SampledDateMaximum> _samples;
        public StubDateMaximaSampler(IReadOnlyList<SampledDateMaximum> samples) => _samples = samples;
        public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db) => _samples;
    }

    [SkippableFact]
    public void Com_Sampler_Returns_DateTime_Max_From_Real_Database()
    {
        string accdbPath = Path.Combine(FindRepoRoot(), "samples", "HomeHealthCare.accdb");
        Skip.IfNot(File.Exists(accdbPath), $"Sample database missing: {accdbPath}");
        Skip.IfNot(OperatingSystem.IsWindows(), "Access COM only available on Windows.");
        var progId = Type.GetTypeFromProgID("Access.Application");
        Skip.If(progId is null, "Access COM not registered on this host.");

        // Build a minimal IR with one table that has a DateTime column.
        var db = new IrDatabase
        {
            Name = "HomeHealthCare",
            Forms = [], Reports = [], Queries = [],
            Tables = [new IrTable
            {
                Name = "tblClients",
                ColumnCount = 1,
                DateTimeColumnNames = ["DateOfBirth", "EnrollmentDate"],
            }],
            Relationships = [], Macros = [], Modules = [],
        };

        using var sampler = new global::AccessHarvester.Core.Com.AccessDateMaximaSampler(accdbPath);
        var samples = sampler.Sample(db);
        Assert.Contains(samples, s => s.TableName == "tblClients" && s.Source == "com");
    }

    private static string FindRepoRoot()
    {
        // Walk up from test bin/ to find samples/ alongside the .sln.
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "samples")) && Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Path.GetDirectoryName(dir)!;
        }
        return AppContext.BaseDirectory;
    }
}
