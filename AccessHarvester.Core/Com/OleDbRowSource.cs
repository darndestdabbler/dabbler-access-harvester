using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Core.Com;

/// <summary>
/// OLE DB-backed implementation of <see cref="IRowSource"/>. Streams every
/// row of the requested column set as a column-name -> value dictionary so
/// the sanitizer can apply masking without knowing the source's storage
/// model.
///
/// <para>
/// Identifiers (table + column names) are validated through
/// <see cref="EscapeIdentifier"/> before bracket-quoting them into SQL —
/// the same defense-in-depth pattern <see cref="OleDbValueSampler"/> uses.
/// Hostile or malformed names are rejected with an
/// <see cref="ArgumentException"/> so the caller surfaces a clear error
/// rather than running a half-constructed query.
/// </para>
///
/// <para>
/// The provider probe iterates the ACE OLE DB versions from newest to
/// oldest and returns the first connection-string that opens cleanly. This
/// matches the policy in <see cref="OleDbDaoSchemaReader"/> and
/// <see cref="OleDbValueSampler"/> so the sanitizer reads from the same
/// provider the scanner used.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OleDbRowSource : IRowSource
{
    private readonly string _connectionString;

    public OleDbRowSource(string databasePath)
    {
        if (databasePath is null) throw new ArgumentNullException(nameof(databasePath));
        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"Database not found: {databasePath}", databasePath);
        _connectionString = BuildConnectionString(databasePath);
    }

    public IEnumerable<IReadOnlyDictionary<string, object?>> ReadRows(
        string tableName,
        IReadOnlyList<string> columnNames)
    {
        if (tableName    is null) throw new ArgumentNullException(nameof(tableName));
        if (columnNames  is null) throw new ArgumentNullException(nameof(columnNames));
        if (columnNames.Count == 0) yield break;

        string tableId = EscapeIdentifier(tableName);
        string columnList = string.Join(", ",
            columnNames.Select(c => "[" + EscapeIdentifier(c) + "]"));

        using var conn = new OleDbConnection(_connectionString);
        conn.Open();
        using var cmd = new OleDbCommand($"SELECT {columnList} FROM [{tableId}]", conn);
        using var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>(columnNames.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columnNames.Count; i++)
            {
                object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                dict[columnNames[i]] = value;
            }
            yield return dict;
        }
    }

    private static string BuildConnectionString(string path)
    {
        string[] providers = { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" };
        Exception? lastError = null;
        foreach (var provider in providers)
        {
            string cs = $"Provider={provider};Data Source={path};Persist Security Info=False;";
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
    /// Defense-in-depth identifier escape mirroring
    /// <see cref="OleDbValueSampler.EscapeIdentifier"/>. Disallows
    /// characters that would break bracketed quoting (`]`, `\0`,
    /// CR/LF) and returns the original string unchanged when safe.
    /// </summary>
    public static string EscapeIdentifier(string raw)
    {
        if (raw is null) throw new ArgumentNullException(nameof(raw));
        if (Regex.IsMatch(raw, @"[\]\0\r\n]"))
            throw new ArgumentException(
                $"Identifier '{raw}' contains characters that cannot be safely embedded in bracketed Access SQL.",
                nameof(raw));
        return raw;
    }
}
