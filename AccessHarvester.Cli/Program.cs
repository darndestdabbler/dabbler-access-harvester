using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core;
using AccessHarvester.Core.Abstractions;

[assembly: SupportedOSPlatform("windows")]

namespace AccessHarvester.Cli;

public static class Program
{
    private static readonly HashSet<string> FlagOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json-status"
    };

    private static readonly JsonSerializerOptions StatusJsonOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        try
        {
            return Dispatch(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Usage error: {ex.Message}");
            PrintUsage();
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    private static int Dispatch(string[] args)
    {
        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var rest = args.Skip(1).ToArray();
        return args[0].ToLowerInvariant() switch
        {
            "extract" => RunExtract(rest),
            "translate" => RunTranslate(rest),
            "run" => RunRun(rest),
            _ => UnknownCommand(args[0]),
        };
    }

    private static int RunExtract(string[] args)
    {
        var opts = ParseOptions(args);
        string db = Path.GetFullPath(Require(opts, "db"));
        string outDir = Path.GetFullPath(Require(opts, "out"));
        var pipeline = BuildPipeline(opts);

        var result = pipeline.Extract(db, outDir);

        Console.Error.WriteLine($"Extracted to: {result.RawDir}");
        ReportExtract(result.Categories);

        int exit = AnyFailures(result.Categories) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "extract",
                Database = db,
                OutputRoot = outDir,
                BaseDir = result.BaseDir,
                RawDir = result.RawDir,
                ExitCode = exit,
                Extract = SummarizeExtract(result.Categories),
            });
        }
        return exit;
    }

    private static int RunTranslate(string[] args)
    {
        var opts = ParseOptions(args);
        string rawDir = Path.GetFullPath(Require(opts, "in"));
        string kbDir = Path.GetFullPath(Require(opts, "out"));
        var pipeline = BuildPipeline(opts);

        var result = pipeline.Translate(rawDir, kbDir);

        Console.Error.WriteLine($"Translated to: {result.KbDir}");
        ReportTranslate(result.Categories);

        int exit = AnyFailures(result.Categories) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "translate",
                RawDir = rawDir,
                KbDir = kbDir,
                ExitCode = exit,
                Translate = SummarizeTranslate(result.Categories),
            });
        }
        return exit;
    }

    private static int RunRun(string[] args)
    {
        var opts = ParseOptions(args);
        string db = Path.GetFullPath(Require(opts, "db"));
        string outDir = Path.GetFullPath(Require(opts, "out"));
        var pipeline = BuildPipeline(opts);

        var result = pipeline.Run(db, outDir);

        Console.Error.WriteLine($"Raw: {result.RawDir}");
        Console.Error.WriteLine($"KB:  {result.KbDir}");
        Console.Error.WriteLine($"IR:  {result.IrDir}");
        ReportExtract(result.Extract);
        ReportTranslate(result.Translate);
        ReportEnrich(result.Enrich);

        int exit = (AnyFailures(result.Extract) || AnyFailures(result.Translate) || AnyFailures(result.Enrich)) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "run",
                Database = db,
                OutputRoot = outDir,
                BaseDir = result.BaseDir,
                RawDir = result.RawDir,
                KbDir = result.KbDir,
                IrDir = result.IrDir,
                ExitCode = exit,
                Extract = SummarizeExtract(result.Extract),
                Translate = SummarizeTranslate(result.Translate),
                Enrich = SummarizeEnrich(result.Enrich),
            });
        }
        return exit;
    }

    private static Pipeline BuildPipeline(Dictionary<string, string> opts)
    {
        var pipeline = Pipeline.Default();
        if (opts.TryGetValue("types", out var types))
        {
            var cats = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            pipeline = pipeline.WithCategories(cats);
        }
        return pipeline;
    }

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown subcommand: {cmd}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            AccessHarvester — extract Access DB metadata and translate into a JSON knowledge base.

            Usage:
              AccessHarvester extract   --db <file.accdb> --out <output-root-dir> [--types ...] [--json-status]
              AccessHarvester translate --in <raw-dir>    --out <kb-dir>          [--types ...] [--json-status]
              AccessHarvester run       --db <file.accdb> --out <output-root-dir> [--types ...] [--json-status]

            `run` is extract + translate + enrich in one invocation.
            Layout under <output-root-dir>: AccessMetaData/<dbName>/{raw,kb,ir}/<Category>/...

            --types         Comma-separated category filter. Valid categories:
                              Forms, Reports, Macros, ModulesAndClasses,
                              Queries, Tables, Relationships
                            Default: all.
            --json-status   Emit a JSON summary to stdout at end (human messages go to stderr).

            Exit codes:
              0  all objects processed successfully
              1  usage error
              2  unexpected error
              3  completed with per-object failures
            """);
    }

    private static bool IsHelpFlag(string arg)
        => arg is "-h" or "--help" or "/?" or "help";

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--"))
                throw new ArgumentException($"Unexpected positional argument: {token}");
            var key = token.Substring(2);
            if (FlagOptions.Contains(key))
            {
                opts[key] = "true";
            }
            else
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                    throw new ArgumentException($"Option --{key} requires a value.");
                opts[key] = args[++i];
            }
        }
        return opts;
    }

    private static string Require(Dictionary<string, string> opts, string key)
    {
        if (!opts.TryGetValue(key, out var value))
            throw new ArgumentException($"Missing required option: --{key}");
        return value;
    }

    private static bool HasJsonStatus(Dictionary<string, string> opts)
        => opts.ContainsKey("json-status");

    private static void EmitStatus(object status)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(status, StatusJsonOptions));
    }

    private static void ReportExtract(IReadOnlyList<CategoryExportResult> categories)
    {
        foreach (var cat in categories)
        {
            int ok = cat.Outcomes.Count(o => o.Success);
            int fail = cat.Outcomes.Count - ok;
            Console.Error.WriteLine($"  [extract] {cat.Category}: {ok} ok, {fail} failed");
            foreach (var f in cat.Outcomes.Where(o => !o.Success))
                Console.Error.WriteLine($"    [X] {f.Name}: {f.ErrorMessage}");
        }
    }

    private static void ReportTranslate(IReadOnlyList<CategoryTranslateResult> categories)
    {
        foreach (var cat in categories)
        {
            int ok = cat.Outcomes.Count(o => o.Success);
            int fail = cat.Outcomes.Count - ok;
            Console.Error.WriteLine($"  [translate] {cat.Category}: {ok} ok, {fail} failed");
            foreach (var f in cat.Outcomes.Where(o => !o.Success))
                Console.Error.WriteLine($"    [X] {f.Name}: {f.ErrorMessage}");
        }
    }

    private static void ReportEnrich(IReadOnlyList<CategoryEnrichResult> categories)
    {
        foreach (var cat in categories)
        {
            int ok = cat.Outcomes.Count(o => o.Success);
            int fail = cat.Outcomes.Count - ok;
            Console.Error.WriteLine($"  [enrich] {cat.Category}: {ok} ok, {fail} failed");
            foreach (var f in cat.Outcomes.Where(o => !o.Success))
                Console.Error.WriteLine($"    [X] {f.Name}: {f.ErrorMessage}");
        }
    }

    private static object SummarizeExtract(IReadOnlyList<CategoryExportResult> categories)
    {
        int ok = categories.Sum(c => c.Outcomes.Count(o => o.Success));
        int fail = categories.Sum(c => c.Outcomes.Count(o => !o.Success));
        var failures = categories
            .SelectMany(c => c.Outcomes
                .Where(o => !o.Success)
                .Select(o => new { Category = c.Category, Name = o.Name, Error = o.ErrorMessage }))
            .ToArray();
        var perCategory = categories.Select(c => new
        {
            Category = c.Category,
            Ok = c.Outcomes.Count(o => o.Success),
            Failed = c.Outcomes.Count(o => !o.Success),
        }).ToArray();
        return new { Ok = ok, Failed = fail, Categories = perCategory, Failures = failures };
    }

    private static object SummarizeTranslate(IReadOnlyList<CategoryTranslateResult> categories)
    {
        int ok = categories.Sum(c => c.Outcomes.Count(o => o.Success));
        int fail = categories.Sum(c => c.Outcomes.Count(o => !o.Success));
        var failures = categories
            .SelectMany(c => c.Outcomes
                .Where(o => !o.Success)
                .Select(o => new { Category = c.Category, Name = o.Name, Error = o.ErrorMessage }))
            .ToArray();
        var perCategory = categories.Select(c => new
        {
            Category = c.Category,
            Ok = c.Outcomes.Count(o => o.Success),
            Failed = c.Outcomes.Count(o => !o.Success),
        }).ToArray();
        return new { Ok = ok, Failed = fail, Categories = perCategory, Failures = failures };
    }

    private static object SummarizeEnrich(IReadOnlyList<CategoryEnrichResult> categories)
    {
        int ok = categories.Sum(c => c.Outcomes.Count(o => o.Success));
        int fail = categories.Sum(c => c.Outcomes.Count(o => !o.Success));
        var failures = categories
            .SelectMany(c => c.Outcomes
                .Where(o => !o.Success)
                .Select(o => new { Category = c.Category, Name = o.Name, Error = o.ErrorMessage }))
            .ToArray();
        var perCategory = categories.Select(c => new
        {
            Category = c.Category,
            Ok = c.Outcomes.Count(o => o.Success),
            Failed = c.Outcomes.Count(o => !o.Success),
        }).ToArray();
        return new { Ok = ok, Failed = fail, Categories = perCategory, Failures = failures };
    }

    private static bool AnyFailures(IReadOnlyList<CategoryExportResult> categories)
        => categories.Any(c => c.Outcomes.Any(o => !o.Success));

    private static bool AnyFailures(IReadOnlyList<CategoryTranslateResult> categories)
        => categories.Any(c => c.Outcomes.Any(o => !o.Success));

    private static bool AnyFailures(IReadOnlyList<CategoryEnrichResult> categories)
        => categories.Any(c => c.Outcomes.Any(o => !o.Success));
}
