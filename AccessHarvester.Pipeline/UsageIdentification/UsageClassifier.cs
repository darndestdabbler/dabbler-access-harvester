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
    // QueryTypeCode constants — values from QueryExporter.QueryTypeName() / DAO dbQueryType enum.
    private const int TypeSelect      = 0;
    private const int TypeMakeTable   = 16;
    private const int TypeAppend      = 32;
    private const int TypeUpdate      = 48;
    private const int TypeDelete      = 64;
    private const int TypeUnion       = 80;
    private const int TypeCrosstab    = 112;
    private const int TypeDataDef     = 128;
    private const int TypePassThrough = 144;

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

        string category;
        double confidence;
        var evidence = new List<string>();

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
                            hasVbaCode, hasCrudHandlers, null, index);
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

        if (tc == TypeCrosstab)
        {
            // Q1
            category   = UsageCategory.Reporting;
            confidence = 0.70;
            evidence.Add("Crosstab");
        }
        else if (tc is TypeDelete or TypeUpdate or TypeAppend or TypeMakeTable)
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
        else if (tc is TypeDataDef or TypePassThrough)
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

        return BuildFinding("Query", query.Name, category, confidence, evidence,
                            "Query", null, false,
                            index.NavigationSources.Contains(query.Name),
                            openedByNav,
                            false, false, query.QueryTypeCode, index);
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
                            vbaReferenced, false, null, index);
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
        string category = hasNavEdges ? UsageCategory.NavigationShell : UsageCategory.StandaloneUtility;
        double confidence = hasNavEdges ? 0.75 : unreferenced ? 0.60 : 0.55;
        string summary = hasNavEdges
            ? $"Macro '{macro.Name}' containing navigation actions that open forms or reports"
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
        SignalIndex index)
    {
        string summary = BuildSummary(objectType, objectName, category, recordSource,
                                      queryTypeCode, index);
        var signals = BuildSignals(objectName, objectType, recordSource, usedAsRecordSource,
                                   isNavSource, isNavTarget, hasVbaCode, queryTypeCode, index);
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
                                        string? recordSource, int? queryTypeCode, SignalIndex index)
    {
        return category switch
        {
            UsageCategory.MainDataEntry   => recordSource != null
                ? $"Main data entry {objectType.ToLowerInvariant()} backed by {recordSource}"
                : $"Main data entry {objectType.ToLowerInvariant()}",
            UsageCategory.LookupReference => $"Lookup/reference {objectType.ToLowerInvariant()} '{name}'",
            UsageCategory.Reporting       => queryTypeCode == TypeCrosstab
                ? $"Crosstab query '{name}' used for reporting or data analysis"
                : recordSource != null
                    ? $"Report backed by {recordSource}"
                    : $"Reporting {objectType.ToLowerInvariant()} '{name}'",
            UsageCategory.NavigationShell => $"Navigation/shell form '{name}' that opens other forms or reports",
            UsageCategory.StandaloneUtility => $"Standalone utility {objectType.ToLowerInvariant()} '{name}' not referenced by any form or report",
            UsageCategory.PossiblyUnused  => $"{objectType} '{name}' is not referenced by any form, report, query, VBA, or macro",
            _                             => $"{objectType} '{name}' — insufficient signals to determine usage",
        };
    }

    private static IReadOnlyList<SignalEntry> BuildSignals(
        string objectName, string objectType,
        string? recordSource, bool usedAsRecordSource,
        bool isNavSource, bool isNavTarget,
        bool hasVbaCode, int? queryTypeCode, SignalIndex index)
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

        return signals;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static bool IsExcluded(string name, IReadOnlySet<string>? excluded)
        => excluded != null && excluded.Contains(name);

    private static bool IsSystemTable(string name)
        => name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("USys", StringComparison.OrdinalIgnoreCase);

    private static bool IsLookupSource(string source, SignalIndex index)
        => index.IsLookupTable(source);

    private static bool IsActionQueryType(int tc)
        => tc is TypeDelete or TypeUpdate or TypeAppend or TypeMakeTable;

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
