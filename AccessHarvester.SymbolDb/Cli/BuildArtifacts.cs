using System;
using System.IO;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.Persistence;
using AccessHarvester.SymbolDb.Reporting;
using AccessHarvester.SymbolDb.SourceLoading;

namespace AccessHarvester.SymbolDb.Cli;

// Static entry-point used by the Session 1 build artifact step. Drives the
// extractor against the canonical NorthwindDevEdition fixture and writes:
//   * tools/symbol-db/symbol-db.sqlite       — checked-in build artifact
//   * tools/symbol-db/measurement-northwind.md — Session 1 measurement report
//
// Exposed as a static method (not a Main) so the orchestrator can call it
// from a tiny dotnet-script harness without pulling in a CLI framework.
public static class BuildArtifacts
{
    public static int RunForNorthwind(string repoRoot)
    {
        if (!Directory.Exists(repoRoot))
            throw new DirectoryNotFoundException(repoRoot);

        string kbRoot = Path.Combine(repoRoot,
            "tmp", "comprehension-ir", "AccessMetaData", "NorthwindDevEdition", "kb");
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

        var result = new SymbolDbBuilder().Build(rawModules);

        string outDir = Path.Combine(repoRoot, "tools", "symbol-db");
        Directory.CreateDirectory(outDir);

        string dbPath = Path.Combine(outDir, "symbol-db.sqlite");
        var dedupReport = WriteDb(result, dbPath);

        string reportPath = Path.Combine(outDir, "measurement-northwind.md");
        var report = new MeasurementReportBuilder().Build(result, dedupReport, "NorthwindDevEdition.accdb (Session 1 fixture: Form_frmCompanyDetail + modStrings)");
        File.WriteAllText(reportPath, report);

        Console.WriteLine($"Wrote {dbPath}");
        Console.WriteLine($"Wrote {reportPath}");
        Console.WriteLine($"Modules={result.Statistics.ModuleCount} Symbols={result.Statistics.SymbolCount} Dead={result.Statistics.DeadSymbolCount} Twins={dedupReport.SiblingsTagged} References={result.References.Count} MemberAccesses={result.MemberAccesses.Count}");
        return 0;
    }

    private static Md5DedupReport WriteDb(BuildResult result, string dbPath)
    {
        using var writer = new SymbolDbWriter(dbPath, overwrite: true);
        writer.Write(result.Modules, result.Symbols, result.Parameters,
            result.References, result.MemberAccesses);
        var dedup = new Md5Deduper();
        return dedup.Run(writer.Connection);
    }
}
