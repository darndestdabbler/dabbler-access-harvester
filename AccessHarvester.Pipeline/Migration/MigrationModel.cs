using System.Collections.Generic;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// The assembled, generator-facing input model for <c>generate-migration</c>.
/// Session 1 populates <see cref="Tables"/> and <see cref="Relationships"/> from
/// the harvested <c>ir/Tables</c> + <c>ir/Relationships</c> tiers and (optionally)
/// the reviewed <see cref="Classification"/> document. <see cref="SanitizedDataDir"/>
/// records where the <c>sanitize</c> subcommand's CSV-per-table output lives; the
/// rows themselves are consumed in Session 2 (data-load), so the directory is
/// captured here as part of the input contract but not read yet.
/// </summary>
public sealed record MigrationInputs(
    string Database,
    IReadOnlyList<MigrationTable> Tables,
    IReadOnlyList<MigrationRelationship> Relationships,
    ClassificationDocument? Classification,
    string? SanitizedDataDir);

/// <summary>
/// A migratable table parsed from <c>ir/Tables/&lt;t&gt;.ir.json</c>. Only the
/// fields the SQL Server generator needs are lifted out of the IR — the shared
/// <see cref="UsageIdentification.IrTable"/> model is intentionally not reused
/// because it omits the per-field type metadata, indexes, and linkage flags this
/// feature depends on.
/// </summary>
public sealed record MigrationTable(
    string Name,
    bool IsSystem,
    bool IsLinked,
    IReadOnlyList<MigrationField> Fields,
    IReadOnlyList<MigrationIndex> Indexes)
{
    /// <summary>The primary-key index, if the table declares one.</summary>
    public MigrationIndex? PrimaryKey
    {
        get
        {
            foreach (var ix in Indexes)
                if (ix.Primary) return ix;
            return null;
        }
    }
}

/// <summary>
/// A column of a migratable table. <see cref="TypeCode"/> is the DAO
/// <c>dbType*</c> constant; <see cref="IsAutoIncrement"/> is decoded from the
/// DAO field-attributes bitmask (<c>dbAutoIncrField = 0x10</c>).
/// </summary>
public sealed record MigrationField(
    string Name,
    int TypeCode,
    string TypeName,
    int? Size,
    bool Required,
    bool IsAutoIncrement,
    string? DefaultValue,
    int OrdinalPosition);

/// <summary>An index on a migratable table (only PK is consumed in Session 1).</summary>
public sealed record MigrationIndex(
    string Name,
    bool Primary,
    bool Unique,
    IReadOnlyList<string> Fields);

/// <summary>
/// A declared relationship parsed from <c>ir/Relationships/&lt;r&gt;.ir.json</c>.
/// The parent table holds the referenced (PK) columns; the child table holds the
/// referencing (FK) columns. Cascade flags carry through to <c>ON UPDATE/DELETE
/// CASCADE</c>.
/// </summary>
public sealed record MigrationRelationship(
    string Name,
    string ParentTable,
    string ChildTable,
    IReadOnlyList<MigrationFieldPair> FieldPairs,
    bool EnforceReferentialIntegrity,
    bool CascadeUpdate,
    bool CascadeDelete);

/// <summary>A single parent-column ↔ child-column pairing within a relationship.</summary>
public sealed record MigrationFieldPair(string ParentField, string ChildField);
