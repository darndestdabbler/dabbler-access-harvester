using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// Schema-only snapshot of an Access database produced by the platform-
/// neutral schema reader contract <see cref="ISchemaReader"/>. Pure data —
/// no COM, no I/O. The heuristics engine consumes a <see cref="RawSchema"/>
/// plus per-column <see cref="ColumnStatistics"/> to produce the final
/// <c>classification.json</c>.
/// </summary>
public sealed record RawSchema(
    string DatabaseName,
    IReadOnlyList<RawTable> Tables);

public sealed record RawTable(
    string Name,
    bool   IsLinkedTable,
    IReadOnlyList<RawColumn> Columns);

public sealed record RawColumn(
    string Name,
    int    OrdinalPosition,
    string DataType,
    bool   IsAutoNumber,
    bool   IsPrimaryKey,
    bool   IsForeignKey,
    RawFkTarget? FkTarget,
    bool   IsMemo,
    bool   IsOleObject,
    bool   IsMultiValue,
    bool   IsRequired);

public sealed record RawFkTarget(string Table, string Column);

/// <summary>Per-column observation set produced by sampling table rows.</summary>
public sealed record ColumnStatistics(
    string TableName,
    string ColumnName,
    int    RowCount,
    int    NullCount,
    int    Cardinality,
    bool   PiiValueMatch,
    string? ValueFormatPattern);
