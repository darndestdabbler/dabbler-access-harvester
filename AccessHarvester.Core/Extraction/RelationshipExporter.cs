using System.Text.Json;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Extracts Access relationships via DAO's CurrentDb().Relations. Writes one JSON
/// file per relationship.
/// </summary>
/// <remarks>
/// DAO: <c>Relation.Table</c> is the primary (parent) table that holds the PK;
/// <c>Relation.ForeignTable</c> is the foreign (child) table that holds the FK.
/// Per-field: <c>Field.Name</c> is the column in the primary table, and
/// <c>Field.ForeignName</c> is the column in the foreign table that references
/// it. The output relabels these as <c>ParentTable</c>/<c>ChildTable</c> and
/// <c>ParentField</c>/<c>ChildField</c> to make the direction unambiguous.
/// </remarks>
public sealed class RelationshipExporter : IObjectExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static RelationshipExporter Instance { get; } = new();

    private RelationshipExporter() { }

    public string Category => "Relationships";

    public IReadOnlyList<ExportOutcome> Export(AccessComSession session, string rawCategoryDir)
    {
        Directory.CreateDirectory(rawCategoryDir);
        var outcomes = new List<ExportOutcome>();

        dynamic db = session.Application.CurrentDb();
        dynamic relations = db.Relations;

        foreach (dynamic rel in relations)
        {
            string name = rel.Name;

            try
            {
                int attributes = (int)rel.Attributes;
                var fields = new List<object>();
                foreach (dynamic f in rel.Fields)
                {
                    fields.Add(new
                    {
                        ParentField = (string)f.Name,
                        ChildField = (string)f.ForeignName,
                    });
                }

                var payload = new
                {
                    Name = name,
                    Type = "Relationship",
                    ParentTable = (string)rel.Table,
                    ChildTable = (string)rel.ForeignTable,
                    Attributes = attributes,
                    AttributesDecoded = DecodeAttributes(attributes),
                    Fields = fields,
                };

                string outFile = Path.Combine(rawCategoryDir, SanitizeFileName(name) + ".json");
                File.WriteAllText(outFile, JsonSerializer.Serialize(payload, JsonOptions));
                outcomes.Add(new ExportOutcome(name, true));
            }
            catch (Exception ex)
            {
                outcomes.Add(new ExportOutcome(name, false, ex.Message));
            }
        }

        return outcomes;
    }

    // DAO dbRelation* constants (bitmask).
    private static List<string> DecodeAttributes(int attributes)
    {
        var flags = new List<string>();
        if ((attributes & 0x00000001) != 0) flags.Add("Unique");
        if ((attributes & 0x00000002) != 0) flags.Add("DontEnforce");
        if ((attributes & 0x00000004) != 0) flags.Add("Inherited");
        if ((attributes & 0x00000100) != 0) flags.Add("UpdateCascade");
        if ((attributes & 0x00001000) != 0) flags.Add("DeleteCascade");
        if ((attributes & 0x01000000) != 0) flags.Add("LeftJoin");
        if ((attributes & 0x02000000) != 0) flags.Add("RightJoin");
        return flags;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
