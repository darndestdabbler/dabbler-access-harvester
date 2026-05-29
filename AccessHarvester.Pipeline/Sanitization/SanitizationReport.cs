using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Structured summary produced by a single <see cref="MaskingEngine"/> run.
/// Emitted alongside the sanitized CSV files as
/// <c>sanitization-report.json</c> so the consumer can see per-table row
/// counts and per-column applied strategies without re-deriving them from
/// the classification document.
/// </summary>
public sealed record SanitizationReport(
    string DatabaseName,
    DateTimeOffset SanitizationEpoch,
    string OutputDirectory,
    IReadOnlyList<TableSanitizationResult> Tables,
    IReadOnlyList<TableColumn> MissingFkTargets,
    IReadOnlyDictionary<TableColumn, int> RemapMapSizes);

public sealed record TableSanitizationResult(
    string Name,
    int RowsRead,
    int RowsWritten,
    string OutputFile,
    IReadOnlyList<AppliedStrategy> AppliedStrategies);

public sealed record AppliedStrategy(string ColumnName, MaskingStrategy Strategy);
