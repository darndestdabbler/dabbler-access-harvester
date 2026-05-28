using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// Serializes <see cref="ClassificationFindings"/> into the
/// <c>classification.json</c> shape defined by
/// <c>AccessHarvester.Pipeline/Classification/classification-schema.json</c>.
/// Pure logic — no I/O, no schema validation (callers can validate via
/// <see cref="AccessHarvester.Pipeline.Validation.SchemaValidator"/>).
/// </summary>
/// <remarks>
/// The writer emits the editor-default <c>humanOverride</c> block for every
/// table and column even though those values are scanner-supplied null/false
/// defaults. The schema marks these fields <c>required</c> on a fresh scan
/// so the HTML editor can rely on their presence; omitting them would force
/// the editor to defensively populate them on first load.
/// </remarks>
public static class ClassificationWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(ClassificationFindings findings)
    {
        if (findings is null) throw new ArgumentNullException(nameof(findings));

        var root = new JsonObject
        {
            ["version"]     = "1.0",
            ["database"]    = findings.DatabaseName,
            ["scannedUtc"]  = findings.ScannedUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["config"]      = SerializeConfig(findings.Config),
            ["tables"]      = SerializeTables(findings.Tables),
        };
        return root.ToJsonString(JsonOptions);
    }

    private static JsonObject SerializeConfig(ScanConfig config) => new()
    {
        ["systemColumnPatterns"] = ToJsonArray(config.SystemColumnPatterns),
        ["piiNamePatterns"]      = ToJsonArray(config.PiiNamePatterns),
        ["piiValuePatterns"]     = ToJsonArray(config.PiiValuePatterns),
        ["valueSampleSize"]      = config.ValueSampleSize,
    };

    private static JsonArray SerializeTables(IReadOnlyList<TableFinding> tables)
    {
        var arr = new JsonArray();
        foreach (var t in tables) arr.Add(SerializeTable(t));
        return arr;
    }

    private static JsonObject SerializeTable(TableFinding t)
    {
        var columns = new JsonArray();
        foreach (var c in t.Columns) columns.Add(SerializeColumn(c));

        return new JsonObject
        {
            ["name"]                       = t.Name,
            ["isLinkedTable"]              = t.IsLinkedTable,
            ["rowCount"]                   = t.RowCount,
            ["lookupGuess"]                = t.LookupGuess,
            ["nonKeyNonSystemColumnCount"] = t.NonKeyNonSystemColumnCount,
            ["referencedByFkCount"]        = t.ReferencedByFkCount,
            ["tableHint"]                  = t.TableHint.ToWire(),
            ["confidence"]                 = RoundOrZero(t.Confidence),
            ["firedHeuristics"]            = ToJsonArray(t.FiredHeuristics),
            ["humanOverride"]              = DefaultTableOverride(),
            ["columns"]                    = columns,
        };
    }

    private static JsonObject SerializeColumn(ColumnFinding c)
    {
        return new JsonObject
        {
            ["name"]                = c.Name,
            ["ordinalPosition"]     = c.OrdinalPosition,
            ["dataType"]            = c.DataType,
            ["isAutoNumber"]        = c.IsAutoNumber,
            ["isPrimaryKey"]        = c.IsPrimaryKey,
            ["isForeignKey"]        = c.IsForeignKey,
            ["fkTarget"]            = c.FkTarget is null ? null : SerializeFk(c.FkTarget),
            ["isMemo"]              = c.IsMemo,
            ["isOleObject"]         = c.IsOleObject,
            ["isMultiValue"]        = c.IsMultiValue,
            ["isRequired"]          = c.IsRequired,
            ["rowCount"]            = c.RowCount,
            ["nullCount"]           = c.NullCount,
            ["cardinality"]         = c.Cardinality,
            ["uniquenessRatio"]     = RoundOrZero(c.UniquenessRatio),
            ["relativeCardinality"] = RoundOrZero(c.RelativeCardinality),
            ["piiNameMatch"]        = c.PiiNameMatch,
            ["piiValueMatch"]       = c.PiiValueMatch,
            ["valueFormatPattern"]  = c.ValueFormatPattern is null ? null : (JsonNode?)c.ValueFormatPattern,
            ["guessedCategory"]     = c.GuessedCategory.ToWire(),
            ["confidence"]          = RoundOrZero(c.Confidence),
            ["firedHeuristics"]     = ToJsonArray(c.FiredHeuristics),
            ["humanOverride"]       = DefaultColumnOverride(),
        };
    }

    private static JsonObject SerializeFk(RawFkTarget fk) => new()
    {
        ["table"]  = fk.Table,
        ["column"] = fk.Column,
    };

    private static JsonObject DefaultTableOverride() => new()
    {
        ["isLookup"]    = null,
        ["reviewedBy"]  = null,
        ["notes"]       = string.Empty,
    };

    private static JsonObject DefaultColumnOverride() => new()
    {
        ["category"]            = null,
        ["maskingStrategy"]     = null,
        ["preserveAutonumber"]  = false,
        ["reviewedBy"]          = null,
        ["notes"]               = string.Empty,
    };

    private static JsonArray ToJsonArray(IReadOnlyList<string> values)
    {
        var arr = new JsonArray();
        foreach (var v in values) arr.Add(v);
        return arr;
    }

    /// <summary>
    /// Clamps confidence / ratio fields to a sane wire range. The schema
    /// constrains these to [0..1]; emit 0 for a NaN to keep the JSON
    /// schema-conformant rather than letting <see cref="JsonNode"/> serialize
    /// <c>NaN</c>, which is invalid JSON.
    /// </summary>
    private static double RoundOrZero(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0.0;
        if (v < 0.0) return 0.0;
        if (v > 1.0) return 1.0;
        return Math.Round(v, 6);
    }
}
