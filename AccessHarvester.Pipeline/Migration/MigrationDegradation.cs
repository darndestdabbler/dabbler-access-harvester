namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// The kind of a migration degradation. A degradation is something the generator
/// could not migrate cleanly and therefore flagged rather than silently dropping
/// or fabricating a lossy substitute.
/// </summary>
public enum MigrationDegradationKind
{
    /// <summary>
    /// A column whose Access type has no honest scalar SQL Server mapping
    /// (multi-value / attachment fields). The column is omitted from the
    /// <c>CREATE TABLE</c>.
    /// </summary>
    UnmappableColumnType,

    /// <summary>
    /// A column the reviewed classification marks as multi-value. Omitted from
    /// the <c>CREATE TABLE</c> even when its scalar <c>TypeCode</c> looks
    /// mappable, because a multi-value field is a hidden child table in Access.
    /// </summary>
    MultiValueColumn,

    /// <summary>
    /// A whole table that is excluded from the migration (a linked/attached
    /// table whose schema and data live in an external source).
    /// </summary>
    LinkedTableSkipped,

    /// <summary>
    /// A column default whose Access expression could not be translated to a
    /// SQL Server default. The column is still emitted, but without a DEFAULT.
    /// </summary>
    UntranslatedDefault,

    /// <summary>
    /// A column mapped to a SQL Server type with an assumed precision/length the
    /// IR does not carry (e.g. decimal precision/scale). The column is emitted
    /// with a documented default; surfaced so a reviewer can tighten it.
    /// </summary>
    AssumedTypePrecision,

    /// <summary>
    /// A foreign key dropped because it references a table or column that is not
    /// part of the migrated schema (an excluded linked table, or a column that
    /// was itself degraded out of its table).
    /// </summary>
    ForeignKeyDropped,
}

/// <summary>
/// A single honestly-flagged migration degradation. Emitted into the migration
/// manifest (Session 3) so nothing is silently dropped or fabricated.
/// </summary>
/// <param name="Kind">The category of degradation.</param>
/// <param name="Table">The affected table.</param>
/// <param name="Column">The affected column, or <c>null</c> for table-level degradations.</param>
/// <param name="Reason">A human-readable explanation a reviewer can act on.</param>
public sealed record MigrationDegradation(
    MigrationDegradationKind Kind,
    string Table,
    string? Column,
    string Reason);
