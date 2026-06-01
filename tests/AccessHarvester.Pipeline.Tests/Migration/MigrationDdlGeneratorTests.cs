using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Migration;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// Hermetic tests for the Session-1 SQL Server DDL generation surface
/// (<see cref="SqlServerTypeMapper"/>, <see cref="DefaultValueTranslator"/>,
/// <see cref="SqlServerDdlGenerator"/>). Inputs are synthetic in-memory schemas;
/// no file I/O. The structure/cases originate from a routed test-generation pass
/// (gemini-pro) and were reconciled against the real API + degradation reasons.
/// </summary>
public class MigrationDdlGeneratorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MigrationField Field(
        string name, int typeCode, int? size = null, bool required = false,
        bool isAutoIncrement = false, string? defaultValue = null, int ordinal = 0) =>
        new(name, typeCode, $"Type{typeCode}", size, required, isAutoIncrement, defaultValue, ordinal);

    private static MigrationTable Table(
        string name, IReadOnlyList<MigrationField> fields,
        IReadOnlyList<MigrationIndex>? indexes = null, bool isSystem = false, bool isLinked = false) =>
        new(name, isSystem, isLinked, fields, indexes ?? Array.Empty<MigrationIndex>());

    private static ClassificationDocument MultiValueClassification(string table, string column)
    {
        var col = new ClassifiedColumn(
            Name: column, OrdinalPosition: 0, DataType: "Text", IsAutoNumber: false,
            IsPrimaryKey: false, IsForeignKey: false, FkTarget: null, IsMemo: false,
            IsOleObject: false, IsMultiValue: true, IsRequired: false, RowCount: 0,
            NullCount: 0, Cardinality: 0, UniquenessRatio: 0, RelativeCardinality: 0,
            PiiNameMatch: false, PiiValueMatch: false, ValueFormatPattern: null,
            GuessedCategory: ColumnCategory.GenericNonSensitive, Confidence: 0,
            FiredHeuristics: Array.Empty<string>(),
            HumanOverride: new ColumnHumanOverride(null, null, false, null, ""));
        var tbl = new ClassifiedTable(
            Name: table, IsLinkedTable: false, RowCount: 0, LookupGuess: false,
            NonKeyNonSystemColumnCount: 0, ReferencedByFkCount: 0, TableHint: "entity",
            Confidence: 0, FiredHeuristics: Array.Empty<string>(),
            HumanOverride: new TableHumanOverride(null, null, ""),
            Columns: new[] { col });
        var config = new ScanConfig(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 0);
        return new ClassificationDocument("1.0", "test", DateTimeOffset.UnixEpoch, config, new[] { tbl });
    }

    // ── Type map ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, null, false, true, "bit", false, null)]
    [InlineData(2, null, false, true, "tinyint", false, null)]
    [InlineData(3, null, false, true, "smallint", false, null)]
    [InlineData(4, null, false, true, "int", false, null)]
    [InlineData(4, null, true, true, "int", true, null)]               // autonumber -> identity
    [InlineData(16, null, false, true, "bigint", false, null)]
    [InlineData(5, null, false, true, "money", false, null)]
    [InlineData(6, null, false, true, "real", false, null)]
    [InlineData(7, null, false, true, "float", false, null)]
    [InlineData(21, null, false, true, "float", false, null)]
    [InlineData(8, null, false, true, "datetime2", false, null)]
    [InlineData(15, null, false, true, "uniqueidentifier", false, null)]
    [InlineData(12, null, false, true, "nvarchar(max)", false, null)]
    [InlineData(11, null, false, true, "varbinary(max)", false, null)]
    [InlineData(10, 50, false, true, "nvarchar(50)", false, null)]
    [InlineData(10, null, false, true, "nvarchar(255)", false, null)]  // text, no size -> default 255
    [InlineData(10, 0, false, true, "nvarchar(255)", false, null)]     // text, zero size -> default 255
    [InlineData(18, 10, false, true, "nchar(10)", false, null)]
    [InlineData(9, 8, false, true, "binary(8)", false, null)]          // binary, sized
    [InlineData(17, 16, false, true, "varbinary(16)", false, null)]    // varbinary, sized
    [InlineData(17, null, false, true, "varbinary(max)", false, null)] // varbinary, no size -> max
    [InlineData(19, null, false, true, "decimal(18,4)", false, MigrationDegradationKind.AssumedTypePrecision)]
    [InlineData(20, null, false, true, "decimal(18,4)", false, MigrationDegradationKind.AssumedTypePrecision)]
    [InlineData(101, null, false, false, "", false, MigrationDegradationKind.UnmappableColumnType)] // attachment/multi-value
    [InlineData(999, null, false, false, "", false, MigrationDegradationKind.UnmappableColumnType)] // unknown code
    public void Map_translates_each_type_branch(
        int typeCode, int? size, bool isAutoIncrement,
        bool expectedMappable, string expectedSqlType, bool expectedIsIdentity,
        MigrationDegradationKind? expectedDegradationKind)
    {
        var mapping = SqlServerTypeMapper.Map(Field("Col", typeCode, size: size, isAutoIncrement: isAutoIncrement));

        Assert.Equal(expectedMappable, mapping.Mappable);
        if (expectedMappable) Assert.Equal(expectedSqlType, mapping.SqlType);
        Assert.Equal(expectedIsIdentity, mapping.IsIdentity);
        Assert.Equal(expectedDegradationKind, mapping.DegradationKind);
        if (mapping.DegradationKind is not null) Assert.False(string.IsNullOrWhiteSpace(mapping.DegradationReason));
    }

    // ── Default-value translation ─────────────────────────────────────────────

    [Theory]
    [InlineData("Date()", true, "CAST(GETDATE() AS date)")]
    [InlineData("Now()", true, "SYSDATETIME()")]
    [InlineData("True", true, "1")]
    [InlineData("Yes", true, "1")]
    [InlineData("False", true, "0")]
    [InlineData("No", true, "0")]
    [InlineData("0", true, "0")]
    [InlineData("-123", true, "-123")]
    [InlineData("3.14", true, "3.14")]
    [InlineData("\"Pending\"", true, "'Pending'")]
    [InlineData("\"It's a quote\"", true, "'It''s a quote'")]
    [InlineData("#2020-01-02#", true, "'2020-01-02'")]                 // strict ISO date literal
    [InlineData("#2020-01-02 13:45:00#", true, "'2020-01-02 13:45:00'")] // strict ISO date-time literal
    [InlineData("#1/2/2020#", false, null)]                            // locale-ambiguous -> not translated
    [InlineData("IIf([x], 1, 2)", false, null)]
    [InlineData("=GetStatus()", false, null)]
    [InlineData(null, false, null)]
    public void TryTranslate_handles_common_defaults(string? accessDefault, bool expectedSuccess, string? expectedSql)
    {
        bool ok = DefaultValueTranslator.TryTranslate(accessDefault, out var sqlDefault);

        Assert.Equal(expectedSuccess, ok);
        if (expectedSuccess) Assert.Equal(expectedSql, sqlDefault);
        else Assert.True(string.IsNullOrEmpty(sqlDefault));
    }

    // ── CREATE TABLE ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateDdl_emits_columns_pk_and_translated_defaults()
    {
        var fields = new[]
        {
            Field("ID", 4, isAutoIncrement: true, required: true, ordinal: 0),
            Field("Name", 10, size: 50, required: true, defaultValue: "\"Default Name\"", ordinal: 1),
            Field("Description", 10, size: null, required: false, ordinal: 2),
            Field("IsActive", 1, required: true, defaultValue: "True", ordinal: 3),
        };
        var pk = new MigrationIndex("PrimaryKey", true, true, new[] { "ID" });
        var inputs = new MigrationInputs("TestDb",
            new[] { Table("Users", fields, new[] { pk }) },
            Array.Empty<MigrationRelationship>(), null, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.Contains("CREATE TABLE [Users]", result.Sql);
        Assert.Contains("[ID] int IDENTITY(1,1) NOT NULL", result.Sql);
        Assert.Contains("[Name] nvarchar(50) NOT NULL DEFAULT ('Default Name')", result.Sql);
        Assert.Contains("[Description] nvarchar(255) NULL", result.Sql);
        Assert.Contains("[IsActive] bit NOT NULL DEFAULT (1)", result.Sql);
        Assert.Contains("CONSTRAINT [PK_Users] PRIMARY KEY ([ID])", result.Sql);
        Assert.Contains("GO", result.Sql);
        Assert.Empty(result.Degradations);
    }

    [Fact]
    public void GenerateDdl_does_not_emit_default_on_identity_column()
    {
        // An autonumber column never carries a DEFAULT even if one is recorded.
        var fields = new[] { Field("ID", 4, isAutoIncrement: true, required: true, defaultValue: "1") };
        var inputs = new MigrationInputs("Db", new[] { Table("T", fields) },
            Array.Empty<MigrationRelationship>(), null, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.Contains("[ID] int IDENTITY(1,1) NOT NULL", result.Sql);
        string idLine = result.Sql.Split('\n').First(l => l.Contains("[ID]"));
        Assert.DoesNotContain("DEFAULT", idLine);
    }

    // ── Degradation paths ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateDdl_flags_degradations_and_never_fabricates()
    {
        var classification = MultiValueClassification("Data", "MultiValueTags");
        var tables = new[]
        {
            Table("Data", new[]
            {
                Field("ID", 4, isAutoIncrement: true, required: true, ordinal: 0),
                Field("Attachment", 101, ordinal: 1),                              // unmappable
                Field("MultiValueTags", 12, ordinal: 2),                           // multi-value via classification
                Field("Status", 10, size: 20, defaultValue: "=GetStatus()", ordinal: 3), // untranslated default
            }),
            Table("ExternalData", new[] { Field("ID", 4) }, isLinked: true),
            Table("MSysObjects", new[] { Field("ID", 4) }, isSystem: true),
        };
        var inputs = new MigrationInputs("Db", tables, Array.Empty<MigrationRelationship>(), classification, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        // Unmappable column omitted + flagged.
        Assert.DoesNotContain("[Attachment]", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.UnmappableColumnType && d.Table == "Data" && d.Column == "Attachment");

        // Multi-value column omitted + flagged.
        Assert.DoesNotContain("[MultiValueTags]", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.MultiValueColumn && d.Table == "Data" && d.Column == "MultiValueTags");

        // Untranslated default: column kept, default dropped, flagged.
        Assert.Contains("[Status] nvarchar(20) NULL", result.Sql);
        Assert.DoesNotContain("DEFAULT", result.Sql.Split('\n').First(l => l.Contains("[Status]")));
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.UntranslatedDefault && d.Table == "Data" && d.Column == "Status");

        // Linked table skipped + flagged.
        Assert.DoesNotContain("CREATE TABLE [ExternalData]", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.LinkedTableSkipped && d.Table == "ExternalData");

        // System table excluded silently (no DDL, no degradation).
        Assert.DoesNotContain("CREATE TABLE [MSysObjects]", result.Sql);
        Assert.DoesNotContain(result.Degradations, d => d.Table == "MSysObjects");
    }

    [Fact]
    public void GenerateDdl_omits_primary_key_when_a_key_column_is_degraded()
    {
        var fields = new[]
        {
            Field("AttachKey", 101, ordinal: 0),     // unmappable PK member
            Field("Name", 10, size: 30, ordinal: 1),
        };
        var pk = new MigrationIndex("PrimaryKey", true, true, new[] { "AttachKey" });
        var inputs = new MigrationInputs("Db", new[] { Table("T", fields, new[] { pk }) },
            Array.Empty<MigrationRelationship>(), null, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.DoesNotContain("PRIMARY KEY", result.Sql);
        Assert.Contains(result.Degradations, d => d.Table == "T" && d.Reason.Contains("Primary key omitted"));
    }

    // ── Foreign keys ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateDdl_emits_enforced_fk_after_tables_with_cascades()
    {
        var tables = new[]
        {
            Table("Departments", new[] { Field("DeptID", 4, isAutoIncrement: true) },
                new[] { new MigrationIndex("PrimaryKey", true, true, new[] { "DeptID" }) }),
            Table("Employees", new[] { Field("EmpID", 4, isAutoIncrement: true), Field("DeptID", 4, ordinal: 1) },
                new[] { new MigrationIndex("PrimaryKey", true, true, new[] { "EmpID" }) }),
            Table("OrphanTable", new[] { Field("ID", 4) }, isLinked: true),
        };
        var relationships = new[]
        {
            new MigrationRelationship("rel_Emp_Dept", "Departments", "Employees",
                new[] { new MigrationFieldPair("DeptID", "DeptID") }, true, true, true),
            new MigrationRelationship("rel_Bad_Ref", "Departments", "Employees",
                new[] { new MigrationFieldPair("DeptID", "DeptID") }, false, false, false),
            new MigrationRelationship("rel_Orphan_Ref", "OrphanTable", "Employees",
                new[] { new MigrationFieldPair("ID", "EmpID") }, true, false, false),
        };
        var inputs = new MigrationInputs("Db", tables, relationships, null, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.Contains("CREATE TABLE [Departments]", result.Sql);
        Assert.Contains("CREATE TABLE [Employees]", result.Sql);
        Assert.DoesNotContain("CREATE TABLE [OrphanTable]", result.Sql);

        Assert.Contains(
            "ALTER TABLE [Employees] ADD CONSTRAINT [FK_Employees_Departments] FOREIGN KEY ([DeptID]) REFERENCES [Departments] ([DeptID]) ON UPDATE CASCADE ON DELETE CASCADE",
            result.Sql);

        // FK comes after the CREATE TABLEs.
        Assert.True(result.Sql.IndexOf("ALTER TABLE", StringComparison.Ordinal)
                  > result.Sql.IndexOf("CREATE TABLE", StringComparison.Ordinal));

        // Non-enforced relationship: dropped + flagged.
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.ForeignKeyDropped
            && d.Reason.Contains("rel_Bad_Ref")
            && d.Reason.Contains("does not enforce referential integrity"));

        // Relationship to a non-migrated (linked) table: dropped + flagged.
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.ForeignKeyDropped
            && d.Reason.Contains("rel_Orphan_Ref")
            && d.Reason.Contains("not in the migrated set"));
    }

    [Fact]
    public void GenerateDdl_is_deterministic()
    {
        var tables = new[]
        {
            Table("Departments", new[] { Field("DeptID", 4, isAutoIncrement: true) },
                new[] { new MigrationIndex("PrimaryKey", true, true, new[] { "DeptID" }) }),
            Table("Employees", new[] { Field("EmpID", 4, isAutoIncrement: true), Field("DeptID", 4, ordinal: 1) },
                new[] { new MigrationIndex("PrimaryKey", true, true, new[] { "EmpID" }) }),
        };
        var relationships = new[]
        {
            new MigrationRelationship("rel_Emp_Dept", "Departments", "Employees",
                new[] { new MigrationFieldPair("DeptID", "DeptID") }, true, true, false),
        };
        var inputs = new MigrationInputs("Db", tables, relationships, null, null);

        Assert.Equal(
            SqlServerDdlGenerator.GenerateDdl(inputs).Sql,
            SqlServerDdlGenerator.GenerateDdl(inputs).Sql);
    }

    [Fact]
    public void GenerateDdl_drops_fk_when_a_referenced_column_was_not_migrated()
    {
        var tables = new[]
        {
            Table("Parent", new[] { Field("PID", 4, isAutoIncrement: true) },
                new[] { new MigrationIndex("PrimaryKey", true, true, new[] { "PID" }) }),
            Table("Child", new[] { Field("CID", 4, isAutoIncrement: true), Field("PID", 4, ordinal: 1) }),
        };
        // Child-side column "MissingCol" does not exist on Child.
        var relationships = new[]
        {
            new MigrationRelationship("rel_missing", "Parent", "Child",
                new[] { new MigrationFieldPair("PID", "MissingCol") }, true, false, false),
        };
        var inputs = new MigrationInputs("Db", tables, relationships, null, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.DoesNotContain("ADD CONSTRAINT", result.Sql);
        Assert.Contains(result.Degradations, d =>
            d.Kind == MigrationDegradationKind.ForeignKeyDropped && d.Reason.Contains("rel_missing"));
    }

    [Fact]
    public void GenerateDdl_uniquifies_duplicate_foreign_key_names()
    {
        var tables = new[]
        {
            Table("Departments", new[] { Field("DeptID", 4, isAutoIncrement: true) },
                new[] { new MigrationIndex("PrimaryKey", true, true, new[] { "DeptID" }) }),
            Table("Employees", new[]
            {
                Field("EmpID", 4, isAutoIncrement: true),
                Field("DeptID", 4, ordinal: 1),
                Field("BackupDeptID", 4, ordinal: 2),
            }, new[] { new MigrationIndex("PrimaryKey", true, true, new[] { "EmpID" }) }),
        };
        // Two enforced relationships, both Employees -> Departments, would collide on FK name.
        var relationships = new[]
        {
            new MigrationRelationship("rel_primary", "Departments", "Employees",
                new[] { new MigrationFieldPair("DeptID", "DeptID") }, true, false, false),
            new MigrationRelationship("rel_backup", "Departments", "Employees",
                new[] { new MigrationFieldPair("DeptID", "BackupDeptID") }, true, false, false),
        };
        var inputs = new MigrationInputs("Db", tables, relationships, null, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.Contains("[FK_Employees_Departments]", result.Sql);
        Assert.Contains("[FK_Employees_Departments_2]", result.Sql);
    }

    [Fact]
    public void GenerateDdl_brackets_and_escapes_identifiers_with_closing_brackets()
    {
        var fields = new[] { Field("Col]X", 10, size: 10, required: true, ordinal: 0) };
        var pk = new MigrationIndex("PrimaryKey", true, true, new[] { "Col]X" });
        var inputs = new MigrationInputs("Db",
            new[] { Table("Weird]Name", fields, new[] { pk }) },
            Array.Empty<MigrationRelationship>(), null, null);

        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.Contains("CREATE TABLE [Weird]]Name]", result.Sql);
        Assert.Contains("[Col]]X] nvarchar(10) NOT NULL", result.Sql);
        Assert.Contains("PRIMARY KEY ([Col]]X])", result.Sql);
    }

    [Fact]
    public void GenerateDdl_uses_fixed_lf_newlines_for_cross_platform_byte_stability()
    {
        var inputs = new MigrationInputs("Db",
            new[] { Table("T", new[] { Field("Id", 4, isAutoIncrement: true, required: true) }) },
            Array.Empty<MigrationRelationship>(), null, null);

        var sql = SqlServerDdlGenerator.GenerateDdl(inputs).Sql;

        Assert.DoesNotContain("\r\n", sql); // never platform CRLF
        Assert.Contains("\n", sql);
    }
}
