using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AccessHarvester.Sql.Parser;
using AccessHarvester.Vba.Parser;
using SqlParseOptions = AccessHarvester.Sql.Parser.ParseOptions;
using VbaParseOptions = AccessHarvester.Vba.Parser.ParseOptions;
using SqlParseMode = AccessHarvester.Sql.Parser.ParseMode;
using VbaParseMode = AccessHarvester.Vba.Parser.ParseMode;
using SqlSeverity = AccessHarvester.Sql.Parser.DiagnosticSeverity;
using VbaSeverity = AccessHarvester.Vba.Parser.DiagnosticSeverity;

namespace AccessHarvester.Pipeline.Tests.ParseSuccessReport;

// Walks a fixture's YAML mirror at samples/DatabaseYaml/<fixture>/
// and produces a ParseSuccessReport by invoking the existing parser
// entry points against each source. Designed to be hermetic — no
// Access COM, no live pipeline — so the report regenerates anywhere
// the YAML mirror is present.
//
// Per the Session 1 spec elaboration, the UnresolvedItems section
// ships `status: "not-measured"` with a documenting note; the
// `totalCount` and `bySourceAndReason` fields are intentionally
// omitted (not zero, not null) so consumers cannot mistake absence
// for "measured zero." The full UnresolvedItemAggregator output
// requires the live enrichment pipeline and is wired by
// `harvester-finish-canonical-golden-output`.
public static class ParseSuccessReportGenerator
{
    public static ParseSuccessReport Generate(string fixtureName, string yamlMirrorRoot)
    {
        if (!Directory.Exists(yamlMirrorRoot))
            throw new DirectoryNotFoundException(
                $"YAML mirror not found at '{yamlMirrorRoot}'. Regenerate via the local COM extractor.");

        var vbaCounts = CountVba(Path.Combine(yamlMirrorRoot, "Modules"));
        var sqlCounts = CountJetSql(Path.Combine(yamlMirrorRoot, "Queries"));
        var macroCounts = CountMacros(Path.Combine(yamlMirrorRoot, "Macros"));
        var propertyCounts = CountPropertySheetBuckets(yamlMirrorRoot);

        return new ParseSuccessReport
        {
            Fixture = fixtureName,
            SchemaVersion = 1,
            Generator = "AccessHarvester.Pipeline.Tests.ParseSuccessReport",
            YamlMirror = "samples/DatabaseYaml/" + fixtureName,
            Summary = new ParseSuccessSummary
            {
                VbaModules = vbaCounts,
                JetSqlQueries = sqlCounts,
                Macros = macroCounts,
                PropertySheet = propertyCounts,
            },
            UnresolvedItems = new UnresolvedItemsSection
            {
                Status = "not-measured",
                Note = "Session 1 first-pass: parser-coverage signal is in `summary`. "
                       + "Full UnresolvedItemAggregator output (resolver / dynamic-SQL / "
                       + "navigation / function-classification) requires the live "
                       + "enrichment pipeline and is wired by "
                       + "harvester-finish-canonical-golden-output. `totalCount` and "
                       + "`bySourceAndReason` are intentionally omitted (not zero) to "
                       + "signal that no measurement has occurred. "
                       + "Property-sheet bucket coverage uses a Jet-SQL wrapping "
                       + "heuristic with a synthetic placeholder operand inserted for "
                       + "Access-criteria forms (`Is Not Null`, `In (...)`, etc.); "
                       + "see ParseSuccessReportGenerator.ParseAsJetWhere for details.",
            },
        };
    }

    // ── VBA modules ──────────────────────────────────────────────────

    // Header lines for procedure-counting. Matches procedure
    // declarations the way grammar-validation does — Sub/Function and
    // Property Get/Let/Set with optional Public/Private/Friend/Static
    // modifiers. Conservative on purpose: the count is a coverage
    // signal, not an exact AST walk. Matching from the start of a line
    // (block-scalar lines are indented 2 spaces inside the YAML) keeps
    // false positives out.
    private static readonly Regex ProcedureHeader = new(
        @"^\s*(?:Public|Private|Friend)?\s*(?:Static\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\s+\w+",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static VbaModuleCounts CountVba(string modulesDir)
    {
        if (!Directory.Exists(modulesDir))
            return new VbaModuleCounts();

        int totalModules = 0;
        int totalProcedures = 0;
        int parseOk = 0;
        int parseFailed = 0;
        int errorTokens = 0;

        foreach (var path in Directory.EnumerateFiles(modulesDir, "*.yaml").OrderBy(p => p))
        {
            totalModules++;
            var body = ExtractBlockScalar(path, "Definition");
            if (body is null)
                continue;

            totalProcedures += ProcedureHeader.Matches(body).Count;

            // Strip the canonical leading header block before parsing.
            // We deliberately do NOT call BuildAst here — the report is
            // a diagnostic-severity count, and forcing AST construction
            // over every YAML mirror would surface unrelated
            // recovery-time AstBuilder fragility that is out of this
            // session set's scope (see
            // docs/session-sets/vba-attribute-header-strip/audit.md
            // "Follow-up findings").
            var (strippedBody, _) = VbaModuleBodyLoader.Load(body);
            var result = VbaParserPipeline.Parse(strippedBody,
                new VbaParseOptions(RequestedMode: VbaParseMode.Recovering));

            // Error-severity diagnostics indicate a real parse failure;
            // Warning entries (e.g., VBA0050 lossy fallback) are
            // tolerated as parse-ok.
            bool failed = false;
            foreach (var diag in result.Diagnostics)
            {
                if (diag.Severity == VbaSeverity.Error)
                    failed = true;
                if (diag.Code == "VBA0001")
                    errorTokens++;
            }
            if (failed) parseFailed++; else parseOk++;
        }

        return new VbaModuleCounts
        {
            TotalModules = totalModules,
            TotalProcedures = totalProcedures,
            ParseOk = parseOk,
            ParseFailed = parseFailed,
            ErrorTokenCount = errorTokens,
        };
    }

    // ── Jet SQL queries ─────────────────────────────────────────────

    private static JetSqlQueryCounts CountJetSql(string queriesDir)
    {
        if (!Directory.Exists(queriesDir))
            return new JetSqlQueryCounts();

        int total = 0;
        int parseOk = 0;
        int parseFailed = 0;

        foreach (var path in Directory.EnumerateFiles(queriesDir, "*.yaml").OrderBy(p => p))
        {
            total++;
            var sql = ExtractBlockScalar(path, "SQL");
            if (string.IsNullOrWhiteSpace(sql))
            {
                // A query YAML without a SQL block is structurally
                // weird; record as parse-failed so the human notices.
                parseFailed++;
                continue;
            }

            var result = SqlParserPipeline.Parse(sql,
                new SqlParseOptions(RequestedMode: SqlParseMode.Recovering));

            bool failed = result.Diagnostics.Any(d => d.Severity == SqlSeverity.Error);
            if (failed) parseFailed++; else parseOk++;
        }

        return new JetSqlQueryCounts
        {
            TotalQueries = total,
            ParseOk = parseOk,
            ParseFailed = parseFailed,
        };
    }

    // ── Macros (SaveAsText structural counts) ───────────────────────

    private static readonly Regex MacroBeginLine = new(
        @"^\s*Begin\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex MacroActionLine = new(
        @"^\s*Action\s*=", RegexOptions.Compiled | RegexOptions.Multiline);

    private static MacroCounts CountMacros(string macrosDir)
    {
        if (!Directory.Exists(macrosDir))
            return new MacroCounts();

        int totalObjects = 0;
        int totalEntries = 0;
        int actionsParsed = 0;

        foreach (var path in Directory.EnumerateFiles(macrosDir, "*.yaml").OrderBy(p => p))
        {
            totalObjects++;
            var body = ExtractBlockScalar(path, "Definition");
            if (body is null) continue;

            totalEntries += MacroBeginLine.Matches(body).Count;
            actionsParsed += MacroActionLine.Matches(body).Count;
        }

        // Session 1 first-pass: structural counts only. The macro
        // action enricher (which would surface UnresolvedNavigationTarget
        // and similar) is not invoked here — that's downstream pipeline
        // work wired by harvester-finish-canonical-golden-output.
        return new MacroCounts
        {
            TotalMacroObjects = totalObjects,
            TotalMacroEntries = totalEntries,
            ActionLines = actionsParsed,
        };
    }

    // ── Property-sheet expression buckets ───────────────────────────

    // Walk every YAML file under the fixture mirror once, classifying
    // each `Key: "value"` line into its bucket and parsing the value
    // through the appropriate dialect. This matches the property-sheet
    // inventory tool's scope (all YAML files, not just Tables/Forms/
    // Reports) — so the report counts and the inventory counts
    // reconcile end-to-end.
    private static PropertySheetCounts CountPropertySheetBuckets(string yamlMirrorRoot)
    {
        var b = new BucketCollector();

        foreach (var path in Directory.EnumerateFiles(yamlMirrorRoot, "*.yaml", SearchOption.AllDirectories)
                                       .OrderBy(p => p))
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }

            foreach (Match m in FieldLine.Matches(text))
            {
                var key = m.Groups["key"].Value;
                var value = m.Groups["value"].Value;
                if (string.IsNullOrEmpty(value)) continue;

                switch (key)
                {
                    case "ValidationRule":
                        b.Tally(ref b.ValidationRules, ParseAsJetWhere(value));
                        break;
                    case "DefaultValue":
                        b.Tally(ref b.DefaultValues, ParseDefaultValue(value));
                        break;
                    case "Filter":
                        b.Tally(ref b.FormFilters, ParseAsJetWhere(value));
                        break;
                    case "OrderBy":
                        b.Tally(ref b.FormOrderBys, ParseAsJetOrderBy(value));
                        break;
                }
            }
        }

        return new PropertySheetCounts
        {
            ValidationRules = b.ValidationRules.ToCounts(),
            DefaultValues = b.DefaultValues.ToCounts(),
            FormFilters = b.FormFilters.ToCounts(),
            FormOrderBys = b.FormOrderBys.ToCounts(),
        };
    }

    private struct MutableBucket { public int Total, Ok, Failed; public PropertyBucketCounts ToCounts() => new() { Total = Total, ParsedOk = Ok, ParseFailed = Failed }; }
    private sealed class BucketCollector
    {
        public MutableBucket ValidationRules, DefaultValues, FormFilters, FormOrderBys;
        public void Tally(ref MutableBucket bucket, bool ok)
        {
            bucket.Total++;
            if (ok) bucket.Ok++; else bucket.Failed++;
        }
    }

    // Match `Key: "value"` where value is a YAML-style double-quoted
    // scalar whose body may contain backslash-escaped inner quotes
    // (e.g., `In (\"M\",\"F\",\"X\")`). A naive `[^"]*` group stops
    // at the first inner quote and silently skips such lines —
    // confirmed by gpt-5-4 R2 verification against the round-1 fix
    // set. The shared inventory tool's regex
    // (`tools/parse_success_inventory.py`) carries the same shape so
    // the two scans remain reconciled.
    private static readonly Regex FieldLine = new(
        @"^\s*(?<key>[A-Za-z]+)\s*:\s*""(?<value>(?:[^""\\]|\\.)*)""\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Access-criteria-style values start with a comparison operator
    // or a unary predicate keyword and have an implicit-operand field
    // reference (the rule applies to the bound column). When we see
    // one of these shapes, prepend a synthetic placeholder operand
    // before SQL-wrapping so the Jet SQL WHERE-clause grammar can
    // accept it. Full-predicate values that already have an explicit
    // operand (`[Field] >= [Other]`) pass through unchanged.
    private static readonly Regex CriteriaLeadingOperator = new(
        @"^\s*(?:Is|Like|Between|In|Not|And|Or|>=|<=|<>|=|<|>)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string NormaliseCriteria(string expression)
    {
        // Strip YAML escape backslash on embedded quotes — the regex
        // captures the raw YAML, so `In ("M","F","X")` arrives as
        // `In (\"M\",\"F\",\"X\")`.
        var unescaped = expression.Replace("\\\"", "\"");
        // Prepend a synthetic operand for Access criteria-style rules.
        // Whole-expression criteria become `x <criterion>`; full
        // predicates pass through.
        if (CriteriaLeadingOperator.IsMatch(unescaped))
            return "x " + unescaped;
        return unescaped;
    }

    // ValidationRule + Filter values look like Jet SQL WHERE-clause
    // predicates. Wrap as `SELECT 1 FROM t WHERE <expr>` (with the
    // criteria-style placeholder operand applied first) and parse
    // via SqlParserPipeline. Returns true on zero Error diagnostics.
    private static bool ParseAsJetWhere(string expression)
    {
        var normalised = NormaliseCriteria(expression);
        var wrapped = $"SELECT 1 FROM t WHERE {normalised}";
        var result = SqlParserPipeline.Parse(wrapped,
            new SqlParseOptions(RequestedMode: SqlParseMode.Recovering));
        return !result.Diagnostics.Any(d => d.Severity == SqlSeverity.Error);
    }

    private static bool ParseAsJetOrderBy(string expression)
    {
        var unescaped = expression.Replace("\\\"", "\"");
        var wrapped = $"SELECT 1 FROM t ORDER BY {unescaped}";
        var result = SqlParserPipeline.Parse(wrapped,
            new SqlParseOptions(RequestedMode: SqlParseMode.Recovering));
        return !result.Diagnostics.Any(d => d.Severity == SqlSeverity.Error);
    }

    // DefaultValue values are mixed:
    //   * Numeric / boolean literals → trivially OK (no parse needed).
    //   * Quoted string literals (e.g., \"Pending\") → trivially OK.
    //   * Expressions (Date(), =Date(), Now(), [TempVars]![UserID]) →
    //     parse as a Jet SQL SELECT-list expression.
    private static bool ParseDefaultValue(string value)
    {
        var v = value.Trim();
        if (v.Length == 0) return true;
        if (v.Equals("True", StringComparison.OrdinalIgnoreCase) ||
            v.Equals("False", StringComparison.OrdinalIgnoreCase) ||
            v.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
            v.Equals("No", StringComparison.OrdinalIgnoreCase))
            return true;

        if (long.TryParse(v, out _) || double.TryParse(v,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return true;

        // Quoted-string literal — already a valid Access default.
        if (v.StartsWith("\\\"") && v.EndsWith("\\\""))
            return true;

        // Expression — strip the optional `=` evaluator prefix.
        if (v.StartsWith("=", StringComparison.Ordinal)) v = v.Substring(1);
        var unescaped = v.Replace("\\\"", "\"");
        var wrapped = $"SELECT {unescaped} FROM t";
        var result = SqlParserPipeline.Parse(wrapped,
            new SqlParseOptions(RequestedMode: SqlParseMode.Recovering));
        return !result.Diagnostics.Any(d => d.Severity == SqlSeverity.Error);
    }

    // ── YAML reader (block-scalar slice for `Key: |` constructs) ────

    // Returns the body of a top-level block scalar `<key>: |\n  ...`
    // with the 2-space block indent removed, or null if the key is
    // absent. Tailored to the SaveAsText YAML mirror format — does
    // NOT pretend to be a full YAML parser.
    private static string? ExtractBlockScalar(string path, string key)
    {
        string text;
        try { text = File.ReadAllText(path); }
        catch { return null; }

        var lines = text.Split('\n');
        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // Top-level block scalar header: `<key>: |` (optionally
            // with the `|-` strip indicator). The mirror only uses
            // `|` for these blocks.
            if (line.StartsWith(key + ": |", StringComparison.Ordinal) ||
                line.TrimEnd('\r').Equals(key + ": |", StringComparison.Ordinal))
            {
                headerIdx = i;
                break;
            }
        }
        if (headerIdx < 0) return null;

        var body = new List<string>();
        for (int i = headerIdx + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0) { body.Add(string.Empty); continue; }
            // Block scalar continues while the line starts with 2+
            // spaces, OR is blank. The first un-indented line ends it.
            if (line.StartsWith("  ", StringComparison.Ordinal))
                body.Add(line.Substring(2).TrimEnd('\r'));
            else if (string.IsNullOrWhiteSpace(line.TrimEnd('\r')))
                body.Add(string.Empty);
            else
                break;
        }
        return string.Join("\n", body);
    }
}
