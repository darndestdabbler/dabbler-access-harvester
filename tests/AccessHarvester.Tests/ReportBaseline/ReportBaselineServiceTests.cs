using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="ReportBaselineService"/>. Uses a fake
/// <see cref="IReportQueryExecutor"/> so the resolve → execute → compute
/// path is exercised without any OLE DB dependency. The fake records
/// the SQL + positional parameters it was called with, so tests can
/// assert that <see cref="ReportBaselineService.Capture"/> propagates
/// inputs faithfully.
/// </summary>
public class ReportBaselineServiceTests
{
    private sealed class FakeExecutor : IReportQueryExecutor
    {
        public List<(string Sql, IReadOnlyList<object?>? PositionalParameters)> Calls { get; } = new();
        public required Func<string, IReadOnlyList<object?>?, DataTable> ResultFor { get; init; }

        public DataTable Execute(string sql, IReadOnlyList<object?>? positionalParameters)
        {
            Calls.Add((sql, positionalParameters));
            return ResultFor(sql, positionalParameters);
        }
    }

    private static DataTable OneRow(int orderId, decimal total)
    {
        var t = new DataTable();
        t.Columns.Add("OrderID", typeof(int));
        t.Columns.Add("Total", typeof(decimal));
        t.Rows.Add(orderId, total);
        return t;
    }

    private static DataTable OneScalar(object? value, Type type)
    {
        var t = new DataTable();
        t.Columns.Add("Value", type);
        t.Rows.Add(value);
        return t;
    }

    [Fact]
    public void Capture_ResolvesObjectName_ToBracketedSelectStar_AndComputesAggregates()
    {
        var fake = new FakeExecutor { ResultFor = (_, _) => OneRow(1, 25m) };
        var svc = new ReportBaselineService(fake, harvesterVersion: "v-test");

        var input = new ReportAggregateInput
        {
            ReportName = "rptInvoice",
            RecordSource = "qryInvoiceDetails",
        };

        var aggregates = svc.Capture(input);

        var call = Assert.Single(fake.Calls);
        Assert.Equal("SELECT * FROM [qryInvoiceDetails]", call.Sql);
        Assert.Null(call.PositionalParameters);
        Assert.Equal(1, aggregates.RowCount);
        Assert.Equal(25m, aggregates.GrandTotals["Total"]);
        Assert.Equal("TableOrQueryName", aggregates.RecordSourceKind);
    }

    [Fact]
    public void Capture_PassesInlineSql_Verbatim_AndForwardsPositionalParameters()
    {
        const string sql = "PARAMETERS [Min] LONG; SELECT * FROM Orders WHERE OrderID >= [Min]";
        var fake = new FakeExecutor { ResultFor = (_, _) => OneRow(7, 100m) };
        var svc = new ReportBaselineService(fake, "v-test");

        var input = new ReportAggregateInput
        {
            ReportName = "rptByMin",
            RecordSource = sql,
            ParametersUsed = ImmutableSortedDictionary<string, object?>.Empty
                .Add("Min", 5),
        };

        var aggregates = svc.Capture(input, positionalParameters: new object?[] { 5 });

        var call = Assert.Single(fake.Calls);
        Assert.Equal(sql, call.Sql);
        Assert.NotNull(call.PositionalParameters);
        Assert.Single(call.PositionalParameters!);
        Assert.Equal(5, call.PositionalParameters![0]);
        Assert.Equal(sql, aggregates.ResolvedSql);
        Assert.Equal("InlineSql", aggregates.RecordSourceKind);
        Assert.Equal(5, aggregates.Provenance.ParametersUsed["Min"]);
    }

    [Fact]
    public void Capture_UsesSavedQuerySql_WhenProvided()
    {
        const string savedQuerySql = "SELECT OrderID, Total FROM Orders";
        var fake = new FakeExecutor { ResultFor = (_, _) => OneRow(2, 50m) };
        var svc = new ReportBaselineService(fake, "v-test");

        var input = new ReportAggregateInput
        {
            ReportName = "rptOrders",
            RecordSource = "qryOrders",
            SavedQuerySql = savedQuerySql,
        };

        var aggregates = svc.Capture(input);

        var call = Assert.Single(fake.Calls);
        Assert.Equal(savedQuerySql, call.Sql);
        Assert.Equal(savedQuerySql, aggregates.ResolvedSql);
        Assert.Equal("TableOrQueryName", aggregates.RecordSourceKind);
    }

    [Fact]
    public void Capture_InlinesSupportedNorthwindDateHelperFunctions_AndRecordsDerivedDefaults()
    {
        const string savedQuerySql =
            "SELECT * FROM Orders WHERE OrderDate Between reportParameterStartDate() And reportParameterEndDate()";
        var fake = new FakeExecutor
        {
            ResultFor = (sql, _) => sql switch
            {
                "SELECT Max([OrderDate]) AS [MaxOrderDate] FROM [Orders]"
                    => OneScalar(new DateTime(1998, 5, 6, 14, 30, 0), typeof(DateTime)),
                "SELECT * FROM Orders WHERE OrderDate Between #1998-02-06# And #1998-05-06#"
                    => OneRow(3, 75m),
                _ => throw new InvalidOperationException($"Unexpected SQL: {sql}"),
            },
        };
        var svc = new ReportBaselineService(fake, "v-test");

        var input = new ReportAggregateInput
        {
            ReportName = "rptSalesByEmployee",
            RecordSource = "qryrptSalesByEmployee",
            SavedQuerySql = savedQuerySql,
        };

        var aggregates = svc.Capture(input);

        Assert.Equal(2, fake.Calls.Count);
        Assert.Equal("SELECT Max([OrderDate]) AS [MaxOrderDate] FROM [Orders]", fake.Calls[0].Sql);
        Assert.Equal(
            "SELECT * FROM Orders WHERE OrderDate Between #1998-02-06# And #1998-05-06#",
            fake.Calls[1].Sql);
        Assert.Equal(fake.Calls[1].Sql, aggregates.ResolvedSql);
        Assert.Equal(new DateTime(1998, 2, 6), aggregates.Provenance.ParametersUsed["reportParameterStartDate"]);
        Assert.Equal(new DateTime(1998, 5, 6), aggregates.Provenance.ParametersUsed["reportParameterEndDate"]);
    }

    [Fact]
    public void CaptureAndWrite_WritesAggregatesJson_WithRoundTrippableProvenance()
    {
        var fake = new FakeExecutor { ResultFor = (_, _) => OneRow(99, 12.34m) };
        var svc = new ReportBaselineService(fake, "v-test");

        var input = new ReportAggregateInput
        {
            ReportName = "rptInvoice",
            RecordSource = "qryInvoice",
        };

        string root = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}");
        string outDir = Path.Combine(root, "AccessMetaData", "NorthwindDev",
            "reports-baseline", "rptInvoice");
        try
        {
            string written = svc.CaptureAndWrite(input, outDir);
            Assert.Equal(Path.Combine(outDir, "aggregates.json"), written);
            Assert.True(File.Exists(written));

            using var doc = JsonDocument.Parse(File.ReadAllText(written));
            Assert.Equal("rptInvoice", doc.RootElement.GetProperty("ReportName").GetString());
            Assert.Equal("v-test",
                doc.RootElement.GetProperty("Provenance").GetProperty("HarvesterVersion").GetString());
            Assert.Equal(ReportAggregateComputer.ContractLiteral,
                doc.RootElement.GetProperty("Provenance").GetProperty("AggregateContract").GetString());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_RejectsNullExecutor()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReportBaselineService(null!, harvesterVersion: "v"));
    }

    [Fact]
    public void Constructor_RejectsBlankHarvesterVersion()
    {
        var fake = new FakeExecutor { ResultFor = (_, _) => new DataTable() };
        Assert.Throws<ArgumentException>(() =>
            new ReportBaselineService(fake, harvesterVersion: "  "));
    }

    [Fact]
    public void CaptureAndWrite_RejectsBlankOutputDirectory()
    {
        var fake = new FakeExecutor { ResultFor = (_, _) => OneRow(1, 1m) };
        var svc = new ReportBaselineService(fake, "v");
        var input = new ReportAggregateInput { ReportName = "r", RecordSource = "t" };

        Assert.Throws<ArgumentException>(() => svc.CaptureAndWrite(input, " "));
    }
}
