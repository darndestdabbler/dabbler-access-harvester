using System;
using System.Collections.Generic;
using System.IO;
using AccessHarvester.Pipeline.Migration;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// Integration tests for <see cref="SqlServerMigrationGenerator"/>: the schema DDL,
/// the FK-safe data-load order, the IDENTITY framing, and the merged degradation
/// list are all produced from one resolved schema, so they stay consistent with
/// each other.
/// </summary>
public class SqlServerMigrationGeneratorTests
{
    private static MigrationField Field(
        string name, int typeCode, int? size = null, bool required = false,
        bool isAutoIncrement = false, int ordinal = 0) =>
        new(name, typeCode, $"Type{typeCode}", size, required, isAutoIncrement, null, ordinal);

    private static MigrationTable Table(
        string name, IReadOnlyList<MigrationField> fields,
        IReadOnlyList<MigrationIndex>? indexes = null, bool isLinked = false) =>
        new(name, false, isLinked, fields, indexes ?? Array.Empty<MigrationIndex>());

    private static MigrationIndex Pk(params string[] cols) => new("PrimaryKey", true, true, cols);

    private static Func<string, TextReader?> Files(Dictionary<string, string> files) =>
        name => files.TryGetValue(name, out var s) ? new StringReader(s) : null;

    /// <summary>Departments (parent) ← Employees (child), enforced FK on DeptID.</summary>
    private static MigrationInputs ParentChildInputs(bool linkedExtra = false)
    {
        var tables = new List<MigrationTable>
        {
            Table("Departments", new[] { Field("DeptID", 4, isAutoIncrement: true, required: true) }, new[] { Pk("DeptID") }),
            Table("Employees", new[]
            {
                Field("EmpID", 4, isAutoIncrement: true, required: true, ordinal: 0),
                Field("Name", 10, size: 50, ordinal: 1),
                Field("DeptID", 4, ordinal: 2),
            }, new[] { Pk("EmpID") }),
        };
        if (linkedExtra)
            tables.Add(Table("LinkedThing", new[] { Field("Id", 4) }, isLinked: true));

        var rels = new[]
        {
            new MigrationRelationship("rel_Emp_Dept", "Departments", "Employees",
                new[] { new MigrationFieldPair("DeptID", "DeptID") }, true, false, false),
        };
        return new MigrationInputs("HR", tables, rels, null, null);
    }

    [Fact]
    public void Generate_produces_ddl_data_and_a_parent_before_child_order()
    {
        var files = new Dictionary<string, string>
        {
            ["Departments"] = "DeptID\r\n1\r\n2\r\n",
            ["Employees"] = "EmpID,Name,DeptID\r\n10,Alice,1\r\n11,Bob,2\r\n",
        };

        var result = SqlServerMigrationGenerator.Generate(ParentChildInputs(), Files(files));

        // DDL: both tables + the enforced FK.
        Assert.Contains("CREATE TABLE [Departments]", result.DdlSql);
        Assert.Contains("CREATE TABLE [Employees]", result.DdlSql);
        Assert.Contains("ADD CONSTRAINT [FK_Employees_Departments] FOREIGN KEY ([DeptID]) REFERENCES [Departments] ([DeptID])", result.DdlSql);

        // Order: parent first.
        Assert.Equal(new[] { "Departments", "Employees" }, result.TableLoadOrder);

        // Data: parent rows before child rows; identity framing on both.
        Assert.Contains("SET IDENTITY_INSERT [Departments] ON;", result.DataSql);
        Assert.Contains("INSERT INTO [Departments] ([DeptID]) VALUES (1);", result.DataSql);
        Assert.Contains("INSERT INTO [Employees] ([EmpID], [Name], [DeptID]) VALUES (10, N'Alice', 1);", result.DataSql);
        Assert.True(result.DataSql.IndexOf("[Departments]", StringComparison.Ordinal)
                  < result.DataSql.IndexOf("[Employees]", StringComparison.Ordinal));

        Assert.Empty(result.Degradations);
    }

    [Fact]
    public void Generate_merges_schema_and_dataload_degradations_sorted()
    {
        var files = new Dictionary<string, string>
        {
            ["Departments"] = "DeptID\r\n1\r\n",
            // Employees CSV missing entirely -> MissingDataFile degradation from the data phase.
        };

        // linkedExtra=true -> a LinkedTableSkipped degradation from the schema phase.
        var result = SqlServerMigrationGenerator.Generate(ParentChildInputs(linkedExtra: true), Files(files));

        Assert.Contains(result.Degradations, d => d.Kind == MigrationDegradationKind.LinkedTableSkipped && d.Table == "LinkedThing");
        Assert.Contains(result.Degradations, d => d.Kind == MigrationDegradationKind.MissingDataFile && d.Table == "Employees");

        // Merged list is sorted by (Table, Column, Kind, Reason) ordinal — Employees before LinkedThing.
        var tables = new List<string>();
        foreach (var d in result.Degradations) tables.Add(d.Table);
        int emp = tables.IndexOf("Employees");
        int linked = tables.IndexOf("LinkedThing");
        Assert.True(emp >= 0 && linked >= 0 && emp < linked);
    }

    [Fact]
    public void Generate_is_deterministic()
    {
        var files = new Dictionary<string, string>
        {
            ["Departments"] = "DeptID\r\n1\r\n",
            ["Employees"] = "EmpID,Name,DeptID\r\n10,Alice,1\r\n",
        };

        var a = SqlServerMigrationGenerator.Generate(ParentChildInputs(), Files(files));
        var b = SqlServerMigrationGenerator.Generate(ParentChildInputs(), Files(files));

        Assert.Equal(a.DdlSql, b.DdlSql);
        Assert.Equal(a.DataSql, b.DataSql);
        Assert.Equal(a.TableLoadOrder, b.TableLoadOrder);
    }
}
