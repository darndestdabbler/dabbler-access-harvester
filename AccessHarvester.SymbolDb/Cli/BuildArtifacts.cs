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

        // Collect table names used as bound_table_or_query in any form control,
        // so the table-lookup-score extractor can award the row-source bonus.
        var rowSourceTableNames = new HashSet<string>(
            formResult.Controls
                .Where(c => c.BoundTableOrQuery is not null)
                .Select(c => c.BoundTableOrQuery!),
            StringComparer.OrdinalIgnoreCase);

        // --- Session 3: Macros ---
        var macroPaths = Directory.GetFiles(Path.Combine(kbRoot, "Macros"), "*.json");
        var macroResult = MacroExtractor.Extract(macroPaths);

        // --- Session 3: Queries ---
        var queryPaths = Directory.GetFiles(Path.Combine(kbRoot, "Queries"), "*.json");
        var queries = QuerySchemaExtractor.Extract(queryPaths);

        // --- Session 3: Tables ---
        var tablePaths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");
        var tables = TableSchemaExtractor.Extract(tablePaths, rowSourceTableNames);

        string outDir = Path.Combine(repoRoot, "tools", "symbol-db");
        Directory.CreateDirectory(outDir);

        string dbPath = Path.Combine(outDir, "symbol-db.sqlite");
        var dedupReport = WriteDb(
            vbaResult, formResult, macroResult, queries, tables, dbPath);

        string reportPath = Path.Combine(outDir, "measurement-northwind.md");
        var report = new MeasurementReportBuilder().Build(
            vbaResult, dedupReport,
            "NorthwindDevEdition.accdb (Session 3 fixture: Form_frmCompanyDetail + modStrings + all macros/queries/tables)");
        File.WriteAllText(reportPath, report);

        Console.WriteLine($"Wrote {dbPath}");
        Console.WriteLine($"Wrote {reportPath}");
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
