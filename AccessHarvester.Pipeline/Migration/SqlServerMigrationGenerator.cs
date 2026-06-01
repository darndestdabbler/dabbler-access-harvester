using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// A complete SQL Server migration: the schema DDL, the FK-safe data-load, the
/// table load order, and the merged, byte-stable degradation list.
/// </summary>
public sealed record MigrationScriptResult(
    string DdlSql,
    string DataSql,
    IReadOnlyList<string> TableLoadOrder,
    IReadOnlyList<MigrationDegradation> Degradations);

/// <summary>
/// End-to-end migration generator: resolves the schema once
/// (<see cref="MigrationSchemaResolver"/>), emits the DDL
/// (<see cref="SqlServerDdlEmitter"/>), computes the FK-safe load order
/// (<see cref="TableDependencyOrderer"/>), and generates the data-load
/// (<see cref="SqlServerDataLoadGenerator"/>) — all from the single resolved
/// schema so the schema, the ordering, and the data never drift. Degradations from
/// all three phases are merged and sorted into one byte-stable list for the
/// Session-3 manifest.
/// </summary>
public static class SqlServerMigrationGenerator
{
    /// <summary>
    /// Generates the full migration. <paramref name="openTableCsv"/> returns a
    /// reader over a table's sanitized CSV, or <c>null</c> when none exists.
    /// </summary>
    public static MigrationScriptResult Generate(MigrationInputs inputs, Func<string, TextReader?> openTableCsv)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        if (openTableCsv is null) throw new ArgumentNullException(nameof(openTableCsv));

        var schema = MigrationSchemaResolver.Resolve(inputs);
        string ddl = SqlServerDdlEmitter.Emit(schema);

        var order = TableDependencyOrderer.Order(schema.Tables, schema.ForeignKeys);
        var data = SqlServerDataLoadGenerator.Generate(
            schema.Database, schema.Tables, order.OrderedTables, openTableCsv);

        var degradations = schema.Degradations
            .Concat(order.Degradations)
            .Concat(data.Degradations)
            .OrderBy(d => d, DegradationComparer.Instance)
            .ToList();

        return new MigrationScriptResult(ddl, data.Sql, order.OrderedTables, degradations);
    }

    private sealed class DegradationComparer : IComparer<MigrationDegradation>
    {
        public static readonly DegradationComparer Instance = new();
        public int Compare(MigrationDegradation? a, MigrationDegradation? b) =>
            MigrationSchemaResolver.CompareDegradations(a!, b!);
    }
}
