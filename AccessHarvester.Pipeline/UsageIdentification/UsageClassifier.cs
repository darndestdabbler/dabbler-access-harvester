using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Classifies all non-excluded objects in an <see cref="IrDatabase"/> using
/// the six signal families and the rule tables defined in
/// <c>docs/planning/object-use-identification-strategy.md</c> §4.
/// </summary>
public static class UsageClassifier
{
    // QueryTypeCode constants are centralized in DaoQueryTypeCodes (006 F-2-1:
    // the previously-inlined table was shifted, mis-classifying crosstabs as
    // possibly-unused action queries).

    /// <summary>
    /// Produces one <see cref="UsageFinding"/> per object in the database.
    /// Objects in <paramref name="excludedObjects"/> are skipped.
    /// </summary>
    public static IReadOnlyList<UsageFinding> Classify(
        IrDatabase db,
        SignalIndex index,
        IReadOnlySet<string>? excludedObjects = null)
    {
        var findings = new List<UsageFinding>();

        foreach (var form in db.Forms)
        {
            if (IsExcluded(form.Name, excludedObjects)) continue;
            findings.Add(ClassifyForm(form, index));
        }
        foreach (var report in db.Reports)
        {
            if (IsExcluded(report.Name, excludedObjects)) continue;
            findings.Add(ClassifyReport(report, index));
        }
        foreach (var query in db.Queries)
        {
            if (IsExcluded(query.Name, excludedObjects)) continue;
            findings.Add(ClassifyQuery(query, index));
        }
        foreach (var table in db.Tables)
        {
            if (IsExcluded(table.Name, excludedObjects)) continue;
            if (IsSystemTable(table.Name)) continue;
            findings.Add(ClassifyTable(table, index));
        }
        foreach (var module in db.Modules)
        {
            if (IsExcluded(module.Name, excludedObjects)) continue;
            findings.Add(ClassifyModule(module, index));
        }
        foreach (var macro in db.Macros)
        {
            if (IsExcluded(macro.Name, excludedObjects)) continue;
            findings.Add(ClassifyMacro(macro, index));
        }

        return findings;
    }

    // ── Forms ────────────────────────────────────────────────────────────────────

    private static UsageFinding ClassifyForm(IrFormOrReport form, SignalIndex index)
    {
        // hasRecordSource: form is bound to any non-empty source (named OR inline SQL)
        bool hasRecordSource = !string.IsNullOrWhiteSpace(form.RecordSource);
        // rs: the named table/query name if RecordSource is a saved object; null for inline SQL
        index.RecordSourceByObject.TryGetValue(form.Name, out string? rs);

        bool hasCrudHandlers = form.HasBeforeUpdate || form.HasAfterUpdate || form.HasOnDelete;
        bool hasNavEdgesOut  = index.NavigationSources.Contains(form.Name);
        bool hasVbaCode      = !string.IsNullOrWhiteSpace(form.VbaCode);

        // Launcher-surface evidence (set 001 Session 1): a switchboard/menu/startup
        // form is a navigation shell even when it has no RecordSource and delegates
        // its opens to a separate macro (so it emits no navigation edges of its own).
        bool launcherName   = SignalIndex.IsLauncherFormName(form.Name);
        bool startupOpened  = index.IsStartupOpened(form.Name);
        bool launcherChrome = HasLauncherChrome(form);
        bool isLauncher     = launcherName || startupOpened;
        // Parameter/filter/criteria dialog by name (set 001 Session 2, rule F8).
        // The unbound + not-launcher gating is structural via branch order below,
        // so the name match only takes effect in the no-RecordSource, non-launcher
        // path; it never overrides a bound or launcher form.
        bool paramDialogName = SignalIndex.IsParameterDialogName(form.Name);

        string category;
        double confidence;
        var evidence = new List<string>();
        // Non-null only when rule F8 fires; carries the ParameterizedInput signal
        // evidence so the signal is gated on the F8 decision, not recomputed from
        // the name at emission time (set 001 Session 1 discipline).
        List<string>? paramEvidence = null;

        if (hasRecordSource && rs != null)
        {
            evidence.Add($"RecordSource={rs}");
            bool rsIsTransactional = index.HasFkChildren(rs);
            bool rsIsLookup = index.IsLookupTable(rs) || IsLookupSource(rs, index);

            if (rsIsTransactional && hasCrudHandlers)
            {
                // F1
                category   = UsageCategory.MainDataEntry;
                confidence = 0.85;
                if (hasCrudHandlers) evidence.Add("CRUD-VBA-handlers");
            }
            else if (rsIsTransactional)
            {
                // F2
                category   = UsageCategory.MainDataEntry;
                confidence = 0.65;
            }
            else if (rsIsLookup)
            {
                // F3
                category   = UsageCategory.LookupReference;
                confidence = 0.75;
            }
            else if (index.KnownQueryNames.Contains(rs))
            {
                // F4 — RecordSource is a known query; Reporting at medium confidence
                category   = UsageCategory.Reporting;
                confidence = 0.55;
            }
            else
            {
                // RecordSource is a named table without FK or lookup signals — treat as MainDataEntry
                // at low confidence (the table's relationships may not be in the IR)
                category   = UsageCategory.MainDataEntry;
                confidence = 0.50;
            }
        }
        else if (hasRecordSource && rs == null)
        {
            // Form is bound (RecordSource is non-empty) but uses inline SQL —
            // most likely a data-entry form. Classify as MainDataEntry at medium confidence.
            category   = UsageCategory.MainDataEntry;
            confidence = 0.60;
            evidence.Add("bound-inline-SQL");
        }
        else if (hasNavEdgesOut)
        {
            // F5
            category   = UsageCategory.NavigationShell;
            confidence = 0.80;
            evidence.Add("has-navigation-edges-out");
        }
        else if (isLauncher)
        {
            // F5b — launcher/menu shell recognized by name or startup linkage even
            // though it emits no navigation edges itself (opens are delegated to a
            // switchboard macro, or it is the database's startup surface).
            category   = UsageCategory.NavigationShell;
            confidence = (launcherName && launcherChrome) ? 0.80
                       : startupOpened                    ? 0.75
                       :                                     0.70;
            if (launcherName)   evidence.Add("launcher-name-pattern");
            if (launcherChrome) evidence.Add("launcher-chrome");
            if (startupOpened)  evidence.Add("opened-by-startup-macro");
        }
        else if (paramDialogName)
        {
            // F8 — unbound parameter/filter/criteria dialog (set 001 Session 2,
            // §2b). Keeps the Unknown category (the ParameterDialogFlow question,
            // not a positive category, is the F8 deliverable per the strategy
            // contract) but is positively recognized: it fires ParameterizedInput
            // and a parameter-dialog summary/question, so it no longer reads as a
            // generic VBA-only Unknown form. Sits ahead of F6 so a frmReportParam_*
            // dialog is recognized rather than lumped with unclassified VBA forms.
            category   = UsageCategory.Unknown;
            confidence = 0.40;
            paramEvidence = ["param-dialog-name-pattern"];
            if (launcherChrome) paramEvidence.Add("dialog-chrome");
            if (index.NavigationTargets.Contains(form.Name)) paramEvidence.Add("opened-as-dialog");
            evidence.AddRange(paramEvidence);
        }
        else if (hasVbaCode)
        {
            // F6
            category   = UsageCategory.Unknown;
            confidence = 0.30;
            evidence.Add("has-VBA-no-record-source");
        }
        else if (index.NavigationTargets.Contains(form.Name))
        {
            // F7b: form is opened by navigation but has no other classifying signals
            category   = UsageCategory.Unknown;
            confidence = 0.40;
            evidence.Add("is-navigation-target-no-other-signals");
        }
        else
        {
            // F7: no signals at all
            category   = UsageCategory.PossiblyUnused;
            confidence = 0.70;
        }

        confidence = AdjustConfidence(confidence, form.Name, "Form", index,
                                      hasRecordSource, hasNavEdgesOut, hasVbaCode, 0);
        if (confidence < 0.2) category = UsageCategory.Unknown;

        return BuildFinding("Form", form.Name, category, confidence, evidence,
                            "Form", form.RecordSource, hasRecordSource,
                            hasNavEdgesOut, index.NavigationTargets.Contains(form.Name),
                            hasVbaCode, hasCrudHandlers, null, index, launcherChrome,
                            parameterDialogForm: paramEvidence != null,
                            parameterEvidence: paramEvidence);
    }

    // ── Reports ──────────────────────────────────────────────────────────────────

    private static UsageFinding ClassifyReport(IrFormOrReport report, SignalIndex index)
    {
        // hasRecordSource: report is bound to any non-empty source (named OR inline SQL)
        bool hasRecordSource = !string.IsNullOrWhiteSpace(report.RecordSource);
        bool openedByNavigation = index.NavigationTargets.Contains(report.Name);

        string category;
        double confidence;
        var evidence = new List<string>();

        if (hasRecordSource)
        {
            // R1: Report has a RecordSource (named or inline SQL)
            category   = UsageCategory.Reporting;
            confidence = 0.90;
            if (index.RecordSourceByObject.TryGetValue(report.Name, out var rs))
                evidence.Add($"RecordSource={rs}");
            else
                evidence.Add("bound-inline-SQL");
        }
        else if (openedByNavigation)
        {
            // R2
            category   = UsageCategory.Reporting;
            confidence = 0.70;
            evidence.Add("opened-by-navigation");
        }
        else
        {
            // R3
            category   = UsageCategory.PossiblyUnused;
            confidence = 0.80;
        }

        confidence = AdjustConfidence(confidence, report.Name, "Report", index,
                                      hasRecordSource, false, false, 0);
        if (confidence < 0.2) category = UsageCategory.Unknown;

        return BuildFinding("Report", report.Name, category, confidence, evidence,
                            "Report", report.RecordSource, hasRecordSource,
                            false, openedByNavigation, false, false, null, index);
    }

    // ── Queries ───────────────────────────────────────────────────────────────────

    private static UsageFinding ClassifyQuery(IrQuery query, SignalIndex index)
    {
        string category;
        double confidence;
        var evidence = new List<string>();

        bool usedAsFormRS   = index.UsedAsFormRecordSource.Contains(query.Name);
        bool usedAsReportRS = index.UsedAsReportRecordSource.Contains(query.Name);
        bool hasParamMarker = query.Sql != null &&
                              (query.Sql.Contains('?') || ContainsParamBracket(query.Sql));
        bool refsNoTables   = query.Sql != null && QueryRefsNoTables(query.Name, index);
        bool unreferenced   = IsCompletelyUnreferenced(query.Name, index);
        bool openedByNav    = index.NavigationTargets.Contains(query.Name);
        bool openedByVba    = index.VbaReferencedObjects.Contains(query.Name);

        int tc = query.QueryTypeCode;

        if (tc == DaoQueryTypeCodes.Crosstab)
        {
            // Q1
            category   = UsageCategory.Reporting;
            confidence = 0.70;
            evidence.Add("Crosstab");
        }
        else if (DaoQueryTypeCodes.IsActionQuery(tc))
        {
            if (openedByNav || openedByVba)
            {
                // Q8
                category   = UsageCategory.StandaloneUtility;
                confidence = 0.75;
                evidence.Add($"ActionQuery(type={tc})");
                evidence.Add(openedByNav ? "opened-by-navigation" : "referenced-in-VBA");
            }
            else
            {
                // Q9
                category   = UsageCategory.PossiblyUnused;
                confidence = 0.65;
                evidence.Add($"ActionQuery(type={tc})");
            }
        }
        else if (tc is DaoQueryTypeCodes.DataDefinition or DaoQueryTypeCodes.PassThrough
                    or DaoQueryTypeCodes.SptBulk)
        {
            // Q11
            category   = UsageCategory.StandaloneUtility;
            confidence = 0.60;
            evidence.Add($"InfraQuery(type={tc})");
        }
        else
        {
            // Select (0) or Union (64) — apply Q2–Q7
            if (usedAsFormRS)
            {
                // Q2
                category   = UsageCategory.MainDataEntry;
                confidence = 0.75;
                evidence.Add("form-RecordSource");
            }
            else if (usedAsReportRS)
            {
                // Q3
                category   = UsageCategory.Reporting;
                confidence = 0.80;
                evidence.Add("report-RecordSource");
            }
            else if (hasParamMarker)
            {
                // Q5
                category   = UsageCategory.Reporting;
                confidence = 0.65;
                evidence.Add("parameter-markers");
            }
            else if (!unreferenced)
            {
                // Q4
                category   = UsageCategory.Reporting;
                confidence = 0.60;
            }
            else
            {
                // Q7
                category   = UsageCategory.PossiblyUnused;
                confidence = 0.65;
            }

            if (refsNoTables)
            {
                category   = UsageCategory.Unknown;
                confidence = 0.40;
                evidence.Add("references-no-tables");
            }
        }

        confidence = AdjustConfidence(confidence, query.Name, "Query", index,
                                      false, false, false, tc);
        if (confidence < 0.2) category = UsageCategory.Unknown;

        // Parameter expectation is a first-class signal, computed independently of
        // the Q-rule category above so a parameterized query that classifies via
        // Q2/Q3 (used as a form/report RecordSource) still surfaces it (set 001
        // Session 2, §3.9). The Q5 category path above is unchanged.
        var parameterEvidence = BuildQueryParameterEvidence(query);

        return BuildFinding("Query", query.Name, category, confidence, evidence,
                            "Query", null, false,
                            index.NavigationSources.Contains(query.Name),
                            openedByNav,
                            false, false, query.QueryTypeCode, index,
                            parameterEvidence: parameterEvidence);
    }

    /// <summary>
    /// Builds the ParameterizedInput evidence for a query: the declared / form- /
    /// report-reference parameters carried on <see cref="IrQuery.Parameters"/>
    /// (parsed at load by <see cref="IrLoader.ExtractQueryParameters"/>), plus two
    /// SQL-text signals computed here — an inline bracketed prompt when no declared
    /// parameter already covers it, and an ODBC <c>?</c> marker. Returns null when
    /// no parameter evidence is present.
    /// </summary>
    private static IReadOnlyList<string>? BuildQueryParameterEvidence(IrQuery query)
    {
        var evidence = new List<string>();

        bool declaredOrRef = false;
        if (query.Parameters != null)
        {
            foreach (var p in query.Parameters)
            {
                switch (p.Source)
                {
                    case "declared":
                        evidence.Add($"declared-parameter:{p.Name}");
                        declaredOrRef = true;
                        break;
                    case "form-reference":
                        evidence.Add($"form-control-reference:{p.Path ?? p.Name}");
                        declaredOrRef = true;
                        break;
                    case "report-reference":
                        evidence.Add($"report-control-reference:{p.Path ?? p.Name}");
                        declaredOrRef = true;
                        break;
                }
            }
        }

        string? sql = query.Sql;
        if (sql != null)
        {
            // An inline bracketed prompt (e.g. WHERE FY = [Enter Fiscal Year]) only
            // when no declared/reference parameter already covers it — avoids
            // re-listing the declared prompt names.
            if (!declaredOrRef && ContainsParamBracket(sql))
                evidence.Add("bracketed-prompt");

            if (sql.Contains('?'))
                evidence.Add("odbc-parameter-marker");
        }

        return evidence.Count > 0 ? evidence : null;
    }

    // ── Tables ────────────────────────────────────────────────────────────────────

    private static UsageFinding ClassifyTable(IrTable table, SignalIndex index)
    {
        string category;
        double confidence;
        var evidence = new List<string>();

        bool hasFkChildren  = index.HasFkChildren(table.Name);
        bool hasFkParents   = index.HasFkParents(table.Name);
        bool usedAsRS       = index.UsedAsRecordSource.Contains(table.Name);
        bool vbaReferenced  = index.VbaReferencedObjects.Contains(table.Name);
        bool isLookupNamed  = index.IsLookupTable(table.Name);
        bool unreferenced   = index.IsCompletelyUnreferenced(table.Name);
        // True only when the launcher-data rule (T5b) actually decides the category,
        // so the launcher summary/signal never fire for a launcher-named table that
        // an earlier rule already explained (e.g., a used 'tblMenuItems' data table).
        bool launcherDataTable = false;

        if (hasFkChildren && usedAsRS)
        {
            // T1
            category   = UsageCategory.MainDataEntry;
            confidence = 0.85;
            evidence.Add("FK-parent");
            evidence.Add("RecordSource");
        }
        else if (hasFkChildren)
        {
            // T2
            category   = UsageCategory.MainDataEntry;
            confidence = 0.65;
            evidence.Add("FK-parent");
        }
        else if (hasFkParents && !hasFkChildren && table.ColumnCount <= 4)
        {
            // T3
            category   = UsageCategory.LookupReference;
            confidence = 0.80;
            evidence.Add("FK-child-only");
            evidence.Add($"ColumnCount={table.ColumnCount}");
        }
        else if (isLookupNamed)
        {
            // T4
            category   = UsageCategory.LookupReference;
            confidence = 0.75;
            evidence.Add("lookup-name-pattern");
        }
        else if (usedAsRS && hasFkParents)
        {
            // T4b — FK-child table used as a RecordSource; treat as a transactional data-entry table
            category   = UsageCategory.MainDataEntry;
            confidence = 0.60;
            evidence.Add("used-as-RecordSource");
            evidence.Add("FK-child");
        }
        else if (vbaReferenced)
        {
            // T5
            category   = UsageCategory.StandaloneUtility;
            confidence = 0.65;
            evidence.Add("VBA-referenced");
        }
        else if (SignalIndex.IsLauncherDataTableName(table.Name))
        {
            // T5b — switchboard/menu navigation-data table backing a launcher
            // surface. Tie it to launcher behavior rather than letting the lack of
            // FK/inbound references read as a generic unexplained/unused object.
            category   = UsageCategory.StandaloneUtility;
            confidence = 0.65;
            evidence.Add("switchboard-navigation-data");
            launcherDataTable = true;
        }
        else if (unreferenced)
        {
            // T6
            category   = UsageCategory.PossiblyUnused;
            confidence = 0.70;
        }
        else
        {
            category   = UsageCategory.Unknown;
            confidence = 0.30;
        }

        confidence = AdjustConfidence(confidence, table.Name, "Table", index,
                                      usedAsRS, false, vbaReferenced, 0);
        if (confidence < 0.2) category = UsageCategory.Unknown;

        return BuildFinding("Table", table.Name, category, confidence, evidence,
                            "Table", null, usedAsRS,
                            false, false,
                            vbaReferenced, false, null, index,
                            launcherDataTable: launcherDataTable);
    }

    // ── Modules ───────────────────────────────────────────────────────────────────

    private static UsageFinding ClassifyModule(IrModule module, SignalIndex index)
    {
        bool hasVba = !string.IsNullOrWhiteSpace(module.VbaCode);
        string summary = hasVba
            ? $"VBA module '{module.Name}' providing shared code for the application"
            : $"VBA module '{module.Name}' (no code detected)";
        return new UsageFinding
        {
            ObjectType     = "Module",
            ObjectName     = module.Name,
            Classification = UsageCategory.StandaloneUtility,
            Confidence     = hasVba ? 0.70 : 0.40,
            UsageSummary   = summary,
            Signals        = BuildSignals(module.Name, "Module", null, false,
                                          false, false, hasVba, null, index),
            Questions      = [],
        };
    }

    // ── Macros ────────────────────────────────────────────────────────────────────

    private static UsageFinding ClassifyMacro(IrMacro macro, SignalIndex index)
    {
        bool hasNavEdges = index.NavigationSources.Contains(macro.Name);
        bool isNavTarget = index.NavigationTargets.Contains(macro.Name);
        bool unreferenced = index.IsCompletelyUnreferenced(macro.Name);
        // A switchboard/menu-named macro is a launcher helper even if its actions did
        // not parse into navigation edges (set 001 Session 1).
        bool launcherNamed = SignalIndex.IsLauncherFormName(macro.Name);
        bool isLauncherMacro = hasNavEdges || launcherNamed;
        string category = isLauncherMacro ? UsageCategory.NavigationShell : UsageCategory.StandaloneUtility;
        double confidence = hasNavEdges ? 0.75 : launcherNamed ? 0.65 : unreferenced ? 0.60 : 0.55;
        // Word the summary by evidence source so it never overstates harvested
        // evidence: only claim "contains navigation actions" when edges were parsed,
        // and only tie it to "a launcher surface" when the macro is launcher-named.
        // A generic edge-backed macro keeps neutral navigation wording.
        string summary =
            (hasNavEdges && launcherNamed)
                ? $"Macro '{macro.Name}' containing navigation actions that open forms or reports for a launcher surface"
            : hasNavEdges
                ? $"Macro '{macro.Name}' containing navigation actions that open forms or reports"
            : launcherNamed
                ? $"Macro '{macro.Name}' named like a switchboard/menu launcher helper (no navigation actions parsed)"
                : $"Macro '{macro.Name}' providing automated actions";
        return new UsageFinding
        {
            ObjectType     = "Macro",
            ObjectName     = macro.Name,
            Classification = category,
            Confidence     = confidence,
            UsageSummary   = summary,
            Signals        = BuildSignals(macro.Name, "Macro", null, false,
                                          hasNavEdges, isNavTarget, false, null, index),
            Questions      = [],
        };
    }

    // ── Confidence adjustment ─────────────────────────────────────────────────────

    private static double AdjustConfidence(double base_, string name, string objectType,
        SignalIndex index, bool hasRecordSource, bool hasNavEdges, bool hasVbaCode,
        int queryTypeCode)
    {
        double adj = base_;

        // +0.05 per additional corroborating signal type (max +0.15)
        int extra = 0;
        if (hasRecordSource) extra++;
        if (hasNavEdges) extra++;
        if (hasVbaCode) extra++;
        adj += Math.Min(extra * 0.05, 0.15);

        // -0.10 cryptic name: no conventional prefix and < 5 chars
        if (IsCrypticName(name))
            adj -= 0.10;

        return Math.Clamp(adj, 0.0, 1.0);
    }

    // ── Usage summary ─────────────────────────────────────────────────────────────

    private static UsageFinding BuildFinding(
        string objectType, string objectName,
        string category, double confidence,
        IReadOnlyList<string> primaryEvidence,
        string objectKind, string? recordSource,
        bool usedAsRecordSource, bool isNavSource, bool isNavTarget,
        bool hasVbaCode, bool hasCrudHandlers,
        int? queryTypeCode,
        SignalIndex index,
        bool launcherChrome = false,
        bool launcherDataTable = false,
        bool parameterDialogForm = false,
        IReadOnlyList<string>? parameterEvidence = null)
    {
        string summary = BuildSummary(objectType, objectName, category, recordSource,
                                      queryTypeCode, index, launcherDataTable, parameterDialogForm);
        var signals = BuildSignals(objectName, objectType, recordSource, usedAsRecordSource,
                                   isNavSource, isNavTarget, hasVbaCode, queryTypeCode, index,
                                   launcherChrome, launcherDataTable, parameterEvidence);
        var questions = QuestionGenerator.Generate(objectType, objectName, category, confidence,
                                                   signals, recordSource, hasVbaCode, isNavTarget,
                                                   queryTypeCode, index);
        return new UsageFinding
        {
            ObjectType    = objectType,
            ObjectName    = objectName,
            Classification = category,
            Confidence    = Math.Round(confidence, 2),
            UsageSummary  = summary,
            Signals       = signals,
            Questions     = questions,
        };
    }

    private static string BuildSummary(string objectType, string name, string category,
                                        string? recordSource, int? queryTypeCode, SignalIndex index,
                                        bool launcherDataTable = false, bool parameterDialogForm = false)
    {
        // Launcher-data tables get a launcher-aware summary instead of the generic
        // "not referenced by any form or report" StandaloneUtility wording — but only
        // when the launcher-data rule (T5b) actually decided the category, never from
        // the name alone.
        if (objectType == "Table" && launcherDataTable)
            return $"Navigation/switchboard configuration table '{name}' backing the application's launcher menu";

        // Parameter-dialog forms (rule F8) get a parameter-aware summary instead of
        // the generic "insufficient signals" Unknown wording — but only when F8
        // actually fired (threaded flag), never recomputed from the name here. This
        // is what makes a recognized parameter dialog read distinctly from a generic
        // unknown form (set 001 Session 2, §2b).
        if (objectType == "Form" && parameterDialogForm)
            return $"Likely parameter/filter dialog form '{name}' — an unbound form that appears to collect run-time criteria for a report or query (no record source of its own)";

        return category switch
        {
            UsageCategory.MainDataEntry   => recordSource != null
                ? $"Main data entry {objectType.ToLowerInvariant()} backed by {recordSource}"
                : $"Main data entry {objectType.ToLowerInvariant()}",
            UsageCategory.LookupReference => $"Lookup/reference {objectType.ToLowerInvariant()} '{name}'",
            UsageCategory.Reporting       => queryTypeCode == DaoQueryTypeCodes.Crosstab
                ? $"Crosstab query '{name}' used for reporting or data analysis"
                : recordSource != null
                    ? $"Report backed by {recordSource}"
                    : $"Reporting {objectType.ToLowerInvariant()} '{name}'",
            // Launcher wording only when launcher evidence is actually present
            // (F5b: launcher name or startup linkage). A generic form classified
            // NavigationShell purely from its own navigation edges (F5) keeps the
            // neutral wording so the summary does not overstate switchboard/menu traits.
            UsageCategory.NavigationShell =>
                (SignalIndex.IsLauncherFormName(name) || index.IsStartupOpened(name))
                    ? $"Navigation/launcher form '{name}' — a switchboard/menu shell that routes to other forms or reports (directly or via a navigation macro)"
                    : $"Navigation/shell form '{name}' that opens other forms or reports",
            UsageCategory.StandaloneUtility => $"Standalone utility {objectType.ToLowerInvariant()} '{name}' not referenced by any form or report",
            UsageCategory.PossiblyUnused  => $"{objectType} '{name}' is not referenced by any form, report, query, VBA, or macro",
            _                             => $"{objectType} '{name}' — insufficient signals to determine usage",
        };
    }

    private static IReadOnlyList<SignalEntry> BuildSignals(
        string objectName, string objectType,
        string? recordSource, bool usedAsRecordSource,
        bool isNavSource, bool isNavTarget,
        bool hasVbaCode, int? queryTypeCode, SignalIndex index,
        bool launcherChrome = false, bool launcherDataTable = false,
        IReadOnlyList<string>? parameterEvidence = null)
    {
        var signals = new List<SignalEntry>();

        // RecordSource signal (for form/report: does it have one; for table/query: is it used as one)
        if (objectType is "Form" or "Report")
        {
            signals.Add(new SignalEntry
            {
                SignalType = UsageSignalType.RecordSource,
                Fired = recordSource != null,
                Evidence = recordSource != null ? [recordSource] : [],
            });
        }
        else
        {
            signals.Add(new SignalEntry
            {
                SignalType = UsageSignalType.RecordSource,
                Fired = usedAsRecordSource,
                Evidence = usedAsRecordSource ? ["used-as-RecordSource"] : [],
            });
        }

        // RowSource
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.RowSource,
            Fired = index.UsedAsRowSource.Contains(objectName),
            Evidence = index.UsedAsRowSource.Contains(objectName) ? ["used-as-RowSource"] : [],
        });

        // Navigation edges
        var navEvidence = new List<string>();
        if (isNavSource) navEvidence.Add("emits-navigation-edges");
        if (isNavTarget) navEvidence.Add("is-navigation-target");
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.NavigationEdge,
            Fired = isNavSource || isNavTarget,
            Evidence = navEvidence,
        });

        // VBA references
        bool vbaRef = index.VbaReferencedObjects.Contains(objectName);
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.VbaReference,
            Fired = vbaRef,
            Evidence = vbaRef ? ["domain-function-or-recordset-reference"] : [],
        });

        // Query SQL references
        bool sqlRef = index.UsedInQuerySql.Contains(objectName);
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.QueryTableReference,
            Fired = sqlRef,
            Evidence = sqlRef ? ["appears-in-query-FROM-or-JOIN"] : [],
        });

        // FK relationships
        bool fkChild = index.HasFkChildren(objectName);
        bool fkParent = index.HasFkParents(objectName);
        var fkEvidence = new List<string>();
        if (fkChild)  fkEvidence.Add("has-FK-children");
        if (fkParent) fkEvidence.Add("has-FK-parents");
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.FkRelationship,
            Fired = fkChild || fkParent,
            Evidence = fkEvidence,
        });

        // Query type (for queries only)
        if (queryTypeCode.HasValue)
        {
            signals.Add(new SignalEntry
            {
                SignalType = UsageSignalType.QueryType,
                Fired = true,
                Evidence = [$"QueryTypeCode={queryTypeCode}"],
            });
        }
        else
        {
            signals.Add(new SignalEntry
            {
                SignalType = UsageSignalType.QueryType,
                Fired = false,
                Evidence = [],
            });
        }

        // Object name convention
        bool cryptic = IsCrypticName(objectName);
        bool lookupNamed = index.IsLookupTable(objectName);
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.ObjectNameConvention,
            Fired = cryptic || lookupNamed,
            Evidence = cryptic ? ["cryptic-name"] : lookupNamed ? ["lookup-name-pattern"] : [],
        });

        // Launcher surface — switchboard/menu/startup traits. Mirrors the
        // classifier's launcher predicates so the signal agrees with the category.
        var launcherEvidence = new List<string>();
        if (objectType == "Form")
        {
            // Mirror F5b exactly: a form is a launcher surface only when it is
            // unbound. A bound startup-opened / launcher-named form classifies from
            // its RecordSource branch, so it must NOT report LauncherSurface (its
            // StartupEntry signal below still fires — that is honest). Chrome is
            // recorded only as a corroborator of a primary name/startup signal.
            bool unbound = string.IsNullOrWhiteSpace(recordSource);
            if (unbound && SignalIndex.IsLauncherFormName(objectName)) launcherEvidence.Add("launcher-name-pattern");
            if (unbound && index.IsStartupOpened(objectName))          launcherEvidence.Add("opened-by-startup-macro");
            if (unbound && launcherChrome && launcherEvidence.Count > 0) launcherEvidence.Add("launcher-chrome");
        }
        else if (objectType == "Table")
        {
            // Gated on the T5b decision (not the name) so a launcher-named table that
            // an earlier rule explained does not emit a launcher signal.
            if (launcherDataTable) launcherEvidence.Add("switchboard-navigation-data");
        }
        else if (objectType == "Macro")
        {
            if (isNavSource)                                launcherEvidence.Add("emits-navigation-edges");
            if (SignalIndex.IsLauncherFormName(objectName)) launcherEvidence.Add("launcher-name-pattern");
        }
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.LauncherSurface,
            Fired = launcherEvidence.Count > 0,
            Evidence = launcherEvidence,
        });

        // Startup entry — opened by an AutoExec/startup macro.
        bool startupEntry = index.IsStartupOpened(objectName);
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.StartupEntry,
            Fired = startupEntry,
            Evidence = startupEntry ? ["opened-by-startup-macro"] : [],
        });

        // Parameterized input — declared/bracketed/control-reference/ODBC
        // parameter evidence for queries, or parameter-dialog recognition for
        // forms (rule F8). Gated on the classifier's threaded evidence list so it
        // agrees with the decision that fired and is never recomputed from the
        // object name here (set 001 Session 2, §3.9; mirrors the launcher flag
        // discipline from Session 1).
        signals.Add(new SignalEntry
        {
            SignalType = UsageSignalType.ParameterizedInput,
            Fired = parameterEvidence is { Count: > 0 },
            Evidence = parameterEvidence ?? [],
        });

        return signals;
    }

    // ── Launcher chrome ───────────────────────────────────────────────────────────

    /// <summary>
    /// True when an unbound form carries classic launcher chrome: no RecordSource,
    /// record selectors off, and navigation buttons off. Used only to corroborate a
    /// name/startup launcher signal (it does not, on its own, classify a form as a
    /// launcher — that would risk catching stripped-chrome parameter dialogs).
    /// </summary>
    private static bool HasLauncherChrome(IrFormOrReport form)
    {
        if (!string.IsNullOrWhiteSpace(form.RecordSource)) return false;
        return PropIsFalse(form, "RecordSelectors") && PropIsFalse(form, "NavigationButtons");
    }

    private static bool PropIsFalse(IrFormOrReport form, string key)
        => form.Properties.TryGetValue(key, out var v)
        && (v.Equals("False", StringComparison.OrdinalIgnoreCase)
            || v.Equals("No", StringComparison.OrdinalIgnoreCase)
            || v == "0");

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static bool IsExcluded(string name, IReadOnlySet<string>? excluded)
        => excluded != null && excluded.Contains(name);

    private static bool IsSystemTable(string name)
        => name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("USys", StringComparison.OrdinalIgnoreCase);

    private static bool IsLookupSource(string source, SignalIndex index)
        => index.IsLookupTable(source);

    private static bool ContainsParamBracket(string sql)
    {
        // Matches [ParameterName] pattern — simple bracket-delimited parameter
        int start = -1;
        for (int i = 0; i < sql.Length; i++)
        {
            if (sql[i] == '[') { start = i; continue; }
            if (sql[i] == ']' && start >= 0)
            {
                string content = sql.Substring(start + 1, i - start - 1);
                // If it contains spaces or looks like a parameter (not a field reference), fire
                if (content.Contains(' ') || content.Contains("Param"))
                    return true;
                start = -1;
            }
        }
        return false;
    }

    private static bool QueryRefsNoTables(string queryName, SignalIndex index)
    {
        if (!index.QuerySqlTargetsByQuery.TryGetValue(queryName, out var targets))
            return false;  // SQL absent; don't fire
        if (targets.Count == 0)
            return false;
        // "References no tables" means all references are to other queries (not base tables)
        // We can't distinguish here without a full object catalog; for now fire when empty SQL refs
        return false;
    }


    private static bool IsCompletelyUnreferenced(string objectName, SignalIndex index)
        => index.IsCompletelyUnreferenced(objectName);

    private static bool IsCrypticName(string name)
    {
        // Cryptic = no conventional prefix (no underscore, no camelCase word boundary
        // visible) AND < 5 chars
        if (name.Length >= 5) return false;
        return !name.Contains('_') && !char.IsUpper(name[Math.Min(1, name.Length - 1)]);
    }
}
