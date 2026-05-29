using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

/// <summary>
/// Helpers that construct realistic <see cref="ClassifiedColumn"/> /
/// <see cref="ClassifiedTable"/> values for tests without hand-writing every
/// field. The defaults reflect a scanner-emitted document with no human
/// overrides; tests pass <see langword="with"/> expressions to vary one
/// field at a time.
/// </summary>
internal static class TestFixtures
{
    public static readonly ColumnHumanOverride EmptyColOverride =
        new(Category: null, MaskingStrategy: null, PreserveAutonumber: false, ReviewedBy: null, Notes: "");

    public static readonly TableHumanOverride EmptyTableOverride =
        new(IsLookup: null, ReviewedBy: null, Notes: "");

    public static readonly ScanConfig EmptyConfig = new(
        SystemColumnPatterns: Array.Empty<string>(),
        PiiNamePatterns:      Array.Empty<string>(),
        PiiValuePatterns:     Array.Empty<string>(),
        ValueSampleSize:      0);

    public static ClassifiedColumn Col(
        string name,
        ColumnCategory guessed,
        bool pk = false,
        bool autoNumber = false,
        bool fk = false,
        ClassifiedFkTarget? fkTarget = null,
        bool memo = false,
        bool required = false,
        string dataType = "Text",
        string? formatPattern = null,
        ColumnHumanOverride? humanOverride = null)
        => new(
            Name: name,
            OrdinalPosition: 0,
            DataType: dataType,
            IsAutoNumber: autoNumber,
            IsPrimaryKey: pk,
            IsForeignKey: fk,
            FkTarget: fkTarget,
            IsMemo: memo,
            IsOleObject: false,
            IsMultiValue: false,
            IsRequired: required,
            RowCount: 0,
            NullCount: 0,
            Cardinality: 0,
            UniquenessRatio: 0,
            RelativeCardinality: 0,
            PiiNameMatch: false,
            PiiValueMatch: false,
            ValueFormatPattern: formatPattern,
            GuessedCategory: guessed,
            Confidence: 1.0,
            FiredHeuristics: Array.Empty<string>(),
            HumanOverride: humanOverride ?? EmptyColOverride);

    public static ClassifiedTable Table(
        string name,
        params ClassifiedColumn[] columns)
        => new(
            Name: name,
            IsLinkedTable: false,
            RowCount: 0,
            LookupGuess: false,
            NonKeyNonSystemColumnCount: columns.Length,
            ReferencedByFkCount: 0,
            TableHint: "unclassified",
            Confidence: 1.0,
            FiredHeuristics: Array.Empty<string>(),
            HumanOverride: EmptyTableOverride,
            Columns: columns);

    public static ClassificationDocument Doc(params ClassifiedTable[] tables)
        => new(
            Version: "1.0",
            Database: "Test.accdb",
            ScannedUtc: new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero),
            Config: EmptyConfig,
            Tables: tables);
}

/// <summary>In-memory <see cref="IRowSource"/> used by unit + invariant tests.</summary>
internal sealed class InMemoryRowSource : IRowSource
{
    private readonly Dictionary<string, List<IReadOnlyDictionary<string, object?>>> _data;

    public InMemoryRowSource()
    {
        _data = new Dictionary<string, List<IReadOnlyDictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);
    }

    public void Add(string table, IReadOnlyDictionary<string, object?> row)
    {
        if (!_data.TryGetValue(table, out var rows))
            _data[table] = rows = new List<IReadOnlyDictionary<string, object?>>();
        rows.Add(row);
    }

    public IEnumerable<IReadOnlyDictionary<string, object?>> ReadRows(
        string tableName, IReadOnlyList<string> columnNames)
    {
        if (!_data.TryGetValue(tableName, out var rows)) return Array.Empty<IReadOnlyDictionary<string, object?>>();
        return rows;
    }
}
