using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core.Com;
using AccessHarvester.Core.ReportBaseline;
using AccessHarvester.Pipeline.Manifest;

namespace AccessHarvester.Cli.Commands;

/// <summary>
/// Implements the <c>report-baseline</c> subcommand: captures the rendered
/// PDF and RecordSource-query aggregates for every report (or a
/// <c>--reports</c> allowlist) in a single Access database. Writes
/// per-report <c>output.pdf</c> + <c>aggregates.json</c> and a top-level
/// <c>baseline-manifest.json</c> under
/// <c>&lt;out&gt;/AccessMetaData/&lt;dbName&gt;/reports-baseline/</c>.
///
/// <para>
/// Exit codes (mirror <c>run</c>): 0 = all considered reports captured
/// (or harmlessly skipped, e.g. unbound); 1 = usage error;
/// 2 = unexpected error; 3 = completed with per-report failures.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public static class ReportBaselineCommand
{
    private static readonly JsonSerializerOptions StatusJsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> FlagOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json-status",
        "no-clean",
    };

    public static int Run(string[] args)
    {
        var opts = ParseOptions(args);
        string db = Path.GetFullPath(Require(opts, "db"));
        string outDir = Path.GetFullPath(Require(opts, "out"));

        if (!File.Exists(db))
        {
            Console.Error.WriteLine($"Database not found: {db}");
            return 1;
        }

        IReadOnlySet<string>? reportFilter = null;
        if (opts.TryGetValue("reports", out var reportsList))
        {
            reportFilter = new HashSet<string>(
                reportsList
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.Ordinal);
            if (reportFilter.Count == 0)
            {
                Console.Error.WriteLine("--reports was provided but contained no report names.");
                return 1;
            }
        }

        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? parametersByReport = null;
        if (opts.TryGetValue("params", out var paramsPath))
        {
            var resolvedParamsPath = Path.GetFullPath(paramsPath);
            if (!File.Exists(resolvedParamsPath))
            {
                Console.Error.WriteLine($"--params file not found: {resolvedParamsPath}");
                return 1;
            }
            try
            {
                parametersByReport = LoadParameters(File.ReadAllText(resolvedParamsPath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"--params file is not a valid {{\"ReportName\": {{\"ParamName\": value}}}} JSON: {ex.Message}");
                return 1;
            }
        }

        bool clean = !opts.ContainsKey("no-clean");

        string dbName = Path.GetFileNameWithoutExtension(db);
        string reportsBaselineRoot = Path.Combine(outDir, "AccessMetaData", dbName, "reports-baseline");
        string harvesterVersion = DatabaseManifest.ResolveHarvesterVersion();

        using var session = new AccessComSession(db);
        var source = new AccessReportSpecReader(session);
        var renderer = new AccessReportPdfRenderer(session);
        using var executor = new OleDbReportQueryExecutorScope(db);
        var service = new ReportBaselineService(executor.Executor, harvesterVersion);
        var runner = new ReportBaselineRunner(source, renderer, service, harvesterVersion);

        var allReports = source.EnumerateAll();
        IReadOnlyList<string> toProcess;
        if (reportFilter is null)
        {
            toProcess = allReports;
        }
        else
        {
            var availableSet = new HashSet<string>(allReports, StringComparer.Ordinal);
            var unknown = reportFilter.Where(n => !availableSet.Contains(n)).ToArray();
            if (unknown.Length > 0)
            {
                Console.Error.WriteLine(
                    "The following --reports name(s) were not found in the database: "
                    + string.Join(", ", unknown));
                return 1;
            }
            toProcess = allReports.Where(reportFilter.Contains).ToArray();
        }

        var manifest = runner.Run(db, toProcess, reportsBaselineRoot, parametersByReport, cleanRoot: clean);

        ReportSummary(manifest);

        int captured = manifest.Reports.Count(r => r.Status == "Captured");
        int skipped = manifest.Reports.Count(r => r.Status == "Skipped");
        int failed = manifest.Reports.Count(r => r.Status == "Failed");
        int exit = failed > 0 ? 3 : 0;

        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "report-baseline",
                HarvesterVersion = harvesterVersion,
                Database = db,
                OutputRoot = outDir,
                ReportsBaselineDir = reportsBaselineRoot,
                ExitCode = exit,
                ReportBaseline = new
                {
                    Captured = captured,
                    Skipped = skipped,
                    Failed = failed,
                    ReportsConsidered = manifest.Reports.Count,
                    AllReportsInDb = allReports.Count,
                    Entries = manifest.Reports.Select(r => new
                    {
                        Name = r.Name,
                        Status = r.Status,
                        Reason = r.Reason,
                        Paths = r.Paths,
                    }).ToArray(),
                },
            });
        }

        return exit;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>> LoadParameters(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Root must be a JSON object keyed by report name.");

        var result = new Dictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>(StringComparer.Ordinal);
        foreach (var report in doc.RootElement.EnumerateObject())
        {
            if (report.Value.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException(
                    $"Value for report '{report.Name}' must be a JSON object of parameter values.");

            // Explicitly ordered list, NOT a dictionary — the list's
            // index is the source of truth for positional binding to
            // the SQL's `?` placeholders. Access OLE DB ignores
            // parameter names for text commands. The runner builds a
            // separate name-keyed dictionary for aggregates.json
            // provenance from this list.
            var ordered = new List<KeyValuePair<string, object?>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var param in report.Value.EnumerateObject())
            {
                if (!seen.Add(param.Name))
                    throw new InvalidOperationException(
                        $"Report '{report.Name}' has a duplicate parameter '{param.Name}'.");
                ordered.Add(new KeyValuePair<string, object?>(param.Name, JsonValueToCoreValue(param.Value)));
            }
            result[report.Name] = ordered;
        }
        return result;
    }

    private static object? JsonValueToCoreValue(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number when v.TryGetInt64(out var i) => i,
        JsonValueKind.Number when v.TryGetDecimal(out var d) => d,
        JsonValueKind.Number => v.GetDouble(),
        _ => v.GetRawText(),
    };

    private static void ReportSummary(BaselineManifest manifest)
    {
        Console.Error.WriteLine($"Captured report baselines written to: {manifest.Reports.Count} report dir(s)");
        foreach (var entry in manifest.Reports)
        {
            string suffix = entry.Reason is null ? "" : $" — {entry.Reason}";
            Console.Error.WriteLine($"  [{entry.Status,-8}] {entry.Name}{suffix}");
        }
    }

    private static void EmitStatus(object status)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(status, StatusJsonOptions));
    }

    private static bool HasJsonStatus(Dictionary<string, string> opts)
        => opts.ContainsKey("json-status");

    private static string Require(Dictionary<string, string> opts, string key)
    {
        if (!opts.TryGetValue(key, out var value))
            throw new ArgumentException($"Missing required option: --{key}");
        return value;
    }

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

    /// <summary>
    /// The <see cref="OleDbReportQueryExecutor"/> does not itself hold a
    /// long-lived OLE DB resource (each <c>Execute</c> opens and closes
    /// its own connection), but wrapping it in an <see cref="IDisposable"/>
    /// keeps the CLI command symmetric with the COM session and gives a
    /// natural seam to add resource lifetime later.
    /// </summary>
    private sealed class OleDbReportQueryExecutorScope : IDisposable
    {
        public OleDbReportQueryExecutor Executor { get; }
        public OleDbReportQueryExecutorScope(string databasePath)
        {
            Executor = new OleDbReportQueryExecutor(databasePath);
        }
        public void Dispose() { /* no-op; per-call connection lifetime */ }
    }
}
