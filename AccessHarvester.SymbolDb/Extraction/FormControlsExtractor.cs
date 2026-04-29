using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.SymbolDb.Models;

namespace AccessHarvester.SymbolDb.Extraction;

// Reads a form KB JSON file and produces FormControlRow and EventBindingRow
// lists. The KB JSON encodes the form's design tree as nested nodes; each
// node with a "Name" in its Properties is a named control. Event bindings
// are detected from known Access event property names whose values are either
// "[Event Procedure]" (vba), a macro name (macro), or an "=..." expression.
public static class FormControlsExtractor
{
    // Access event property names — the superset seen across forms + controls.
    // Any property in this set whose value is non-empty is an event binding.
    private static readonly HashSet<string> EventPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AfterDelConfirm", "AfterInsert", "AfterUpdate",
        "ApplyFilter", "BeforeDelConfirm", "BeforeInsert", "BeforeUpdate",
        "OnActivate", "OnChange", "OnClick", "OnClose", "OnCurrent",
        "OnDeactivate", "OnDelete", "OnDblClick", "OnDirty",
        "OnEnter", "OnError", "OnExit", "OnFilter",
        "OnGotFocus", "OnKeyDown", "OnKeyPress", "OnKeyUp",
        "OnLoad", "OnLostFocus", "OnMouseDown", "OnMouseMove", "OnMouseUp",
        "OnOpen", "OnPush", "OnResize", "OnTimer", "OnUndo",
    };

    // Node types that are structural containers; we recurse into them but do
    // not emit a FormControlRow for them (they carry no "Name" in practice).
    private static readonly HashSet<string> ContainerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Form", "FormHeader", "FormFooter", "Section", "Tab", "Page",
    };

    // Irregular property-name → VBA-event-suffix mappings. Most events strip
    // the "On" prefix uniformly, but a few do not match the procedure name
    // VBA generates. The most consequential case is OnPush, which Access
    // emits for the "click" event on legacy command buttons; the actual VBA
    // handler is named <ControlName>_Click, not <ControlName>_Push.
    private static readonly Dictionary<string, string> IrregularEventSuffix =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "OnPush", "Click" },
    };

    public sealed record Result(
        IReadOnlyList<FormControlRow> Controls,
        IReadOnlyList<EventBindingRow> Bindings);

    public static Result Extract(string kbJsonPath, string formFqn)
    {
        if (!File.Exists(kbJsonPath))
            throw new FileNotFoundException($"Form KB JSON not found: {kbJsonPath}");

        using var stream = File.OpenRead(kbJsonPath);
        var root = JsonNode.Parse(stream)?.AsObject()
            ?? throw new InvalidDataException($"Expected JSON object at root of {kbJsonPath}");

        var controls = new List<FormControlRow>();
        var bindings = new List<EventBindingRow>();

        // Form-level events live on the root Properties; control_name = null.
        EmitFormLevelBindings(root, formFqn, bindings);

        // Walk all descendant nodes.
        WalkChildren(root, formFqn, controls, bindings);

        return new Result(controls, bindings);
    }

    private static void EmitFormLevelBindings(JsonObject node, string formFqn, List<EventBindingRow> bindings)
    {
        var props = node["Properties"]?.AsObject();
        if (props is null) return;

        foreach (var (key, val) in props)
        {
            if (!EventPropertyNames.Contains(key)) continue;
            string? value = val?.GetValue<string>();
            if (string.IsNullOrEmpty(value)) continue;
            EmitBinding(formFqn, controlName: null, eventPropName: key, value: value, bindings);
        }
    }

    private static void WalkChildren(JsonObject node, string formFqn,
        List<FormControlRow> controls, List<EventBindingRow> bindings)
    {
        if (node["Children"] is not JsonArray children) return;

        foreach (var child in children)
        {
            if (child is not JsonObject childObj) continue;
            string nodeType = childObj["Type"]?.GetValue<string>() ?? "";
            var props = childObj["Properties"]?.AsObject();

            string? controlName = props?["Name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(controlName))
            {
                // Named node → emit FormControlRow and its event bindings.
                controls.Add(BuildControlRow(formFqn, controlName!, nodeType, props!));
                EmitControlBindings(formFqn, controlName!, props!, bindings);
            }

            // Always recurse regardless of whether the node had a name.
            WalkChildren(childObj, formFqn, controls, bindings);
        }
    }

    private static FormControlRow BuildControlRow(
        string formFqn, string controlName, string controlType, JsonObject props)
    {
        string? labelText = null;    // not reliably present in KB JSON without sibling Label walk
        string? controlSource = GetProp(props, "ControlSource");
        string? rowSource = GetProp(props, "RowSource");
        string? baseInfo = GetProp(props, "BaseInfo");

        string? boundTableOrQuery = null;
        string? boundColumn = null;
        if (!string.IsNullOrEmpty(baseInfo))
        {
            // BaseInfo is a semicolon-delimited, backslash-quote-escaped string:
            //   \"RowSource\";\"BoundTable\";\"\";\"BoundColumn\";\"DisplayCol\";\"IndexName\"
            // Strip backslash-quote escapes, then split on semicolons.
            var parts = baseInfo!.Replace("\\\"", "").Split(';');
            if (parts.Length >= 4)
            {
                boundTableOrQuery = NullIfEmpty(parts[1]);
                boundColumn = NullIfEmpty(parts[3]);
            }
        }

        return new FormControlRow(
            FormFqn: formFqn,
            ControlName: controlName,
            ControlType: controlType,
            LabelText: labelText,
            ControlSource: NullIfEmpty(controlSource),
            RowSource: NullIfEmpty(rowSource),
            BoundTableOrQuery: boundTableOrQuery,
            BoundColumn: boundColumn);
    }

    private static void EmitControlBindings(
        string formFqn, string controlName, JsonObject props, List<EventBindingRow> bindings)
    {
        foreach (var (key, val) in props)
        {
            if (!EventPropertyNames.Contains(key)) continue;
            string? value = val?.GetValue<string>();
            if (string.IsNullOrEmpty(value)) continue;
            EmitBinding(formFqn, controlName, key, value!, bindings);
        }
    }

    private static void EmitBinding(
        string formFqn, string? controlName, string eventPropName, string value,
        List<EventBindingRow> bindings)
    {
        string eventSuffix = StripOnPrefix(eventPropName);

        if (value == "[Event Procedure]")
        {
            // VBA handler: Form_<event> for form-level; <ControlName>_<event> for control-level.
            string procName = controlName is null
                ? $"Form_{eventSuffix}"
                : $"{controlName}_{eventSuffix}";
            bindings.Add(new EventBindingRow(
                FormFqn: formFqn,
                ControlName: controlName,
                EventName: eventSuffix,
                HandlerKind: "vba",
                HandlerFqn: $"{formFqn}.{procName}",
                HandlerExpression: null));
        }
        else if (value.StartsWith('='))
        {
            bindings.Add(new EventBindingRow(
                FormFqn: formFqn,
                ControlName: controlName,
                EventName: eventSuffix,
                HandlerKind: "expression",
                HandlerFqn: null,
                HandlerExpression: value));
        }
        else
        {
            // Non-empty, non-expression value → assume macro name.
            bindings.Add(new EventBindingRow(
                FormFqn: formFqn,
                ControlName: controlName,
                EventName: eventSuffix,
                HandlerKind: "macro",
                HandlerFqn: value,
                HandlerExpression: null));
        }
    }

    // Map an Access event property name to the VBA procedure-name suffix
    // Access uses for the corresponding event handler. Most names just lose
    // their "On" prefix (OnClick → Click, OnLoad → Load); a few are irregular
    // and need explicit handling — see IrregularEventSuffix.
    private static string StripOnPrefix(string name)
    {
        if (IrregularEventSuffix.TryGetValue(name, out var mapped)) return mapped;
        return name.StartsWith("On", StringComparison.Ordinal) && name.Length > 2
            ? name[2..]
            : name;
    }

    private static string? GetProp(JsonObject props, string key)
    {
        if (props.TryGetPropertyValue(key, out var node))
            return node?.GetValue<string>();
        return null;
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
