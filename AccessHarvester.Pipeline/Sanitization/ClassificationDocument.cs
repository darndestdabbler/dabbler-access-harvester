using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.Classification;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Typed reader-side model of a reviewed <c>classification.json</c> document
/// (schema v1.0). Mirror of the writer-side <see cref="ClassificationFindings"/>
/// shape with the <c>humanOverride</c> blocks populated.
/// </summary>
public sealed record ClassificationDocument(
    string Version,
    string Database,
    DateTimeOffset ScannedUtc,
    ScanConfig Config,
    IReadOnlyList<ClassifiedTable> Tables);

public sealed record ClassifiedTable(
    string Name,
    bool IsLinkedTable,
    int RowCount,
    bool LookupGuess,
    int NonKeyNonSystemColumnCount,
    int ReferencedByFkCount,
    string TableHint,
    double Confidence,
    IReadOnlyList<string> FiredHeuristics,
    TableHumanOverride HumanOverride,
    IReadOnlyList<ClassifiedColumn> Columns);

public sealed record TableHumanOverride(
    bool? IsLookup,
    string? ReviewedBy,
    string Notes);

public sealed record ClassifiedColumn(
    string Name,
    int OrdinalPosition,
    string DataType,
    bool IsAutoNumber,
    bool IsPrimaryKey,
    bool IsForeignKey,
    ClassifiedFkTarget? FkTarget,
    bool IsMemo,
    bool IsOleObject,
    bool IsMultiValue,
    bool IsRequired,
    int RowCount,
    int NullCount,
    int Cardinality,
    double UniquenessRatio,
    double RelativeCardinality,
    bool PiiNameMatch,
    bool PiiValueMatch,
    string? ValueFormatPattern,
    ColumnCategory GuessedCategory,
    double Confidence,
    IReadOnlyList<string> FiredHeuristics,
    ColumnHumanOverride HumanOverride);

public sealed record ColumnHumanOverride(
    ColumnCategory? Category,
    MaskingStrategy? MaskingStrategy,
    bool PreserveAutonumber,
    string? ReviewedBy,
    string Notes);

public sealed record ClassifiedFkTarget(string Table, string Column);
