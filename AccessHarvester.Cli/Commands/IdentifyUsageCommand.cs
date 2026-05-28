using System.Text.Json;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Cli.Commands;

/// <summary>
/// Implements the <c>identify-usage</c> subcommand.
/// Reads KB/IR output from a harvested Access database and produces:
/// - <c>object-use.json</c> — per-object usage classification findings.
/// - <c>object-use-checklist.json</c> — uat-checklist-editor-compatible checklist.
/// </summary>
public static class IdentifyUsageCommand
{
    private static readonly JsonSerializerOptions _statusOpts = new() { WriteIndented = true };

    /// <summary>
    /// Runs the identify-usage subcommand. Returns 0 on success, 1 on usage
    /// error, 2 on unexpected I/O error.
    /// </summary>
    public static int Run(string[] args)
    {
        var opts = ParseOptions(args);

        bool hasIn       = opts.ContainsKey("in");
        bool hasManifest = opts.ContainsKey("manifest");

        if (!hasIn && !hasManifest)
        {
            Console.Error.WriteLine("identify-usage: --in <harvested-output-root> or --manifest <path> is required.");
            PrintUsage();
            return 1;
        }
        if (hasIn && hasManifest)
        {
            Console.Error.WriteLine("identify-usage: --in and --manifest are mutually exclusive.");
            return 1;
        }

        bool jsonStatus = opts.ContainsKey("json-status");

        try
        {
            string inputRoot = hasIn
                ? Path.GetFullPath(opts["in"])
                : Path.GetDirectoryName(Path.GetFullPath(opts["manifest"]))!;

            if (!Directory.Exists(inputRoot))
            {
                Console.Error.WriteLine($"identify-usage: directory not found: {inputRoot}");
                return 1;
            }

            string outputDir = inputRoot;

            // Load the IR database
            IrDatabase db = IrLoader.Load(inputRoot);

            // Optional cleanup exclusion list
            var excludedObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (opts.TryGetValue("exclude-cleanup", out string? cleanupPath))
            {
                if (!File.Exists(cleanupPath))
                {
                    Console.Error.WriteLine($"identify-usage: cleanup checklist not found: {cleanupPath}");
                    return 1;
                }
                LoadExclusions(cleanupPath, excludedObjects);
            }

            // Build signal index and classify
            var index    = SignalIndex.Build(db);
            var findings = UsageClassifier.Classify(db, index, excludedObjects);

            var result = new ObjectUseFindings
            {
                Database        = db.Name,
                GeneratedAt     = DateTime.UtcNow.ToString("o"),
                ExcludedObjects = excludedObjects.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                Objects         = findings,
            };

            // Write outputs
            UsageChecklistWriter.Write(result, outputDir);

            Console.Error.WriteLine($"[identify-usage] {findings.Count} objects classified.");
            Console.Error.WriteLine($"[identify-usage] Wrote: {Path.Combine(outputDir, "object-use.json")}");
            Console.Error.WriteLine($"[identify-usage] Wrote: {Path.Combine(outputDir, "object-use-checklist.json")}");

            if (jsonStatus)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    Command          = "identify-usage",
                    Database         = db.Name,
                    OutputDir        = outputDir,
                    ObjectsClassified = findings.Count,
                    ExcludedCount    = excludedObjects.Count,
                    ExitCode         = 0,
                }, _statusOpts));
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"identify-usage: unexpected error: {ex.Message}");
            return 2;
        }
    }

    private static void LoadExclusions(string cleanupPath, HashSet<string> excluded)
    {
        string json = File.ReadAllText(cleanupPath, System.Text.Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);

        // uat-checklist-editor format: { "Review": [ { "Subarea": "name", "Passes": true } ] }
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

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage:
              AccessHarvester identify-usage --in <harvested-output-root> [--exclude-cleanup <path>] [--json-status]
              AccessHarvester identify-usage --manifest <manifest.json>    [--exclude-cleanup <path>] [--json-status]

            --in              Root directory produced by `run` or `translate` (contains ir/ subdirectory).
            --manifest        Path to a single manifest.json (output dir is inferred as its parent).
            --exclude-cleanup Path to an object-cleanup-checklist.json from `identify-cleanup`.
                              Objects with Passes=true are excluded from classification.
            --json-status     Emit a JSON summary to stdout.
            """);
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] flagOptions = ["json-status"];
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected positional argument: {token}");
            string key = token[2..];
            if (flagOptions.Contains(key, StringComparer.OrdinalIgnoreCase))
                opts[key] = "true";
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
