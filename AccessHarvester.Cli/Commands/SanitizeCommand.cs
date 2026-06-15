using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core.Com;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Cli.Commands;

/// <summary>
/// Implements the <c>sanitize</c> subcommand.
///
/// Usage:
///   AccessHarvester sanitize --classification &lt;classification.json&gt;
///                            --in            &lt;file.accdb&gt;
///                            --out           &lt;output-dir&gt;
///                            [--json-status]
///
/// Writes per-table CSV files plus a <c>sanitization-report.json</c> under
/// <c>&lt;output-dir&gt;/&lt;database-slug&gt;/</c>. The CSV format choice is
/// documented in <c>docs/session-sets/harvester-data-classifier/spec.md</c>
/// Session 4 actuals: CSV is the safe default per the spec's FK-remap
/// stop rule, removes the need for fragile DAO transactional rewrites of
/// the source <c>.accdb</c>, and loads cleanly into both Access (link to
/// text) and SQL Server (BULK INSERT).
///
/// Exit codes:
///   0 success
///   1 usage error
///   2 unexpected error
///   3 partial success (missing FK targets recorded in the report).
/// </summary>
[SupportedOSPlatform("windows")]
public static class SanitizeCommand
{
    private static readonly JsonSerializerOptions _statusOpts = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        Dictionary<string, string> opts;
        try { opts = ParseOptions(args); }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"sanitize: {ex.Message}");
            PrintUsage();
            return 1;
        }

        if (!opts.TryGetValue("classification", out var classPath))
        {
            Console.Error.WriteLine("sanitize: --classification <classification.json> is required.");
            PrintUsage();
            return 1;
        }
        if (!opts.TryGetValue("in", out var inPath))
        {
            Console.Error.WriteLine("sanitize: --in <file.accdb> is required.");
            PrintUsage();
            return 1;
        }
        if (!opts.TryGetValue("out", out var outDir))
        {
            Console.Error.WriteLine("sanitize: --out <output-dir> is required.");
            PrintUsage();
            return 1;
        }

        string classFull = Path.GetFullPath(classPath);
        if (!File.Exists(classFull))
        {
            Console.Error.WriteLine($"sanitize: classification not found: {classFull}");
            return 1;
        }

        string dbFull = Path.GetFullPath(inPath);
        if (!File.Exists(dbFull))
        {
            Console.Error.WriteLine($"sanitize: database not found: {dbFull}");
            return 1;
        }

        string outRoot = Path.GetFullPath(outDir);
        string slug    = SlugifyDbName(Path.GetFileName(dbFull));
        string outSubdir = Path.Combine(outRoot, slug);

        try
        {
            var rowSource = new OleDbRowSource(dbFull);
            var result = SanitizationRunner.Run(
                classificationPath: classFull,
                outDirectory:       outSubdir,
                rowSource:          rowSource,
                sanitizationEpoch:  DateTimeOffset.UtcNow);

            Console.Error.WriteLine($"[sanitize] {result.Report.Tables.Count} table(s) sanitized.");
            Console.Error.WriteLine($"[sanitize] Output: {outSubdir}");
            Console.Error.WriteLine($"[sanitize] Report: {result.ReportPath}");
            if (result.Report.MissingFkTargets.Count > 0)
            {
                Console.Error.WriteLine(
                    $"[sanitize] {result.Report.MissingFkTargets.Count} FK target(s) could not be resolved; treated as preserve.");
                foreach (var m in result.Report.MissingFkTargets.Take(20))
                    Console.Error.WriteLine($"    {m.Table}.{m.Column}");
            }

            int exit = result.Report.MissingFkTargets.Count > 0 ? 3 : 0;
            if (opts.ContainsKey("json-status"))
            {
                EmitStatus(new
                {
                    Command           = "sanitize",
                    Classification    = classFull,
                    Database          = dbFull,
                    OutputDirectory   = outSubdir,
                    ReportFile        = result.ReportPath,
                    TablesSanitized   = result.Report.Tables.Count,
                    TotalRowsWritten  = result.Report.Tables.Sum(t => t.RowsWritten),
                    MissingFkTargets  = result.Report.MissingFkTargets
                        .Select(m => new { Table = m.Table, Column = m.Column }).ToArray(),
                    ExitCode          = exit,
                });
            }
            return exit;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"sanitize: {ex.Message}");
            return 1;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"sanitize: classification document is invalid: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"sanitize: unexpected error: {ex.Message}");
            return 2;
        }
    }

    private static void EmitStatus(object status)
        => Console.Out.WriteLine(JsonSerializer.Serialize(status, _statusOpts));

    private static string SlugifyDbName(string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        return System.Text.RegularExpressions.Regex
            .Replace(baseName, @"[^\w-]", "-")
            .ToLowerInvariant();
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage:
              AccessHarvester sanitize --classification <classification.json>
                                       --in            <file.accdb>
                                       --out           <output-dir>
                                       [--json-status]

            --classification  Path to the reviewed classification.json (schema v1.0).
            --in              Path to the source Access database (.accdb / .mdb).
            --out             Output directory; per-table CSVs and a
                              sanitization-report.json are written to
                              <out>/<database-slug>/.
            --json-status     Emit a JSON summary to stdout (human text on stderr).
            """);
    }

    // 006 O-1-4: route through the shared OptionParser so an unknown --switch is
    // an honest usage error. ValueOptions are the keys this command reads
    // (--in, --out, --classification); --json-status is the sole flag.
    private static readonly string[] ValueOptions = ["in", "out", "classification"];
    private static readonly string[] FlagOptions = ["json-status"];

    private static Dictionary<string, string> ParseOptions(string[] args)
        => OptionParser.Parse(args, ValueOptions, FlagOptions);
}
