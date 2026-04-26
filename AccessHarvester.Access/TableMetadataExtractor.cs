using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Pure-logic transform from the raw <c>kb/Tables/&lt;Name&gt;.json</c> JSON
/// emitted by <c>AccessHarvester.Core.Extraction.TableExporter</c> (passed
/// through unchanged by <c>JsonPassthroughTranslator</c>) into a typed
/// <see cref="TableMetadata"/> record. No COM, no Access, no I/O.
/// </summary>
/// <remarks>
/// The extractor lifts structural collections out of the raw JSON
/// (<c>Fields</c>, <c>Indexes</c>) into typed <see cref="TableField"/> /
/// <see cref="TableIndex"/> lists, derives <see cref="TableMetadata.PrimaryKey"/>
/// from the primary index, decodes <see cref="TableMetadata.IsLinked"/> /
/// <see cref="TableField.IsAutoIncrement"/> / <see cref="IndexField.IsDescending"/>
/// from the relevant <c>Attributes</c> bitmask bits, and leaves the
/// <c>Attributes</c> integers themselves accessible for further bit decoding
/// downstream.
/// </remarks>
public static class TableMetadataExtractor
{
    private const int DbAttachExclusive = 0x00010000;
    private const int DbAttachSavePwd   = 0x00020000;
    private const int DbAttachedTable   = 0x40000000;
    private const int DbAttachedODBC    = unchecked((int)0x20000000);
    private const int DbAutoIncrField   = 0x00000010;
    private const int DbDescending      = 0x00000001;

    private static readonly HashSet<string> PromotedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "Type",
        "Connect",
        "SourceTableName",
        "ValidationRule",
        "ValidationText",
        "Fields",
        "Indexes",
    };

    public static TableMetadata Extract(string rawJson)
    {
        if (rawJson is null) throw new ArgumentNullException(nameof(rawJson));
        using var doc = JsonDocument.Parse(rawJson);
        return Extract(doc.RootElement);
    }

    public static TableMetadata Extract(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"Expected JSON object at the root of a Table payload but got {root.ValueKind}.",
                nameof(root));

        string name = ReadString(root, "Name")
            ?? throw new ArgumentException("Table payload is missing the required 'Name' field.", nameof(root));

        int? attributes = ReadInt(root, "Attributes");
        var fields = ReadFields(root);
        var indexes = ReadIndexes(root);

        return new TableMetadata
        {
            Name = name,
            IsLinked = DeriveIsLinked(attributes),
            Connect = ReadString(root, "Connect"),
            SourceTableName = ReadString(root, "SourceTableName"),
            Fields = fields,
            PrimaryKey = DerivePrimaryKey(indexes),
            Indexes = indexes,
            ValidationRule = ReadString(root, "ValidationRule"),
            ValidationText = ReadString(root, "ValidationText"),
            Properties = BuildPropertiesBag(root),
        };
    }

    private static bool? DeriveIsLinked(int? attributes)
    {
        if (attributes is null) return null;
        int a = attributes.Value;
        return (a & (DbAttachedTable | DbAttachedODBC | DbAttachExclusive | DbAttachSavePwd)) != 0;
    }

    private static IReadOnlyList<string>? DerivePrimaryKey(IReadOnlyList<TableIndex>? indexes)
    {
        if (indexes is null) return null;
        var pk = indexes.FirstOrDefault(i => i.IsPrimary == true);
        if (pk is null || pk.Fields is null || pk.Fields.Count == 0) return null;
        return pk.Fields.Select(f => f.Name).ToList();
    }

    private static IReadOnlyList<TableField>? ReadFields(JsonElement root)
    {
        if (!root.TryGetProperty("Fields", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var fields = new List<TableField>(arr.GetArrayLength());
        foreach (var f in arr.EnumerateArray())
        {
            if (f.ValueKind != JsonValueKind.Object) continue;
            string fName = ReadString(f, "Name") ?? string.Empty;
            int? attr = ReadInt(f, "Attributes");
            fields.Add(new TableField
            {
                Name = fName,
                DataType = ReadString(f, "TypeName"),
                DataTypeCode = ReadInt(f, "TypeCode"),
                Size = ReadInt(f, "Size"),
                Required = ReadBool(f, "Required"),
                AllowZeroLength = ReadBool(f, "AllowZeroLength"),
                DefaultValue = ReadString(f, "DefaultValue"),
                ValidationRule = ReadString(f, "ValidationRule"),
                ValidationText = ReadString(f, "ValidationText"),
                OrdinalPosition = ReadInt(f, "OrdinalPosition"),
                IsAutoIncrement = attr is null ? null : (attr.Value & DbAutoIncrField) != 0,
                ForeignName = ReadString(f, "ForeignName"),
                Attributes = attr,
            });
        }
        return fields;
    }

    private static IReadOnlyList<TableIndex>? ReadIndexes(JsonElement root)
    {
        if (!root.TryGetProperty("Indexes", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var indexes = new List<TableIndex>(arr.GetArrayLength());
        foreach (var i in arr.EnumerateArray())
        {
            if (i.ValueKind != JsonValueKind.Object) continue;
            string iName = ReadString(i, "Name") ?? string.Empty;
            indexes.Add(new TableIndex
            {
                Name = iName,
                IsPrimary = ReadBool(i, "Primary"),
                IsUnique = ReadBool(i, "Unique"),
                IsRequired = ReadBool(i, "Required"),
                Fields = ReadIndexFields(i),
            });
        }
        return indexes;
    }

    private static IReadOnlyList<IndexField>? ReadIndexFields(JsonElement index)
    {
        if (!index.TryGetProperty("Fields", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var fields = new List<IndexField>(arr.GetArrayLength());
        foreach (var f in arr.EnumerateArray())
        {
            if (f.ValueKind != JsonValueKind.Object) continue;
            string fName = ReadString(f, "Name") ?? string.Empty;
            int? attr = ReadInt(f, "Attributes");
            fields.Add(new IndexField
            {
                Name = fName,
                IsDescending = attr is null ? null : (attr.Value & DbDescending) != 0,
                Attributes = attr,
            });
        }
        return fields;
    }

    private static IReadOnlyDictionary<string, JsonElement>? BuildPropertiesBag(JsonElement root)
    {
        var bag = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
        {
            if (PromotedKeys.Contains(prop.Name)) continue;
            bag[prop.Name] = prop.Value.Clone();
        }
        return bag.Count > 0 ? bag : null;
    }

    private static string? ReadString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static bool? ReadBool(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static int? ReadInt(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : null;
    }
}
