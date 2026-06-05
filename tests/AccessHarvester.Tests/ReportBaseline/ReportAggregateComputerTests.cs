using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="ReportAggregateComputer.Compute"/>. Every
/// fixture is a hand-built <see cref="DataTable"/>; no OLE DB, no file IO,
/// no Access process. Covers the contract semantics (RecordSourceResultSet)
/// the downstream consumer relies on.
/// </summary>
public class ReportAggregateComputerTests
{
    private const string Version = "1.2.3-test";

    private static ResolvedRecordSource ObjectNameResolved(string name) =>
        new()
        {
            Original = name,
            Kind = RecordSourceKind.TableOrQueryName,
            SqlToExecute = $"SELECT * FROM [{name}]",
        };

    private static ResolvedRecordSource SqlResolved(string sql) =>
        new()
        {
            Original = sql,
            Kind = RecordSourceKind.InlineSql,
            SqlToExecute = sql,
        };

    private static DataTable BuildTable(IEnumerable<(string name, Type type)> columns, params object?[][] rows)
    {
        var t = new DataTable();
        foreach (var (name, type) in columns)
            t.Columns.Add(name, type);
        foreach (var r in rows)
            t.Rows.Add(r);
        return t;
    }

    // ── Empty result sets ────────────────────────────────────────────────

    [Fact]
    public void Compute_EmptyResultSet_ReportsZeroRowCount_AndEmptyAggregates()
    {
        var table = BuildTable(new[] { ("OrderID", typeof(int)), ("Total", typeof(decimal)) });

        var input = new ReportAggregateInput
        {
            ReportName = "rptSummary",
            RecordSource = "qrySummary",
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qrySummary"), table, Version);

        Assert.Equal(0, result.RowCount);
        Assert.Equal(new[] { "OrderID", "Total" }, result.NumericColumns);
        Assert.Equal(0m, result.GrandTotals["OrderID"]);
        Assert.Equal(0m, result.GrandTotals["Total"]);
        Assert.Empty(result.GroupLevels);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Compute_ResultSetWithNoNumericColumns_ProducesEmptyTotals()
    {
        var table = BuildTable(
            new[] { ("Name", typeof(string)), ("Address", typeof(string)) },
            new object?[] { "alpha", "1 main st" },
            new object?[] { "beta", "2 main st" });

        var input = new ReportAggregateInput
        {
            ReportName = "rptRoster",
            RecordSource = "Customers",
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Customers"), table, Version);

        Assert.Equal(2, result.RowCount);
        Assert.Empty(result.NumericColumns);
        Assert.Empty(result.GrandTotals);
    }

    // ── Grand totals across numeric types ───────────────────────────────

    [Fact]
    public void Compute_GrandTotals_SumNumericColumns_AcrossMixedNumericTypes()
    {
        var table = BuildTable(
            new[] { ("OrderID", typeof(int)), ("Qty", typeof(short)), ("Unit", typeof(decimal)), ("Ratio", typeof(double)) },
            new object?[] { 1, (short)10, 4.50m, 1.5 },
            new object?[] { 2, (short)20, 7.25m, 2.5 },
            new object?[] { 3, (short)5, 1.00m, DBNull.Value });

        var input = new ReportAggregateInput
        {
            ReportName = "rptLedger",
            RecordSource = "Orders",
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Orders"), table, Version);

        Assert.Equal(3, result.RowCount);
        Assert.Equal(6m, result.GrandTotals["OrderID"]);
        Assert.Equal(35m, result.GrandTotals["Qty"]);
        Assert.Equal(12.75m, result.GrandTotals["Unit"]);
        Assert.Equal(4m, result.GrandTotals["Ratio"]);
    }

    [Fact]
    public void Compute_GrandTotals_IgnoreNullsInSum()
    {
        var table = BuildTable(
            new[] { ("Price", typeof(decimal)) },
            new object?[] { 10m },
            new object?[] { DBNull.Value },
            new object?[] { 5m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptPrices",
            RecordSource = "Prices",
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Prices"), table, Version);

        Assert.Equal(15m, result.GrandTotals["Price"]);
    }

    // ── Group-level aggregates ──────────────────────────────────────────

    [Fact]
    public void Compute_GroupLevel_BySimpleFieldName_ProducesBucketsOrderedByKey()
    {
        var table = BuildTable(
            new[] { ("Country", typeof(string)), ("Total", typeof(decimal)) },
            new object?[] { "USA", 10m },
            new object?[] { "UK", 5m },
            new object?[] { "USA", 7m },
            new object?[] { "DE", 3m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptTotalsByRegion",
            RecordSource = "Sales",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "Country",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Sales"), table, Version);

        var level = Assert.Single(result.GroupLevels);
        Assert.Equal(0, level.Level);
        Assert.Equal("Country", level.GroupOn);

        var keys = level.Buckets.Select(b => b.Key).ToArray();
        Assert.Equal(new[] { "DE", "UK", "USA" }, keys);

        var usa = level.Buckets.Single(b => b.Key == "USA");
        Assert.Equal(2, usa.RowCount);
        Assert.Equal(17m, usa.Subtotals["Total"]);

        var de = level.Buckets.Single(b => b.Key == "DE");
        Assert.Equal(1, de.RowCount);
        Assert.Equal(3m, de.Subtotals["Total"]);
    }

    [Fact]
    public void Compute_GroupLevel_WithNullKeys_UsesIsNullFlag_NotStringSentinel()
    {
        var table = BuildTable(
            new[] { ("Category", typeof(string)), ("Amount", typeof(decimal)) },
            new object?[] { "A", 1m },
            new object?[] { DBNull.Value, 2m },
            new object?[] { DBNull.Value, 4m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptByCategory",
            RecordSource = "Things",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "Category",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Things"), table, Version);

        var level = Assert.Single(result.GroupLevels);
        var nullBucket = level.Buckets.Single(b => b.IsNull);
        Assert.Equal(string.Empty, nullBucket.Key);
        Assert.Equal(2, nullBucket.RowCount);
        Assert.Equal(6m, nullBucket.Subtotals["Amount"]);
    }

    [Fact]
    public void Compute_GroupLevel_LiteralParenNullParen_String_DoesNotMergeWithNullBucket()
    {
        // Defends the contract that a real string value of "(null)" is
        // distinct from a DBNull-source bucket. Prior to the IsNull flag,
        // both would collapse into a single bucket keyed "(null)".
        var table = BuildTable(
            new[] { ("Category", typeof(string)), ("Amount", typeof(decimal)) },
            new object?[] { "(null)", 1m },       // literal string
            new object?[] { "(null)", 2m },       // literal string
            new object?[] { DBNull.Value, 4m },   // actual null
            new object?[] { "real", 8m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptCollisionGuard",
            RecordSource = "Things",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "Category",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Things"), table, Version);

        var level = Assert.Single(result.GroupLevels);
        Assert.Equal(3, level.Buckets.Count);

        var nullBucket = level.Buckets.Single(b => b.IsNull);
        Assert.Equal(1, nullBucket.RowCount);
        Assert.Equal(4m, nullBucket.Subtotals["Amount"]);

        var literalNullBucket = level.Buckets.Single(b => !b.IsNull && b.Key == "(null)");
        Assert.Equal(2, literalNullBucket.RowCount);
        Assert.Equal(3m, literalNullBucket.Subtotals["Amount"]);

        var realBucket = level.Buckets.Single(b => b.Key == "real");
        Assert.False(realBucket.IsNull);
        Assert.Equal(8m, realBucket.Subtotals["Amount"]);
    }

    [Fact]
    public void Compute_GroupLevel_NullBucketSortsBeforeNonNullBuckets()
    {
        var table = BuildTable(
            new[] { ("Country", typeof(string)) },
            new object?[] { "USA" },
            new object?[] { DBNull.Value },
            new object?[] { "DE" });

        var input = new ReportAggregateInput
        {
            ReportName = "r",
            RecordSource = "Sales",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "Country",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Sales"), table, Version);

        var level = Assert.Single(result.GroupLevels);
        Assert.True(level.Buckets[0].IsNull);
        Assert.Equal(new[] { "DE", "USA" },
            level.Buckets.Skip(1).Select(b => b.Key).ToArray());
    }

    [Fact]
    public void Compute_GroupLevel_WithDateKey_FormatsAsRoundTripIso()
    {
        var table = BuildTable(
            new[] { ("OrderDate", typeof(DateTime)), ("Total", typeof(decimal)) },
            new object?[] { new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), 10m },
            new object?[] { new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc), 20m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptByDate",
            RecordSource = "Orders",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "OrderDate",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Orders"), table, Version);

        var level = Assert.Single(result.GroupLevels);
        Assert.All(level.Buckets, b =>
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}T", b.Key));
    }

    // ── Typed-value bucket identity ─────────────────────────────────────

    [Fact]
    public void Compute_GroupLevel_NumericallyEqualDecimals_MergeIntoOneBucket()
    {
        // Defends typed-value group identity: 1.00m / 1.0m / 1m all
        // format differently as decimal strings but are numerically
        // equal, so they must share a bucket. Pre-remediation the
        // formatted-string GroupBy produced three separate buckets.
        var table = BuildTable(
            new[] { ("Rate", typeof(decimal)), ("Amount", typeof(decimal)) },
            new object?[] { 1.00m, 10m },
            new object?[] { 1.0m, 20m },
            new object?[] { 1m,   30m },
            new object?[] { 2m,   100m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptByRate",
            RecordSource = "Sales",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "Rate",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Sales"), table, Version);

        var level = Assert.Single(result.GroupLevels);
        Assert.Equal(2, level.Buckets.Count);

        // Canonical key strips trailing zeros after the decimal point —
        // all three of 1m / 1.0m / 1.00m emit "1".
        var oneBucket = level.Buckets.Single(b => !b.IsNull && b.Key == "1");
        Assert.Equal(3, oneBucket.RowCount);
        Assert.Equal(60m, oneBucket.Subtotals["Amount"]);

        var twoBucket = level.Buckets.Single(b => b.Key == "2");
        Assert.Equal(1, twoBucket.RowCount);
        Assert.Equal(100m, twoBucket.Subtotals["Amount"]);
    }

    [Fact]
    public void Compute_GroupBucketKey_IsRowOrderInvariant_ForNumericallyEqualValues()
    {
        // Defends against the LINQ-GroupBy-exemplar drift the R3 verifier
        // flagged: regardless of which numeric form lands first in the
        // source row order, the emitted JSON Key for the merged bucket
        // is identical.
        DataTable Build(params decimal[] rates)
        {
            var t = new DataTable();
            t.Columns.Add("Rate", typeof(decimal));
            t.Columns.Add("Amount", typeof(decimal));
            foreach (var r in rates) t.Rows.Add(r, 1m);
            return t;
        }

        ReportAggregateInput Input() => new()
        {
            ReportName = "rptCanon",
            RecordSource = "Sales",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "Rate",
            }),
        };

        var resolved = ObjectNameResolved("Sales");

        var a = ReportAggregateComputer.Compute(Input(), resolved,
            Build(1m, 1.00m, 1.0m), Version);
        var b = ReportAggregateComputer.Compute(Input(), resolved,
            Build(1.00m, 1m, 1.0m), Version);
        var c = ReportAggregateComputer.Compute(Input(), resolved,
            Build(1.0m, 1.00m, 1m), Version);

        string aKey = Assert.Single(a.GroupLevels[0].Buckets).Key;
        string bKey = Assert.Single(b.GroupLevels[0].Buckets).Key;
        string cKey = Assert.Single(c.GroupLevels[0].Buckets).Key;
        Assert.Equal(aKey, bKey);
        Assert.Equal(aKey, cKey);
        Assert.Equal("1", aKey);

        // JSON must also be byte-identical across permutations.
        Assert.Equal(
            ReportAggregatesWriter.Serialize(a),
            ReportAggregatesWriter.Serialize(b));
        Assert.Equal(
            ReportAggregatesWriter.Serialize(a),
            ReportAggregatesWriter.Serialize(c));
    }

    [Fact]
    public void Compute_SkippedOrder_IsRowOrderInvariant_ForMultipleOverflowingBuckets()
    {
        // R3 fix: the Skipped list is sorted deterministically at the
        // end of Compute(). Two runs with the same logical content but
        // permuted row order must produce byte-identical Skipped.
        DataTable Build(params (string region, double amount)[] rows)
        {
            var t = new DataTable();
            t.Columns.Add("Region", typeof(string));
            t.Columns.Add("Amount", typeof(double));
            foreach (var (r, a) in rows) t.Rows.Add(r, a);
            return t;
        }

        ReportAggregateInput Input() => new()
        {
            ReportName = "rptLedger",
            RecordSource = "Orders",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "Region",
            }),
        };

        // Two buckets ("East", "West") both overflow when summed.
        var ordered = ReportAggregateComputer.Compute(Input(), ObjectNameResolved("Orders"),
            Build(("East", double.MaxValue), ("East", double.MaxValue),
                  ("West", double.MaxValue), ("West", double.MaxValue)), Version);
        var permuted = ReportAggregateComputer.Compute(Input(), ObjectNameResolved("Orders"),
            Build(("West", double.MaxValue), ("East", double.MaxValue),
                  ("West", double.MaxValue), ("East", double.MaxValue)), Version);

        Assert.Equal(
            ordered.Skipped.Select(s => (s.Kind, s.Detail)),
            permuted.Skipped.Select(s => (s.Kind, s.Detail)));
        Assert.Equal(
            ReportAggregatesWriter.Serialize(ordered),
            ReportAggregatesWriter.Serialize(permuted));
    }

    [Fact]
    public void Compute_GroupLevel_DecimalAndDoubleEqualValues_MergeIntoOneBucket()
    {
        // Mixed CLR numeric types representing the same number must
        // share a bucket. Column is object so individual rows can
        // carry decimal or double.
        var table = new DataTable();
        table.Columns.Add("X", typeof(object));
        table.Columns.Add("Amount", typeof(decimal));
        table.Rows.Add(100m, 1m);
        table.Rows.Add(100.0d, 2m);
        table.Rows.Add(100, 4m);

        var input = new ReportAggregateInput
        {
            ReportName = "rptMixed",
            RecordSource = "X",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "X",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("X"), table, Version);

        var level = Assert.Single(result.GroupLevels);
        var bucket = Assert.Single(level.Buckets);
        Assert.Equal(3, bucket.RowCount);
        Assert.Equal(7m, bucket.Subtotals["Amount"]);
    }

    // ── Skipped reasons ─────────────────────────────────────────────────

    [Fact]
    public void Compute_GroupLevelWithAccessExpression_RecordsSkipped()
    {
        var table = BuildTable(
            new[] { ("OrderDate", typeof(DateTime)), ("Total", typeof(decimal)) },
            new object?[] { DateTime.UtcNow, 1m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptByYear",
            RecordSource = "Orders",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "=Year([OrderDate])",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Orders"), table, Version);

        Assert.Empty(result.GroupLevels);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("GroupLevelExpression", skipped.Kind);
        Assert.Contains("=Year([OrderDate])", skipped.Detail);
    }

    [Fact]
    public void Compute_GroupLevelOnMissingColumn_RecordsSkipped()
    {
        var table = BuildTable(
            new[] { ("OrderID", typeof(int)) },
            new object?[] { 1 });

        var input = new ReportAggregateInput
        {
            ReportName = "rptLedger",
            RecordSource = "Orders",
            GroupLevels = ImmutableArray.Create(new ReportGroupLevelKey
            {
                Level = 0,
                GroupOn = "CustomerName",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Orders"), table, Version);

        Assert.Empty(result.GroupLevels);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("GroupLevelMissingColumn", skipped.Kind);
        Assert.Contains("CustomerName", skipped.Detail);
    }

    [Fact]
    public void Compute_NumericOverflow_OnDoubleSum_RecordsNullSumAndSkippedItem()
    {
        var table = BuildTable(
            new[] { ("BigNumber", typeof(double)) },
            new object?[] { double.MaxValue },
            new object?[] { double.MaxValue });

        var input = new ReportAggregateInput
        {
            ReportName = "rptBig",
            RecordSource = "Big",
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("Big"), table, Version);

        Assert.Null(result.GrandTotals["BigNumber"]);
        Assert.Contains(result.Skipped, s => s.Kind == "NumericOverflow");
    }

    // ── Provenance ──────────────────────────────────────────────────────

    [Fact]
    public void Compute_StampsProvenance_WithContractAndParameters()
    {
        var table = BuildTable(new[] { ("X", typeof(int)) });

        var parameters = ImmutableDictionary<string, object?>.Empty
            .Add("MinOrderId", 100)
            .Add("ShipCountry", "USA");

        var input = new ReportAggregateInput
        {
            ReportName = "rptParametrized",
            RecordSource = "SELECT X FROM Foo WHERE OrderID >= ? AND ShipCountry = ?",
            ParametersUsed = parameters,
        };

        var resolved = SqlResolved(input.RecordSource);

        var result = ReportAggregateComputer.Compute(input, resolved, table, Version);

        Assert.Equal(ReportAggregateComputer.ContractLiteral, result.Provenance.AggregateContract);
        Assert.Equal(ReportAggregateComputer.ContractNote, result.Provenance.ContractNote);
        Assert.Equal(Version, result.Provenance.HarvesterVersion);
        Assert.Equal(parameters, result.Provenance.ParametersUsed);
        Assert.Equal("InlineSql", result.RecordSourceKind);
        Assert.Equal(input.RecordSource, result.ResolvedSql);
    }

    // ── Argument validation ─────────────────────────────────────────────

    [Fact]
    public void Compute_RejectsBlankHarvesterVersion()
    {
        var table = BuildTable(new[] { ("X", typeof(int)) });
        var input = new ReportAggregateInput { ReportName = "r", RecordSource = "t" };

        Assert.Throws<ArgumentException>(() =>
            ReportAggregateComputer.Compute(input, ObjectNameResolved("t"), table, " "));
    }

    // ── Display totals (footer aggregate expressions) ────────────────────

    private static FooterAggregateControl ReportFooterControl(
        string name, string expression, bool runningSum = false) =>
        new()
        {
            ControlName = name,
            Section = "ReportFooter",
            IsReportFooter = true,
            Expression = expression,
            RunningSum = runningSum,
        };

    [Fact]
    public void Compute_ReportFooterSumExpression_EmitsDisplayTotal()
    {
        // The rptSummary footer subtotal: =Sum((1-[Discount])*[Quantity]*[UnitPrice]).
        // Row1: (1-0.1)*2*100 = 180 ; Row2: (1-0)*3*10 = 30 → 210.
        var table = BuildTable(
            new[]
            {
                ("UnitPrice", typeof(decimal)),
                ("Quantity", typeof(int)),
                ("Discount", typeof(double)),
            },
            new object?[] { 100m, 2, 0.1 },
            new object?[] { 10m, 3, 0.0 });

        var input = new ReportAggregateInput
        {
            ReportName = "rptSummary",
            RecordSource = "qrySummary",
            FooterAggregates = ImmutableArray.Create(
                ReportFooterControl("txtSubTotal", "=Sum((1-[Discount])*[Quantity]*[UnitPrice])")),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qrySummary"), table, Version);

        var displayTotal = Assert.Single(result.DisplayTotals);
        Assert.Equal("txtSubTotal", displayTotal.Control);
        Assert.Equal("ReportFooter", displayTotal.Section);
        Assert.Equal("=Sum((1-[Discount])*[Quantity]*[UnitPrice])", displayTotal.Expression);
        Assert.Equal(210m, displayTotal.ComputedValue);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Compute_ReportFooterSumOfColumn_MatchesGrandTotalOfThatColumn()
    {
        // rptTotalsByStaff / rptTotalsByItem footer: =Sum([OrderTotal]).
        // The derived display total must equal the result-set grand total of
        // the same column — the like-for-like cross-check.
        var table = BuildTable(
            new[] { ("OrderTotal", typeof(decimal)) },
            new object?[] { 100.25m }, new object?[] { 50.75m }, new object?[] { 9.00m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptTotalsByStaff",
            RecordSource = "qryTotals",
            FooterAggregates = ImmutableArray.Create(
                ReportFooterControl("GrandTotalSum", "=Sum([OrderTotal])")),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qryTotals"), table, Version);

        var displayTotal = Assert.Single(result.DisplayTotals);
        Assert.Equal(result.GrandTotals["OrderTotal"], displayTotal.ComputedValue);
        Assert.Equal(160.00m, displayTotal.ComputedValue);
    }

    [Fact]
    public void Compute_GroupFooterAggregate_RoutedToSkipped()
    {
        var table = BuildTable(
            new[] { ("OrderTotal", typeof(decimal)) }, new object?[] { 1m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptTotalsByItem",
            RecordSource = "qryTotals",
            FooterAggregates = ImmutableArray.Create(new FooterAggregateControl
            {
                ControlName = "SumOfOrderTotal",
                Section = "GroupFooter1",
                IsReportFooter = false,
                Expression = "=Sum([OrderTotal])",
            }),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qryTotals"), table, Version);

        Assert.Empty(result.DisplayTotals);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("DisplayTotalGroupFooter", skipped.Kind);
        Assert.Contains("SumOfOrderTotal", skipped.Detail);
    }

    [Fact]
    public void Compute_RunningSumFooter_RoutedToSkipped_EvenWhenExpressionWouldParse()
    {
        var table = BuildTable(
            new[] { ("OrderTotal", typeof(decimal)) }, new object?[] { 1m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptRunning",
            RecordSource = "qryTotals",
            FooterAggregates = ImmutableArray.Create(
                ReportFooterControl("txtRunning", "=Sum([OrderTotal])", runningSum: true)),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qryTotals"), table, Version);

        Assert.Empty(result.DisplayTotals);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("DisplayTotalRunningSum", skipped.Kind);
    }

    [Fact]
    public void Compute_UnsupportedFunctionFooter_RoutedToSkipped()
    {
        var table = BuildTable(
            new[] { ("TaxStatusID", typeof(int)), ("taxrate", typeof(double)),
                    ("Discount", typeof(double)), ("Quantity", typeof(int)), ("UnitPrice", typeof(decimal)) },
            new object?[] { 1, 0.1, 0.0, 1, 10m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptSummary",
            RecordSource = "qrySummary",
            FooterAggregates = ImmutableArray.Create(
                ReportFooterControl("txtTax",
                    "=IIf([TaxStatusID]=1,[taxrate]*Sum((1-[Discount])*[Quantity]*[UnitPrice]),0)")),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qrySummary"), table, Version);

        Assert.Empty(result.DisplayTotals);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("DisplayTotalUnsupportedFunction", skipped.Kind);
    }

    [Fact]
    public void Compute_NonAggregateFooterControls_RoutedToSkipped()
    {
        var table = BuildTable(
            new[] { ("ShippingFee", typeof(decimal)) }, new object?[] { 9m });

        var input = new ReportAggregateInput
        {
            ReportName = "rptSummary",
            RecordSource = "qrySummary",
            FooterAggregates = ImmutableArray.Create(
                ReportFooterControl("txtShipping", "=[ShippingFee]"),
                ReportFooterControl("txtTotal", "=[txtSubTotal]+[txtTax]+[txtShipping]")),
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qrySummary"), table, Version);

        Assert.Empty(result.DisplayTotals);
        Assert.Equal(2, result.Skipped.Count);
        Assert.All(result.Skipped, s => Assert.Equal("DisplayTotalNonAggregate", s.Kind));
    }

    [Fact]
    public void Compute_NoFooterAggregates_EmitsEmptyDisplayTotals()
    {
        // srptClientBudgets / srptClientCurrentNeeds have no =-bound footer
        // controls at all: no display totals, nothing skipped on their behalf.
        var table = BuildTable(
            new[] { ("Amount", typeof(decimal)) }, new object?[] { 5m });

        var input = new ReportAggregateInput
        {
            ReportName = "srptClientBudgets",
            RecordSource = "qryBudgets",
        };

        var result = ReportAggregateComputer.Compute(
            input, ObjectNameResolved("qryBudgets"), table, Version);

        Assert.Empty(result.DisplayTotals);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Compute_DisplayTotals_AreSorted_RowAndInputOrderInvariant()
    {
        var table = BuildTable(
            new[] { ("A", typeof(int)), ("B", typeof(int)) },
            new object?[] { 1, 10 }, new object?[] { 2, 20 });

        FooterAggregateControl ctlA = ReportFooterControl("zSumA", "=Sum([A])");
        FooterAggregateControl ctlB = ReportFooterControl("aSumB", "=Sum([B])");

        var forward = ReportAggregateComputer.Compute(
            new ReportAggregateInput
            {
                ReportName = "r",
                RecordSource = "t",
                FooterAggregates = ImmutableArray.Create(ctlA, ctlB),
            },
            ObjectNameResolved("t"), table, Version);

        var reversed = ReportAggregateComputer.Compute(
            new ReportAggregateInput
            {
                ReportName = "r",
                RecordSource = "t",
                FooterAggregates = ImmutableArray.Create(ctlB, ctlA),
            },
            ObjectNameResolved("t"), table, Version);

        // Sorted by (Section, Control) ordinal — "aSumB" precedes "zSumA"
        // regardless of input order.
        Assert.Equal(
            new[] { "aSumB", "zSumA" },
            forward.DisplayTotals.Select(d => d.Control));
        Assert.Equal(
            forward.DisplayTotals.Select(d => (d.Control, d.ComputedValue)),
            reversed.DisplayTotals.Select(d => (d.Control, d.ComputedValue)));
    }
}
