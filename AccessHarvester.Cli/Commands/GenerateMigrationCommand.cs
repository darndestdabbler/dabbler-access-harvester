using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Pipeline.Migration;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Cli.Commands;

/// <summary>
/// Implements the <c>generate-migration</c> subcommand — the classify -&gt;
/// sanitize -&gt; migrate pipeline's final, runnable product.
///
/// <para>
/// Emits a deterministic SQL Server migration from the harvester's already-captured
/// artifacts: the harvested <c>ir/Tables</c> + <c>ir/Relationships</c> tiers
/// (schema), the optional reviewed <c>classification.json</c> (multi-value /
/// preserve decisions), and the optional <c>sanitize</c> CSV-per-table output
/// (the masked / preserved row values). Output under
/// <c>&lt;out&gt;/&lt;database-slug&gt;/</c>:
///   <list type="bullet">
///     <item><c>schema.sql</c> — <c>CREATE TABLE</c> (types, nullability, defaults,
///       PK) + <c>FOREIGN KEY</c> constraints.</item>
///     <item><c>data.sql</c> — FK-safe <c>INSERT</c> data-load (parents before
///       children) with <c>SET IDENTITY_INSERT</c> framing where required. Run
///       <c>schema.sql</c> first, then <c>data.sql</c>.</item>
///     <item><c>migration-manifest.json</c> — what was emitted plus every honestly
///       flagged degradation (OLE / multi-value / linked-table / missing-data /
///       cycle), never silently dropped or fabricated.</item>
///   </list>
/// </para>
///
/// <para>
/// This subcommand is COM-free: it reads only the already-harvested JSON, the
/// classification, and the sanitized CSVs, so it runs without Microsoft Access.
/// </para>
///
/// Usage:
///   AccessHarvester generate-migration --in   &lt;harvested-output-root&gt;
///                                      --out  &lt;output-dir&gt;
///                                      [--classification &lt;classification.json&gt;]
///                                      [--data &lt;sanitized-csv-dir&gt;]
///                                      [--json-status]
///
/// Exit codes:
///   0 success — a clean migration with no flagged degradations
///   1 usage error (missing/invalid args, input not found, invalid classification)
///   2 unexpected error
///   3 completed with flagged degradations (review migration-manifest.json)
/// </summary>
public static class GenerateMigrationCommand
{
    private static readonly JsonSerializerOptions _statusOpts = new() { WriteIndented = true };
    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private const string SchemaFileName = "schema.sql";
    private const string DataFileName = "data.sql";
    private const string ManifestFileName = "migration-manifest.json";

    public static int Run(string[] args)
    {
        Dictionary<string, string> opts;
        try { opts = ParseOptions(args); }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"generate-migration: {ex.Message}");
            PrintUsage();
            return 1;
        }

        if (!opts.TryGetValue("in", out var inPath))
        {
            Console.Error.WriteLine("generate-migration: --in <harvested-output-root> is required.");
            PrintUsage();
            return 1;
        }
        if (!opts.TryGetValue("out", out var outDir))
        {
            Console.Error.WriteLine("generate-migration: --out <output-dir> is required.");
            PrintUsage();
            return 1;
        }

        string inRoot = Path.GetFullPath(inPath);
        if (!Directory.Exists(inRoot))
        {
            Console.Error.WriteLine($"generate-migration: input directory not found: {inRoot}");
            return 1;
        }

        string? classFull = null;
        if (opts.TryGetValue("classification", out var classPath))
        {
            classFull = Path.GetFullPath(classPath);
            if (!File.Exists(classFull))
            {
                Console.Error.WriteLine($"generate-migration: classification not found: {classFull}");
                return 1;
            }
        }

        string? dataDir = null;
        if (opts.TryGetValue("data", out var dataPath))
        {
            dataDir = Path.GetFullPath(dataPath);
            if (!Directory.Exists(dataDir))
            {
                Console.Error.WriteLine($"generate-migration: sanitized data directory not found: {dataDir}");
                return 1;
            }
        }

        try
        {
            ClassificationDocument? classification = null;
            if (classFull is not null)
            {
                // Tight catch scoped to the classification parse so the diagnostic is
                // truthful — a malformed IR or sanitized CSV must not be reported as a
                // classification error. Other read failures (I/O, sharing violation, the
                // file vanishing after the File.Exists check) fall through to the
                // command-level handlers below for a controlled exit code + message.
                try { classification = ClassificationReader.ReadFile(classFull); }
                catch (InvalidDataException ex)
                {
                    Console.Error.WriteLine($"generate-migration: classification document is invalid: {ex.Message}");
                    return 1;
                }
            }

            var inputs = MigrationIrReader.Read(inRoot, classification, dataDir);
            var result = SqlServerMigrationGenerator.Generate(inputs, OpenTableCsv(dataDir));

            string outRoot   = Path.GetFullPath(outDir);
            string slug      = SlugifyDbName(inputs.Database);
            string outSubdir = Path.Combine(outRoot, slug);
            Directory.CreateDirectory(outSubdir);

            string schemaPath = Path.Combine(outSubdir, SchemaFileName);
            string dataPathOut = Path.Combine(outSubdir, DataFileName);
            string manifestPath = Path.Combine(outSubdir, ManifestFileName);

            File.WriteAllText(schemaPath, result.DdlSql, _utf8NoBom);
            File.WriteAllText(dataPathOut, result.DataSql, _utf8NoBom);
            File.WriteAllText(manifestPath, BuildManifestJson(inputs.Database, result), _utf8NoBom);

            int exit = result.Degradations.Count > 0 ? 3 : 0;

            Console.Error.WriteLine($"[generate-migration] {result.TableLoadOrder.Count} table(s) ordered for load.");
            Console.Error.WriteLine($"[generate-migration] Schema:   {schemaPath}");
            Console.Error.WriteLine($"[generate-migration] Data:     {dataPathOut}");
            Console.Error.WriteLine($"[generate-migration] Manifest: {manifestPath}");
            if (result.Degradations.Count > 0)
            {
                Console.Error.WriteLine(
                    $"[generate-migration] {result.Degradations.Count} degradation(s) flagged (see {ManifestFileName}):");
                foreach (var d in result.Degradations.Take(20))
                    Console.Error.WriteLine($"    [{d.Kind}] {d.Table}{(d.Column is null ? "" : "." + d.Column)}: {d.Reason}");
                if (result.Degradations.Count > 20)
                    Console.Error.WriteLine($"    ... and {result.Degradations.Count - 20} more.");
            }

            if (opts.ContainsKey("json-status"))
            {
                EmitStatus(new
                {
                    Command          = "generate-migration",
                    HarvesterVersion = DatabaseManifest.ResolveHarvesterVersion(),
                    Database         = inputs.Database,
                    InputRoot        = inRoot,
                    Classification   = classFull,
                    SanitizedDataDir = dataDir,
                    OutputDirectory  = outSubdir,
                    SchemaFile       = schemaPath,
                    DataFile         = dataPathOut,
                    ManifestFile     = manifestPath,
                    TablesOrdered    = result.TableLoadOrder.Count,
                    DegradationCount = result.Degradations.Count,
                    ExitCode         = exit,
                });
            }
            return exit;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"generate-migration: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"generate-migration: unexpected error: {ex.Message}");
            return 2;
        }
    }

    /// <summary>
    /// Returns the data-load CSV opener. When no <c>--data</c> directory was
    /// supplied, every table reports a missing data file (DDL still stands); the
    /// generator flags that honestly rather than fabricating rows. The generator
    /// owns disposal of the returned reader.
    /// </summary>
    private static Func<string, TextReader?> OpenTableCsv(string? dataDir) => tableName =>
    {
        if (dataDir is null) return null;
        string path = Path.Combine(dataDir, tableName + SanitizeCsvContract.FileExtension);
        return File.Exists(path) ? new StreamReader(path) : null;
    };

    private static string BuildManifestJson(string database, MigrationScriptResult result)
    {
        var manifest = new
        {
            SchemaType       = "MigrationManifest",
            HarvesterVersion = DatabaseManifest.ResolveHarvesterVersion(),
            Database         = database,
            GeneratedFiles   = new { Schema = SchemaFileName, Data = DataFileName },
            TableLoadOrder   = result.TableLoadOrder,
            DegradationCount = result.Degradations.Count,
            Degradations     = result.Degradations.Select(d => new
            {
                Kind   = d.Kind.ToString(),
                Table  = d.Table,
                Column = d.Column,
                Reason = d.Reason,
            }).ToArray(),
        };
        return JsonSerializer.Serialize(manifest, _statusOpts);
    }

    private static void EmitStatus(object status)
        => Console.Out.WriteLine(JsonSerializer.Serialize(status, _statusOpts));

    private static string SlugifyDbName(string databaseName)
    {
        string baseName = Path.GetFileNameWithoutExtension(databaseName);
        return System.Text.RegularExpressions.Regex
            .Replace(baseName, @"[^\w-]", "-")
            .ToLowerInvariant();
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage:
              AccessHarvester generate-migration --in   <harvested-output-root>
                                                 --out  <output-dir>
                                                 [--classification <classification.json>]
                                                 [--data <sanitized-csv-dir>]
                                                 [--json-status]

            --in              Harvested output root (the directory containing ir/, e.g. a
                              `run` --out root's AccessMetaData/<dbName>/ — or the ir/
                              directory itself). ir/Tables + ir/Relationships are the schema source.
            --out             Output directory; schema.sql, data.sql, and a
                              migration-manifest.json are written to <out>/<database-slug>/.
            --classification  Reviewed classification.json (schema v1.0). Supplies multi-value /
                              preserve decisions; optional but recommended.
            --data            The `sanitize` subcommand's CSV-per-table output directory
                              (<sanitize-out>/<database-slug>/). Optional: when omitted, schema.sql
                              is still emitted and every table is flagged as missing data.
            --json-status     Emit a JSON summary to stdout (human text on stderr).

            Run schema.sql first, then data.sql. migration-manifest.json indexes the emitted
            files, the FK-safe table load order, and every flagged degradation.

            Exit codes:
              0  clean migration (no degradations flagged)
              1  usage error (bad args, input not found, invalid classification)
              2  unexpected error
              3  completed with flagged degradations (review migration-manifest.json)
            """);
    }

    private static readonly string[] ValueOptions = ["in", "out", "classification", "data"];
    private static readonly string[] FlagOptions = ["json-status"];

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected positional argument: {token}");
            string key = token[2..];
            bool isFlag = Array.Exists(FlagOptions, f => string.Equals(f, key, StringComparison.OrdinalIgnoreCase));
            bool isValue = Array.Exists(ValueOptions, f => string.Equals(f, key, StringComparison.OrdinalIgnoreCase));
            // Reject unknown switches rather than silently ignoring them: a typo
            // (e.g. --datta) must surface as a usage error, never quietly degrade
            // the migration (a mistyped --data would otherwise flag every table as
            // missing data and still "succeed" with exit 3).
            if (!isFlag && !isValue)
                throw new ArgumentException($"Unknown option: --{key}");
            if (isFlag)
            {
                opts[key] = "true";
            }
            else
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Option --{key} requires a value.");
                opts[key] = args[++i];
            }
        }
        return opts;
    }
}
