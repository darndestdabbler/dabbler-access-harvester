using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using AccessHarvester.Pipeline.Classification;

namespace AccessHarvester.Core.Com;

/// <summary>
/// OLE DB-backed implementation of <see cref="IValueSampler"/>. Samples
/// values for non-key data columns, computes cardinality using a Jet/ACE-
/// compatible subquery (plan § 8 — Jet has no <c>COUNT(DISTINCT col)</c>),
/// detects PII value-pattern matches, and derives a canonical
/// <c>valueFormatPattern</c> via <see cref="ValueFormatDetector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Raw sampled values never leave the sampler — only derived signals
/// (booleans + the canonical format pattern) are stored on the returned
/// <see cref="ColumnStatistics"/>. This is the privacy invariant from
/// plan § 4 enforced at the source.
/// </para>
/// <para>
/// Identifiers (table + column names) come from the OLE DB schema
/// rowsets but are still escaped before being interpolated into SQL
/// — see <see cref="EscapeIdentifier"/>. This is defense-in-depth: a
/// hostile or merely sloppily-named .accdb (column names containing
/// <c>]</c>, brackets, or null characters) cannot poison the generated
/// SQL into running unexpected queries. SQL-construction failures are
/// surfaced as <see cref="DegradationKind.SamplingError"/> degradations
/// rather than silently collapsed to a zero count.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class OleDbValueSampler : IValueSampler, IDegradationReporter
{
    public IReadOnlyList<ColumnStatistics> Sample(string databasePath, RawSchema schema, ScanConfig config)
        => SampleCore(databasePath, schema, config, sink: null);

    public IReadOnlyList<ColumnStatistics> SampleWithDegradations(
        string databasePath, RawSchema schema, ScanConfig config,
        IList<ClassificationDegradation> sink)
        => SampleCore(databasePath, schema, config, sink);

    public RawSchema ReadWithDegradations(string databasePath, IList<ClassificationDegradation> sink)
        => throw new NotSupportedException(
            "OleDbValueSampler does not implement schema reading; use OleDbDaoSchemaReader.");

    private static IReadOnlyList<ColumnStatistics> SampleCore(
        string databasePath, RawSchema schema, ScanConfig config,
        IList<ClassificationDegradation>? sink)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"Database not found: {databasePath}");

        var results = new List<ColumnStatistics>();
        string cs = BuildConnectionString(databasePath);

        using var conn = new OleDbConnection(cs);
        conn.Open();

        var valueRegexes = config.PiiValuePatterns
            .Where(id => PiiDictionary.DefaultPiiValuePatterns.ContainsKey(id))
            .Select(id => PiiDictionary.DefaultPiiValuePatterns[id])
            .ToArray();

        foreach (var table in schema.Tables)
        {
            if (table.IsLinkedTable) continue; // skip linked tables' value sampling
            string tableId;
            try
            {
                tableId = EscapeIdentifier(table.Name);
            }
            catch (ArgumentException ex)
            {
                // Unsafe table identifier — surface as a degradation and
                // skip the table rather than aborting the entire scan.
                // Real-world `.accdb` files in messy environments do
                // sometimes carry identifiers we cannot safely quote.
                sink?.Add(new ClassificationDegradation(
                    Kind: DegradationKind.SamplingError,
                    Location: table.Name,
                    Detail: $"Table identifier rejected by escaper: {ex.Message}"));
                continue;
            }

            int rowCount;
            try
            {
                rowCount = QueryScalarInt(conn, $"SELECT COUNT(*) FROM [{tableId}]");
            }
            catch (Exception ex)
            {
                sink?.Add(new ClassificationDegradation(
                    Kind: DegradationKind.SamplingError,
                    Location: table.Name,
                    Detail: $"Row-count query failed: {ex.Message}"));
                continue;
            }

            foreach (var col in table.Columns)
            {
                string colId;
                try
                {
                    colId = EscapeIdentifier(col.Name);
                }
                catch (ArgumentException ex)
                {
                    sink?.Add(new ClassificationDegradation(
                        Kind: DegradationKind.SamplingError,
                        Location: $"{table.Name}.{col.Name}",
                        Detail: $"Column identifier rejected by escaper: {ex.Message}"));
                    continue;
                }
                bool sampleValues = ShouldSampleValues(col);

                int nullCount = 0;
                if (rowCount > 0)
                {
                    try
                    {
                        nullCount = QueryScalarInt(conn,
                            $"SELECT COUNT(*) FROM [{tableId}] WHERE [{colId}] IS NULL");
                    }
                    catch (Exception ex)
                    {
                        sink?.Add(new ClassificationDegradation(
                            Kind: DegradationKind.SamplingError,
                            Location: $"{table.Name}.{col.Name}",
                            Detail: $"Null-count query failed: {ex.Message}"));
                    }
                }

                int cardinality = 0;
                if (rowCount > 0)
                {
                    try
                    {
                        // Plan § 8: Jet/ACE does NOT support COUNT(DISTINCT col); use a subquery.
                        cardinality = QueryScalarInt(conn,
                            $"SELECT COUNT(*) FROM (SELECT DISTINCT [{colId}] FROM [{tableId}] WHERE [{colId}] IS NOT NULL)");
                    }
                    catch (Exception ex)
                    {
                        sink?.Add(new ClassificationDegradation(
                            Kind: DegradationKind.SamplingError,
                            Location: $"{table.Name}.{col.Name}",
                            Detail: $"Cardinality query failed: {ex.Message}"));
                    }
                }

                bool   piiValueMatch = false;
                string? formatPattern = null;
                if (sampleValues && rowCount > 0)
                {
                    IReadOnlyList<string?> sampled;
                    try
                    {
                        sampled = SampleValues(conn, tableId, colId, config.ValueSampleSize);
                    }
                    catch (Exception ex)
                    {
                        sink?.Add(new ClassificationDegradation(
                            Kind: DegradationKind.SamplingError,
                            Location: $"{table.Name}.{col.Name}",
                            Detail: $"Value-sample query failed: {ex.Message}"));
                        sampled = Array.Empty<string?>();
                    }
                    if (valueRegexes.Length > 0)
                        piiValueMatch = sampled.Any(v => v is not null && valueRegexes.Any(r => r.IsMatch(v)));
                    formatPattern = ValueFormatDetector.Detect(sampled);
                }

                results.Add(new ColumnStatistics(
                    TableName: table.Name,
                    ColumnName: col.Name,
                    RowCount: rowCount,
                    NullCount: nullCount,
                    Cardinality: cardinality,
                    PiiValueMatch: piiValueMatch,
                    ValueFormatPattern: formatPattern));
            }
        }
        return results;
    }

    /// <summary>FK / PK / OLE / multi-value columns are not value-sampled.</summary>
    private static bool ShouldSampleValues(RawColumn col) =>
        !col.IsForeignKey
     && !col.IsAutoNumber
     && !col.IsOleObject
     && !col.IsMultiValue;

    private static int QueryScalarInt(OleDbConnection conn, string sql)
    {
        using var cmd = new OleDbCommand(sql, conn);
        object? v = cmd.ExecuteScalar();
        if (v is null || v is DBNull) return 0;
        return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<string?> SampleValues(
        OleDbConnection conn, string tableId, string colId, int sampleSize)
    {
        if (sampleSize <= 0) return Array.Empty<string?>();
        var list = new List<string?>(sampleSize);
        // Jet/ACE supports TOP N — use it to bound the result set instead of
        // streaming the whole column. The TOP literal is a fixed integer
        // (not from an external source), so it does not need escaping.
        string sql = $"SELECT TOP {sampleSize} [{colId}] FROM [{tableId}] WHERE [{colId}] IS NOT NULL";
        using var cmd = new OleDbCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read() && list.Count < sampleSize)
        {
            object? v;
            try
            {
                v = rdr.GetValue(0);
            }
            catch (InvalidOperationException)
            {
                // Some OLE Object / memo blobs and unrecognized types throw
                // on GetValue; that is genuinely "this row's value is not
                // representable as a managed object." Skip the row but
                // keep iterating — this is a per-row failure, not a query
                // failure, and is the one narrow case where swallowing is
                // correct.
                continue;
            }
            if (v is null || v is DBNull) continue;
            list.Add(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
        }
        return list;
    }

    private static string BuildConnectionString(string path)
    {
        string[] providers = { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" };
        Exception? lastError = null;
        foreach (var provider in providers)
        {
            string cs = AceConnectionString.Build(provider, path);
            try
            {
                using var probe = new OleDbConnection(cs);
                probe.Open();
                return cs;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }
        throw new InvalidOperationException(
            "No working Microsoft.ACE.OLEDB provider was found. " +
            $"Last error: {lastError?.Message}");
    }

    /// <summary>
    /// Defense-in-depth escaping for an Access bracketed identifier. Inside
    /// <c>[...]</c> Access treats <c>]</c> as the terminator; the only way
    /// to embed a literal close bracket is to drop or escape it. Access
    /// SQL has no defined doubling convention for <c>]</c>, so for the
    /// scanner's use case (read-only SELECT against a database the user
    /// is inspecting) we reject names containing characters that cannot
    /// be safely represented and require the caller to surface a
    /// degradation. Normal identifiers (PascalCase, underscores, digits,
    /// spaces) pass through unchanged.
    /// </summary>
    public static string EscapeIdentifier(string raw)
    {
        if (raw is null) throw new ArgumentNullException(nameof(raw));
        // Disallow chars that would break bracketed quoting. The
        // ']' and '\0' set is the practical minimum; '[' inside a
        // bracketed identifier is fine.
        if (Regex.IsMatch(raw, @"[\]\0\r\n]"))
            throw new ArgumentException(
                $"Identifier '{raw}' contains characters that cannot be safely embedded in bracketed Access SQL.",
                nameof(raw));
        return raw;
    }
}
