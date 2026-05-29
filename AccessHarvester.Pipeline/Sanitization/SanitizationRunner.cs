using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Convenience wrapper that reads a classification file, runs the
/// <see cref="MaskingEngine"/> against an arbitrary
/// <see cref="IRowSource"/>, and writes the sanitization report. Pure of
/// COM; the runner is testable with any source. The CLI wraps this with
/// the production <c>OleDbRowSource</c>.
/// </summary>
public static class SanitizationRunner
{
    public sealed record RunResult(SanitizationReport Report, string ReportPath);

    public static RunResult Run(
        string classificationPath,
        string outDirectory,
        IRowSource rowSource,
        DateTimeOffset sanitizationEpoch)
    {
        if (classificationPath is null) throw new ArgumentNullException(nameof(classificationPath));
        if (outDirectory       is null) throw new ArgumentNullException(nameof(outDirectory));
        if (rowSource          is null) throw new ArgumentNullException(nameof(rowSource));

        var document = ClassificationReader.ReadFile(classificationPath);

        Directory.CreateDirectory(outDirectory);
        var csvOutput = new FileSystemCsvOutput(outDirectory);

        var engine = new MaskingEngine();
        var report = engine.Run(document, rowSource, csvOutput, sanitizationEpoch);

        string reportPath = Path.Combine(outDirectory, "sanitization-report.json");
        File.WriteAllText(reportPath, SerializeReport(report), new UTF8Encoding(false));

        return new RunResult(report, reportPath);
    }

    /// <summary>
    /// Serialises a <see cref="SanitizationReport"/> to JSON. The shape is
    /// stable and the consumer's contract; the CLI also emits a subset of
    /// these fields in its <c>--json-status</c> envelope.
    /// </summary>
    public static string SerializeReport(SanitizationReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var tables = new JsonArray();
        foreach (var t in report.Tables)
        {
            var strategies = new JsonArray();
            foreach (var s in t.AppliedStrategies)
                strategies.Add(new JsonObject
                {
                    ["column"]   = s.ColumnName,
                    ["strategy"] = s.Strategy.ToWire(),
                });
            tables.Add(new JsonObject
            {
                ["name"]              = t.Name,
                ["rowsRead"]          = t.RowsRead,
                ["rowsWritten"]       = t.RowsWritten,
                ["outputFile"]        = t.OutputFile,
                ["appliedStrategies"] = strategies,
            });
        }

        var missing = new JsonArray();
        foreach (var m in report.MissingFkTargets)
            missing.Add(new JsonObject { ["table"] = m.Table, ["column"] = m.Column });

        var remapSizes = new JsonArray();
        foreach (var (key, size) in report.RemapMapSizes)
            remapSizes.Add(new JsonObject
            {
                ["table"]    = key.Table,
                ["column"]   = key.Column,
                ["mapSize"]  = size,
            });

        var root = new JsonObject
        {
            ["version"]            = "1.0",
            ["database"]           = report.DatabaseName,
            ["sanitizationEpoch"]  = report.SanitizationEpoch.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["outputDirectory"]    = report.OutputDirectory,
            ["tables"]             = tables,
            ["missingFkTargets"]   = missing,
            ["remapMapSizes"]      = remapSizes,
        };
        return root.ToJsonString(opts);
    }
}
