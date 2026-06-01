using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="ReportAggregatesWriter"/>: shape is
/// JsonElement-stable and writes round-trip equal to the source.
/// </summary>
public class ReportAggregatesWriterTests
{
    private static ReportAggregates Sample() => new()
    {
        ReportName = "rptInvoice",
        RecordSource = "qryInvoice",
        RecordSourceKind = "TableOrQueryName",
        ResolvedSql = "SELECT * FROM [qryInvoice]",
        RowCount = 7,
        NumericColumns = ImmutableArray.Create("OrderID", "Total"),
        GrandTotals = ImmutableSortedDictionary<string, decimal?>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("OrderID", 28m)
            .Add("Total", 142.50m),
        GroupLevels = ImmutableArray.Create(new ReportGroupLevelAggregate
        {
            Level = 0,
            GroupOn = "Country",
            Buckets = ImmutableArray.Create(new GroupBucket
            {
                IsNull = false,
                Key = "USA",
                RowCount = 5,
                Subtotals = ImmutableSortedDictionary<string, decimal?>.Empty
                    .WithComparers(StringComparer.Ordinal)
                    .Add("Total", 100m),
            }),
        }),
        DisplayTotals = ImmutableArray.Create(new DisplayTotal
        {
            Control = "txtSubTotal",
            Section = "ReportFooter",
            Expression = "=Sum((1-[Discount])*[Quantity]*[UnitPrice])",
            ComputedValue = 142.50m,
        }),
        Skipped = ImmutableArray<SkippedItem>.Empty,
        Provenance = new ReportAggregateProvenance
        {
            HarvesterVersion = "1.0.0",
            AggregateContract = ReportAggregateComputer.ContractLiteral,
            ContractNote = ReportAggregateComputer.ContractNote,
            ParametersUsed = ImmutableSortedDictionary<string, object?>.Empty
                .WithComparers(StringComparer.Ordinal),
        },
    };

    [Fact]
    public void Serialize_ProducesValidJson_WithExpectedTopLevelShape()
    {
        string json = ReportAggregatesWriter.Serialize(Sample());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("rptInvoice", root.GetProperty("ReportName").GetString());
        Assert.Equal("qryInvoice", root.GetProperty("RecordSource").GetString());
        Assert.Equal("TableOrQueryName", root.GetProperty("RecordSourceKind").GetString());
        Assert.Equal(7, root.GetProperty("RowCount").GetInt64());

        var grand = root.GetProperty("GrandTotals");
        Assert.Equal(28m, grand.GetProperty("OrderID").GetDecimal());
        Assert.Equal(142.50m, grand.GetProperty("Total").GetDecimal());

        var displayTotals = root.GetProperty("DisplayTotals");
        Assert.Equal(1, displayTotals.GetArrayLength());
        var dt = displayTotals[0];
        Assert.Equal("txtSubTotal", dt.GetProperty("Control").GetString());
        Assert.Equal("ReportFooter", dt.GetProperty("Section").GetString());
        Assert.Equal(142.50m, dt.GetProperty("ComputedValue").GetDecimal());

        var prov = root.GetProperty("Provenance");
        Assert.Equal(ReportAggregateComputer.ContractLiteral,
            prov.GetProperty("AggregateContract").GetString());
        Assert.Equal("1.0.0", prov.GetProperty("HarvesterVersion").GetString());
    }

    [Fact]
    public void Serialize_IsDeterministic_AcrossRepeatedCalls()
    {
        var sample = Sample();
        string first = ReportAggregatesWriter.Serialize(sample);
        string second = ReportAggregatesWriter.Serialize(sample);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Serialize_GrandTotals_AreEmittedInOrdinalKeyOrder()
    {
        // Build two samples whose GrandTotals are constructed in
        // different insertion orders. Because they round-trip through
        // ReportAggregateComputer-canonical ImmutableSortedDictionary
        // (Ordinal), the serialized JSON must be byte-identical.
        ReportAggregates Build(params (string col, decimal? val)[] cols) => new()
        {
            ReportName = "r",
            RecordSource = "t",
            RecordSourceKind = "TableOrQueryName",
            ResolvedSql = "SELECT * FROM [t]",
            RowCount = 0,
            NumericColumns = ImmutableArray<string>.Empty,
            GrandTotals = cols.Aggregate(
                ImmutableSortedDictionary<string, decimal?>.Empty.WithComparers(StringComparer.Ordinal),
                (acc, c) => acc.Add(c.col, c.val)),
            GroupLevels = ImmutableArray<ReportGroupLevelAggregate>.Empty,
            DisplayTotals = ImmutableArray<DisplayTotal>.Empty,
            Skipped = ImmutableArray<SkippedItem>.Empty,
            Provenance = new ReportAggregateProvenance
            {
                HarvesterVersion = "v",
                AggregateContract = ReportAggregateComputer.ContractLiteral,
                ContractNote = ReportAggregateComputer.ContractNote,
                ParametersUsed = ImmutableSortedDictionary<string, object?>.Empty
                    .WithComparers(StringComparer.Ordinal),
            },
        };

        string ascending = ReportAggregatesWriter.Serialize(
            Build(("Apples", 1m), ("Bananas", 2m), ("Cherries", 3m)));
        string descending = ReportAggregatesWriter.Serialize(
            Build(("Cherries", 3m), ("Bananas", 2m), ("Apples", 1m)));

        Assert.Equal(ascending, descending);

        using var doc = JsonDocument.Parse(ascending);
        var keys = doc.RootElement.GetProperty("GrandTotals")
            .EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "Apples", "Bananas", "Cherries" }, keys);
    }

    [Fact]
    public void Serialize_ParametersUsed_AreEmittedInOrdinalKeyOrder_AndSnapshotted()
    {
        // Two ParametersUsed dictionaries built in different insertion
        // orders. They must produce byte-identical JSON because the
        // computer snapshots into ImmutableSortedDictionary at compute
        // time and the writer enumerates in that order.
        var table = new System.Data.DataTable();
        table.Columns.Add("X", typeof(int));

        var resolved = new ResolvedRecordSource
        {
            Original = "SELECT X FROM Foo",
            Kind = RecordSourceKind.InlineSql,
            SqlToExecute = "SELECT X FROM Foo",
        };

        var a = new ReportAggregateInput
        {
            ReportName = "r",
            RecordSource = "SELECT X FROM Foo",
            ParametersUsed = ImmutableSortedDictionary<string, object?>.Empty
                .Add("Zeta", 1)
                .Add("Alpha", 2)
                .Add("Mu", 3),
        };
        var b = new ReportAggregateInput
        {
            ReportName = "r",
            RecordSource = "SELECT X FROM Foo",
            ParametersUsed = ImmutableSortedDictionary<string, object?>.Empty
                .Add("Alpha", 2)
                .Add("Mu", 3)
                .Add("Zeta", 1),
        };

        string aJson = ReportAggregatesWriter.Serialize(
            ReportAggregateComputer.Compute(a, resolved, table, "v"));
        string bJson = ReportAggregatesWriter.Serialize(
            ReportAggregateComputer.Compute(b, resolved, table, "v"));

        Assert.Equal(aJson, bJson);

        using var doc = JsonDocument.Parse(aJson);
        var keys = doc.RootElement
            .GetProperty("Provenance")
            .GetProperty("ParametersUsed")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToArray();
        Assert.Equal(new[] { "Alpha", "Mu", "Zeta" }, keys);
    }

    [Fact]
    public void WriteToFile_WritesByteIdenticalToSerialize()
    {
        var sample = Sample();
        string expected = ReportAggregatesWriter.Serialize(sample);

        string path = Path.Combine(Path.GetTempPath(),
            $"aggregates-{Guid.NewGuid():N}.json");
        try
        {
            ReportAggregatesWriter.WriteToFile(sample, path);
            string actual = File.ReadAllText(path);
            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void WriteToFile_CreatesParentDirectoryWhenMissing()
    {
        var sample = Sample();
        string root = Path.Combine(Path.GetTempPath(), $"agg-{Guid.NewGuid():N}");
        string path = Path.Combine(root, "deep", "nested", "aggregates.json");
        try
        {
            ReportAggregatesWriter.WriteToFile(sample, path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
