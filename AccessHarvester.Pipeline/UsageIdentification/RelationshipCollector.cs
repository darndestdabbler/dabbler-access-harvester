using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Collects FK relationships from relationship IR files.
/// Skips MSysNavPane* system relationships that Access creates automatically.
/// </summary>
public static class RelationshipCollector
{
    public sealed record FkEdge(string ParentTable, string ChildTable);

    /// <summary>Returns all non-system FK edges in the database.</summary>
    public static IReadOnlyList<FkEdge> Collect(IrDatabase db)
    {
        var edges = new List<FkEdge>();
        foreach (var rel in db.Relationships)
        {
            if (IsSystemRelationship(rel.Name)) continue;
            edges.Add(new FkEdge(rel.ParentTable, rel.ChildTable));
        }
        return edges;
    }

    private static bool IsSystemRelationship(string name)
        => name.StartsWith("MSysNavPane", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase);
}
