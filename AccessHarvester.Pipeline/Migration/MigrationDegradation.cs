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

    /// <summary>
    /// A set of tables that form a foreign-key reference cycle, so no strict
    /// parent-before-child load order exists. The tables are still emitted (in a
    /// deterministic order) and flagged so a reviewer can defer/disable the
    /// offending constraints during load.
    /// </summary>
    ForeignKeyCycle,

    /// <summary>
    /// A migrated table whose sanitized CSV data file is absent, so no data-load
    /// (<c>INSERT</c>) statements could be emitted for it. The <c>CREATE TABLE</c>
    /// still stands; only its rows are missing.
    /// </summary>
    MissingDataFile,

    /// <summary>
    /// A surviving schema column that is not loaded from the sanitized CSV — either
    /// the CSV has no column for it, or its data (e.g. binary/OLE) cannot round-trip
    /// through the text CSV. The column stays in the schema; it is simply omitted
    /// from the generated <c>INSERT</c> column list.
    /// </summary>
    DataColumnOmitted,

    /// <summary>
    /// A numeric- or bit-typed column whose sanitized CSV carried a token that is
    /// not a valid invariant numeric / bit literal (a stale pre-rename CSV, a
    /// hand-edited cell, or a classification-vs-CSV type drift). The generator emits
    /// <c>NULL</c> for the offending cell rather than interpolating the raw token bare
    /// into the <c>INSERT</c> — which would produce malformed or arbitrary SQL —
    /// following the project's validate-or-degrade posture (F-2-7). Flagged once per
    /// affected column.
    /// </summary>
    InvalidDataToken,
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
