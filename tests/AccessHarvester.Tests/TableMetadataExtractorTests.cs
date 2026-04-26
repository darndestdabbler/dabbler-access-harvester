using System.Linq;
using AccessHarvester.Access;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="TableMetadataExtractor"/>.
/// </summary>
public class TableMetadataExtractorTests
{
    private const string CustomersTableJson = """
    {
      "Name": "Customers",
      "Type": "Table",
      "Attributes": 0,
      "DateCreated": "2026-01-01T00:00:00",
      "LastUpdated": "2026-01-02T00:00:00",
      "RecordCount": 42,
      "SourceTableName": "",
      "Connect": "",
      "Fields": [
        {
          "Name": "CustomerId",
          "TypeCode": 4,
          "TypeName": "Long",
          "Size": 4,
          "Required": true,
          "AllowZeroLength": false,
          "DefaultValue": null,
          "ValidationRule": null,
          "ValidationText": null,
          "Attributes": 17,
          "OrdinalPosition": 0,
          "ForeignName": null
        },
        {
          "Name": "Name",
          "TypeCode": 10,
          "TypeName": "Text",
          "Size": 50,
          "Required": false,
          "AllowZeroLength": true,
          "DefaultValue": null,
          "ValidationRule": "Len([Name]) > 0",
          "ValidationText": "Name required",
          "Attributes": 2,
          "OrdinalPosition": 1,
          "ForeignName": null
        }
      ],
      "Indexes": [
        {
          "Name": "PrimaryKey",
          "Primary": true,
          "Unique": true,
          "Required": true,
          "Fields": [
            { "Name": "CustomerId", "Attributes": 0 }
          ]
        },
        {
          "Name": "ByName",
          "Primary": false,
          "Unique": false,
          "Required": false,
          "Fields": [
            { "Name": "Name", "Attributes": 1 }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void Extract_LiftsNameAndStructuralCollections()
    {
        var meta = TableMetadataExtractor.Extract(CustomersTableJson);

        Assert.Equal("Customers", meta.Name);
        Assert.NotNull(meta.Fields);
        Assert.Equal(2, meta.Fields!.Count);
        Assert.NotNull(meta.Indexes);
        Assert.Equal(2, meta.Indexes!.Count);
    }

    [Fact]
    public void Extract_FieldDecodingPreservesTypeCodeAndDecodesAutoIncrement()
    {
        var meta = TableMetadataExtractor.Extract(CustomersTableJson);

        var id = meta.Fields!.Single(f => f.Name == "CustomerId");
        Assert.Equal("Long", id.DataType);
        Assert.Equal(4, id.DataTypeCode);
        Assert.Equal(4, id.Size);
        Assert.True(id.Required);
        // Attributes 17 = 0x11 = dbAutoIncrField (0x10) | dbFixedField (0x1) — autoincrement should be true.
        Assert.True(id.IsAutoIncrement);
        Assert.Equal(17, id.Attributes);
    }

    [Fact]
    public void Extract_NonAutoIncrementFieldDecodesFalse()
    {
        var meta = TableMetadataExtractor.Extract(CustomersTableJson);

        var name = meta.Fields!.Single(f => f.Name == "Name");
        Assert.False(name.IsAutoIncrement);
        Assert.Equal("Text", name.DataType);
        Assert.Equal(50, name.Size);
        Assert.True(name.AllowZeroLength);
        Assert.Equal("Len([Name]) > 0", name.ValidationRule);
        Assert.Equal("Name required", name.ValidationText);
    }

    [Fact]
    public void Extract_DerivesPrimaryKeyFromPrimaryIndex()
    {
        var meta = TableMetadataExtractor.Extract(CustomersTableJson);

        Assert.NotNull(meta.PrimaryKey);
        Assert.Equal(new[] { "CustomerId" }, meta.PrimaryKey!.ToArray());
    }

    [Fact]
    public void Extract_NoPrimaryIndex_PrimaryKeyIsNull()
    {
        const string raw = """
        {
          "Name": "Staging",
          "Type": "Table",
          "Attributes": 0,
          "Fields": [{ "Name": "X", "TypeCode": 4, "TypeName": "Long", "Attributes": 0 }],
          "Indexes": []
        }
        """;

        var meta = TableMetadataExtractor.Extract(raw);

        Assert.Null(meta.PrimaryKey);
        Assert.NotNull(meta.Indexes);
        Assert.Empty(meta.Indexes!);
    }

    [Fact]
    public void Extract_IndexFieldDescendingFlagDecodedFromAttributes()
    {
        var meta = TableMetadataExtractor.Extract(CustomersTableJson);

        var byName = meta.Indexes!.Single(i => i.Name == "ByName");
        var f = byName.Fields!.Single();
        Assert.True(f.IsDescending);   // Attributes 1 = dbDescending
        Assert.Equal(1, f.Attributes);

        var pk = meta.Indexes.Single(i => i.IsPrimary == true);
        var pkF = pk.Fields!.Single();
        Assert.False(pkF.IsDescending); // Attributes 0
    }

    [Fact]
    public void Extract_IsLinkedFalseForLocalTable()
    {
        var meta = TableMetadataExtractor.Extract(CustomersTableJson);
        Assert.False(meta.IsLinked);
    }

    [Fact]
    public void Extract_IsLinkedTrueWhenAttachedBitSet()
    {
        // dbAttachedODBC = 0x20000000 (treated as int by JSON.Number; JSON parser
        // accepts the negative two's-complement form for signed-int round-trip).
        const int dbAttachedODBC = 0x20000000;
        string raw = $$"""
        {
          "Name": "LinkedOrders",
          "Type": "Table",
          "Attributes": {{dbAttachedODBC}},
          "Connect": "ODBC;DSN=Foo",
          "SourceTableName": "Orders",
          "Fields": [],
          "Indexes": []
        }
        """;

        var meta = TableMetadataExtractor.Extract(raw);

        Assert.True(meta.IsLinked);
        Assert.Equal("ODBC;DSN=Foo", meta.Connect);
        Assert.Equal("Orders", meta.SourceTableName);
    }

    [Fact]
    public void Extract_RemovesPromotedKeysFromPropertiesBag()
    {
        var meta = TableMetadataExtractor.Extract(CustomersTableJson);

        Assert.NotNull(meta.Properties);
        Assert.False(meta.Properties!.ContainsKey("Name"));
        Assert.False(meta.Properties.ContainsKey("Type"));
        Assert.False(meta.Properties.ContainsKey("Connect"));
        Assert.False(meta.Properties.ContainsKey("SourceTableName"));
        Assert.False(meta.Properties.ContainsKey("Fields"));
        Assert.False(meta.Properties.ContainsKey("Indexes"));
        // Untyped raw values stay in the bag.
        Assert.True(meta.Properties.ContainsKey("Attributes"));
        Assert.True(meta.Properties.ContainsKey("DateCreated"));
        Assert.True(meta.Properties.ContainsKey("RecordCount"));
    }

    [Fact]
    public void Extract_MissingName_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => TableMetadataExtractor.Extract("""{ "Type": "Table" }"""));
    }
}
