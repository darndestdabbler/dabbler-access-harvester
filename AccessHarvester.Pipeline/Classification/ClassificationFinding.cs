using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// The 10-category column taxonomy from data-classification-plan § 3.5. The
/// JSON wire form uses kebab-case (see <see cref="ColumnCategoryWire"/>).
/// </summary>
public enum ColumnCategory
{
    DirectIdentifierPii,
    QuasiIdentifier,
    SensitiveNonPii,
    FreeTextMemo,
    LookupReference,
    SystemAudit,
    ForeignKey,
    SurrogateAutonumberPk,
    BusinessNaturalKey,
    GenericNonSensitive,
}

/// <summary>Coarse table-shape hint paired with the lookup guess (informational).</summary>
public enum TableHint
{
    LookupReference,
    EntityPiiBearing,
    EntityNonPii,
    Transaction,
    AuditLog,
    Junction,
    Linked,
    Unclassified,
}

/// <summary>
/// Per-table classification produced by <see cref="HeuristicsEngine"/>. Carries
/// the AI guess fields plus the structured <see cref="Columns"/> list. The
/// <c>humanOverride</c> block is the editor's territory — emitted with
/// scanner defaults.
/// </summary>
public sealed record TableFinding(
    string Name,
    bool   IsLinkedTable,
    int    RowCount,
    bool   LookupGuess,
    int    NonKeyNonSystemColumnCount,
    int    ReferencedByFkCount,
    TableHint TableHint,
    double Confidence,
    IReadOnlyList<string> FiredHeuristics,
    IReadOnlyList<ColumnFinding> Columns);

/// <summary>Per-column classification produced by <see cref="HeuristicsEngine"/>.</summary>
public sealed record ColumnFinding(
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
    bool   IsRequired,
    int    RowCount,
    int    NullCount,
    int    Cardinality,
    double UniquenessRatio,
    double RelativeCardinality,
    bool   PiiNameMatch,
    bool   PiiValueMatch,
    string? ValueFormatPattern,
    ColumnCategory GuessedCategory,
    double Confidence,
    IReadOnlyList<string> FiredHeuristics);

/// <summary>Aggregate output for one <c>classify</c> run.</summary>
public sealed record ClassificationFindings(
    string DatabaseName,
    DateTimeOffset ScannedUtc,
    ScanConfig Config,
    IReadOnlyList<TableFinding> Tables);

/// <summary>Wire-form (kebab-case) names for <see cref="ColumnCategory"/> and <see cref="TableHint"/>.</summary>
public static class ColumnCategoryWire
{
    private static readonly Dictionary<ColumnCategory, string> ToWireMap = new()
    {
        [ColumnCategory.DirectIdentifierPii]    = "direct-identifier-pii",
        [ColumnCategory.QuasiIdentifier]        = "quasi-identifier",
        [ColumnCategory.SensitiveNonPii]        = "sensitive-non-pii",
        [ColumnCategory.FreeTextMemo]           = "free-text-memo",
        [ColumnCategory.LookupReference]        = "lookup-reference",
        [ColumnCategory.SystemAudit]            = "system-audit",
        [ColumnCategory.ForeignKey]             = "foreign-key",
        [ColumnCategory.SurrogateAutonumberPk]  = "surrogate-autonumber-pk",
        [ColumnCategory.BusinessNaturalKey]     = "business-natural-key",
        [ColumnCategory.GenericNonSensitive]    = "generic-non-sensitive",
    };

    private static readonly Dictionary<TableHint, string> HintToWireMap = new()
    {
        [TableHint.LookupReference]   = "lookup-reference",
        [TableHint.EntityPiiBearing]  = "entity-pii-bearing",
        [TableHint.EntityNonPii]      = "entity-non-pii",
        [TableHint.Transaction]       = "transaction",
        [TableHint.AuditLog]          = "audit-log",
        [TableHint.Junction]          = "junction",
        [TableHint.Linked]            = "linked",
        [TableHint.Unclassified]      = "unclassified",
    };

    public static string ToWire(this ColumnCategory category) => ToWireMap[category];
    public static string ToWire(this TableHint hint)          => HintToWireMap[hint];
}
