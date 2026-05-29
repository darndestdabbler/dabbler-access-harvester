using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AccessHarvester.Pipeline.Classification;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Parses a reviewed <c>classification.json</c> document (schema v1.0) into
/// a typed <see cref="ClassificationDocument"/>. Pure logic — no I/O beyond
/// the file-read convenience overload. The parser is strict on the v1.0
/// shape: missing required fields throw <see cref="InvalidDataException"/>
/// so the caller can surface a usage-grade error rather than masking with
/// defaults.
/// </summary>
public static class ClassificationReader
{
    public static ClassificationDocument ReadFile(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Classification file not found: {path}", path);
        string json = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return Parse(json);
    }

    public static ClassificationDocument Parse(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string version = root.RequireString("version");
        if (version != "1.0")
            throw new InvalidDataException(
                $"Unsupported classification schema version '{version}'; expected '1.0'.");

        string database   = root.RequireString("database");
        var scannedRaw    = root.RequireString("scannedUtc");
        if (!DateTimeOffset.TryParse(scannedRaw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var scannedUtc))
            throw new InvalidDataException($"scannedUtc is not a valid ISO 8601 date-time: '{scannedRaw}'.");

        var config = ParseScanConfig(root.GetProperty("config"));

        var tables = new List<ClassifiedTable>();
        foreach (var t in root.GetProperty("tables").EnumerateArray())
            tables.Add(ParseTable(t));

        return new ClassificationDocument(version, database, scannedUtc, config, tables);
    }

    private static ScanConfig ParseScanConfig(JsonElement el) => new(
        SystemColumnPatterns: ParseStringArray(el, "systemColumnPatterns"),
        PiiNamePatterns:      ParseStringArray(el, "piiNamePatterns"),
        PiiValuePatterns:     ParseStringArray(el, "piiValuePatterns"),
        ValueSampleSize:      el.RequireInt("valueSampleSize"));

    private static ClassifiedTable ParseTable(JsonElement el) => new(
        Name:                       el.RequireString("name"),
        IsLinkedTable:              el.RequireBool("isLinkedTable"),
        RowCount:                   el.RequireInt("rowCount"),
        LookupGuess:                el.RequireBool("lookupGuess"),
        NonKeyNonSystemColumnCount: el.RequireInt("nonKeyNonSystemColumnCount"),
        ReferencedByFkCount:        el.RequireInt("referencedByFkCount"),
        TableHint:                  el.RequireString("tableHint"),
        Confidence:                 el.RequireDouble("confidence"),
        FiredHeuristics:            ParseStringArray(el, "firedHeuristics"),
        HumanOverride:              ParseTableOverride(el.GetProperty("humanOverride")),
        Columns:                    el.GetProperty("columns").EnumerateArray().Select(ParseColumn).ToList());

    private static TableHumanOverride ParseTableOverride(JsonElement el) => new(
        IsLookup:   el.OptionalBoolNullable("isLookup"),
        ReviewedBy: el.OptionalStringNullable("reviewedBy"),
        Notes:      el.RequireString("notes"));

    private static ClassifiedColumn ParseColumn(JsonElement el)
    {
        var fkProp = el.GetProperty("fkTarget");
        ClassifiedFkTarget? fkTarget = fkProp.ValueKind == JsonValueKind.Null
            ? null
            : new ClassifiedFkTarget(fkProp.RequireString("table"), fkProp.RequireString("column"));

        return new ClassifiedColumn(
            Name:                el.RequireString("name"),
            OrdinalPosition:     el.RequireInt("ordinalPosition"),
            DataType:            el.RequireString("dataType"),
            IsAutoNumber:        el.RequireBool("isAutoNumber"),
            IsPrimaryKey:        el.RequireBool("isPrimaryKey"),
            IsForeignKey:        el.RequireBool("isForeignKey"),
            FkTarget:            fkTarget,
            IsMemo:              el.RequireBool("isMemo"),
            IsOleObject:         el.RequireBool("isOleObject"),
            IsMultiValue:        el.RequireBool("isMultiValue"),
            IsRequired:          el.RequireBool("isRequired"),
            RowCount:            el.RequireInt("rowCount"),
            NullCount:           el.RequireInt("nullCount"),
            Cardinality:         el.RequireInt("cardinality"),
            UniquenessRatio:     el.RequireDouble("uniquenessRatio"),
            RelativeCardinality: el.RequireDouble("relativeCardinality"),
            PiiNameMatch:        el.RequireBool("piiNameMatch"),
            PiiValueMatch:       el.RequireBool("piiValueMatch"),
            ValueFormatPattern:  el.OptionalStringNullable("valueFormatPattern"),
            GuessedCategory:     ColumnCategoryWire.FromWire(el.RequireString("guessedCategory")),
            Confidence:          el.RequireDouble("confidence"),
            FiredHeuristics:     ParseStringArray(el, "firedHeuristics"),
            HumanOverride:       ParseColumnOverride(el.GetProperty("humanOverride")));
    }

    private static ColumnHumanOverride ParseColumnOverride(JsonElement el)
    {
        string? rawCategory = el.OptionalStringNullable("category");
        string? rawStrategy = el.OptionalStringNullable("maskingStrategy");
        return new ColumnHumanOverride(
            Category:           rawCategory is null ? null : ColumnCategoryWire.FromWire(rawCategory),
            MaskingStrategy:    rawStrategy is null ? null : MaskingStrategyWire.FromWire(rawStrategy),
            PreserveAutonumber: el.RequireBool("preserveAutonumber"),
            ReviewedBy:         el.OptionalStringNullable("reviewedBy"),
            Notes:              el.RequireString("notes"));
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Missing or non-array property: '{property}'.");
        var list = new List<string>(arr.GetArrayLength());
        foreach (var x in arr.EnumerateArray())
        {
            if (x.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"Array '{property}' contains a non-string element.");
            list.Add(x.GetString()!);
        }
        return list;
    }
}

/// <summary>Strict-required-field reading helpers for <see cref="JsonElement"/>.</summary>
internal static class JsonElementExtensions
{
    public static string RequireString(this JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v))
            throw new InvalidDataException($"Missing required property: '{property}'.");
        if (v.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Property '{property}' is not a string.");
        return v.GetString()!;
    }

    public static bool RequireBool(this JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v))
            throw new InvalidDataException($"Missing required property: '{property}'.");
        return v.ValueKind switch
        {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"Property '{property}' is not a boolean."),
        };
    }

    public static int RequireInt(this JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v) || v.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"Missing or non-numeric property: '{property}'.");
        return v.GetInt32();
    }

    public static double RequireDouble(this JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v) || v.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"Missing or non-numeric property: '{property}'.");
        return v.GetDouble();
    }

    public static string? OptionalStringNullable(this JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v) || v.ValueKind == JsonValueKind.Null)
            return null;
        if (v.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Property '{property}' is not a string-or-null.");
        return v.GetString();
    }

    public static bool? OptionalBoolNullable(this JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v) || v.ValueKind == JsonValueKind.Null)
            return null;
        return v.ValueKind switch
        {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"Property '{property}' is not a boolean-or-null."),
        };
    }
}
