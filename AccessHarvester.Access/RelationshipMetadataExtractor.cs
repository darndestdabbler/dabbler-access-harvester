using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Pure-logic transform from the raw <c>kb/Relationships/&lt;Name&gt;.json</c>
/// JSON emitted by <c>AccessHarvester.Core.Extraction.RelationshipExporter</c>
/// into a typed <see cref="RelationshipMetadata"/> record.
/// </summary>
/// <remarks>
/// Decodes the DAO <c>Attributes</c> bitmask into the typed flag fields
/// (<see cref="RelationshipMetadata.IsEnforced"/>, <see cref="RelationshipMetadata.CascadeUpdate"/>,
/// <see cref="RelationshipMetadata.CascadeDelete"/>, <see cref="RelationshipMetadata.IsInherited"/>,
/// <see cref="RelationshipMetadata.IsUnique"/>, <see cref="RelationshipMetadata.JoinType"/>),
/// preserving the raw integer on <see cref="RelationshipMetadata.Attributes"/>
/// for downstream consumers that need finer decoding.
/// </remarks>
public static class RelationshipMetadataExtractor
{
    private const int DbRelationUnique        = 0x00000001;
    private const int DbRelationDontEnforce   = 0x00000002;
    private const int DbRelationInherited     = 0x00000004;
    private const int DbRelationUpdateCascade = 0x00000100;
    private const int DbRelationDeleteCascade = 0x00001000;
    private const int DbRelationLeftJoin      = 0x01000000;
    private const int DbRelationRightJoin     = 0x02000000;

    public static RelationshipMetadata Extract(string rawJson)
    {
        if (rawJson is null) throw new ArgumentNullException(nameof(rawJson));
        using var doc = JsonDocument.Parse(rawJson);
        return Extract(doc.RootElement);
    }

    public static RelationshipMetadata Extract(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"Expected JSON object at the root of a Relationship payload but got {root.ValueKind}.",
                nameof(root));

        string name = ReadString(root, "Name")
            ?? throw new ArgumentException("Relationship payload is missing 'Name'.", nameof(root));
        string parent = ReadString(root, "ParentTable")
            ?? throw new ArgumentException("Relationship payload is missing 'ParentTable'.", nameof(root));
        string child = ReadString(root, "ChildTable")
            ?? throw new ArgumentException("Relationship payload is missing 'ChildTable'.", nameof(root));

        int? attributes = ReadInt(root, "Attributes");

        return new RelationshipMetadata
        {
            Name = name,
            ParentTable = parent,
            ChildTable = child,
            Fields = ReadFieldPairs(root),
            IsEnforced = attributes is null ? null : (attributes.Value & DbRelationDontEnforce) == 0,
            CascadeUpdate = attributes is null ? null : (attributes.Value & DbRelationUpdateCascade) != 0,
            CascadeDelete = attributes is null ? null : (attributes.Value & DbRelationDeleteCascade) != 0,
            IsInherited = attributes is null ? null : (attributes.Value & DbRelationInherited) != 0,
            IsUnique = attributes is null ? null : (attributes.Value & DbRelationUnique) != 0,
            JoinType = DeriveJoinType(attributes),
            Attributes = attributes,
        };
    }

    private static string? DeriveJoinType(int? attributes)
    {
        if (attributes is null) return null;
        int a = attributes.Value;
        if ((a & DbRelationLeftJoin) != 0) return "LeftOuter";
        if ((a & DbRelationRightJoin) != 0) return "RightOuter";
        return "Inner";
    }

    private static IReadOnlyList<RelationshipFieldPair>? ReadFieldPairs(JsonElement root)
    {
        if (!root.TryGetProperty("Fields", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var fields = new List<RelationshipFieldPair>(arr.GetArrayLength());
        foreach (var f in arr.EnumerateArray())
        {
            if (f.ValueKind != JsonValueKind.Object) continue;
            string parentField = ReadString(f, "ParentField") ?? string.Empty;
            string childField = ReadString(f, "ChildField") ?? string.Empty;
            fields.Add(new RelationshipFieldPair
            {
                ParentField = parentField,
                ChildField = childField,
            });
        }
        return fields;
    }

    private static string? ReadString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static int? ReadInt(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : null;
    }
}
