using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using System.Linq;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Computes a <see cref="ReportAggregates"/> document from a result set
/// already materialized as a <see cref="DataTable"/>. Purely functional —
/// no OLE DB, no file IO — so the entire aggregate contract is hermetically
/// testable on hand-built fixture tables.
///
/// <para>
/// The aggregator implements the resolved S1 contract: <em>query-result
/// aggregates</em>. It does not evaluate Access expression syntax
/// (<c>=Sum(...)</c>, <c>=IIf(...)</c>, etc.); group levels whose key is
/// an expression are surfaced as a Skipped item rather than computed.
/// </para>
/// </summary>
public static class ReportAggregateComputer
{
    /// <summary>
    /// The contract literal stamped into
    /// <see cref="ReportAggregateProvenance.AggregateContract"/>. Bump
    /// (mint a new value) only when the aggregate semantics change.
    /// </summary>
    public const string ContractLiteral = "RecordSourceResultSet";

    /// <summary>
    /// The human-readable contract note stamped into
    /// <see cref="ReportAggregateProvenance.ContractNote"/>. Phrased for
    /// the downstream AI consumer that reads the document.
    /// </summary>
    public const string ContractNote =
        "Aggregates reflect the report's RecordSource query result set " +
        "(executed via ACE OLE DB), not the rendered report's footer-control " +
        "expressions. Parity comparisons must compare like-for-like.";

    /// <summary>
    /// Compute aggregates for the given <paramref name="input"/> against
    /// <paramref name="resultSet"/>. The <paramref name="resolved"/>
    /// argument is propagated into <see cref="ReportAggregates.ResolvedSql"/>
    /// and <see cref="ReportAggregates.RecordSourceKind"/> verbatim so
    /// callers can be sure the SQL they ran matches what is reported.
    /// </summary>
    public static ReportAggregates Compute(
        ReportAggregateInput input,
        ResolvedRecordSource resolved,
        DataTable resultSet,
        string harvesterVersion)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (resolved is null) throw new ArgumentNullException(nameof(resolved));
        if (resultSet is null) throw new ArgumentNullException(nameof(resultSet));
        if (string.IsNullOrWhiteSpace(harvesterVersion))
            throw new ArgumentException("HarvesterVersion is required.", nameof(harvesterVersion));

        var numericColumns = resultSet.Columns
            .Cast<DataColumn>()
            .Where(c => IsNumeric(c.DataType))
            .Select(c => c.ColumnName)
            .ToImmutableArray();

        var skipped = ImmutableArray.CreateBuilder<SkippedItem>();
        var grandTotals = ComputeColumnSums(resultSet.Rows.Cast<DataRow>(), numericColumns, skipped, bucketLabel: "(grand)");

        var groupAggregates = ImmutableArray.CreateBuilder<ReportGroupLevelAggregate>();
        foreach (var level in input.GroupLevels.OrderBy(l => l.Level))
        {
            if (IsAccessExpression(level.GroupOn))
            {
                skipped.Add(new SkippedItem
                {
                    Kind = "GroupLevelExpression",
                    Detail = $"Level {level.Level} GroupOn is an Access expression " +
                             $"('{level.GroupOn}'); expression evaluation is not " +
                             "part of the RecordSourceResultSet contract.",
                });
                continue;
            }

            if (!resultSet.Columns.Contains(level.GroupOn))
            {
                skipped.Add(new SkippedItem
                {
                    Kind = "GroupLevelMissingColumn",
                    Detail = $"Level {level.Level} GroupOn '{level.GroupOn}' is not " +
                             "a column in the executed result set.",
                });
                continue;
            }

            groupAggregates.Add(BuildGroupLevel(level, resultSet, numericColumns, skipped));
        }

        return new ReportAggregates
        {
            ReportName = input.ReportName,
            RecordSource = input.RecordSource,
            RecordSourceKind = resolved.Kind.ToString(),
            ResolvedSql = resolved.SqlToExecute,
            RowCount = resultSet.Rows.Count,
            NumericColumns = numericColumns,
            GrandTotals = grandTotals,
            GroupLevels = groupAggregates.ToImmutable(),
            Skipped = skipped
                .OrderBy(s => s.Kind, StringComparer.Ordinal)
                .ThenBy(s => s.Detail, StringComparer.Ordinal)
                .ToImmutableArray(),
            Provenance = new ReportAggregateProvenance
            {
                HarvesterVersion = harvesterVersion,
                AggregateContract = ContractLiteral,
                ContractNote = ContractNote,
                ParametersUsed = input.ParametersUsed.ToImmutableSortedDictionary(
                    kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
            },
        };
    }

    private static ReportGroupLevelAggregate BuildGroupLevel(
        ReportGroupLevelKey level,
        DataTable resultSet,
        ImmutableArray<string> numericColumns,
        ImmutableArray<SkippedItem>.Builder skipped)
    {
        // Group on the underlying typed value (TypedValueComparer normalizes
        // numeric and DateTime equality so e.g. decimal 1.00m / 1.0m / 1m
        // collapse into one bucket). Format only at the emission boundary
        // so the JSON key is deterministic; null source values flip the
        // IsNull flag and carry an empty formatted Key.
        var buckets = resultSet.Rows
            .Cast<DataRow>()
            .GroupBy(r => r[level.GroupOn], TypedValueComparer.Instance)
            .Select(g =>
            {
                bool isNull = g.Key is null or DBNull;
                string formatted = isNull ? string.Empty : CanonicalGroupKey(g.Key!);
                return new GroupBucket
                {
                    IsNull = isNull,
                    Key = formatted,
                    RowCount = g.LongCount(),
                    Subtotals = ComputeColumnSums(
                        g, numericColumns, skipped,
                        bucketLabel: $"level={level.Level} key={(isNull ? "(null)" : formatted)}"),
                };
            })
            .OrderBy(b => b.IsNull ? 0 : 1)
            .ThenBy(b => b.Key, StringComparer.Ordinal)
            .ToImmutableArray();

        return new ReportGroupLevelAggregate
        {
            Level = level.Level,
            GroupOn = level.GroupOn,
            Buckets = buckets,
        };
    }

    private static IReadOnlyDictionary<string, decimal?> ComputeColumnSums(
        IEnumerable<DataRow> rows,
        ImmutableArray<string> numericColumns,
        ImmutableArray<SkippedItem>.Builder skipped,
        string bucketLabel)
    {
        if (numericColumns.IsDefaultOrEmpty)
            return ImmutableSortedDictionary<string, decimal?>.Empty.WithComparers(StringComparer.Ordinal);

        var materialized = rows.ToList();
        var totals = ImmutableSortedDictionary.CreateBuilder<string, decimal?>(StringComparer.Ordinal);

        foreach (var column in numericColumns)
        {
            try
            {
                decimal acc = 0m;
                foreach (var row in materialized)
                {
                    object value = row[column];
                    if (value is null || value is DBNull) continue;
                    acc = checked(acc + Convert.ToDecimal(value, CultureInfo.InvariantCulture));
                }
                totals[column] = acc;
            }
            catch (OverflowException)
            {
                totals[column] = null;
                skipped.Add(new SkippedItem
                {
                    Kind = "NumericOverflow",
                    Detail = $"Column '{column}' sum overflowed decimal precision " +
                             $"in bucket {bucketLabel}; recorded as null.",
                });
            }
        }

        return totals.ToImmutable();
    }

    private static bool IsNumeric(Type t) =>
        t == typeof(byte) || t == typeof(sbyte) ||
        t == typeof(short) || t == typeof(ushort) ||
        t == typeof(int) || t == typeof(uint) ||
        t == typeof(long) || t == typeof(ulong) ||
        t == typeof(float) || t == typeof(double) ||
        t == typeof(decimal);

    private static bool IsAccessExpression(string groupOn) =>
        groupOn is not null && groupOn.TrimStart().StartsWith('=');

    /// <summary>
    /// Row-order-invariant string form for a group's typed key. Numeric
    /// values are normalized through <see cref="decimal"/> with trailing
    /// zeros after the decimal point stripped, so <c>1m</c>, <c>1.0m</c>,
    /// <c>1.00m</c>, and <c>1.0d</c> all emit the literal <c>"1"</c>
    /// regardless of which row LINQ's <c>GroupBy</c> happened to pick as
    /// the bucket exemplar. DateTime values use the round-trip <c>"O"</c>
    /// format. Other types fall back to invariant <see cref="object.ToString"/>.
    /// </summary>
    private static string CanonicalGroupKey(object value)
    {
        if (value is DateTime dt)
            return dt.ToString("O", CultureInfo.InvariantCulture);

        if (IsNumeric(value.GetType()))
        {
            try
            {
                decimal d = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                string s = d.ToString(CultureInfo.InvariantCulture);
                if (s.Contains('.'))
                    s = s.TrimEnd('0').TrimEnd('.');
                return s.Length == 0 ? "0" : s;
            }
            catch (OverflowException) { /* fall through to ToString */ }
        }

        if (value is IFormattable f)
            return f.ToString(null, CultureInfo.InvariantCulture);
        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Equality + hashing for group-bucket identity. Normalizes numeric
    /// values across CLR types (so <c>1.00m</c>, <c>1.0m</c>, <c>1m</c>,
    /// <c>1</c>, <c>1.0d</c> share a bucket) and treats <see cref="DBNull"/>
    /// as equal to CLR null. Falls back to <see cref="object.Equals(object?)"/>
    /// for anything else (strings, dates, etc.).
    /// </summary>
    private sealed class TypedValueComparer : IEqualityComparer<object?>
    {
        public static readonly TypedValueComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            bool xNull = x is null or DBNull;
            bool yNull = y is null or DBNull;
            if (xNull || yNull) return xNull && yNull;

            if (TryToDecimal(x!, out var xd) && TryToDecimal(y!, out var yd))
                return xd == yd;
            if (x is DateTime dx && y is DateTime dy) return dx == dy;
            return x.Equals(y);
        }

        public int GetHashCode(object? obj)
        {
            if (obj is null or DBNull) return 0;
            if (TryToDecimal(obj, out var d)) return d.GetHashCode();
            return obj.GetHashCode();
        }

        private static bool TryToDecimal(object value, out decimal result)
        {
            try
            {
                if (value is decimal d) { result = d; return true; }
                if (IsNumeric(value.GetType()))
                {
                    result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch (OverflowException) { /* fall through */ }
            catch (InvalidCastException) { /* fall through */ }
            result = 0m;
            return false;
        }
    }
}
