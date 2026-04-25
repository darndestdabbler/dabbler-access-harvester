using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Core.Abstractions;

namespace AccessHarvester.Core.Enrichment;

/// <summary>
/// Decodes Access SaveAsText's <c>"NotDefault"</c> marker: boolean properties
/// whose effective value differs from the control/form default are written as
/// the literal string <c>"NotDefault"</c> without the actual value. This
/// enricher looks up the property's default (for its node Type) in an embedded
/// defaults table and rewrites the property value to <c>"True"</c> or
/// <c>"False"</c> — whichever is the inverse of the default.
/// <para>
/// The "intentionally deviated from default" signal is preserved on the same
/// node as a sibling <c>NonDefaultProperties</c> array listing the names of
/// all properties that were <c>"NotDefault"</c> in the source. Downstream
/// consumers that care about "the user explicitly changed this" can read that
/// array; consumers that just want the effective value can read Properties.
/// </para>
/// <para>
/// The defaults table is regenerated from a ground-truth defaults DB via
/// <c>tools/generate_property_defaults.py</c>; see that script for the
/// procedure and for notes on supporting older Access versions.
/// </para>
/// </summary>
public sealed class PropertyDecoder : IObjectEnricher
{
    private const string NotDefault = "NotDefault";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly Lazy<Defaults> DefaultsInstance =
        new(LoadEmbeddedDefaults, isThreadSafe: true);

    private PropertyDecoder(string category)
    {
        Category = category;
    }

    public string Category { get; }

    public static PropertyDecoder Forms { get; } = new("Forms");
    public static PropertyDecoder Reports { get; } = new("Reports");

    public void EnrichFile(string inFile, string outFile)
    {
        string json = File.ReadAllText(inFile);
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidDataException($"Root must be a JSON object: {inFile}");

        Walk(root, DefaultsInstance.Value);

        File.WriteAllText(outFile, root.ToJsonString(JsonOptions));
    }

    private static void Walk(JsonObject node, Defaults defaults)
    {
        DecodeNode(node, defaults);
        if (node["Children"] is JsonArray children)
        {
            foreach (var c in children)
            {
                if (c is JsonObject co) Walk(co, defaults);
            }
        }
    }

    private static void DecodeNode(JsonObject node, Defaults defaults)
    {
        if (node["Properties"] is not JsonObject props) return;
        string type = node["Type"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(type)) return;

        // Collect keys to avoid mutating the dict while iterating.
        var nonDefault = new List<string>();
        var decoded = new List<(string Key, string NewValue)>();

        foreach (var kv in props)
        {
            if (kv.Value is not JsonValue v) continue;
            if (!v.TryGetValue<string>(out var s)) continue;
            if (s != NotDefault) continue;

            nonDefault.Add(kv.Key);
            if (defaults.TryGetDefault(type, kv.Key, out bool def))
            {
                decoded.Add((kv.Key, def ? "False" : "True"));
            }
            // If the type / property isn't in our table, leave "NotDefault" in
            // place — that's a load-bearing "unknown" signal, not a failure.
        }

        foreach (var (key, newValue) in decoded)
        {
            props[key] = newValue;
        }

        if (nonDefault.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var n in nonDefault) arr.Add(n);
            node["NonDefaultProperties"] = arr;
        }
    }

    // ---------- embedded defaults ----------

    private static Defaults LoadEmbeddedDefaults()
    {
        var asm = typeof(PropertyDecoder).Assembly;
        const string resourceName = "AccessHarvester.Core.Resources.property-defaults.json";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource not found: {resourceName}. Ensure csproj includes " +
                "<EmbeddedResource Include=\"Resources\\property-defaults.json\"/> and " +
                "that tools/generate_property_defaults.py has been run at least once.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var doc = JsonNode.Parse(json) as JsonObject
                  ?? throw new InvalidDataException("property-defaults.json is not a JSON object.");

        var table = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
        if (doc["defaults"] is JsonObject defaultsObj)
        {
            foreach (var kindEntry in defaultsObj)
            {
                if (kindEntry.Value is not JsonObject propsObj) continue;
                var inner = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in propsObj)
                {
                    if (prop.Value is JsonValue v && v.TryGetValue<bool>(out var b))
                        inner[prop.Key] = b;
                }
                table[kindEntry.Key] = inner;
            }
        }
        return new Defaults(table);
    }

    private sealed class Defaults
    {
        private readonly Dictionary<string, Dictionary<string, bool>> _byKind;

        public Defaults(Dictionary<string, Dictionary<string, bool>> byKind)
        {
            _byKind = byKind;
        }

        public bool TryGetDefault(string kind, string propertyName, out bool value)
        {
            value = false;
            return _byKind.TryGetValue(kind, out var kindDefaults)
                   && kindDefaults.TryGetValue(propertyName, out value);
        }
    }
}
