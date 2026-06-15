using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using AccessHarvester.Core.Com;
using AccessHarvester.Pipeline.Classification;
// `Linq` is required for the .Take / .Select calls when projecting
// degradations into the --json-status envelope.


namespace AccessHarvester.Cli.Commands;

/// <summary>
/// Implements the <c>classify</c> subcommand.
///
/// Usage:
///   AccessHarvester classify --in &lt;file.accdb&gt; --out &lt;dir&gt;
///                            [--exclude-cleanup &lt;path&gt;]
///                            [--json-status]
///
/// Writes:
///   &lt;dir&gt;/&lt;database-slug&gt;/classification.json
///
/// Exit codes: 0 success, 1 usage error, 2 unexpected error,
///             3 partial-failure (degradations recorded in JSON status).
/// </summary>
[SupportedOSPlatform("windows")]
public static class ClassifyCommand
{
    private static readonly JsonSerializerOptions _statusOpts = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        Dictionary<string, string> opts;
        try { opts = ParseOptions(args); }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"classify: {ex.Message}");
            PrintUsage();
            return 1;
        }

        if (!opts.TryGetValue("in", out var inPath))
        {
            Console.Error.WriteLine("classify: --in <file.accdb> is required.");
            PrintUsage();
            return 1;
        }
        if (!opts.TryGetValue("out", out var outDir))
        {
            Console.Error.WriteLine("classify: --out <output-dir> is required.");
            PrintUsage();
            return 1;
        }

        string dbPath = Path.GetFullPath(inPath);
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"classify: database not found: {dbPath}");
            return 1;
        }

        string outRoot = Path.GetFullPath(outDir);
        string slug    = SlugifyDbName(Path.GetFileName(dbPath));
        string outSubdir = Path.Combine(outRoot, slug);
        Directory.CreateDirectory(outSubdir);
        string outFile = Path.Combine(outSubdir, "classification.json");

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (opts.TryGetValue("exclude-cleanup", out var cleanupPath))
        {
            string cleanupFull = Path.GetFullPath(cleanupPath);
            if (!File.Exists(cleanupFull))
            {
                Console.Error.WriteLine($"classify: cleanup checklist not found: {cleanupFull}");
                return 1;
            }
            LoadExclusions(cleanupFull, excluded);
        }

        try
        {
            var schemaReader = new OleDbDaoSchemaReader();
            var sampler      = new OleDbValueSampler();
            var config       = ScanConfig.Default();

            var result = ClassificationRunner.Run(
                databasePath: dbPath,
                schemaReader: schemaReader,
                valueSampler: sampler,
                config:       config,
                excludedTableNames: excluded.Count > 0 ? excluded : null,
                scannedUtc:   DateTimeOffset.UtcNow);

            File.WriteAllText(outFile, result.Json, new UTF8Encoding(false));
            Console.Error.WriteLine($"[classify] {result.Findings.Tables.Count} tables classified.");
            Console.Error.WriteLine($"[classify] Wrote: {outFile}");
            if (result.ExcludedTables.Count > 0)
                Console.Error.WriteLine($"[classify] Excluded {result.ExcludedTables.Count} table(s) via --exclude-cleanup.");
            if (result.Degradations.Count > 0)
            {
                Console.Error.WriteLine($"[classify] {result.Degradations.Count} degradation(s) recorded:");
                foreach (var d in result.Degradations.Take(20))
                    Console.Error.WriteLine($"    {d.Kind} [{d.Location}] {d.Detail}");
                if (result.Degradations.Count > 20)
                    Console.Error.WriteLine($"    ... and {result.Degradations.Count - 20} more (see --json-status for the full list).");
            }

            int exit = result.Degradations.Count > 0 ? 3 : 0;
            if (opts.ContainsKey("json-status"))
            {
                EmitStatus(new
                {
                    Command          = "classify",
                    Database         = dbPath,
                    OutputFile       = outFile,
                    TablesClassified = result.Findings.Tables.Count,
                    ExcludedTables   = result.ExcludedTables,
                    Degradations     = result.Degradations.Select(d => new
                    {
                        Kind     = d.Kind.ToString(),
                        d.Location,
                        d.Detail,
                    }).ToArray(),
                    ExitCode         = exit,
                });
            }
            return exit;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"classify: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"classify: unexpected error: {ex.Message}");
            return 2;
        }
    }

    private static void LoadExclusions(string cleanupPath, HashSet<string> excluded)
    {
        // Same uat-checklist-editor format as identify-usage:
        // { "Review": [ { "Subarea": "<table-name>", "Passes": true } ] }
        // A Passes=true entry means the human has confirmed the object is
        // safe to exclude from downstream classification work.
        string json = File.ReadAllText(cleanupPath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("Review", out var reviews)
            || reviews.ValueKind != JsonValueKind.Array)
            return;

        foreach (var review in reviews.EnumerateArray())
        {
            if (review.TryGetProperty("Passes", out var passes)
                && passes.ValueKind == JsonValueKind.True
                && review.TryGetProperty("Subarea", out var subarea)
                && subarea.ValueKind == JsonValueKind.String)
            {
                string? name = subarea.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    excluded.Add(name);
            }
        }
    }

    private static string SlugifyDbName(string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        return System.Text.RegularExpressions.Regex
            .Replace(baseName, @"[^\w-]", "-")
            .ToLowerInvariant();
    }

    private static void EmitStatus(object status)
        => Console.Out.WriteLine(JsonSerializer.Serialize(status, _statusOpts));

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage:
              AccessHarvester classify --in <file.accdb> --out <output-dir>
                                       [--exclude-cleanup <path>] [--json-status]

            --in              Path to the Access database (.accdb / .mdb).
            --out             Output directory; classification.json is written to
                              <out>/<database-slug>/classification.json.
            --exclude-cleanup Path to an object-cleanup-checklist.json from
                              identify-cleanup. Tables with Passes=true are
                              skipped (per the harvester-object-cleanup-identifier
                              session-set contract).
            --json-status     Emit a JSON summary to stdout (human text on stderr).
            """);
    }

    // 006 O-1-4: route through the shared OptionParser so an unknown --switch is
    // an honest usage error. ValueOptions are the keys this command reads
    // (--in, --out, --exclude-cleanup); --json-status is the sole flag.
    private static readonly string[] ValueOptions = ["in", "out", "exclude-cleanup"];
    private static readonly string[] FlagOptions = ["json-status"];

    private static Dictionary<string, string> ParseOptions(string[] args)
        => OptionParser.Parse(args, ValueOptions, FlagOptions);
}
