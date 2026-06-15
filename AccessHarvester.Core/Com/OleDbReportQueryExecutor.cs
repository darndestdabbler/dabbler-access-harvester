using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Runtime.Versioning;
using AccessHarvester.Core.ReportBaseline;

namespace AccessHarvester.Core.Com;

/// <summary>
/// Executes a single SQL statement against an Access database via ACE OLE
/// DB and materializes the result set as a <see cref="DataTable"/>.
/// Companion to <see cref="OleDbRowSource"/> and <see cref="OleDbValueSampler"/>:
/// uses the same provider-probe policy so a database that opened cleanly
/// in those readers also opens here.
///
/// <para>
/// Intended caller is the report-baseline aggregator: it takes the SQL
/// produced by <see cref="ReportBaseline.RecordSourceResolver"/> and feeds
/// the resulting <see cref="DataTable"/> into
/// <see cref="ReportBaseline.ReportAggregateComputer.Compute"/>. The
/// computer is purely functional; this executor is the only part of the
/// pipeline that touches OLE DB.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OleDbReportQueryExecutor : IReportQueryExecutor
{
    private readonly string _connectionString;

    public OleDbReportQueryExecutor(string databasePath)
    {
        if (databasePath is null) throw new ArgumentNullException(nameof(databasePath));
        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"Database not found: {databasePath}", databasePath);
        _connectionString = BuildConnectionString(databasePath);
    }

    /// <summary>
    /// Run <paramref name="sql"/> and return a fully-materialized
    /// <see cref="DataTable"/>. Optional <paramref name="positionalParameters"/>
    /// are bound to the SQL's <c>?</c> placeholders <em>in order</em> —
    /// OLE DB text commands ignore parameter names and bind strictly
    /// positionally, so the caller is responsible for arranging the list
    /// to match the SQL's placeholder order. Use the report's manifest
    /// metadata (placeholder list / param-name → position map) to derive
    /// that order; passing more or fewer values than the SQL expects
    /// surfaces as an <see cref="OleDbException"/> at <see cref="OleDbDataAdapter.Fill"/>.
    /// </summary>
    public DataTable Execute(
        string sql,
        IReadOnlyList<object?>? positionalParameters = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL is empty.", nameof(sql));

        using var conn = new OleDbConnection(_connectionString);
        conn.Open();
        using var cmd = new OleDbCommand(sql, conn);
        if (positionalParameters is { Count: > 0 })
        {
            for (int i = 0; i < positionalParameters.Count; i++)
            {
                // OleDb ignores the name for text commands; the index is
                // what binds. The synthesized name ("p0", "p1", ...) is
                // for debugger inspection only.
                cmd.Parameters.AddWithValue($"p{i}", positionalParameters[i] ?? DBNull.Value);
            }
        }

        var table = new DataTable();
        using var adapter = new OleDbDataAdapter(cmd);
        adapter.Fill(table);
        return table;
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
}
