using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AccessHarvester.Pipeline.Sanitization;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// Reads the harvested <c>ir/Tables</c> and <c>ir/Relationships</c> tiers into the
/// generator-facing <see cref="MigrationTable"/> / <see cref="MigrationRelationship"/>
/// model.
/// <para>
/// Generation is driven by the structured per-field metadata (<c>TypeCode</c>,
/// <c>Size</c>, <c>Required</c>, <c>DefaultValue</c>, <c>Attributes</c>,
/// <c>OrdinalPosition</c>), NOT by re-parsing the <c>Ddl</c> string. The <c>Ddl</c>
/// in an IR file is itself a reconstruction the harvester builds from these same
/// structured collections (DAO does not expose a verbatim <c>TableDef.DDL</c>), so
/// the structured fields are the authoritative source and are richer + safer to
/// consume than a rendered string.
/// </para>
/// </summary>
public static class MigrationIrReader
{
    private const int DbAutoIncrField = 0x10; // DAO dbAutoIncrField attribute bit

    private static readonly JsonDocumentOptions JsonOpts = new() { AllowTrailingCommas = true };
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Reads from a harvested output root (the directory containing the <c>ir/</c>
    /// subdirectory, or the <c>ir/</c> directory itself), assembling the inputs and
    /// optionally attaching a reviewed classification document and the sanitize
    /// CSV directory.
    /// </summary>
    public static MigrationInputs Read(
        string outputRoot,
        ClassificationDocument? classification = null,
        string? sanitizedDataDir = null)
    {
        if (outputRoot is null) throw new ArgumentNullException(nameof(outputRoot));

        string irDir = Directory.Exists(Path.Combine(outputRoot, "ir"))
            ? Path.Combine(outputRoot, "ir")
            : outputRoot;

        var tables = ReadTables(Path.Combine(irDir, "Tables"));
        var relationships = ReadRelationships(Path.Combine(irDir, "Relationships"));
        string database = classification?.Database ?? InferDatabaseName(outputRoot);

        return new MigrationInputs(database, tables, relationships, classification, sanitizedDataDir);
    }

    // ── Tables ──────────────────────────────────────────────────────────────

    public static IReadOnlyList<MigrationTable> ReadTables(string tablesDir)
    {
        if (!Directory.Exists(tablesDir)) return [];
        var result = new List<MigrationTable>();
        foreach (var file in Directory.GetFiles(tablesDir, "*.ir.json", SearchOption.TopDirectoryOnly)
                                      .OrderBy(f => f, StringComparer.Ordinal))
        {
            string json = File.ReadAllText(file, Utf8NoBom);
            using var doc = JsonDocument.Parse(json, JsonOpts);
            result.Add(ParseTable(doc.RootElement));
        }
        return result;
    }

    public static MigrationTable ParseTable(JsonElement el)
    {
        string name = el.GetProperty("Name").GetString() ?? "";
        string connect = OptionalString(el, "Connect");
        string sourceTable = OptionalString(el, "SourceTableName");
        bool isLinked = !string.IsNullOrEmpty(connect) || !string.IsNullOrEmpty(sourceTable);

        var fields = new List<MigrationField>();
        if (el.TryGetProperty("Fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Array)
            foreach (var f in fieldsEl.EnumerateArray())
                fields.Add(ParseField(f));

        var indexes = new List<MigrationIndex>();
        if (el.TryGetProperty("Indexes", out var idxEl) && idxEl.ValueKind == JsonValueKind.Array)
            foreach (var ix in idxEl.EnumerateArray())
                indexes.Add(ParseIndex(ix));

        return new MigrationTable(name, IsSystemTableName(name), isLinked, fields, indexes);
    }

    private static MigrationField ParseField(JsonElement el)
    {
        int attributes = OptionalInt(el, "Attributes") ?? 0;
        int? size = OptionalInt(el, "Size");
        string def = OptionalString(el, "DefaultValue");
        return new MigrationField(
            Name:            el.GetProperty("Name").GetString() ?? "",
            TypeCode:        OptionalInt(el, "TypeCode") ?? 0,
            TypeName:        OptionalString(el, "TypeName"),
            Size:            size is > 0 ? size : null,
            Required:        OptionalBool(el, "Required"),
            IsAutoIncrement: (attributes & DbAutoIncrField) != 0,
            DefaultValue:    string.IsNullOrEmpty(def) ? null : def,
            OrdinalPosition: OptionalInt(el, "OrdinalPosition") ?? 0);
    }

    private static MigrationIndex ParseIndex(JsonElement el)
    {
        var fields = new List<string>();
        if (el.TryGetProperty("Fields", out var fEl) && fEl.ValueKind == JsonValueKind.Array)
            foreach (var f in fEl.EnumerateArray())
                fields.Add(f.GetProperty("Name").GetString() ?? "");
        return new MigrationIndex(
            Name:    OptionalString(el, "Name"),
            Primary: OptionalBool(el, "Primary"),
            Unique:  OptionalBool(el, "Unique"),
            Fields:  fields);
    }

    // ── Relationships ───────────────────────────────────────────────────────

    public static IReadOnlyList<MigrationRelationship> ReadRelationships(string relDir)
    {
        if (!Directory.Exists(relDir)) return [];
        var result = new List<MigrationRelationship>();
        foreach (var file in Directory.GetFiles(relDir, "*.ir.json", SearchOption.TopDirectoryOnly)
                                      .OrderBy(f => f, StringComparer.Ordinal))
        {
            string json = File.ReadAllText(file, Utf8NoBom);
            using var doc = JsonDocument.Parse(json, JsonOpts);
            result.Add(ParseRelationship(doc.RootElement));
        }
        return result;
    }

    public static MigrationRelationship ParseRelationship(JsonElement el)
    {
        var pairs = new List<MigrationFieldPair>();
        if (el.TryGetProperty("Fields", out var fEl) && fEl.ValueKind == JsonValueKind.Array)
            foreach (var f in fEl.EnumerateArray())
                pairs.Add(new MigrationFieldPair(
                    ParentField: OptionalString(f, "ParentField"),
                    ChildField:  OptionalString(f, "ChildField")));

        return new MigrationRelationship(
            Name:                         OptionalString(el, "Name"),
            ParentTable:                  OptionalString(el, "ParentTable"),
            ChildTable:                   OptionalString(el, "ChildTable"),
            FieldPairs:                   pairs,
            EnforceReferentialIntegrity:  OptionalBool(el, "EnforceReferentialIntegrity"),
            CascadeUpdate:                OptionalBool(el, "CascadeUpdate"),
            CascadeDelete:                OptionalBool(el, "CascadeDelete"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>System (MSys*) and temporary (~*) Access tables are out of migration scope.</summary>
    public static bool IsSystemTableName(string name) =>
        name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("~", StringComparison.Ordinal);

    private static string OptionalString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static int? OptionalInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : null;

    private static bool OptionalBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

    private static string InferDatabaseName(string outputRoot)
    {
        var parts = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
                                     StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts.Reverse())
            if (!string.Equals(part, "ir", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(part, "AccessMetaData", StringComparison.OrdinalIgnoreCase))
                return part;
        return "Unknown";
    }
}
