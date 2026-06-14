using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Access;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Loads IR JSON files from a harvested output root directory and produces
/// an <see cref="IrDatabase"/> for the signal collectors and classifier.
/// Reads ir/Forms, ir/Reports, ir/Queries, ir/Tables, ir/Relationships,
/// ir/Macros, ir/ModulesAndClasses directories.
/// </summary>
public static class IrLoader
{
    private static readonly JsonDocumentOptions _jsonOpts = new() { AllowTrailingCommas = true };

    /// <summary>
    /// Loads an <see cref="IrDatabase"/> from the given output root.
    /// The root is the directory that contains the ir/ subdirectory.
    /// </summary>
    public static IrDatabase Load(string outputRoot)
    {
        string irDir = Directory.Exists(Path.Combine(outputRoot, "ir"))
            ? Path.Combine(outputRoot, "ir")
            : outputRoot;

        string dbName = InferDatabaseName(outputRoot);
        return new IrDatabase
        {
            Name = dbName,
            Forms = LoadFormOrReports(Path.Combine(irDir, "Forms"), "Form"),
            Reports = LoadFormOrReports(Path.Combine(irDir, "Reports"), "Report"),
            Queries = LoadQueries(Path.Combine(irDir, "Queries")),
            Tables = LoadTables(Path.Combine(irDir, "Tables")),
            Relationships = LoadRelationships(Path.Combine(irDir, "Relationships")),
            Macros = LoadMacros(Path.Combine(irDir, "Macros")),
            Modules = LoadModules(Path.Combine(irDir, "ModulesAndClasses")),
        };
    }

    private static string InferDatabaseName(string outputRoot)
    {
        // Try to derive from the directory path: last non-empty segment
        // that isn't "ir" or "AccessMetaData".
        var parts = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
                                     StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts.Reverse())
        {
            if (!string.Equals(part, "ir", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(part, "AccessMetaData", StringComparison.OrdinalIgnoreCase))
                return part;
        }
        return "Unknown";
    }

    // ── Forms / Reports ─────────────────────────────────────────────────────────

    private static IReadOnlyList<IrFormOrReport> LoadFormOrReports(string dir, string objectType)
    {
        if (!Directory.Exists(dir)) return [];
        var results = new List<IrFormOrReport>();
        foreach (var file in Directory.GetFiles(dir, "*.ir.json", SearchOption.TopDirectoryOnly)
                                      .OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json, _jsonOpts);
                results.Add(ParseFormOrReport(doc.RootElement, name, objectType));
            }
            catch { /* skip unparseable files */ }
        }
        return results;
    }

    private static IrFormOrReport ParseFormOrReport(JsonElement root, string name, string objectType)
    {
        string? recordSource = null;
        string? vbaCode = null;
        bool hasBeforeUpdate = false, hasAfterUpdate = false, hasOnDelete = false;
        var controls = new List<IrControl>();
        var macroActions = new List<IrMacroAction>();
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        if (root.TryGetProperty("Properties", out var props))
        {
            recordSource = GetString(props, "RecordSource");
            hasBeforeUpdate = HasEventProcedure(props, "BeforeUpdate");
            hasAfterUpdate  = HasEventProcedure(props, "AfterUpdate");
            hasOnDelete     = HasEventProcedure(props, "OnDelete");
            CopyStringProperties(props, properties);
        }

        if (root.TryGetProperty("VbaCode", out var vba) && vba.ValueKind == JsonValueKind.String)
            vbaCode = vba.GetString();

        // Walk Children: extract control nodes (ComboBox/ListBox with RowSource)
        // and macro action nodes (Begin with Action = OpenForm/OpenReport/etc.)
        if (root.TryGetProperty("Children", out var children)
            && children.ValueKind == JsonValueKind.Array)
        {
            WalkChildren(children, controls, macroActions);
        }

        // Detect CRUD VBA handlers from VbaCode procedure names
        if (vbaCode != null)
        {
            hasBeforeUpdate = hasBeforeUpdate || vbaCode.Contains("Form_BeforeUpdate", StringComparison.OrdinalIgnoreCase);
            hasAfterUpdate  = hasAfterUpdate  || vbaCode.Contains("Form_AfterUpdate",  StringComparison.OrdinalIgnoreCase);
            hasOnDelete     = hasOnDelete     || vbaCode.Contains("Form_Delete",        StringComparison.OrdinalIgnoreCase);
        }

        return new IrFormOrReport
        {
            Name = name,
            ObjectType = objectType,
            RecordSource = NullIfEmpty(recordSource),
            VbaCode = NullIfEmpty(vbaCode),
            Controls = controls,
            HasBeforeUpdate = hasBeforeUpdate,
            HasAfterUpdate  = hasAfterUpdate,
            HasOnDelete     = hasOnDelete,
            MacroActions    = macroActions,
            Properties      = properties,
            EventMacros     = ReadEventMacros(root),
        };
    }

    private static void CopyStringProperties(JsonElement props, Dictionary<string, string> sink)
    {
        if (props.ValueKind != JsonValueKind.Object) return;
        foreach (var p in props.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String)
            {
                string? val = p.Value.GetString();
                if (val != null)
                    sink[p.Name] = val;
            }
        }
    }

    private static void WalkChildren(JsonElement children, List<IrControl> controls,
                                     List<IrMacroAction> macroActions)
    {
        foreach (var child in children.EnumerateArray())
        {
            string? type = GetString(child, "Type");
            if (type is null) continue;

            if (type is "ComboBox" or "ListBox")
            {
                string? childName = null;
                string? rowSource = null, rowSourceType = null;
                var childProps = new Dictionary<string, string>(StringComparer.Ordinal);
                if (child.TryGetProperty("Properties", out var p))
                {
                    childName = GetString(p, "Name");
                    rowSource = GetString(p, "RowSource");
                    rowSourceType = GetString(p, "RowSourceType");
                    CopyStringProperties(p, childProps);
                }
                controls.Add(new IrControl
                {
                    Type = type,
                    Name = childName,
                    RowSource = NullIfEmpty(rowSource),
                    RowSourceType = NullIfEmpty(rowSourceType),
                    Properties = childProps,
                    EventMacros = ReadEventMacros(child),
                });
            }
            else if (type == "Begin" && child.TryGetProperty("Properties", out var bProps))
            {
                // Embedded macro-action node on a form/report. Must be checked
                // BEFORE the generic-properties branch below, otherwise Begin
                // nodes get consumed as generic controls and never reach
                // MacroActions (regression fixed per R2 Issue 2).
                string? action = GetString(bProps, "Action");
                if (action != null)
                {
                    var args = ReadArguments(bProps);
                    macroActions.Add(new IrMacroAction
                    {
                        MacroName = GetString(bProps, "MacroName"),
                        Action = action,
                        Arguments = args,
                    });
                }
            }
            else if (child.TryGetProperty("Properties", out var anyProps))
            {
                var childProps = new Dictionary<string, string>(StringComparer.Ordinal);
                CopyStringProperties(anyProps, childProps);
                if (childProps.Count > 0)
                {
                    controls.Add(new IrControl
                    {
                        Type = type,
                        Name = GetString(anyProps, "Name"),
                        RowSource = null,
                        RowSourceType = null,
                        Properties = childProps,
                        EventMacros = ReadEventMacros(child),
                    });
                }
            }

            // Recurse into nested Children
            if (child.TryGetProperty("Children", out var nested)
                && nested.ValueKind == JsonValueKind.Array)
            {
                WalkChildren(nested, controls, macroActions);
            }
        }
    }

    // Reads the structured `EventMacros` block MacroActionsEnricher attaches
    // to a control node (set 012 S2). Shape:
    //   "EventMacros": { "OnClick": { "Actions": [ { "Action": "...",
    //                    "Arguments": { "ReportName": "..." } } ] } }
    // Returns null when the control has no embedded macros so the IR model
    // omits the field rather than carrying an empty list.
    private static IReadOnlyList<IrControlEventMacro>? ReadEventMacros(JsonElement controlNode)
    {
        if (!controlNode.TryGetProperty("EventMacros", out var em)
            || em.ValueKind != JsonValueKind.Object)
            return null;

        var events = new List<IrControlEventMacro>();
        foreach (var ev in em.EnumerateObject())
        {
            if (ev.Value.ValueKind != JsonValueKind.Object) continue;
            var actions = new List<IrEventMacroAction>();
            if (ev.Value.TryGetProperty("Actions", out var acts)
                && acts.ValueKind == JsonValueKind.Array)
            {
                foreach (var act in acts.EnumerateArray())
                {
                    string? actionName = GetString(act, "Action");
                    if (actionName is null) continue;
                    var args = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (act.TryGetProperty("Arguments", out var argObj)
                        && argObj.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var a in argObj.EnumerateObject())
                            if (a.Value.ValueKind == JsonValueKind.String)
                                args[a.Name] = a.Value.GetString() ?? "";
                    }
                    actions.Add(new IrEventMacroAction { Action = actionName, Arguments = args });
                }
            }
            events.Add(new IrControlEventMacro { Event = ev.Name, Actions = actions });
        }
        return events.Count == 0 ? null : events;
    }

    private static bool HasEventProcedure(JsonElement props, string propertyName)
    {
        if (props.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String)
        {
            string? s = val.GetString();
            return s != null && (s.Contains("[Event Procedure]") || s.Contains("Event Procedure"));
        }
        return false;
    }

    // ── Queries ──────────────────────────────────────────────────────────────────

    private static IReadOnlyList<IrQuery> LoadQueries(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var results = new List<IrQuery>();
        foreach (var file in Directory.GetFiles(dir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json, _jsonOpts);
                var root = doc.RootElement;
                string name = GetString(root, "Name")
                    ?? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                int typeCode = 0;
                if (root.TryGetProperty("QueryTypeCode", out var tc) && tc.ValueKind == JsonValueKind.Number)
                    typeCode = tc.GetInt32();
                string? sql = NullIfEmpty(GetString(root, "Sql"));
                results.Add(new IrQuery
                {
                    Name = name,
                    QueryTypeCode = typeCode,
                    Sql = sql,
                    Parameters = ExtractQueryParameters(sql),
                });
            }
            catch { }
        }
        return results;
    }

    /// <summary>
    /// Extracts saved-query parameters (declared <c>PARAMETERS</c> entries and
    /// implicit <c>[Forms]!</c>/<c>[Reports]!</c> control references) from a query's
    /// SQL, reusing the shared <see cref="QueryMetadataExtractor.ParseParameters"/>
    /// scan rather than re-implementing it. Returns null when none are present.
    /// Public so in-memory tests can populate <see cref="IrQuery.Parameters"/> the
    /// same way the loader does. See set 001 Session 2, strategy §3.9.
    /// </summary>
    public static IReadOnlyList<IrQueryParameter>? ExtractQueryParameters(string? sql)
    {
        if (string.IsNullOrEmpty(sql)) return null;
        var parsed = QueryMetadataExtractor.ParseParameters(sql);
        if (parsed is null || parsed.Count == 0) return null;
        var list = new List<IrQueryParameter>(parsed.Count);
        foreach (var p in parsed)
            list.Add(new IrQueryParameter { Name = p.Name, Source = p.Source, Path = p.Path });
        return list;
    }

    // ── Tables ───────────────────────────────────────────────────────────────────

    private static IReadOnlyList<IrTable> LoadTables(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var results = new List<IrTable>();
        foreach (var file in Directory.GetFiles(dir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json, _jsonOpts);
                var root = doc.RootElement;
                string name = GetString(root, "Name")
                    ?? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                int colCount = 0;
                var dateTimeColumns = new List<string>();
                if (root.TryGetProperty("Fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
                {
                    colCount = fields.GetArrayLength();
                    foreach (var field in fields.EnumerateArray())
                    {
                        string? typeName = GetString(field, "TypeName");
                        string? fieldName = GetString(field, "Name");
                        if (fieldName != null && typeName != null
                            && (typeName.Contains("Date", StringComparison.OrdinalIgnoreCase)
                                || typeName.Contains("Time", StringComparison.OrdinalIgnoreCase)))
                        {
                            dateTimeColumns.Add(fieldName);
                        }
                    }
                }
                System.DateTimeOffset? lastUpdated = ParseNaiveUtc(GetString(root, "LastUpdated"));
                string? connect = GetString(root, "Connect");
                results.Add(new IrTable
                {
                    Name = name,
                    ColumnCount = colCount,
                    LastUpdated = lastUpdated,
                    Connect = connect,
                    DateTimeColumnNames = dateTimeColumns,
                });
            }
            catch { }
        }
        return results;
    }

    private static System.DateTimeOffset? ParseNaiveUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (System.DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var dt))
        {
            return new System.DateTimeOffset(System.DateTime.SpecifyKind(dt, System.DateTimeKind.Utc), System.TimeSpan.Zero);
        }
        return null;
    }

    // ── Relationships ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<IrRelationship> LoadRelationships(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var results = new List<IrRelationship>();
        foreach (var file in Directory.GetFiles(dir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json, _jsonOpts);
                var root = doc.RootElement;
                string? name    = GetString(root, "Name");
                string? parent  = GetString(root, "ParentTable");
                string? child   = GetString(root, "ChildTable");
                if (name != null && parent != null && child != null)
                    results.Add(new IrRelationship { Name = name, ParentTable = parent, ChildTable = child });
            }
            catch { }
        }
        return results;
    }

    // ── Macros ────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<IrMacro> LoadMacros(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var results = new List<IrMacro>();
        foreach (var file in Directory.GetFiles(dir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                string macroName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json, _jsonOpts);
                var actions = new List<IrMacroAction>();
                if (doc.RootElement.TryGetProperty("Children", out var children)
                    && children.ValueKind == JsonValueKind.Array)
                {
                    CollectMacroActions(children, actions);
                }
                results.Add(new IrMacro { Name = macroName, Actions = actions });
            }
            catch { }
        }
        return results;
    }

    private static void CollectMacroActions(JsonElement children, List<IrMacroAction> actions)
    {
        foreach (var child in children.EnumerateArray())
        {
            string? type = GetString(child, "Type");
            if (type == "Begin" && child.TryGetProperty("Properties", out var p))
            {
                string? action = GetString(p, "Action");
                if (action != null)
                {
                    actions.Add(new IrMacroAction
                    {
                        MacroName = GetString(p, "MacroName"),
                        Action = action,
                        Arguments = ReadArguments(p),
                    });
                }
            }
            if (child.TryGetProperty("Children", out var nested)
                && nested.ValueKind == JsonValueKind.Array)
            {
                CollectMacroActions(nested, actions);
            }
        }
    }

    // ── Modules ───────────────────────────────────────────────────────────────────

    private static IReadOnlyList<IrModule> LoadModules(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var results = new List<IrModule>();
        foreach (var file in Directory.GetFiles(dir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json, _jsonOpts);
                string? vbaCode = null;
                if (doc.RootElement.TryGetProperty("VbaCode", out var vba)
                    && vba.ValueKind == JsonValueKind.String)
                    vbaCode = vba.GetString();
                results.Add(new IrModule { Name = name, VbaCode = NullIfEmpty(vbaCode) });
            }
            catch { }
        }
        return results;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString();
        return null;
    }

    private static IReadOnlyList<string> ReadArguments(JsonElement props)
    {
        if (!props.TryGetProperty("Argument", out var argEl)) return [];
        if (argEl.ValueKind == JsonValueKind.String)
            return [argEl.GetString() ?? ""];
        if (argEl.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in argEl.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    list.Add(item.GetString() ?? "");
            return list;
        }
        return [];
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
