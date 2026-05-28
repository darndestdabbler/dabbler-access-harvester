using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// Pure-logic classification engine. Consumes a <see cref="RawSchema"/> +
/// per-column <see cref="ColumnStatistics"/> + a <see cref="ScanConfig"/>
/// and emits the structural finding tree. No COM, no I/O — every input is
/// data so the engine is fully unit-testable.
/// </summary>
/// <remarks>
/// Heuristic application order is deliberate; <c>firedHeuristics</c> on
/// each output column / table records the names in the order they fired
/// so a reviewer can read why a guess landed where it did.
/// </remarks>
public static class HeuristicsEngine
{
    public static ClassificationFindings Run(
        RawSchema rawSchema,
        IReadOnlyList<ColumnStatistics> statistics,
        ScanConfig config,
        DateTimeOffset scannedUtc)
    {
        if (rawSchema is null) throw new ArgumentNullException(nameof(rawSchema));
        if (statistics is null) throw new ArgumentNullException(nameof(statistics));
        if (config is null) throw new ArgumentNullException(nameof(config));

        var nameRegexes        = config.PiiNamePatterns.Select(PiiDictionary.CompileGlob).ToArray();
        var systemRegexes      = config.SystemColumnPatterns.Select(PiiDictionary.CompileGlob).ToArray();
        var statsLookup        = statistics.ToDictionary(s => (s.TableName, s.ColumnName));
        var systemColumnSet    = BuildSystemColumnSet(rawSchema, systemRegexes);
        var referencedByCount  = CountReferencesByTable(rawSchema);
        var maxCardinality     = statistics.Count == 0 ? 0 : statistics.Max(s => s.Cardinality);

        var tableFindings = new List<TableFinding>(rawSchema.Tables.Count);
        foreach (var table in rawSchema.Tables)
        {
            var columnFindings = new List<ColumnFinding>(table.Columns.Count);
            bool tablePiiObserved = false;
            foreach (var col in table.Columns)
            {
                var stats = statsLookup.TryGetValue((table.Name, col.Name), out var s) ? s : null;
                bool isSystemColumn = systemColumnSet.Contains((table.Name, col.Name));
                var classified = ClassifyColumn(
                    table, col, stats, nameRegexes, isSystemColumn, maxCardinality);
                if (classified.GuessedCategory is ColumnCategory.DirectIdentifierPii
                                              or ColumnCategory.QuasiIdentifier)
                    tablePiiObserved = true;
                columnFindings.Add(classified);
            }

            // PII veto pass (plan § 3.3): a thin table whose columns include
            // PII signals is an entity table, not reference data — re-run the
            // table-level classification with the veto applied.
            int nonKeyNonSystem = CountNonKeyNonSystem(columnFindings, systemColumnSet, table);
            var (lookupGuess, hint, confidence, fired) = ClassifyTable(
                table, columnFindings, nonKeyNonSystem, referencedByCount.GetValueOrDefault(table.Name), tablePiiObserved);

            // Post-pass (plan § 3.5): when the table-level guess is a lookup
            // table (`LookupReference` hint), promote payload columns to
            // `lookup-reference` so the sanitizer's preserve default
            // applies. The promotion is gated by NEGATIVE exclusions
            // (structural categories we must not overwrite) rather than
            // by a positive allowlist of prior categories — this is the
            // R2 fix to the gap where a fixed-format code column (which
            // landed as `sensitive-non-pii` because the format detector
            // recognised a stable shape) stayed mis-categorised inside a
            // lookup table. Stronger structural categories — PK, FK,
            // memo, system-audit, every PII flavour — keep their original
            // category; everything else in a lookup table becomes
            // `lookup-reference`.
            if (lookupGuess && hint == TableHint.LookupReference)
            {
                for (int i = 0; i < columnFindings.Count; i++)
                {
                    var c = columnFindings[i];
                    if (c.IsPrimaryKey || c.IsForeignKey || c.IsMemo) continue;
                    if (systemColumnSet.Contains((table.Name, c.Name))) continue;
                    if (c.GuessedCategory is ColumnCategory.SurrogateAutonumberPk
                                          or ColumnCategory.ForeignKey
                                          or ColumnCategory.FreeTextMemo
                                          or ColumnCategory.SystemAudit
                                          or ColumnCategory.DirectIdentifierPii
                                          or ColumnCategory.QuasiIdentifier
                                          or ColumnCategory.LookupReference)
                        continue;
                    var firedList = new List<string>(c.FiredHeuristics) { "lookup-table-payload" };
                    columnFindings[i] = c with
                    {
                        GuessedCategory = ColumnCategory.LookupReference,
                        Confidence      = Math.Max(c.Confidence, 0.9),
                        FiredHeuristics = firedList,
                    };
                }
            }

            int rowCount = columnFindings.FirstOrDefault()?.RowCount ?? 0;
            tableFindings.Add(new TableFinding(
                Name: table.Name,
                IsLinkedTable: table.IsLinkedTable,
                RowCount: rowCount,
                LookupGuess: lookupGuess,
                NonKeyNonSystemColumnCount: nonKeyNonSystem,
                ReferencedByFkCount: referencedByCount.GetValueOrDefault(table.Name),
                TableHint: hint,
                Confidence: confidence,
                FiredHeuristics: fired,
                Columns: columnFindings));
        }

        // Stable sort so the JSON output is deterministic for diffing.
        tableFindings.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

        return new ClassificationFindings(
            DatabaseName: rawSchema.DatabaseName,
            ScannedUtc:   scannedUtc,
            Config:       config,
            Tables:       tableFindings);
    }

    // ───────────────────────────────────────────── system-column detection

    /// <summary>
    /// A column is a system/audit column only when its name matches one of
    /// the configured patterns AND the same name recurs across multiple
    /// tables (plan § 3.2). The recurrence requirement prevents stripping
    /// a genuinely meaningful column (e.g., <c>UserId</c> on a table that
    /// is actually <i>about</i> users).
    /// </summary>
    private static HashSet<(string Table, string Column)> BuildSystemColumnSet(
        RawSchema schema, IReadOnlyList<Regex> systemRegexes)
    {
        var occurrences = new Dictionary<string, List<(string Table, string Column)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in schema.Tables)
        {
            foreach (var col in table.Columns)
            {
                if (!NameMatches(systemRegexes, col.Name)) continue;
                if (!occurrences.TryGetValue(col.Name, out var list))
                {
                    list = new List<(string Table, string Column)>();
                    occurrences[col.Name] = list;
                }
                list.Add((table.Name, col.Name));
            }
        }

        var result = new HashSet<(string Table, string Column)>();
        foreach (var pair in occurrences)
        {
            if (pair.Value.Count >= 2)
                foreach (var hit in pair.Value)
                    result.Add(hit);
        }
        return result;
    }

    private static Dictionary<string, int> CountReferencesByTable(RawSchema schema)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in schema.Tables)
        foreach (var col in table.Columns)
            if (col.FkTarget is not null)
                counts[col.FkTarget.Table] = counts.GetValueOrDefault(col.FkTarget.Table) + 1;
        return counts;
    }

    private static int CountNonKeyNonSystem(
        IReadOnlyList<ColumnFinding> columns,
        HashSet<(string Table, string Column)> systemColumnSet,
        RawTable table)
    {
        int n = 0;
        foreach (var c in columns)
        {
            if (c.IsPrimaryKey || c.IsForeignKey) continue;
            if (systemColumnSet.Contains((table.Name, c.Name))) continue;
            n++;
        }
        return n;
    }

    // ───────────────────────────────────────────── per-column classification

    private static ColumnFinding ClassifyColumn(
        RawTable table,
        RawColumn col,
        ColumnStatistics? stats,
        IReadOnlyList<Regex> nameRegexes,
        bool isSystemColumn,
        int maxCardinality)
    {
        var fired = new List<string>();
        int  rowCount      = stats?.RowCount     ?? 0;
        int  nullCount     = stats?.NullCount    ?? 0;
        int  cardinality   = stats?.Cardinality  ?? 0;
        bool piiValueMatch = stats?.PiiValueMatch ?? false;
        string? formatPattern = stats?.ValueFormatPattern;
        bool piiNameMatch = NameMatches(nameRegexes, col.Name);

        double uniqueness = rowCount > 0 ? Math.Min(1.0, (double)cardinality / rowCount) : 0.0;
        double relCard    = maxCardinality > 0 ? Math.Min(1.0, (double)cardinality / maxCardinality) : 0.0;

        // Category resolution order matters: hard structural signals win
        // before name/value heuristics. Autonumber PK > FK > memo > system >
        // PII > name signals > generic. The PII signal still vetoes a
        // lookup at the table level (see ClassifyTable below).
        ColumnCategory category;
        double confidence;

        if (col.IsAutoNumber && col.IsPrimaryKey)
        {
            fired.Add("autonumber-pk");
            if (uniqueness >= 0.99) fired.Add("uniqueness-high");
            category   = ColumnCategory.SurrogateAutonumberPk;
            confidence = 0.98;
        }
        else if (col.IsForeignKey)
        {
            fired.Add("fk-declared");
            if (rowCount > 0 && cardinality > 0 && uniqueness < 0.05) fired.Add("low-cardinality");
            category   = ColumnCategory.ForeignKey;
            confidence = 0.99;
        }
        else if (col.IsMemo)
        {
            fired.Add("memo-free-text");
            category   = ColumnCategory.FreeTextMemo;
            confidence = 0.90;
        }
        else if (isSystemColumn)
        {
            fired.Add("system-audit-name-and-recurrence");
            category   = ColumnCategory.SystemAudit;
            confidence = 0.92;
        }
        else if (piiValueMatch && piiNameMatch)
        {
            fired.Add("pii-name-match");
            fired.Add("pii-value-match");
            if (uniqueness >= 0.9) fired.Add("uniqueness-high");
            category   = ColumnCategory.DirectIdentifierPii;
            confidence = 0.99;
        }
        else if (piiValueMatch)
        {
            fired.Add("pii-value-match");
            category   = ColumnCategory.DirectIdentifierPii;
            confidence = 0.92;
        }
        else if (piiNameMatch)
        {
            fired.Add("pii-name-match");
            if (LooksLikeQuasiIdentifierName(col.Name))
            {
                if (uniqueness >= 0.9) fired.Add("uniqueness-high");
                category   = ColumnCategory.QuasiIdentifier;
                confidence = 0.88;
            }
            else
            {
                category   = ColumnCategory.DirectIdentifierPii;
                confidence = 0.95;
            }
        }
        else if (col.IsPrimaryKey)
        {
            // Non-autonumber PK is treated as a business/natural key by default;
            // the human can override to surrogate-autonumber-pk via the editor.
            fired.Add("business-natural-key-pk");
            category   = ColumnCategory.BusinessNaturalKey;
            confidence = 0.7;
        }
        else if (formatPattern is not null)
        {
            // A consistent format pattern with no PII-name signal usually
            // means a sensitive code (MRN, account number) the vocabulary
            // does not cover. Lean to sensitive-non-pii; the human can
            // promote to business-natural-key.
            fired.Add("format-pattern-recognized");
            if (uniqueness >= 0.9) fired.Add("uniqueness-high");
            category   = ColumnCategory.SensitiveNonPii;
            confidence = 0.78;
        }
        else
        {
            if (IsNumericLike(col.DataType)) fired.Add("numeric-non-key");
            else if (IsTextLike(col.DataType)) fired.Add("text-non-key");
            category   = ColumnCategory.GenericNonSensitive;
            confidence = 0.7;
        }

        return new ColumnFinding(
            Name: col.Name,
            OrdinalPosition: col.OrdinalPosition,
            DataType: col.DataType,
            IsAutoNumber: col.IsAutoNumber,
            IsPrimaryKey: col.IsPrimaryKey,
            IsForeignKey: col.IsForeignKey,
            FkTarget: col.FkTarget,
            IsMemo: col.IsMemo,
            IsOleObject: col.IsOleObject,
            IsMultiValue: col.IsMultiValue,
            IsRequired: col.IsRequired,
            RowCount: rowCount,
            NullCount: nullCount,
            Cardinality: cardinality,
            UniquenessRatio: Math.Round(uniqueness, 6),
            RelativeCardinality: Math.Round(relCard, 6),
            PiiNameMatch: piiNameMatch,
            PiiValueMatch: piiValueMatch,
            ValueFormatPattern: formatPattern,
            GuessedCategory: category,
            Confidence: confidence,
            FiredHeuristics: fired);
    }

    // ───────────────────────────────────────────── per-table classification

    private static (bool lookupGuess, TableHint hint, double confidence, IReadOnlyList<string> fired) ClassifyTable(
        RawTable table,
        IReadOnlyList<ColumnFinding> columns,
        int nonKeyNonSystem,
        int referencedByFkCount,
        bool tablePiiObserved)
    {
        var fired = new List<string>();

        if (table.IsLinkedTable)
        {
            fired.Add("linked-table");
            return (false, TableHint.Linked, 0.95, fired);
        }

        bool anyAuditColumn = columns.Any(c => c.GuessedCategory == ColumnCategory.SystemAudit);
        bool allCategoryAudit = columns.Count > 0 && columns.All(
            c => c.GuessedCategory == ColumnCategory.SystemAudit
              || c.GuessedCategory == ColumnCategory.SurrogateAutonumberPk
              || c.GuessedCategory == ColumnCategory.ForeignKey);
        if (anyAuditColumn && allCategoryAudit && nonKeyNonSystem == 0 && referencedByFkCount == 0
            && columns.Any(c => c.GuessedCategory == ColumnCategory.SystemAudit && c.IsRequired == false) == false
            && LooksLikeAuditTableName(table.Name))
        {
            fired.Add("audit-log-name-match");
            return (false, TableHint.AuditLog, 0.7, fired);
        }

        if (nonKeyNonSystem == 1)
        {
            fired.Add("lookup-thin-table");
            if (columns.Count > 0 && columns.First().IsAutoNumber)
                fired.Add("autonumber-pk");
            if (tablePiiObserved)
            {
                fired.Add("pii-veto");
                return (false, TableHint.EntityPiiBearing, 0.9, fired);
            }
            return (true, TableHint.LookupReference, 0.96, fired);
        }
        if (nonKeyNonSystem is 2 or 3)
        {
            fired.Add("lookup-thin-table-borderline");
            if (tablePiiObserved)
            {
                fired.Add("pii-veto");
                return (false, TableHint.EntityPiiBearing, 0.88, fired);
            }
            return (true, TableHint.LookupReference, 0.7, fired);
        }

        // Junction tables: exactly 2 FKs, no non-key data columns of note.
        int fkCount = columns.Count(c => c.IsForeignKey);
        if (fkCount >= 2 && nonKeyNonSystem == 0)
        {
            fired.Add("junction-fk-only");
            return (false, TableHint.Junction, 0.85, fired);
        }

        // Default entity classification. PII signals lift confidence
        // toward entity-pii-bearing; otherwise leave as entity-non-pii
        // (or transaction when the table is large + FK-bearing).
        if (tablePiiObserved)
        {
            fired.Add("pii-name-match");
            fired.Add("pii-veto");
            return (false, TableHint.EntityPiiBearing, 0.92, fired);
        }

        if (fkCount >= 1 && nonKeyNonSystem >= 1)
        {
            fired.Add("fk-recurrence");
            int rowCount = columns.Count > 0 ? columns[0].RowCount : 0;
            if (rowCount >= 10_000) fired.Add("high-row-count");
            return (false, TableHint.Transaction, 0.9, fired);
        }

        fired.Add("entity-default");
        return (false, TableHint.EntityNonPii, 0.7, fired);
    }

    // ───────────────────────────────────────────── helpers

    private static bool NameMatches(IReadOnlyList<Regex> regexes, string name)
    {
        foreach (var r in regexes)
            if (r.IsMatch(name)) return true;
        return false;
    }

    private static bool IsNumericLike(string dataType) =>
        dataType.Contains("Long",     StringComparison.OrdinalIgnoreCase)
     || dataType.Contains("Integer",  StringComparison.OrdinalIgnoreCase)
     || dataType.Contains("Double",   StringComparison.OrdinalIgnoreCase)
     || dataType.Contains("Single",   StringComparison.OrdinalIgnoreCase)
     || dataType.Contains("Currency", StringComparison.OrdinalIgnoreCase)
     || dataType.Contains("Decimal",  StringComparison.OrdinalIgnoreCase);

    private static bool IsTextLike(string dataType) =>
        dataType.Contains("Text",     StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Date-of-birth / postal codes / generic location columns are the
    /// classic quasi-identifier set per the HIPAA Safe Harbor literature
    /// (and the project plan § 3.5). Their PII signal alone is not enough
    /// to identify a person, but they're useful for re-identification.
    /// </summary>
    private static bool LooksLikeQuasiIdentifierName(string name) =>
        name.Equals("DOB",         StringComparison.OrdinalIgnoreCase)
     || name.Equals("DateOfBirth", StringComparison.OrdinalIgnoreCase)
     || name.Equals("BirthDate",   StringComparison.OrdinalIgnoreCase)
     || name.Equals("Zip",         StringComparison.OrdinalIgnoreCase)
     || name.StartsWith("Zip",     StringComparison.OrdinalIgnoreCase)
     || name.Equals("PostalCode",  StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAuditTableName(string name) =>
        name.Contains("Audit",  StringComparison.OrdinalIgnoreCase)
     || name.Contains("Log",    StringComparison.OrdinalIgnoreCase)
     || name.Contains("History",StringComparison.OrdinalIgnoreCase);
}
