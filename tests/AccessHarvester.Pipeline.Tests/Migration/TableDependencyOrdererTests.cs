using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Migration;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// Hermetic tests for <see cref="TableDependencyOrderer"/>: parents-before-children
/// topological ordering, multi-FK dedupe, self-reference handling, deterministic
/// tie-breaking, and cycle detection that still emits every table.
/// </summary>
public class TableDependencyOrdererTests
{
    private static MigrationResolvedTable T(string name, params string[] cols) =>
        new(name,
            cols.Select(c => new MigrationResolvedColumn(c, 4, "int", false, true, null)).ToList(),
            null);

    private static MigrationResolvedForeignKey Fk(string parent, string child) =>
        new($"FK_{child}_{parent}", parent, child,
            new[] { new MigrationFieldPair(parent + "Id", parent + "Id") }, false, false);

    private static int Pos(TableOrderResult r, string name) =>
        r.OrderedTables.ToList().FindIndex(n => n == name);

    [Fact]
    public void Order_places_parents_before_children()
    {
        var tables = new[] { T("Employees", "Id"), T("Departments", "Id"), T("Companies", "Id") };
        var fks = new[] { Fk("Departments", "Employees"), Fk("Companies", "Departments") };

        var result = TableDependencyOrderer.Order(tables, fks);

        Assert.Empty(result.Degradations);
        Assert.Equal(3, result.OrderedTables.Count);
        Assert.True(Pos(result, "Companies") < Pos(result, "Departments"));
        Assert.True(Pos(result, "Departments") < Pos(result, "Employees"));
    }

    [Fact]
    public void Order_breaks_ties_by_ordinal_name_for_determinism()
    {
        var tables = new[] { T("Zebra", "Id"), T("Apple", "Id"), T("Mango", "Id") };

        var result = TableDependencyOrderer.Order(tables, Array.Empty<MigrationResolvedForeignKey>());

        Assert.Equal(new[] { "Apple", "Mango", "Zebra" }, result.OrderedTables);
    }

    [Fact]
    public void Order_dedupes_multiple_foreign_keys_between_the_same_pair()
    {
        var tables = new[] { T("Parent", "Id"), T("Child", "Id") };
        // Two distinct FKs Parent -> Child must count as a single ordering edge.
        var fks = new[]
        {
            new MigrationResolvedForeignKey("FK_Child_Parent", "Parent", "Child",
                new[] { new MigrationFieldPair("Id", "PId1") }, false, false),
            new MigrationResolvedForeignKey("FK_Child_Parent_2", "Parent", "Child",
                new[] { new MigrationFieldPair("Id", "PId2") }, false, false),
        };

        var result = TableDependencyOrderer.Order(tables, fks);

        Assert.Empty(result.Degradations);
        Assert.Equal(new[] { "Parent", "Child" }, result.OrderedTables);
    }

    [Fact]
    public void Order_ignores_self_referencing_foreign_keys()
    {
        var tables = new[] { T("Tree", "Id", "ParentId") };
        var fks = new[] { Fk("Tree", "Tree") };

        var result = TableDependencyOrderer.Order(tables, fks);

        Assert.Empty(result.Degradations);
        Assert.Equal(new[] { "Tree" }, result.OrderedTables);
    }

    [Fact]
    public void Order_detects_a_two_table_cycle_and_still_emits_both_tables()
    {
        var tables = new[] { T("A", "Id"), T("B", "Id") };
        var fks = new[] { Fk("A", "B"), Fk("B", "A") };

        var result = TableDependencyOrderer.Order(tables, fks);

        Assert.Contains(result.Degradations, d => d.Kind == MigrationDegradationKind.ForeignKeyCycle);
        Assert.Contains("A", result.OrderedTables);
        Assert.Contains("B", result.OrderedTables);
        Assert.Equal(2, result.OrderedTables.Count);
        // The cycle degradation names both members.
        var cycle = result.Degradations.Single(d => d.Kind == MigrationDegradationKind.ForeignKeyCycle);
        Assert.Contains("[A]", cycle.Reason);
        Assert.Contains("[B]", cycle.Reason);
    }

    [Fact]
    public void Order_emits_acyclic_prefix_before_appending_cycle_members()
    {
        var tables = new[] { T("Root", "Id"), T("A", "Id"), T("B", "Id") };
        // Root is an acyclic parent of A; A <-> B form a cycle.
        var fks = new[] { Fk("Root", "A"), Fk("A", "B"), Fk("B", "A") };

        var result = TableDependencyOrderer.Order(tables, fks);

        Assert.Single(result.Degradations);
        Assert.Equal(MigrationDegradationKind.ForeignKeyCycle, result.Degradations[0].Kind);
        Assert.Equal("Root", result.OrderedTables[0]);              // acyclic root first
        Assert.Equal(3, result.OrderedTables.Count);
        Assert.Contains("A", result.OrderedTables);
        Assert.Contains("B", result.OrderedTables);
    }

    [Fact]
    public void Order_names_only_the_true_cycle_and_keeps_downstream_children_after_it()
    {
        // A <-> B is the cycle; AA merely depends on B (not part of the cycle).
        var tables = new[] { T("A", "Id"), T("B", "Id"), T("AA", "Id") };
        var fks = new[] { Fk("A", "B"), Fk("B", "A"), Fk("B", "AA") };

        var result = TableDependencyOrderer.Order(tables, fks);

        // Exactly one cycle degradation, naming ONLY the true SCC members A and B.
        var cycle = Assert.Single(result.Degradations);
        Assert.Equal(MigrationDegradationKind.ForeignKeyCycle, cycle.Kind);
        Assert.Contains("[A]", cycle.Reason);
        Assert.Contains("[B]", cycle.Reason);
        Assert.DoesNotContain("AA", cycle.Reason);

        // AA is not misreported as cyclic, and it still loads AFTER its parent B.
        Assert.Equal(3, result.OrderedTables.Count);
        Assert.True(Pos(result, "B") < Pos(result, "AA"));
    }
}
