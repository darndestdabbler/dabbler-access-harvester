using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds the <see cref="MacroChain"/> list for
/// <see cref="NavigationGraphInputs.Macros"/> from the enriched
/// <c>ir/Macros/*.ir.json</c> artifacts. One macro file becomes one
/// <see cref="MacroChain"/>; the chain's actions are the
/// <c>Action</c>-bearing <c>Begin</c> nodes found by walking the macro
/// tree (submacro grouping is not modelled here — that detail lives in
/// the per-macro IR artifact).
/// </summary>
/// <remarks>
/// Positional macro arguments (the SaveAsText <c>Argument</c> array) are
/// mapped to their documented Access argument names for the common
/// actions (see <see cref="ArgumentNames"/>); arguments of an action not
/// in that table — and any positional slots beyond a known signature —
/// fall back to deterministic <c>Argument{n}</c> keys so no value is
/// dropped. Pure and deterministic; the only I/O is reading the macro
/// files in ordinal filename order.
/// </remarks>
public static class MacroChainBuilder
{
    /// <summary>
    /// Documented positional argument names for common Access macro
    /// actions, in declaration order. Case-insensitive on the action
    /// name. Unlisted actions fall back to <c>Argument{n}</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> ArgumentNames =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["OpenForm"] = new[] { "FormName", "View", "FilterName", "WhereCondition", "DataMode", "WindowMode", "OpenArgs" },
            ["OpenReport"] = new[] { "ReportName", "View", "FilterName", "WhereCondition", "WindowMode", "OpenArgs" },
            ["OpenQuery"] = new[] { "QueryName", "View", "DataMode" },
            ["OpenTable"] = new[] { "TableName", "View", "DataMode" },
            ["Close"] = new[] { "ObjectType", "ObjectName", "Save" },
            ["RunCode"] = new[] { "FunctionName" },
            ["RunMacro"] = new[] { "MacroName", "RepeatCount", "RepeatExpression" },
            ["RunCommand"] = new[] { "Command" },
            ["RunMenuCommand"] = new[] { "Command" },
            ["GoToControl"] = new[] { "ControlName" },
            ["GoToRecord"] = new[] { "ObjectType", "ObjectName", "Record", "Offset" },
            ["Requery"] = new[] { "ControlName" },
            ["SetValue"] = new[] { "Item", "Expression" },
            ["SetTempVar"] = new[] { "Name", "Expression" },
            ["ApplyFilter"] = new[] { "FilterName", "WhereCondition", "ControlName" },
            ["MsgBox"] = new[] { "Message", "Beep", "Type", "Title" },
            ["MessageBox"] = new[] { "Message", "Beep", "Type", "Title" },
            ["SetWarnings"] = new[] { "WarningsOn" },
            ["Quit"] = new[] { "Options" },
        };

    /// <summary>
    /// Reads <c>ir/Macros/*.ir.json</c> and returns one
    /// <see cref="MacroChain"/> per file, ordered by filename. Returns an
    /// empty list when the directory is absent or holds no macros.
    /// </summary>
    public static IReadOnlyList<MacroChain> Build(string irMacrosDir)
    {
        if (string.IsNullOrEmpty(irMacrosDir) || !Directory.Exists(irMacrosDir))
            return Array.Empty<MacroChain>();

        var chains = new List<MacroChain>();
        foreach (var file in Directory.GetFiles(irMacrosDir, "*.ir.json")
                                      .OrderBy(f => f, StringComparer.Ordinal))
        {
            // Strip both ".json" and ".ir" to recover the macro name.
            var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
            JsonNode? root;
            try { root = JsonNode.Parse(File.ReadAllText(file)); }
            catch { continue; }
            if (root is null) continue;

            var actions = new List<MacroAction>();
            CollectActions(root, actions);
            chains.Add(new MacroChain { Name = name, Actions = actions });
        }
        return chains;
    }

    /// <summary>
    /// Builds one <see cref="MacroChain"/> from an already-parsed macro
    /// document root (exposed for hermetic testing).
    /// </summary>
    public static MacroChain FromDocument(string name, JsonNode root)
    {
        var actions = new List<MacroAction>();
        CollectActions(root, actions);
        return new MacroChain { Name = name, Actions = actions };
    }

    private static void CollectActions(JsonNode node, List<MacroAction> sink)
    {
        if (node is JsonObject obj)
        {
            if (obj["Properties"] is JsonObject props
                && props["Action"] is JsonValue actionVal
                && actionVal.TryGetValue<string>(out var actionName)
                && !string.IsNullOrWhiteSpace(actionName))
            {
                sink.Add(BuildAction(actionName, props["Argument"]));
            }

            if (obj["Children"] is JsonArray children)
                foreach (var child in children)
                    if (child is not null) CollectActions(child, sink);
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
                if (child is not null) CollectActions(child, sink);
        }
    }

    private static MacroAction BuildAction(string actionName, JsonNode? argumentNode)
    {
        var positional = ReadPositional(argumentNode);
        var names = ArgumentNames.TryGetValue(actionName, out var n) ? n : Array.Empty<string>();

        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < positional.Count; i++)
        {
            string key = i < names.Length ? names[i] : $"Argument{i + 1}";
            args[key] = positional[i];
        }

        string? callsProcedure = null;
        if (string.Equals(actionName, "RunCode", StringComparison.OrdinalIgnoreCase)
            && positional.Count > 0)
        {
            callsProcedure = StripParens(positional[0]);
        }

        return new MacroAction
        {
            ActionName = actionName,
            Arguments = args,
            CallsProcedureName = callsProcedure,
        };
    }

    // The SaveAsText `Argument` property is a scalar string, an array of
    // strings, or absent. Normalise to an ordered string list.
    private static IReadOnlyList<string> ReadPositional(JsonNode? argumentNode)
    {
        switch (argumentNode)
        {
            case JsonArray arr:
                return arr.Select(a => a?.GetValue<object>() is { } v
                        ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty
                        : string.Empty)
                    .ToList();
            case JsonValue val:
                return new[] { val.TryGetValue<string>(out var s) ? s : val.ToString() };
            default:
                return Array.Empty<string>();
        }
    }

    private static string StripParens(string raw)
    {
        int paren = raw.IndexOf('(');
        return paren >= 0 ? raw[..paren].Trim() : raw.Trim();
    }
}
