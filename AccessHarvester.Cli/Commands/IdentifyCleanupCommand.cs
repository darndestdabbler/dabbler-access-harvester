using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core.Com;
using AccessHarvester.Pipeline.Cleanup;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Cli.Commands;

/// <summary>Implements the <c>identify-cleanup</c> subcommand.</summary>
public static class IdentifyCleanupCommand
{
    private static readonly JsonSerializerOptions _statusOpts = new() { WriteIndented = true };

    /// <summary>Runs identify-cleanup. Returns 0 on success, 2 on usage error, 1 on other failure.</summary>
    public static int Run(string[] args)
    {
        Dictionary<string, string> opts;
        try { opts = ParseOptions(args); }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"identify-cleanup: {ex.Message}");
            PrintUsage();
            return 2;
        }

        bool hasIn = opts.ContainsKey("in");
        bool hasManifest = opts.ContainsKey("manifest");
        if (!hasIn && !hasManifest)
        {
            Console.Error.WriteLine("identify-cleanup: --in <harvested-output-root> or --manifest <path> is required.");
            PrintUsage();
            return 2;
        }
        if (hasIn && hasManifest)
        {
            Console.Error.WriteLine("identify-cleanup: --in and --manifest are mutually exclusive.");
            PrintUsage();
            return 2;
        }

        int thresholdDays = 90;
        if (opts.TryGetValue("threshold-days", out var thStr))
        {
            if (!int.TryParse(thStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out thresholdDays)
                || thresholdDays < 1)
            {
                Console.Error.WriteLine("identify-cleanup: --threshold-days must be a positive integer (>= 1).");
                return 2;
            }
        }

        DateOnly harvestDate = DateOnly.FromDateTime(DateTime.Now);
        if (opts.TryGetValue("harvest-date", out var hdStr))
        {
            if (!DateOnly.TryParseExact(hdStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out harvestDate))
            {
                Console.Error.WriteLine("identify-cleanup: --harvest-date must be a calendar date in YYYY-MM-DD format.");
                return 2;
            }
        }

        bool noCom = opts.ContainsKey("no-com");
        bool jsonStatus = opts.ContainsKey("json-status");

        try
        {
            string inputRoot = hasIn
                ? Path.GetFullPath(opts["in"])
                : Path.GetDirectoryName(Path.GetFullPath(opts["manifest"]))!;
            if (!Directory.Exists(inputRoot))
            {
                Console.Error.WriteLine($"identify-cleanup: directory not found: {inputRoot}");
                return 1;
            }
            string outputDir = inputRoot;

            IrDatabase db = IrLoader.Load(inputRoot);

            // Per strategy §3.2 / §5, the date-signal source is selected per-table.
            // The CLI supplies a DAO-metadata fallback sampler always; when COM is
            // available we additionally supply the COM sampler as the primary. The
            // analyser then chooses per-table based on whether the table has DateTime
            // columns AND whether COM produced a sample for it.
            //
            // COM is best-effort: §3.2 says "The fallback path also activates
            // automatically when COM extraction throws or the host platform is
            // non-Windows — `--no-com` is a discipline knob, not a correctness gate."
            // We therefore probe COM by performing an upfront Sample() call inside a
            // try/catch. On any failure (Access not installed, .accdb file lock, COM
            // activation failure, OpenCurrentDatabase exception) we drop the COM
            // sampler, accumulate a degradation note, and fall back to DAO metadata.
            IDateMaximaSampler fallback = new IrLastUpdatedSampler();
            IDateMaximaSampler? primary = null;
            bool comAvailable = false;
            string? comDegradationNote = null;
            IDisposable? primaryDisposable = null;
            if (!noCom && OperatingSystem.IsWindows() && TryLocateAccdb(inputRoot, db.Name, out string? accdbPath))
            {
                var probeResult = ComDateMaximaProbe.Probe(
                    () => new AccessDateMaximaSampler(accdbPath!),
                    db);
                primary = probeResult.Primary;
                comAvailable = probeResult.ComAvailable;
                comDegradationNote = probeResult.DegradationNote;
                primaryDisposable = probeResult.DisposableSampler;
                if (probeResult.DegradationNote is not null)
                {
                    Console.Error.WriteLine(
                        $"[identify-cleanup] COM date sampling failed: {probeResult.OriginalErrorMessage}. Falling back to DAO LastUpdated.");
                }
            }

            CleanupFindings findings;
            try
            {
                findings = CleanupAnalyser.Run(db, fallback, harvestDate, thresholdDays, comAvailable, primary);
                // Prepend the COM-failure note only when the DAO fallback actually
                // had data to use; otherwise the analyser's own "Date sampling
                // unavailable: COM declined AND no IR LastUpdated values present"
                // note is more accurate and the probe message would contradict it.
                if (comDegradationNote is not null && findings.DateSignalSource != "unavailable")
                {
                    var augmented = new List<string> { comDegradationNote };
                    augmented.AddRange(findings.DegradationNotes);
                    findings = findings with { DegradationNotes = augmented };
                }
            }
            finally
            {
                if (primaryDisposable is IDisposable pd) pd.Dispose();
                if (fallback is IDisposable fd) fd.Dispose();
            }

            CleanupChecklistWriter.Write(findings, outputDir);

            Console.Error.WriteLine($"[identify-cleanup] {findings.Findings.Count} objects flagged.");
            Console.Error.WriteLine($"[identify-cleanup] Wrote: {Path.Combine(outputDir, "object-cleanup.json")}");
            Console.Error.WriteLine($"[identify-cleanup] Wrote: {Path.Combine(outputDir, "object-cleanup-checklist.json")}");

            if (jsonStatus)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    Command          = "identify-cleanup",
                    Database         = db.Name,
                    OutputDir        = outputDir,
                    ObjectsFlagged   = findings.Findings.Count,
                    DateSignalSource = findings.DateSignalSource,
                    ThresholdDays    = findings.ThresholdDays,
                    HarvestDate      = findings.HarvestDate,
                    ExitCode         = 0,
                }, _statusOpts));
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"identify-cleanup: unexpected error: {ex.Message}");
            return 1;
        }
    }

    private static bool TryLocateAccdb(string inputRoot, string databaseName, out string? accdbPath)
    {
        accdbPath = null;
        // Search likely locations for a matching .accdb / .mdb. Best-effort only.
        foreach (var ext in new[] { ".accdb", ".mdb" })
        {
            string candidate = Path.Combine(inputRoot, databaseName + ext);
            if (File.Exists(candidate)) { accdbPath = candidate; return true; }
        }
        try
        {
            var matches = Directory.GetFiles(inputRoot, "*.accdb", SearchOption.TopDirectoryOnly);
            if (matches.Length == 1) { accdbPath = matches[0]; return true; }
        }
        catch { }
        return false;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage:
              AccessHarvester identify-cleanup --in <harvested-output-root> [--harvest-date <YYYY-MM-DD>] [--threshold-days <N>] [--no-com] [--json-status]
              AccessHarvester identify-cleanup --manifest <manifest.json>    [--harvest-date <YYYY-MM-DD>] [--threshold-days <N>] [--no-com] [--json-status]

            --in              Root directory produced by `run` or `translate` (contains ir/ subdirectory).
            --manifest        Path to a single manifest.json (output dir is inferred as its parent).
            --harvest-date    Calendar date anchoring the date-signal threshold (YYYY-MM-DD; default: today).
            --threshold-days  Date-signal threshold in days, integer >= 1 (default: 90).
            --no-com          Skip COM date sampling; rely on DAO LastUpdated fallback.
            --json-status     Emit a JSON summary to stdout.
            """);
    }

    // 006 O-1-4: route through the shared OptionParser so an unknown --switch is
    // an honest usage error. ValueOptions are the keys this command reads
    // (--in, --manifest, --harvest-date, --threshold-days); --no-com and
    // --json-status are flags.
    private static readonly string[] ValueOptions = ["in", "manifest", "harvest-date", "threshold-days"];
    private static readonly string[] FlagOptions = ["no-com", "json-status"];

    private static Dictionary<string, string> ParseOptions(string[] args)
        => OptionParser.Parse(args, ValueOptions, FlagOptions);
}
