using AccessHarvester.Core.Extraction;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic tests for <see cref="TableDdlBuilder"/>. The builder is pure
/// (string → string), so these tests need neither Access nor any COM hop.
/// </summary>
public class TableDdlBuilderTests
{
    private static TableDdlBuilder.DdlField F(
        string name,
        int typeCode,
        int? size = null,
        bool required = false,
        bool autoInc = false,
        string? defaultValue = null) =>
        new(name, typeCode, size, required, autoInc, defaultValue);

    [Fact]
    public void Build_TextNotRequired_EmitsTextWithSize()
    {
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("Col", typeCode: 10, size: 50) },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[Col] TEXT(50)", ddl);
        Assert.DoesNotContain("NOT NULL", ddl);
    }

    [Fact]
    public void Build_TextRequired_EmitsNotNull()
    {
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("FirstName", typeCode: 10, size: 50, required: true) },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[FirstName] TEXT(50) NOT NULL", ddl);
    }

    [Fact]
    public void Build_AutoIncrementLong_EmitsCounterWithoutNotNull()
    {
        // AutoNumber columns are NOT NULL by definition in Jet, but the YAML
        // baseline does NOT emit NOT NULL on COUNTER columns — we match that.
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("ClientID", typeCode: 4, size: 4, required: true, autoInc: true) },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[ClientID] COUNTER", ddl);
        Assert.DoesNotContain("COUNTER NOT NULL", ddl);
    }

    [Fact]
    public void Build_NonAutoIncrementLong_EmitsLong()
    {
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("FkCol", typeCode: 4) },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[FkCol] LONG", ddl);
        Assert.DoesNotContain("COUNTER", ddl);
    }

    [Fact]
    public void Build_DateTimeRequiredWithFunctionDefault_EmitsDefaultVerbatim()
    {
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("EnrollmentDate", typeCode: 8, required: true, defaultValue: "Date()") },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[EnrollmentDate] DATETIME NOT NULL DEFAULT Date()", ddl);
    }

    [Fact]
    public void Build_BooleanDefaultTrue_EmitsBitDefaultTrue()
    {
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("IsActive", typeCode: 1, defaultValue: "True") },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[IsActive] BIT DEFAULT True", ddl);
    }

    [Fact]
    public void Build_MemoEmitsLongText()
    {
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("Notes", typeCode: 12) },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[Notes] LONGTEXT", ddl);
    }

    [Fact]
    public void Build_PrimaryKeyEmitsConstraintAfterFields()
    {
        var ddl = TableDdlBuilder.Build(
            "tblClients",
            new[] { F("ClientID", typeCode: 4, autoInc: true) },
            primaryKey: new TableDdlBuilder.DdlPrimaryKey("PrimaryKey", new[] { "ClientID" }),
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("CONSTRAINT [PrimaryKey] PRIMARY KEY ([ClientID])", ddl);
        Assert.EndsWith(");", ddl.TrimEnd());
    }

    [Fact]
    public void Build_ForeignKeyEmitsConstraintWithCascadeUpdate()
    {
        var fk = new TableDdlBuilder.DdlForeignKey(
            Name: "rel_State_Client",
            ParentTable: "tlkpStates",
            FieldPairs: new[] { ("StateCode", "StateCode") },
            CascadeUpdate: true,
            CascadeDelete: false);

        var ddl = TableDdlBuilder.Build(
            "tblClients",
            new[] { F("StateCode", typeCode: 10, size: 2) },
            primaryKey: null,
            foreignKeys: new[] { fk });

        Assert.Contains(
            "CONSTRAINT [rel_State_Client] FOREIGN KEY ([StateCode]) REFERENCES [tlkpStates] ([StateCode]) ON UPDATE CASCADE",
            ddl);
        Assert.DoesNotContain("ON DELETE CASCADE", ddl);
    }

    [Fact]
    public void Build_ForeignKeyEmitsBothCascadesWhenSet()
    {
        var fk = new TableDdlBuilder.DdlForeignKey(
            Name: "fk_X",
            ParentTable: "P",
            FieldPairs: new[] { ("Pid", "Pid") },
            CascadeUpdate: true,
            CascadeDelete: true);

        var ddl = TableDdlBuilder.Build(
            "C",
            new[] { F("Pid", typeCode: 4) },
            primaryKey: null,
            foreignKeys: new[] { fk });

        Assert.Contains("ON UPDATE CASCADE ON DELETE CASCADE", ddl);
    }

    [Fact]
    public void Build_LinesAreCommaSeparatedExceptLast()
    {
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] {
                F("A", typeCode: 4, autoInc: true),
                F("B", typeCode: 10, size: 10),
            },
            primaryKey: new TableDdlBuilder.DdlPrimaryKey("PK", new[] { "A" }),
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        // Field A → has trailing comma; field B → has trailing comma (PK follows);
        // PK constraint → no trailing comma (last line before `);`).
        Assert.Contains("[A] COUNTER,", ddl);
        Assert.Contains("[B] TEXT(10),", ddl);
        var lines = ddl.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var lastConstraint = lines.Reverse().First(l => l.TrimStart().StartsWith("CONSTRAINT"));
        Assert.False(lastConstraint.TrimEnd().EndsWith(","),
            $"Final constraint line should not have a trailing comma: '{lastConstraint}'");
    }

    [Fact]
    public void Build_MultiColumnPrimaryKey_EmitsAllColumns()
    {
        var ddl = TableDdlBuilder.Build(
            "Junction",
            new[] { F("A", typeCode: 4), F("B", typeCode: 4) },
            primaryKey: new TableDdlBuilder.DdlPrimaryKey("PK", new[] { "A", "B" }),
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("CONSTRAINT [PK] PRIMARY KEY ([A], [B])", ddl);
    }

    [Fact]
    public void Build_MultiColumnForeignKey_EmitsAllPairs()
    {
        var fk = new TableDdlBuilder.DdlForeignKey(
            Name: "fk",
            ParentTable: "P",
            FieldPairs: new[] { ("A", "X"), ("B", "Y") },
            CascadeUpdate: false,
            CascadeDelete: false);

        var ddl = TableDdlBuilder.Build(
            "C",
            new[] { F("A", typeCode: 4), F("B", typeCode: 4) },
            primaryKey: null,
            foreignKeys: new[] { fk });

        Assert.Contains("FOREIGN KEY ([A], [B]) REFERENCES [P] ([X], [Y])", ddl);
        Assert.DoesNotContain("ON UPDATE", ddl);
        Assert.DoesNotContain("ON DELETE", ddl);
    }

    [Fact]
    public void Build_TextFieldWithoutSize_EmitsTextWithoutZeroDimension()
    {
        // Defensive: SafeInt failure → Size null. Should NOT emit "TEXT(0)" — that is
        // not valid Jet DDL.
        var ddl = TableDdlBuilder.Build(
            "T",
            new[] { F("Col", typeCode: 10, size: null) },
            primaryKey: null,
            foreignKeys: Array.Empty<TableDdlBuilder.DdlForeignKey>());

        Assert.Contains("[Col] TEXT", ddl);
        Assert.DoesNotContain("TEXT(0)", ddl);
    }

    [Fact]
    public void Build_TblClientsShapeRoundTripsKeySyntacticElements()
    {
        // A minimal slice of tblClients matching the YAML baseline. This isn't
        // a byte-for-byte equality test (whitespace and Required vs. NOT NULL
        // emit ordering differ slightly across Access versions); it verifies
        // the headline lines all appear.
        var fields = new[] {
            F("ClientID",       typeCode: 4,  size: 4,   required: true, autoInc: true),
            F("FirstName",      typeCode: 10, size: 50,  required: true),
            F("SSNLast4",       typeCode: 10, size: 4),
            F("Gender",         typeCode: 10, size: 1,   defaultValue: "\"X\""),
            F("EnrollmentDate", typeCode: 8,             required: true, defaultValue: "Date()"),
            F("IsActive",       typeCode: 1,             defaultValue: "True"),
            F("Notes",          typeCode: 12),
        };
        var pk = new TableDdlBuilder.DdlPrimaryKey("PrimaryKey", new[] { "ClientID" });
        var fk = new TableDdlBuilder.DdlForeignKey(
            Name: "rel_State_Client",
            ParentTable: "tlkpStates",
            FieldPairs: new[] { ("StateCode", "StateCode") },
            CascadeUpdate: true,
            CascadeDelete: false);

        var ddl = TableDdlBuilder.Build("tblClients", fields, pk, new[] { fk });

        Assert.StartsWith("CREATE TABLE [tblClients] (", ddl);
        Assert.Contains("[ClientID] COUNTER", ddl);
        Assert.Contains("[FirstName] TEXT(50) NOT NULL", ddl);
        Assert.Contains("[SSNLast4] TEXT(4)", ddl);
        Assert.Contains("[Gender] TEXT(1) DEFAULT \"X\"", ddl);
        Assert.Contains("[EnrollmentDate] DATETIME NOT NULL DEFAULT Date()", ddl);
        Assert.Contains("[IsActive] BIT DEFAULT True", ddl);
        Assert.Contains("[Notes] LONGTEXT", ddl);
        Assert.Contains("CONSTRAINT [PrimaryKey] PRIMARY KEY ([ClientID])", ddl);
        Assert.Contains(
            "CONSTRAINT [rel_State_Client] FOREIGN KEY ([StateCode]) REFERENCES [tlkpStates] ([StateCode]) ON UPDATE CASCADE",
            ddl);
        Assert.EndsWith(");", ddl.TrimEnd());
    }
}
