using System.Linq;
using AccessHarvester.Access;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="RelationshipMetadataExtractor"/>.
/// Verifies the directional Parent/Child labelling, the bitmask-driven flag
/// decoding, and the JoinType derivation.
/// </summary>
public class RelationshipMetadataExtractorTests
{
    [Fact]
    public void Extract_LiftsCoreFieldsAndPreservesAttributes()
    {
        const string raw = """
        {
          "Name": "CustomersOrders",
          "Type": "Relationship",
          "ParentTable": "Customers",
          "ChildTable": "Orders",
          "Attributes": 4352,
          "AttributesDecoded": ["UpdateCascade", "DeleteCascade"],
          "Fields": [
            { "ParentField": "CustomerId", "ChildField": "CustomerId" }
          ]
        }
        """;
        // 4352 = 0x1100 = UpdateCascade (0x100) | DeleteCascade (0x1000)

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.Equal("CustomersOrders", meta.Name);
        Assert.Equal("Customers", meta.ParentTable);
        Assert.Equal("Orders", meta.ChildTable);
        Assert.Equal(4352, meta.Attributes);
    }

    [Fact]
    public void Extract_DecodesEnforcementCascadeAndJoinTypeFlags()
    {
        const string raw = """
        {
          "Name": "R",
          "Type": "Relationship",
          "ParentTable": "P",
          "ChildTable": "C",
          "Attributes": 4352,
          "Fields": []
        }
        """;

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.True(meta.IsEnforced);          // dbRelationDontEnforce (0x2) NOT set
        Assert.True(meta.CascadeUpdate);       // 0x100 set
        Assert.True(meta.CascadeDelete);       // 0x1000 set
        Assert.False(meta.IsInherited);
        Assert.False(meta.IsUnique);
        Assert.Equal("Inner", meta.JoinType);  // no Left/Right join bits set
    }

    [Fact]
    public void Extract_DontEnforceFlag_FlipsIsEnforcedToFalse()
    {
        // Attributes 2 = dbRelationDontEnforce only.
        const string raw = """
        {
          "Name": "R",
          "Type": "Relationship",
          "ParentTable": "P",
          "ChildTable": "C",
          "Attributes": 2,
          "Fields": []
        }
        """;

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.False(meta.IsEnforced);
    }

    [Fact]
    public void Extract_LeftJoinBitDerivesLeftOuterJoinType()
    {
        const int dbRelationLeftJoin = 0x01000000;
        string raw = $$"""
        {
          "Name": "R",
          "Type": "Relationship",
          "ParentTable": "P",
          "ChildTable": "C",
          "Attributes": {{dbRelationLeftJoin}},
          "Fields": []
        }
        """;

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.Equal("LeftOuter", meta.JoinType);
    }

    [Fact]
    public void Extract_RightJoinBitDerivesRightOuterJoinType()
    {
        const int dbRelationRightJoin = 0x02000000;
        string raw = $$"""
        {
          "Name": "R",
          "Type": "Relationship",
          "ParentTable": "P",
          "ChildTable": "C",
          "Attributes": {{dbRelationRightJoin}},
          "Fields": []
        }
        """;

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.Equal("RightOuter", meta.JoinType);
    }

    [Fact]
    public void Extract_PreservesCompositeFieldPairOrder()
    {
        const string raw = """
        {
          "Name": "R",
          "Type": "Relationship",
          "ParentTable": "P",
          "ChildTable": "C",
          "Attributes": 0,
          "Fields": [
            { "ParentField": "A", "ChildField": "X" },
            { "ParentField": "B", "ChildField": "Y" }
          ]
        }
        """;

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.NotNull(meta.Fields);
        Assert.Equal(2, meta.Fields!.Count);
        Assert.Equal("A", meta.Fields[0].ParentField);
        Assert.Equal("X", meta.Fields[0].ChildField);
        Assert.Equal("B", meta.Fields[1].ParentField);
        Assert.Equal("Y", meta.Fields[1].ChildField);
    }

    [Fact]
    public void Extract_UniqueAndInheritedFlagsDecodedFromAttributes()
    {
        // Attributes 0x05 = dbRelationUnique (0x1) | dbRelationInherited (0x4).
        const string raw = """
        {
          "Name": "R",
          "Type": "Relationship",
          "ParentTable": "P",
          "ChildTable": "C",
          "Attributes": 5,
          "Fields": []
        }
        """;

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.True(meta.IsUnique);
        Assert.True(meta.IsInherited);
        Assert.True(meta.IsEnforced);          // dbRelationDontEnforce (0x2) not set
        Assert.False(meta.CascadeUpdate);
        Assert.False(meta.CascadeDelete);
        Assert.Equal("Inner", meta.JoinType);
    }

    [Fact]
    public void Extract_MissingParentTable_Throws()
    {
        const string raw = """
        { "Name": "R", "Type": "Relationship", "ChildTable": "C", "Attributes": 0 }
        """;
        Assert.Throws<System.ArgumentException>(() => RelationshipMetadataExtractor.Extract(raw));
    }

    [Fact]
    public void Extract_MissingAttributes_FlagsAreNull()
    {
        const string raw = """
        { "Name": "R", "Type": "Relationship", "ParentTable": "P", "ChildTable": "C" }
        """;

        var meta = RelationshipMetadataExtractor.Extract(raw);

        Assert.Null(meta.IsEnforced);
        Assert.Null(meta.CascadeUpdate);
        Assert.Null(meta.JoinType);
        Assert.Null(meta.Attributes);
    }
}
