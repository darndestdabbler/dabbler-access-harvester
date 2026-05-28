using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// Wires <see cref="ISchemaReader"/> + <see cref="IValueSampler"/> +
/// <see cref="HeuristicsEngine"/> + <see cref="ClassificationWriter"/>
/// into a single in-memory pipeline. Pure logic — the COM-using
/// implementations of the two interfaces are supplied by the caller.
/// </summary>
public static class ClassificationRunner
{
    public sealed record RunResult(
        ClassificationFindings Findings,
        string Json,
        IReadOnlyList<string> ExcludedTables,
        IReadOnlyList<ClassificationDegradation> Degradations);

    public static RunResult Run(
        string databasePath,
        ISchemaReader schemaReader,
        IValueSampler valueSampler,
        ScanConfig    config,
        IReadOnlySet<string>? excludedTableNames,
        DateTimeOffset scannedUtc)
    {
        if (schemaReader is null) throw new ArgumentNullException(nameof(schemaReader));
        if (valueSampler is null) throw new ArgumentNullException(nameof(valueSampler));
        if (config       is null) throw new ArgumentNullException(nameof(config));

        var degradations = new List<ClassificationDegradation>();
        RawSchema rawSchema = schemaReader is IDegradationReporter dr1
            ? dr1.ReadWithDegradations(databasePath, degradations)
            : schemaReader.Read(databasePath);

        var excluded = new List<string>();

        // Apply --exclude-cleanup filter (case-insensitive table-name match).
        IReadOnlyList<RawTable> tables = rawSchema.Tables;
        if (excludedTableNames is { Count: > 0 })
        {
            var kept = new List<RawTable>(tables.Count);
            foreach (var t in tables)
            {
                if (excludedTableNames.Contains(t.Name))
                {
                    excluded.Add(t.Name);
                    continue;
                }
                kept.Add(t);
            }
            tables = kept;
            rawSchema = rawSchema with { Tables = kept };
        }

        IReadOnlyList<ColumnStatistics> stats =
            valueSampler is IDegradationReporter dr2
                ? dr2.SampleWithDegradations(databasePath, rawSchema, config, degradations)
                : valueSampler.Sample(databasePath, rawSchema, config);
        var findings = HeuristicsEngine.Run(rawSchema, stats, config, scannedUtc);
        string json  = ClassificationWriter.Serialize(findings);

        return new RunResult(findings, json, excluded, degradations);
    }
}
