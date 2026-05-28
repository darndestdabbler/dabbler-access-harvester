using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Collects the table and query names referenced in each query's SQL string.
/// Parses FROM-clause names (FROM tbl, JOIN tbl, FROM tbl1, tbl2) and strips
/// AS aliases. Only identifiers that look like Access object names (no spaces
/// unless bracket-quoted) are collected; sub-queries and inline SQL are skipped.
/// </summary>
public static class QueryTableReferenceCollector
{
    // FROM name [AS alias] and JOIN name [AS alias], including bracket-quoted names
    private static readonly Regex _fromJoinPattern =
        new(@"(?:FROM|JOIN)\s+(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_]*))\s*(?:AS\s+\w+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Comma-separated additional table names after FROM: FROM tbl1, tbl2
    private static readonly Regex _commaTablePattern =
        new(@",\s*(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_]*))\s*(?:AS\s+\w+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record QueryTableReference(string QueryName, string ReferencedObject);

    /// <summary>
    /// Returns one entry per (query, referenced-table-or-query) pair.
    /// </summary>
    public static IReadOnlyList<QueryTableReference> Collect(IrDatabase db)
    {
        var results = new List<QueryTableReference>();
        foreach (var query in db.Queries)
        {
            if (string.IsNullOrWhiteSpace(query.Sql)) continue;
            var names = ExtractTableNames(query.Sql);
            foreach (var name in names)
                results.Add(new QueryTableReference(query.Name, name));
        }
        return results;
    }

    internal static IReadOnlyList<string> ExtractTableNames(string sql)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();

        void TryAdd(string name)
        {
            string trimmed = name.Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                names.Add(trimmed);
        }

        foreach (Match m in _fromJoinPattern.Matches(sql))
            TryAdd(m.Groups["name"].Value);

        // Also pick up comma-separated tables in FROM lists
        // Find the FROM clause(s) and scan for additional tables
        int fromIdx = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        while (fromIdx >= 0)
        {
            // Find the end of this FROM clause (next WHERE/GROUP/HAVING/ORDER/UNION/JOIN)
            int clauseEnd = IndexOfNextClauseKeyword(sql, fromIdx + 4);
            string fromClause = sql.Substring(fromIdx + 4,
                                              (clauseEnd >= 0 ? clauseEnd : sql.Length) - fromIdx - 4);
            foreach (Match m in _commaTablePattern.Matches(fromClause))
                TryAdd(m.Groups["name"].Value);

            fromIdx = sql.IndexOf("FROM", fromIdx + 4, StringComparison.OrdinalIgnoreCase);
        }

        return names;
    }

    private static int IndexOfNextClauseKeyword(string sql, int startIdx)
    {
        string[] keywords = ["WHERE", "GROUP", "HAVING", "ORDER", "UNION"];
        int result = -1;
        foreach (var kw in keywords)
        {
            int idx = sql.IndexOf(kw, startIdx, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (result < 0 || idx < result))
                result = idx;
        }
        return result;
    }
}
