using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Core.Abstractions;

namespace AccessHarvester.Core.Enrichment;

/// <summary>
/// Enriches Form and Report KB JSON with normalized geometry: for every node
/// whose properties include any of Left/Top/Width/Height (as integer twips),
/// adds a sibling <c>GeometryIn</c> object with those values converted to inches.
/// Raw twip properties are preserved unchanged.
/// </summary>
/// <remarks>
/// 1 inch = 1440 twips. Values are rounded to 3 decimal places to avoid floating-
/// point noise in fixture diffs. Nodes missing a given dimension simply omit that
/// key from GeometryIn (e.g., a Section has only Height — its GeometryIn contains
/// only H).
/// </remarks>
public sealed class GeometryEnricher : IObjectEnricher
{
    private const double TwipsPerInch = 1440.0;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private GeometryEnricher(string category)
    {
        Category = category;
    }

    public string Category { get; }

    public static GeometryEnricher Forms { get; } = new("Forms");
    public static GeometryEnricher Reports { get; } = new("Reports");

    public void EnrichFile(string kbFile, string irFile)
    {
        string kbJson = File.ReadAllText(kbFile);
        var node = JsonNode.Parse(kbJson)
                   ?? throw new InvalidDataException($"Empty or invalid JSON: {kbFile}");

        Enrich(node);

        File.WriteAllText(irFile, node.ToJsonString(JsonOptions));
    }

    private static void Enrich(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            AddGeometryIfPresent(obj);
            if (obj["Children"] is JsonArray children)
            {
                foreach (var child in children)
                {
                    if (child is not null) Enrich(child);
                }
            }
        }
    }

    private static void AddGeometryIfPresent(JsonObject node)
    {
        if (node["Properties"] is not JsonObject props) return;

        var geometry = new JsonObject();
        if (TryGetInches(props, "Left", out var x)) geometry["X"] = x;
        if (TryGetInches(props, "Top", out var y)) geometry["Y"] = y;
        if (TryGetInches(props, "Width", out var w)) geometry["W"] = w;
        if (TryGetInches(props, "Height", out var h)) geometry["H"] = h;

        if (geometry.Count > 0)
        {
            node["GeometryIn"] = geometry;
        }
    }

    private static bool TryGetInches(JsonObject props, string key, out double inches)
    {
        inches = 0;
        if (props[key] is not JsonValue v) return false;
        if (!v.TryGetValue<string>(out var s)) return false;
        if (!long.TryParse(s, out var twips)) return false;
        inches = Math.Round(twips / TwipsPerInch, 3);
        return true;
    }
}
