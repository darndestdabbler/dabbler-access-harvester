using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.SymbolDb.Models;

namespace AccessHarvester.SymbolDb.Extraction;

// Reads table KB JSON files and produces TableRow lists.
// Computes:
//   primary_key_column — first column in the PrimaryKey index, if one exists.
//   candidate_label_column — a text/memo column whose name suggests a label
//       (contains Value/Text/Label/Caption/Name/Description, case-insensitive),
//       which participates in a unique index.
//   is_lookup_table_score — 0-100 composite score:
//       +20  has a primary key
//       +30  record count ≤ 200 (caller supplies rowCounts or we read from JSON)
//       +20  has a candidate text label column
//       +30  table name appears as a bound_table_or_query in at least one FormControl
//   is_lookup_table_reason — human-readable explanation of the score breakdown.
public static class TableSchemaExtractor
{
    // Column name fragments that suggest a display/label column.
    private static readonly string[] LabelHints =
        ["value", "text", "label", "caption", "name", "description"];

    // Text/memo DAO type codes.
    private static readonly HashSet<int> TextTypeCodes = [10, 12]; // Text=10, Memo=12

    public static IReadOnlyList<TableRow> Extract(
        IEnumerable<string> kbJsonPaths,
        IReadOnlySet<string>? rowSourceTableNames = null)
    {
        var rows = new List<TableRow>();
        foreach (var path in kbJsonPaths)
        {
            var row = ExtractOne(path, rowSourceTableNames);
            if (row is not null) rows.Add(row);
        }
        return rows;
    }

    private static TableRow? ExtractOne(string path, IReadOnlySet<string>? rowSourceNames)
    {
        using var stream = File.OpenRead(path);
        var root = JsonNode.Parse(stream)?.AsObject();
        if (root is null) return null;

        string tableName = root["Name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(path);

        int? recordCount = root["RecordCount"]?.GetValue<int>();
        var fields = root["Fields"]?.AsArray();
        var indexes = root["Indexes"]?.AsArray();

        // --- Primary key detection ---
        string? primaryKeyColumn = null;
        var uniqueColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (indexes is not null)
        {
            foreach (var idx in indexes)
            {
                var idxObj = idx?.AsObject();
                if (idxObj is null) continue;
                bool isPrimary = idxObj["Primary"]?.GetValue<bool>() ?? false;
                bool isUnique = idxObj["Unique"]?.GetValue<bool>() ?? false;
                var idxFields = idxObj["Fields"]?.AsArray();
                if (idxFields is null) continue;

                foreach (var f in idxFields)
                {
                    string? colName = f?.AsObject()?["Name"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(colName))
                    {
                        if (isUnique) uniqueColumnNames.Add(colName!);
                        if (isPrimary && primaryKeyColumn is null) primaryKeyColumn = colName;
                    }
                }
            }
        }

        // --- Candidate label column detection ---
        string? candidateLabelColumn = null;
        if (fields is not null)
        {
            foreach (var f in fields)
            {
                var fObj = f?.AsObject();
                if (fObj is null) continue;
                string? colName = fObj["Name"]?.GetValue<string>();
                int? typeCode = fObj["TypeCode"]?.GetValue<int>();
                if (string.IsNullOrEmpty(colName) || typeCode is null) continue;

                if (!TextTypeCodes.Contains(typeCode.Value)) continue;
                if (!uniqueColumnNames.Contains(colName!)) continue;

                string lower = colName!.ToLowerInvariant();
                if (LabelHints.Any(h => lower.Contains(h)))
                {
                    candidateLabelColumn = colName;
                    break;
                }
            }
        }

        // --- Lookup-table score ---
        int score = 0;
        var reasons = new List<string>();

        if (primaryKeyColumn is not null) { score += 20; reasons.Add("has-pk(+20)"); }
        if (recordCount is not null && recordCount.Value <= 200) { score += 30; reasons.Add($"record-count={recordCount}(+30)"); }
        if (candidateLabelColumn is not null) { score += 20; reasons.Add($"label-col={candidateLabelColumn}(+20)"); }
        if (rowSourceNames is not null && rowSourceNames.Contains(tableName)) { score += 30; reasons.Add("is-row-source(+30)"); }

        string? reason = reasons.Count > 0 ? string.Join("; ", reasons) : null;

        return new TableRow(
            Fqn: tableName,
            PrimaryKeyColumn: primaryKeyColumn,
            CandidateLabelColumn: candidateLabelColumn,
            IsLookupTableScore: score,
            IsLookupTableReason: reason);
    }
}
