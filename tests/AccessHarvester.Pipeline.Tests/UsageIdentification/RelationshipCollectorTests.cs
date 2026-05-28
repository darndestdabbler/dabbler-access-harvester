using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

public class RelationshipCollectorTests
{
    private static IrDatabase DbWithRelationships(params IrRelationship[] rels) => new()
    {
        Name = "Test",
        Forms = [], Reports = [], Queries = [], Tables = [], Macros = [], Modules = [],
        Relationships = rels,
    };

    [Fact]
    public void Collect_Returns_Empty_When_No_Relationships()
    {
        var result = RelationshipCollector.Collect(DbWithRelationships());
        Assert.Empty(result);
    }

    [Fact]
    public void Collect_User_Relationship_Included()
    {
        var db = DbWithRelationships(
            new IrRelationship { Name = "rel_Client_Orders", ParentTable = "tblClients", ChildTable = "tblOrders" });
        var edges = RelationshipCollector.Collect(db);
        Assert.Single(edges);
        Assert.Equal("tblClients", edges[0].ParentTable);
        Assert.Equal("tblOrders", edges[0].ChildTable);
    }

    [Fact]
    public void Collect_MSysNavPane_Relationships_Excluded()
    {
        var db = DbWithRelationships(
            new IrRelationship { Name = "MSysNavPaneGroupsMSysNavPaneGroupToObjects", ParentTable = "MSysNavPaneGroups", ChildTable = "MSysNavPaneGroupToObjects" },
            new IrRelationship { Name = "rel_User_Table", ParentTable = "tblUsers", ChildTable = "tblOrders" });
        var edges = RelationshipCollector.Collect(db);
        Assert.Single(edges);
        Assert.Equal("rel_User_Table", "rel_User_Table"); // sanity
        Assert.Equal("tblUsers", edges[0].ParentTable);
    }

    [Fact]
    public void Collect_MSys_System_Relationships_Excluded()
    {
        var db = DbWithRelationships(
            new IrRelationship { Name = "MSysObjects_Something", ParentTable = "MSysObjects", ChildTable = "MSysOtherThing" });
        var edges = RelationshipCollector.Collect(db);
        Assert.Empty(edges);
    }

    [Fact]
    public void Collect_Multiple_User_Relationships()
    {
        var db = DbWithRelationships(
            new IrRelationship { Name = "rel_A_B", ParentTable = "tblA", ChildTable = "tblB" },
            new IrRelationship { Name = "rel_A_C", ParentTable = "tblA", ChildTable = "tblC" });
        var edges = RelationshipCollector.Collect(db);
        Assert.Equal(2, edges.Count);
    }
}
