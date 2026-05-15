using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessHarvester.Access;

/// <summary>
/// Internal helpers shared by <see cref="FormMetadataExtractor"/> and
/// <see cref="ReportMetadataExtractor"/> for reading the enriched IR JSON
/// AccessNode tree produced by <c>AccessHarvester.Core</c>. Handles the
/// case-mapping mismatches between enricher-written keys (e.g.
/// <c>LabelResolver</c> writes lowercase <c>{ source, text }</c>) and the
/// typed records' PascalCase fields, and produces the
/// <see cref="AccessControl"/> tree by recursively walking the node's
/// <c>Children</c> array.
/// </summary>
/// <remarks>
/// Top-level enricher outputs (<c>Procedures</c>, <c>EventMacros</c>) and
/// per-control enricher outputs (<c>GeometryIn</c>, <c>Label</c>,
/// <c>NonDefaultProperties</c>) live as siblings of the node's
/// <c>Properties</c> bag in the IR JSON, not inside it — so the
/// inner-bag clone in <see cref="BuildPromotedPropertiesBag"/> and
/// <see cref="BuildControlPropertiesBag"/> only needs to skip the
/// promoted keys themselves.
/// </remarks>
internal static class AccessObjectReader
{
    internal static IReadOnlyDictionary<string, JsonElement>? BuildPromotedPropertiesBag(
        JsonElement? rawProperties,
        HashSet<string> promotedKeys)
    {
        if (rawProperties is null || rawProperties.Value.ValueKind != JsonValueKind.Object) return null;
        var bag = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in rawProperties.Value.EnumerateObject())
        {
            if (promotedKeys.Contains(prop.Name)) continue;
            bag[prop.Name] = prop.Value.Clone();
        }
        return bag.Count > 0 ? bag : null;
    }

    internal static IReadOnlyDictionary<string, JsonElement>? BuildControlPropertiesBag(JsonElement? rawProperties)
    {
        if (rawProperties is null || rawProperties.Value.ValueKind != JsonValueKind.Object) return null;
        var bag = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in rawProperties.Value.EnumerateObject())
        {
            if (prop.Name == "Name") continue;
            bag[prop.Name] = prop.Value.Clone();
        }
        return bag.Count > 0 ? bag : null;
    }

    internal static IReadOnlyList<AccessControl>? ReadChildren(JsonElement node)
    {
        if (!node.TryGetProperty("Children", out var children) || children.ValueKind != JsonValueKind.Array)
            return null;
        var list = new List<AccessControl>(children.GetArrayLength());
        foreach (var child in children.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object) continue;
            list.Add(ReadControl(child));
        }
        return list.Count > 0 ? list : null;
    }

    internal static AccessControl ReadControl(JsonElement node)
    {
        string type = ReadString(node, "Type")
            ?? throw new InvalidDataException("Control node missing required 'Type' field.");
        var rawProperties = GetObject(node, "Properties");
        string? controlName = rawProperties is null ? null : ReadString(rawProperties.Value, "Name");

        return new AccessControl
        {
            Type = type,
            Name = controlName,
            Properties = BuildControlPropertiesBag(rawProperties),
            GeometryIn = ReadGeometry(node),
            Label = ReadLabel(node),
            NonDefaultProperties = ReadStringArray(node, "NonDefaultProperties"),
            Children = ReadChildren(node),
        };
    }

    internal static GeometryInches? ReadGeometry(JsonElement node)
    {
        if (!node.TryGetProperty("GeometryIn", out var geom) || geom.ValueKind != JsonValueKind.Object) return null;
        var result = new GeometryInches
        {
            X = ReadDouble(geom, "X"),
            Y = ReadDouble(geom, "Y"),
            W = ReadDouble(geom, "W"),
            H = ReadDouble(geom, "H"),
        };
        if (result.X is null && result.Y is null && result.W is null && result.H is null) return null;
        return result;
    }

    /// <summary>
    /// Reads <see cref="LabelInfo"/> from the <c>Label</c> field. The
    /// underlying <c>LabelResolver</c> writes the lowercase JSON shape
    /// <c>{ source, text }</c>; the typed <see cref="LabelInfo"/> record
    /// uses PascalCase <c>Source</c> / <c>Text</c>. This reader accepts
    /// either casing so callers serializing the typed record back to JSON
    /// (PascalCase) round-trip cleanly through the extractor.
    /// </summary>
    internal static LabelInfo? ReadLabel(JsonElement node)
    {
        if (!node.TryGetProperty("Label", out var label) || label.ValueKind != JsonValueKind.Object) return null;
        string? source = ReadString(label, "source") ?? ReadString(label, "Source");
        if (source is null) return null;
        string? text = ReadString(label, "text") ?? ReadString(label, "Text");
        return new LabelInfo { Source = source, Text = text };
    }

    internal static IReadOnlyList<string>? ReadStringArray(JsonElement node, string key)
    {
        if (!node.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var list = new List<string>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (s is not null) list.Add(s);
            }
        }
        return list.Count > 0 ? list : null;
    }

    internal static JsonElement? GetObject(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.Object ? v : null;
    }

    internal static string? ReadString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    internal static string? ReadString(JsonElement? obj, string key)
        => obj is null ? null : ReadString(obj.Value, key);

    internal static double? ReadDouble(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double d) ? d : null;
    }

    // Matches: Attribute VB_Name = "Form_frmPatients"  (leading whitespace allowed)
    private static readonly Regex VbNameRegex = new(
        @"^\s*Attribute\s+VB_Name\s*=\s*""(?<name>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Extracts the VBA class-module name from the <c>Attribute VB_Name = "..."</c>
    /// line near the top of a form or report's <c>VbaCode</c> string.
    /// Returns null when <paramref name="vbaCode"/> is null or the attribute is absent.
    /// </summary>
    internal static string? ReadModuleName(string? vbaCode)
    {
        if (vbaCode is null) return null;
        // Attribute VB_Name always appears within the first few lines of a VBA module
        // (before Option Compare, Option Explicit, or any procedure declarations).
        // Scanning 10 lines is sufficient for all known Access SaveAsText output shapes.
        using var reader = new StringReader(vbaCode);
        int linesScanned = 0;
        while (linesScanned < 10)
        {
            var line = reader.ReadLine();
            if (line is null) break;
            var match = VbNameRegex.Match(line);
            if (match.Success) return match.Groups["name"].Value;
            linesScanned++;
        }
        return null;
    }

    internal static string? DecodeDefaultView(
        JsonElement? rawProperties,
        Dictionary<string, string> labels)
    {
        if (rawProperties is null) return null;
        if (!rawProperties.Value.TryGetProperty("DefaultView", out var v)) return null;
        string? raw = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number when v.TryGetInt32(out var i) => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
        if (raw is null) return null;
        return labels.TryGetValue(raw, out var label) ? label : null;
    }
}
