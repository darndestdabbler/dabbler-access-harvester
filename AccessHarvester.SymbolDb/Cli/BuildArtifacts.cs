using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.Models;
using AccessHarvester.SymbolDb.Persistence;
using AccessHarvester.SymbolDb.Reporting;
using AccessHarvester.SymbolDb.SourceLoading;

namespace AccessHarvester.SymbolDb.Cli;

// Static entry-point that drives the full symbol-db build pipeline through
// Session 3 (FormControls, EventBindings, Macros, Queries, Tables) against
// the canonical NorthwindDevEdition fixture and writes:
//   * tools/symbol-db/symbol-db.sqlite       — checked-in build artifact
//   * tools/symbol-db/measurement-northwind.md — measurement report
public static class BuildArtifacts
{
    public static int RunForNorthwind(string repoRoot)
    {
        return RunForNorthwind(repoRoot,
            Path.Combine(repoRoot, "tools", "symbol-db", "symbol-db.sqlite"),
            writeMeasurementReport: true,
            reportPath: Path.Combine(repoRoot, "tools", "symbol-db", "measurement-northwind.md"));
    }

    public static int RunForNorthwind(
        string repoRoot,
        string dbPath,
        bool writeMeasurementReport,
        string? reportPath = null)
    {
        if (!Directory.Exists(repoRoot))
            throw new DirectoryNotFoundException(repoRoot);

        string kbRoot = Path.Combine(repoRoot,
            "tmp", "comprehension-ir", "AccessMetaData", "NorthwindDevEdition", "kb");

        // --- VBA symbols (Sessions 1 + 2) ---
        var rawModules = new[]
        {
            KbVbaSourceLoader.FromKbJson(
                Path.Combine(kbRoot, "Forms", "frmCompanyDetail.json"),
                "Form_frmCompanyDetail",
                "Form")
                ?? throw new InvalidOperationException("frmCompanyDetail.json has no VbaCode."),
            KbVbaSourceLoader.FromKbJson(
                Path.Combine(kbRoot, "ModulesAndClasses", "modStrings.json"),
                "modStrings",
                "Module")
                ?? throw new InvalidOperationException("modStrings.json has no VbaCode."),
        };
        var vbaResult = new SymbolDbBuilder().Build(rawModules);

        // --- Session 3: FormControls + EventBindings ---
        var formResult = FormControlsExtractor.Extract(
            Path.Combine(kbRoot, "Forms", "frmCompanyDetail.json"),
            "Form_frmCompanyDetail");

        // Count, per table or query name, how many form controls reference it
        // as a row source or bound table. The TableSchemaExtractor uses this
        // for the count-tiered is_lookup_table_score bonus, and the union of
        // names here also feeds the query-deadness analysis below.
        var rowSourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in formResult.Controls)
        {
            if (c.RowSource is not null)
                rowSourceCounts[c.RowSource] = rowSourceCounts.GetValueOrDefault(c.RowSource) + 1;
            if (c.BoundTableOrQuery is not null)
                rowSourceCounts[c.BoundTableOrQuery] = rowSourceCounts.GetValueOrDefault(c.BoundTableOrQuery) + 1;
        }

        // --- Session 3: Macros ---
        var macroPaths = Directory.GetFiles(Path.Combine(kbRoot, "Macros"), "*.json");
        var macroResult = MacroExtractor.Extract(macroPaths);

        // --- Session 3: Queries (with deadness analysis) ---
        // A query is alive if any of these reference its Fqn:
        //   * form-control RowSource or BoundTableOrQuery
        //   * a VBA reference identifier (literal-string DLookup et al. cases
        //     that survived as identifiers in Session 2's reference graph)
        //   * a macro OpenQuery / RunSQL action argument
        var callerNames = new HashSet<string>(rowSourceCounts.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var r in vbaResult.References)
        {
            if (!string.IsNullOrEmpty(r.Identifier)) callerNames.Add(r.Identifier);
        }
        foreach (var a in macroResult.MacroActions)
        {
            // Cheap: any non-null arguments_json with the action's quoted
            // value is enough to keep a referenced query alive. We extract
            // potential query names from common macro-action arg shapes.
            if (a.ActionName is "OpenQuery" or "RunSQL" or "DeleteObject" && a.ArgumentsJson is not null)
            {
                foreach (var token in a.ArgumentsJson.Split(['"', ',', ':', '{', '}', '[', ']']))
                {
                    string trimmed = token.Trim();
                    if (trimmed.Length > 0) callerNames.Add(trimmed);
                }
            }
        }

        var queryPaths = Directory.GetFiles(Path.Combine(kbRoot, "Queries"), "*.json");
        var queries = QuerySchemaExtractor.Extract(queryPaths, callerNames);

        // --- Session 3: Tables ---
        var tablePaths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");
        var tables = TableSchemaExtractor.Extract(tablePaths, rowSourceCounts);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)
            ?? throw new InvalidOperationException($"Cannot determine directory for {dbPath}."));
        var dedupReport = WriteDb(
            vbaResult, formResult, macroResult, queries, tables, dbPath);

        Console.WriteLine($"Wrote {dbPath}");

        if (writeMeasurementReport)
        {
            string effectiveReportPath = reportPath
                ?? Path.Combine(repoRoot, "tools", "symbol-db", "measurement-northwind.md");
            Directory.CreateDirectory(Path.GetDirectoryName(effectiveReportPath)
                ?? throw new InvalidOperationException(
                    $"Cannot determine directory for {effectiveReportPath}."));
            var report = new MeasurementReportBuilder().Build(
                vbaResult, dedupReport,
                "NorthwindDevEdition.accdb (Session 3 fixture: Form_frmCompanyDetail + modStrings + all macros/queries/tables)");
            File.WriteAllText(effectiveReportPath, report);
            Console.WriteLine($"Wrote {effectiveReportPath}");
        }

        Console.WriteLine(
            $"Modules={vbaResult.Statistics.ModuleCount} " +
            $"Symbols={vbaResult.Statistics.SymbolCount} " +
            $"Dead={vbaResult.Statistics.DeadSymbolCount} " +
            $"Twins={dedupReport.SiblingsTagged} " +
            $"References={vbaResult.References.Count} " +
            $"MemberAccesses={vbaResult.MemberAccesses.Count} " +
            $"FormControls={formResult.Controls.Count} " +
            $"EventBindings={formResult.Bindings.Count} " +
            $"Macros={macroResult.Macros.Count} " +
            $"MacroActions={macroResult.MacroActions.Count} " +
            $"Queries={queries.Count} " +
            $"Tables={tables.Count}");
        return 0;
    }

    private static Md5DedupReport WriteDb(
        BuildResult vbaResult,
        FormControlsExtractor.Result formResult,
        MacroExtractor.Result macroResult,
        IReadOnlyList<QueryRow> queries,
        IReadOnlyList<TableRow> tables,
        string dbPath)
    {
        using var writer = new SymbolDbWriter(dbPath, overwrite: true);
        writer.Write(
            vbaResult.Modules,
            vbaResult.Symbols,
            vbaResult.Parameters,
            vbaResult.References,
            vbaResult.MemberAccesses,
            formControls: formResult.Controls,
            eventBindings: formResult.Bindings,
            macros: macroResult.Macros,
            macroActions: macroResult.MacroActions,
            queries: queries,
            tables: tables);
        var dedup = new Md5Deduper();
        return dedup.Run(writer.Connection);
    }
}
