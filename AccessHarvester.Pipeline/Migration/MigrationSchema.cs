using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// One column that survived into the migrated schema, resolved to its SQL Server
/// type plus the facts both the DDL emitter and the data-load generator need.
/// </summary>
/// <param name="Name">The source (Access) column name; emitted verbatim, bracket-quoted.</param>
/// <param name="TypeCode">The DAO <c>dbType*</c> code — drives data-load literal formatting.</param>
/// <param name="SqlType">The resolved SQL Server type (e.g. <c>nvarchar(50)</c>, <c>int</c>).</param>
/// <param name="IsIdentity">True for an AutoNumber column mapped to <c>IDENTITY(1,1)</c>.</param>
/// <param name="NotNull">Whether the <c>CREATE TABLE</c> column line is <c>NOT NULL</c>.</param>
/// <param name="SqlDefault">The translated SQL default expression, or <c>null</c> for none.</param>
public sealed record MigrationResolvedColumn(
    string Name,
    int TypeCode,
    string SqlType,
    bool IsIdentity,
    bool NotNull,
    string? SqlDefault);

/// <summary>
/// A migrated table resolved to only its surviving columns (in <c>CREATE TABLE</c>
/// emission order) plus its primary key, if every key column survived.
/// </summary>
public sealed record MigrationResolvedTable(
    string Name,
    IReadOnlyList<MigrationResolvedColumn> Columns,
    IReadOnlyList<string>? PrimaryKeyColumns)
{
    /// <summary>The single identity column, if the table has one; otherwise <c>null</c>.</summary>
    public MigrationResolvedColumn? IdentityColumn
    {
        get
        {
            foreach (var c in Columns)
                if (c.IsIdentity) return c;
            return null;
        }
    }
}

/// <summary>
/// A real, enforced foreign key that survived into the migrated schema. The
/// parent→child direction is also the load-order dependency (parent rows must
/// exist before child rows reference them).
/// </summary>
public sealed record MigrationResolvedForeignKey(
    string ConstraintName,
    string ParentTable,
    string ChildTable,
    IReadOnlyList<MigrationFieldPair> FieldPairs,
    bool CascadeUpdate,
    bool CascadeDelete);

/// <summary>
/// The fully-resolved migration schema: the surviving tables/columns, the real FK
/// edges, and every honest degradation discovered while resolving. This is the
/// single source of truth consumed by the DDL emitter, the table orderer, and the
/// data-load generator, so the three never drift on "what survived".
/// </summary>
public sealed record MigrationSchema(
    string Database,
    IReadOnlyList<MigrationResolvedTable> Tables,
    IReadOnlyList<MigrationResolvedForeignKey> ForeignKeys,
    IReadOnlyList<MigrationDegradation> Degradations);
