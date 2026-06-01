using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>The DDL portion of a migration: the script text plus any honest degradations.</summary>
public sealed record MigrationDdlResult(
    string Sql,
    IReadOnlyList<MigrationDegradation> Degradations);

/// <summary>
/// Generates deterministic SQL Server schema DDL (<c>CREATE TABLE</c> + primary
/// keys, then <c>ALTER TABLE ... ADD FOREIGN KEY</c>) from harvested Access
/// metadata. Columns and tables that cannot migrate cleanly are flagged as
/// <see cref="MigrationDegradation"/>s and omitted — never silently dropped,
/// never fabricated.
/// <para>
/// This is a thin wrapper that resolves the schema once
/// (<see cref="MigrationSchemaResolver"/>) and emits the DDL text
/// (<see cref="SqlServerDdlEmitter"/>). The same resolved schema drives the
/// FK-safe table ordering and the data-load generation, so the three never drift
/// on "what survived". Foreign keys are emitted as post-create <c>ALTER TABLE</c>
/// statements so the <c>CREATE TABLE</c> order is independent of the FK graph; the
/// FK-safe <em>data-load</em> ordering is handled by
/// <see cref="TableDependencyOrderer"/>.
/// </para>
/// </summary>
public static class SqlServerDdlGenerator
{
    public static MigrationDdlResult GenerateDdl(MigrationInputs inputs)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        var schema = MigrationSchemaResolver.Resolve(inputs);
        return new MigrationDdlResult(SqlServerDdlEmitter.Emit(schema), schema.Degradations);
    }
}
