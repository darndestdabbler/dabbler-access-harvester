using System;
using System.Collections.Generic;
using System.Data;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="SumAggregateEvaluator"/>. Every fixture is a
/// hand-built <see cref="DataTable"/>; no OLE DB, no Access process. Covers the
/// closed footer-aggregate grammar (Sum over arithmetic of numeric result-set
/// columns) and every "unsupported → Skipped" outcome the evaluator surfaces.
/// </summary>
public class SumAggregateEvaluatorTests
{
    private static DataTable BuildTable(IEnumerable<(string name, Type type)> columns, params object?[][] rows)
    {
        var t = new DataTable();
        foreach (var (name, type) in columns)
            t.Columns.Add(name, type);
        foreach (var r in rows)
            t.Rows.Add(r);
        return t;
    }

    private static decimal AssertComputed(SumAggregateResult result)
    {
        var computed = Assert.IsType<SumAggregateComputed>(result);
        return computed.Value;
    }

    // ── Derivable Sum aggregates ─────────────────────────────────────────

    [Fact]
    public void Evaluate_SumOfBareColumn_SumsTheColumn()
    {
        var table = BuildTable(
            new[] { ("OrderTotal", typeof(double)) },
            new object?[] { 10.5 }, new object?[] { 4.25 }, new object?[] { 5.0 });

        var result = SumAggregateEvaluator.Evaluate("=Sum([OrderTotal])", table);

        Assert.Equal(19.75m, AssertComputed(result));
    }

    [Fact]
    public void Evaluate_RptSummarySubtotalExpression_ComputesExtendedPriceSum()
    {
        // A representative report footer display total:
        //   =Sum((1-[Discount])*[Quantity]*[UnitPrice])
        // Row 1: (1-0.1)*2*100 = 180 ; Row 2: (1-0)*3*10 = 30 → 210.
        var table = BuildTable(
            new[]
            {
                ("UnitPrice", typeof(decimal)),
                ("Quantity", typeof(int)),
                ("Discount", typeof(double)),
            },
            new object?[] { 100m, 2, 0.1 },
            new object?[] { 10m, 3, 0.0 });

        var result = SumAggregateEvaluator.Evaluate(
            "=Sum((1-[Discount])*[Quantity]*[UnitPrice])", table);

        Assert.Equal(210m, AssertComputed(result));
    }

    [Fact]
    public void Evaluate_RowWithNullReferencedColumn_IsExcludedFromSum()
    {
        // Access Sum ignores a row whose argument evaluates to Null.
        var table = BuildTable(
            new[] { ("A", typeof(int)), ("B", typeof(int)) },
            new object?[] { 2, 3 },
            new object?[] { 5, DBNull.Value },   // B null → row excluded
            new object?[] { 4, 1 });

        var result = SumAggregateEvaluator.Evaluate("=Sum([A]*[B])", table);

        Assert.Equal(10m, AssertComputed(result)); // 2*3 + 4*1
    }

    [Fact]
    public void Evaluate_EmptyResultSet_ReturnsZero()
    {
        var table = BuildTable(new[] { ("OrderTotal", typeof(decimal)) });

        var result = SumAggregateEvaluator.Evaluate("=Sum([OrderTotal])", table);

        Assert.Equal(0m, AssertComputed(result));
    }

    [Fact]
    public void Evaluate_ColumnReference_IsCaseInsensitive()
    {
        var table = BuildTable(
            new[] { ("OrderTotal", typeof(int)) },
            new object?[] { 7 }, new object?[] { 8 });

        var result = SumAggregateEvaluator.Evaluate("=Sum([orderTOTAL])", table);

        Assert.Equal(15m, AssertComputed(result));
    }

    [Fact]
    public void Evaluate_DecimalArithmetic_IsExact_NoFloatingPointDrift()
    {
        // 0.1 + 0.1 + 0.1 must equal exactly 0.3 in decimal (it would not in
        // binary floating point). The column is decimal so values are exact.
        var table = BuildTable(
            new[] { ("Price", typeof(decimal)) },
            new object?[] { 0.1m }, new object?[] { 0.1m }, new object?[] { 0.1m });

        var result = SumAggregateEvaluator.Evaluate("=Sum([Price])", table);

        Assert.Equal(0.3m, AssertComputed(result));
    }

    [Fact]
    public void Evaluate_UnaryNegation_IsSupported()
    {
        var table = BuildTable(
            new[] { ("A", typeof(int)) },
            new object?[] { 5 }, new object?[] { 3 });

        var result = SumAggregateEvaluator.Evaluate("=Sum(-[A])", table);

        Assert.Equal(-8m, AssertComputed(result));
    }

    [Fact]
    public void Evaluate_Division_IsSupported_AndDivByZeroExcludesRow()
    {
        var table = BuildTable(
            new[] { ("A", typeof(decimal)), ("B", typeof(decimal)) },
            new object?[] { 10m, 2m },   // 5
            new object?[] { 7m, 0m },    // div by zero → row excluded
            new object?[] { 9m, 3m });   // 3

        var result = SumAggregateEvaluator.Evaluate("=Sum([A]/[B])", table);

        Assert.Equal(8m, AssertComputed(result));
    }

    // ── Unsupported → explicit Skipped reasons ───────────────────────────

    [Theory]
    [InlineData("=IIf([TaxStatusID]=1,[taxrate]*Sum((1-[Discount])*[Quantity]*[UnitPrice]),0)")]
    [InlineData("=Now()")]
    [InlineData("=Avg([OrderTotal])")]
    [InlineData("=Count(*)")]
    public void Evaluate_UnsupportedAggregateFunction_RecordsUnsupportedFunction(string expr)
    {
        var table = BuildTable(new[] { ("OrderTotal", typeof(int)) }, new object?[] { 1 });

        var result = SumAggregateEvaluator.Evaluate(expr, table);

        var unsupported = Assert.IsType<SumAggregateUnsupported>(result);
        Assert.Equal(SumAggregateEvaluator.KindUnsupportedFunction, unsupported.Kind);
    }

    [Theory]
    [InlineData("=[ShippingFee]")]                  // bare scalar at footer scope
    [InlineData("=[txtSubTotal]+[txtTax]+[txtShipping]")] // references other controls
    [InlineData("=Sum([A])+1")]                     // compound, not a single Sum
    [InlineData("=Sum([A])+Sum([B])")]              // two aggregates
    [InlineData("=5")]                               // bare literal
    public void Evaluate_NotASingleAggregate_RecordsNonAggregate(string expr)
    {
        var table = BuildTable(
            new[] { ("A", typeof(int)), ("B", typeof(int)) },
            new object?[] { 1, 2 });

        var result = SumAggregateEvaluator.Evaluate(expr, table);

        var unsupported = Assert.IsType<SumAggregateUnsupported>(result);
        Assert.Equal(SumAggregateEvaluator.KindNonAggregate, unsupported.Kind);
    }

    [Fact]
    public void Evaluate_UnresolvedColumn_RecordsUnresolvedColumn()
    {
        var table = BuildTable(new[] { ("A", typeof(int)) }, new object?[] { 1 });

        var result = SumAggregateEvaluator.Evaluate("=Sum([DoesNotExist])", table);

        var unsupported = Assert.IsType<SumAggregateUnsupported>(result);
        Assert.Equal(SumAggregateEvaluator.KindUnresolvedColumn, unsupported.Kind);
    }

    [Fact]
    public void Evaluate_NonNumericColumn_RecordsNonNumericColumn()
    {
        var table = BuildTable(
            new[] { ("Name", typeof(string)) },
            new object?[] { "Acme" });

        var result = SumAggregateEvaluator.Evaluate("=Sum([Name])", table);

        var unsupported = Assert.IsType<SumAggregateUnsupported>(result);
        Assert.Equal(SumAggregateEvaluator.KindNonNumericColumn, unsupported.Kind);
    }

    [Theory]
    [InlineData("=Sum([A]&[B])")]   // string concatenation operator
    [InlineData("=Sum()")]           // empty argument
    [InlineData("=Sum([A],[B])")]    // argument separator
    [InlineData("=Sum([A]")]         // unbalanced — missing ')'
    [InlineData("=Sum(.)")]          // lone '.' — malformed numeric literal
    [InlineData("=Sum(99999999999999999999999999999999999999999)")] // overflows decimal
    public void Evaluate_MalformedInnerExpression_RecordsParseError(string expr)
    {
        var table = BuildTable(
            new[] { ("A", typeof(int)), ("B", typeof(int)) },
            new object?[] { 1, 2 });

        var result = SumAggregateEvaluator.Evaluate(expr, table);

        var unsupported = Assert.IsType<SumAggregateUnsupported>(result);
        Assert.Equal(SumAggregateEvaluator.KindParseError, unsupported.Kind);
    }

    [Fact]
    public void Evaluate_DecimalOverflow_RecordsNumericOverflow()
    {
        var table = BuildTable(
            new[] { ("A", typeof(decimal)) },
            new object?[] { decimal.MaxValue }, new object?[] { decimal.MaxValue });

        var result = SumAggregateEvaluator.Evaluate("=Sum([A])", table);

        var unsupported = Assert.IsType<SumAggregateUnsupported>(result);
        Assert.Equal(SumAggregateEvaluator.KindNumericOverflow, unsupported.Kind);
    }
}
