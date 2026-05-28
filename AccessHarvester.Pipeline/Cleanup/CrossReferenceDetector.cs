using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>Cross-reference absence detector — see strategy doc §3.3.</summary>
public static class CrossReferenceDetector
{
    private static readonly Regex FromJoinPattern =
        new(@"(?:FROM|JOIN)\s+(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_]*))\s*(?:AS\s+\w+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CommaTablePattern =
        new(@",\s*(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_]*))\s*(?:AS\s+\w+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DCountLookupPattern =
        new(@"(?i)\b(?:DCount|DLookup|DSum|DMin|DMax|DAvg)\s*\(\s*[""'][^""']*[""']\s*,\s*[""'](?<name>[^""']+)[""']",
            RegexOptions.Compiled);

    private static readonly Regex OpenRecordsetPattern =
        new(@"(?i)\bOpenRecordset\s*\(\s*[""'](?<name>[^""']+)[""']",
            RegexOptions.Compiled);

    private static readonly Regex BareIdentifierPattern =
        new(@"^[A-Za-z][\w]*$", RegexOptions.Compiled);

    // VBA DoCmd.Open* / RunMacro navigation patterns. The target name may be a
    // double-quoted string, single-quoted string, or a bracketed identifier
    // ([Object Name]) or a bare identifier (Access permits all forms).
    private static readonly Regex DoCmdOpenFormPattern =
        new(@"(?i)\bDoCmd\.OpenForm\s+(?:""(?<name>[^""]+)""|'(?<name>[^']+)'|\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][\w]*))",
            RegexOptions.Compiled);
    private static readonly Regex DoCmdOpenReportPattern =
        new(@"(?i)\bDoCmd\.OpenReport\s+(?:""(?<name>[^""]+)""|'(?<name>[^']+)'|\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][\w]*))",
            RegexOptions.Compiled);
    private static readonly Regex DoCmdOpenQueryPattern =
        new(@"(?i)\bDoCmd\.OpenQuery\s+(?:""(?<name>[^""]+)""|'(?<name>[^']+)'|\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][\w]*))",
            RegexOptions.Compiled);
    private static readonly Regex DoCmdRunMacroPattern =
        new(@"(?i)\bDoCmd\.(?:RunMacro|OpenMacro)\s+(?:""(?<name>[^""]+)""|'(?<name>[^']+)'|\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][\w]*))",
            RegexOptions.Compiled);

    /// <summary>One CrossReferenceAbsenceSignal per unreferenced object per the §3.3.3 matrix.</summary>
    public static IReadOnlyList<(string ObjectType, string ObjectName, CrossReferenceAbsenceSignal Signal)>
        Detect(IrDatabase db)
    {
        var formNames = new HashSet<string>(db.Forms.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
        var reportNames = new HashSet<string>(db.Reports.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        var queryNames = new HashSet<string>(db.Queries.Select(q => q.Name), StringComparer.OrdinalIgnoreCase);
        var tableNames = new HashSet<string>(db.Tables.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
        var moduleNames = new HashSet<string>(db.Modules.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
        var macroNames = new HashSet<string>(db.Macros.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);

        // Build per-target sets of referrers per reference type. Self-references excluded.
        var refIndex = new RefIndex();

        // RecordSource on forms/reports → table/query names
        foreach (var fr in db.Forms.Concat(db.Reports))
        {
            foreach (string target in CollectRecordSourceTargets(fr, tableNames, queryNames))
            {
                if (string.Equals(target, fr.Name, StringComparison.OrdinalIgnoreCase)) continue;
                refIndex.Add(target, "RecordSource");
            }
        }

        // RowSource on controls → table/query names
        foreach (var fr in db.Forms.Concat(db.Reports))
        {
            foreach (var ctl in fr.Controls)
            {
                foreach (string target in CollectRowSourceTargets(ctl, tableNames, queryNames))
                {
                    if (string.Equals(target, fr.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    refIndex.Add(target, "RowSource");
                }
            }
        }

        // NavigationEdge — macros and form/report event properties.
        foreach (var macro in db.Macros)
        {
            foreach (var act in macro.Actions)
            {
                foreach (string target in NavigationTargets(act, formNames, reportNames, queryNames, macroNames))
                    refIndex.Add(target, "NavigationEdge");
            }
        }
        foreach (var fr in db.Forms.Concat(db.Reports))
        {
            foreach (var act in fr.MacroActions)
            {
                foreach (string target in NavigationTargets(act, formNames, reportNames, queryNames, macroNames))
                {
                    if (string.Equals(target, fr.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    refIndex.Add(target, "NavigationEdge");
                }
            }
            // event-property bindings on top-level Properties and every control
            CollectEventPropertyBindings(fr.Properties, macroNames, formNames, reportNames, queryNames,
                fr.Name, refIndex);
            foreach (var ctl in fr.Controls)
                CollectEventPropertyBindings(ctl.Properties, macroNames, formNames, reportNames, queryNames,
                    fr.Name, refIndex);
        }

        // VBA-driven references. Each VBA blob (form/report code-behind or a standalone
        // module) is scanned for:
        //   - DCount/DLookup/etc. + OpenRecordset (data references → VbaReference)
        //   - DoCmd.OpenForm / OpenReport / OpenQuery / OpenMacro / RunMacro
        //     (navigation references → NavigationEdge to the named form/report/query/macro)
        //   - Bare-token form/report/module/macro names appearing in OTHER VBA blobs
        //     (best-effort name presence → VbaReference). Self-references are excluded:
        //     a form's own code-behind does not count as referencing the form itself.
        foreach (var fr in db.Forms.Concat(db.Reports))
        {
            if (fr.VbaCode is null) continue;
            ProcessVbaBlob(fr.VbaCode, fr.Name, refIndex,
                formNames, reportNames, queryNames, macroNames,
                moduleNames, tableNames);
        }
        foreach (var mod in db.Modules)
        {
            if (mod.VbaCode is null) continue;
            ProcessVbaBlob(mod.VbaCode, mod.Name, refIndex,
                formNames, reportNames, queryNames, macroNames,
                moduleNames, tableNames);
        }

        // QueryTableReference — FROM/JOIN in other queries' SQL
        foreach (var q in db.Queries)
        {
            if (string.IsNullOrWhiteSpace(q.Sql)) continue;
            foreach (string referenced in ExtractFromJoinNames(q.Sql))
            {
                if (string.Equals(referenced, q.Name, StringComparison.OrdinalIgnoreCase)) continue;
                refIndex.Add(referenced, "QueryTableReference");
            }
        }

        // FkRelationship — table parent or child of any non-MSysNavPane* relationship.
        foreach (var rel in db.Relationships)
        {
            if (rel.Name.StartsWith("MSysNavPane", StringComparison.OrdinalIgnoreCase)) continue;
            refIndex.Add(rel.ParentTable, "FkRelationship");
            refIndex.Add(rel.ChildTable, "FkRelationship");
        }

        // Apply the per-object-type matrix.
        var results = new List<(string ObjectType, string ObjectName, CrossReferenceAbsenceSignal Signal)>();

        foreach (var t in db.Tables)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Table", t.Name)) continue;
            string[] checks = ["RecordSource", "RowSource", "VbaReference", "QueryTableReference", "FkRelationship"];
            if (IsUnreferenced(t.Name, checks, refIndex))
                results.Add(("Table", t.Name, BuildSignal(checks)));
        }
        foreach (var q in db.Queries)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Query", q.Name)) continue;
            string[] checks = ["RecordSource", "RowSource", "VbaReference", "QueryTableReference", "NavigationEdge"];
            if (IsUnreferenced(q.Name, checks, refIndex))
                results.Add(("Query", q.Name, BuildSignal(checks)));
        }
        foreach (var f in db.Forms)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Form", f.Name)) continue;
            string[] checks = ["NavigationEdge", "VbaReference"];
            if (IsUnreferenced(f.Name, checks, refIndex))
                results.Add(("Form", f.Name, BuildSignal(checks)));
        }
        foreach (var r in db.Reports)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Report", r.Name)) continue;
            string[] checks = ["NavigationEdge", "VbaReference"];
            if (IsUnreferenced(r.Name, checks, refIndex))
                results.Add(("Report", r.Name, BuildSignal(checks)));
        }
        foreach (var m in db.Modules)
        {
            if (NamePatternDetector.IsCodeBehindModuleName(m.Name)) continue;
            string[] checks = ["VbaReference"];
            if (IsUnreferenced(m.Name, checks, refIndex))
                results.Add(("Module", m.Name, BuildSignal(checks)));
        }
        foreach (var m in db.Macros)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Macro", m.Name)) continue;
            string[] checks = ["NavigationEdge", "VbaReference"];
            if (IsUnreferenced(m.Name, checks, refIndex))
                results.Add(("Macro", m.Name, BuildSignal(checks)));
        }

        return results;
    }

    private static bool IsUnreferenced(string name, string[] checks, RefIndex idx)
    {
        foreach (var c in checks)
            if (idx.HasReferrers(name, c)) return false;
        return true;
    }

    private static CrossReferenceAbsenceSignal BuildSignal(string[] checks)
    {
        string summary = "Not referenced as " + string.Join(", ", checks);
        return new CrossReferenceAbsenceSignal
        {
            Summary = summary,
            CheckedReferenceTypes = checks,
        };
    }

    private static IEnumerable<string> CollectRecordSourceTargets(IrFormOrReport fr,
        HashSet<string> tableNames, HashSet<string> queryNames)
    {
        if (string.IsNullOrWhiteSpace(fr.RecordSource)) yield break;
        string rs = fr.RecordSource!.TrimStart();
        if (LooksLikeInlineSql(rs))
        {
            foreach (string n in ExtractFromJoinNames(rs))
                if (tableNames.Contains(n) || queryNames.Contains(n))
                    yield return n;
        }
        else if (!LooksLikeValueListLiteral(rs))
        {
            if (tableNames.Contains(rs) || queryNames.Contains(rs))
                yield return rs;
        }
    }

    private static IEnumerable<string> CollectRowSourceTargets(IrControl ctl,
        HashSet<string> tableNames, HashSet<string> queryNames)
    {
        // Try control's RowSource field first; fall back to Properties[RowSource]
        string? rs = ctl.RowSource;
        if (string.IsNullOrWhiteSpace(rs) && ctl.Properties.TryGetValue("RowSource", out var pRs))
            rs = pRs;
        if (string.IsNullOrWhiteSpace(rs)) yield break;
        string trimmed = rs!.TrimStart();
        if (LooksLikeValueListLiteral(trimmed)) yield break;
        if (LooksLikeInlineSql(trimmed))
        {
            foreach (string n in ExtractFromJoinNames(trimmed))
                if (tableNames.Contains(n) || queryNames.Contains(n))
                    yield return n;
        }
        else
        {
            // Filter semicolon-delimited value-lists
            if (trimmed.Contains(';')) yield break;
            if (tableNames.Contains(trimmed) || queryNames.Contains(trimmed))
                yield return trimmed;
        }
    }

    private static bool LooksLikeInlineSql(string value)
    {
        string trimmed = value.TrimStart();
        if (trimmed.Length == 0) return false;
        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && (trimmed.Length == 6 || !char.IsLetterOrDigit(trimmed[6])))
            return true;
        if (trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
            && (trimmed.Length == 4 || !char.IsLetterOrDigit(trimmed[4])))
            return true;
        return false;
    }

    private static bool LooksLikeValueListLiteral(string value)
    {
        if (value.Length == 0) return false;
        char c = value[0];
        if (c == '(' || c == '"' || c == '\'') return true;
        if (char.IsDigit(c))
        {
            int semi = value.IndexOf(';');
            if (semi > 0) return true;
        }
        return false;
    }

    private static IReadOnlyList<string> ExtractFromJoinNames(string sql)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        void Add(string n)
        {
            string t = n.Trim();
            if (!string.IsNullOrEmpty(t) && seen.Add(t)) names.Add(t);
        }
        foreach (Match m in FromJoinPattern.Matches(sql))
            Add(m.Groups["name"].Value);
        int idx = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            int end = IndexOfNextClause(sql, idx + 4);
            string clause = sql.Substring(idx + 4, (end >= 0 ? end : sql.Length) - idx - 4);
            foreach (Match m in CommaTablePattern.Matches(clause))
                Add(m.Groups["name"].Value);
            idx = sql.IndexOf("FROM", idx + 4, StringComparison.OrdinalIgnoreCase);
        }
        return names;
    }

    private static int IndexOfNextClause(string sql, int startIdx)
    {
        string[] keywords = ["WHERE", "GROUP", "HAVING", "ORDER", "UNION"];
        int result = -1;
        foreach (var kw in keywords)
        {
            int i = sql.IndexOf(kw, startIdx, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (result < 0 || i < result)) result = i;
        }
        return result;
    }

    private static IEnumerable<string> NavigationTargets(IrMacroAction action,
        HashSet<string> formNames, HashSet<string> reportNames,
        HashSet<string> queryNames, HashSet<string> macroNames)
    {
        if (action.Arguments.Count == 0) yield break;
        string arg0 = action.Arguments[0]?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(arg0)) yield break;
        switch (action.Action)
        {
            case "OpenForm":
                if (formNames.Contains(arg0)) yield return arg0;
                break;
            case "OpenReport":
                if (reportNames.Contains(arg0)) yield return arg0;
                break;
            case "OpenQuery":
                if (queryNames.Contains(arg0)) yield return arg0;
                break;
            case "OpenMacro":
            case "RunMacro":
                if (macroNames.Contains(arg0)) yield return arg0;
                break;
        }
        if (!string.IsNullOrEmpty(action.MacroName) && macroNames.Contains(action.MacroName!))
            yield return action.MacroName!;
    }

    // Per strategy §3.3.2: a bare-identifier event-property value only counts as a
    // NavigationEdge when it names an existing MACRO. Bare identifiers matching
    // forms/reports/queries are not treated as references here — they are either
    // [Event Procedure] (handled via VBA scanning) or unrecognised values.
    private static void CollectEventPropertyBindings(IReadOnlyDictionary<string, string> props,
        HashSet<string> macroNames, HashSet<string> formNames, HashSet<string> reportNames,
        HashSet<string> queryNames, string ownerName, RefIndex refIndex)
    {
        _ = formNames; _ = reportNames; _ = queryNames; // signature kept for call-site clarity
        foreach (var kvp in props)
        {
            string name = kvp.Key;
            if (!(name.StartsWith("On", StringComparison.Ordinal)
               || name.StartsWith("Before", StringComparison.Ordinal)
               || name.StartsWith("After", StringComparison.Ordinal)))
                continue;
            string val = kvp.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(val)) continue;
            if (val.StartsWith("[Event Procedure]", StringComparison.Ordinal)) continue;
            if (val.StartsWith("=", StringComparison.Ordinal)) continue;
            if (!BareIdentifierPattern.IsMatch(val)) continue;
            if (!macroNames.Contains(val)) continue;
            if (string.Equals(val, ownerName, StringComparison.OrdinalIgnoreCase)) continue;
            refIndex.Add(val, "NavigationEdge");
        }
    }

    // Process a single VBA blob (form/report code-behind, or a standalone module).
    // 'ownerName' is the name of the object whose VBA this is — used for the
    // self-reference exclusion (strategy §3.3 "Self-reference exclusion") so that
    // a form's code-behind does not register the form itself as referenced.
    private static void ProcessVbaBlob(string vba, string ownerName, RefIndex refIndex,
        HashSet<string> formNames, HashSet<string> reportNames,
        HashSet<string> queryNames, HashSet<string> macroNames,
        HashSet<string> moduleNames, HashSet<string> tableNames)
    {
        // Data-access references (DCount/DLookup/DSum/etc. second arg + OpenRecordset
        // first arg) → VbaReference to a table or query.
        foreach (Match m in DCountLookupPattern.Matches(vba))
        {
            string name = m.Groups["name"].Value.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (string.Equals(name, ownerName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!tableNames.Contains(name) && !queryNames.Contains(name)) continue;
            refIndex.Add(name, "VbaReference");
        }
        foreach (Match m in OpenRecordsetPattern.Matches(vba))
        {
            string raw = m.Groups["name"].Value.Trim();
            if (string.IsNullOrEmpty(raw)) continue;
            if (LooksLikeInlineSql(raw))
            {
                foreach (string n in ExtractFromJoinNames(raw))
                {
                    if (string.Equals(n, ownerName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!tableNames.Contains(n) && !queryNames.Contains(n)) continue;
                    refIndex.Add(n, "VbaReference");
                }
            }
            else
            {
                if (string.Equals(raw, ownerName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!tableNames.Contains(raw) && !queryNames.Contains(raw)) continue;
                refIndex.Add(raw, "VbaReference");
            }
        }

        // Navigation references — DoCmd.Open* / RunMacro → NavigationEdge.
        ScanDoCmd(vba, DoCmdOpenFormPattern, formNames, ownerName, "NavigationEdge", refIndex);
        ScanDoCmd(vba, DoCmdOpenReportPattern, reportNames, ownerName, "NavigationEdge", refIndex);
        ScanDoCmd(vba, DoCmdOpenQueryPattern, queryNames, ownerName, "NavigationEdge", refIndex);
        ScanDoCmd(vba, DoCmdRunMacroPattern, macroNames, ownerName, "NavigationEdge", refIndex);
        // DoCmd.RunMacro also satisfies the per-object-type matrix's VbaReference
        // for Macro (strategy §3.3.3).
        ScanDoCmd(vba, DoCmdRunMacroPattern, macroNames, ownerName, "VbaReference", refIndex);

        // Bare-token presence in OTHER modules — best-effort module/class/form/report
        // name detection. Strategy §3.3 VbaReference row + §3.3.3 matrix:
        //   - Form / Report VbaReference = the form-/report-name token in another
        //     module's VBA (NOT the form's own code-behind). The strategy says
        //     "the form-name token" — this is the loose bare-token scan.
        //   - Module / Class VbaReference = the module name appears as `Call`,
        //     `Set ... = New`, or a qualifying namespace token (`MyModule.`) —
        //     NOT any bare occurrence (strategy §3.3 VbaReference row).
        // Self-reference exclusion already applied via ownerName guard.
        ScanBareTokenPresence(vba, formNames, ownerName, refIndex);
        ScanBareTokenPresence(vba, reportNames, ownerName, refIndex);
        ScanModuleContextPresence(vba, moduleNames, ownerName, refIndex);
    }

    private static void ScanDoCmd(string vba, Regex pattern, HashSet<string> candidateNames,
        string ownerName, string refType, RefIndex refIndex)
    {
        foreach (Match m in pattern.Matches(vba))
        {
            string name = m.Groups["name"].Value.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (!candidateNames.Contains(name)) continue;
            if (string.Equals(name, ownerName, StringComparison.OrdinalIgnoreCase)) continue;
            refIndex.Add(name, refType);
        }
    }

    private static void ScanBareTokenPresence(string vba, HashSet<string> names,
        string ownerName, RefIndex refIndex)
    {
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            if (string.Equals(n, ownerName, StringComparison.OrdinalIgnoreCase)) continue;
            // Use a manual word-boundary scan rather than Regex \b because '_' is a
            // VBA identifier character. We require the surrounding characters to NOT
            // be ASCII letters/digits/underscores so the match is a stand-alone token.
            if (ContainsBareToken(vba, n)) refIndex.Add(n, "VbaReference");
        }
    }

    /// <summary>
    /// Module/class names per strategy §3.3 VbaReference row: a module is
    /// referenced when its name appears as one of three callable / constructor /
    /// qualifier contexts in another module's VBA. A bare occurrence (e.g. in a
    /// comment, string literal, or as a local variable name that happens to
    /// match) is NOT a reference (R2 Issue 4 narrowing).
    /// </summary>
    private static void ScanModuleContextPresence(string vba, HashSet<string> moduleNames,
        string ownerName, RefIndex refIndex)
    {
        foreach (var n in moduleNames)
        {
            if (string.IsNullOrEmpty(n)) continue;
            if (string.Equals(n, ownerName, StringComparison.OrdinalIgnoreCase)) continue;
            string esc = Regex.Escape(n);
            // Build the three context regexes per module name. We allow optional
            // surrounding whitespace and use a negative-lookahead/-lookbehind for
            // VBA word boundaries (identifier-char class '\w') so 'modUtilA' does
            // not match 'modUtilities'.
            // 1. Call <name>
            // 2. Set <var> = New <name>  (anything between Set and New, on one line)
            // 3. <name>. (qualifier dot-access)
            // Compile each as case-insensitive single-line regex.
            string callPattern = $@"(?i)\bCall\s+{esc}\b";
            string newPattern = $@"(?i)\bSet\b[^\r\n]*\bNew\s+{esc}\b";
            string qualPattern = $@"(?<![A-Za-z0-9_]){esc}\.";

            if (Regex.IsMatch(vba, callPattern)
                || Regex.IsMatch(vba, newPattern)
                || Regex.IsMatch(vba, qualPattern))
            {
                refIndex.Add(n, "VbaReference");
            }
        }
    }

    private static bool ContainsBareToken(string haystack, string token)
    {
        int idx = 0;
        while ((idx = haystack.IndexOf(token, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            bool leftOk = idx == 0 || !IsIdentChar(haystack[idx - 1]);
            int end = idx + token.Length;
            bool rightOk = end >= haystack.Length || !IsIdentChar(haystack[end]);
            if (leftOk && rightOk) return true;
            idx = end;
        }
        return false;
    }

    private static bool IsIdentChar(char c)
        => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Per-target × ref-type index of referrers.</summary>
    private sealed class RefIndex
    {
        private readonly Dictionary<string, HashSet<string>> _data = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string target, string refType)
        {
            if (string.IsNullOrWhiteSpace(target)) return;
            if (!_data.TryGetValue(target, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _data[target] = set;
            }
            set.Add(refType);
        }

        public bool HasReferrers(string target, string refType)
            => _data.TryGetValue(target, out var set) && set.Contains(refType);
    }
}
