using System.Text;
using System.Text.Json;

namespace AccessHarvester.IrCodegen;

/// <summary>
/// Minimal slice generator for the ModalDetailForm IR type. Reads a
/// schema-conformant IR (already JSON-converted from YAML if needed) and
/// emits a .razor + .razor.cs pair targeting MudBlazor 9.x. Out-of-scope
/// abstractions (the request runner, the named-operator catalog, the
/// _ref pre-processor) are emitted as TODO method stubs on the
/// partial-class code-behind, to be hand-filled in <ClassName>.Custom.cs.
/// </summary>
internal sealed class ModalDetailFormGenerator
{
    private readonly JsonElement _ir;
    private readonly string _className;
    private readonly string _namespace;

    public ModalDetailFormGenerator(JsonElement ir, string className, string ns)
    {
        _ir = ir;
        _className = className;
        _namespace = ns;
    }

    // -- public entry points -------------------------------------------------

    public string EmitRazor()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"@namespace {_namespace}");
        sb.AppendLine($"@inherits {_className}Base");
        sb.AppendLine();

        var metadata = _ir.GetProperty("metadata");
        var modalType = metadata.GetProperty("modalType").GetString() ?? "page";
        var title = metadata.GetProperty("title").GetString() ?? _className;

        if (modalType == "modal")
        {
            sb.AppendLine("<MudDialog>");
            sb.AppendLine("    <TitleContent>");
            sb.AppendLine($"        <MudText Typo=\"Typo.h6\">{Escape(title)}</MudText>");
            sb.AppendLine("    </TitleContent>");
            sb.AppendLine("    <DialogContent>");
            EmitBlock(sb, _ir.GetProperty("layout"), indent: 8);
            sb.AppendLine("    </DialogContent>");
            sb.AppendLine("</MudDialog>");
        }
        else
        {
            sb.AppendLine($"<MudText Typo=\"Typo.h4\" GutterBottom=\"true\">{Escape(title)}</MudText>");
            EmitBlock(sb, _ir.GetProperty("layout"), indent: 0);
        }
        return sb.ToString();
    }

    public string EmitRazorCs()
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.AspNetCore.Components;");
        sb.AppendLine("using MudBlazor;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {_className}Base : ComponentBase");
        sb.AppendLine("{");

        // Dialog instance (for modalType=modal)
        sb.AppendLine("    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }");
        sb.AppendLine("    [Inject] protected ISnackbar Snackbar { get; set; } = default!;");
        sb.AppendLine("    [Inject] protected IDialogService DialogService { get; set; } = default!;");
        sb.AppendLine();

        // Page-level state fields
        if (_ir.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("    // -- state -----------------------------------------------------------");
            foreach (var kv in state.EnumerateObject())
            {
                var clrType = ClrTypeForState(kv.Value);
                sb.AppendLine($"    protected {clrType} {kv.Name};");
            }
            sb.AppendLine();
        }

        // Request result rows. We model rows as `Dictionary<string, object?>`
        // (single row) or `List<Dictionary<string, object?>>` (list). We
        // statically classify each request as list-typed if any layout block
        // names it as an `optionsRequest`; otherwise single-row.
        if (_ir.TryGetProperty("requests", out var requests) && requests.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("    // -- request result rows ---------------------------------------------");
            foreach (var kv in requests.EnumerateObject())
            {
                var typeName = IsListRequest(kv.Name)
                    ? "List<Dictionary<string, object?>>?"
                    : "Dictionary<string, object?>?";
                sb.AppendLine($"    protected {typeName} {kv.Name};");
            }
            sb.AppendLine();
        }

        // Events
        if (_ir.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Object)
        {
            foreach (var evt in events.EnumerateObject())
            {
                EmitEventHandler(sb, evt.Name, evt.Value);
            }
        }

        // Stubs for the named-query runner and confirmation prompt. The
        // escape-hatch pattern is: user overrides these in <Class>.Custom.cs
        // to dispatch to their actual EF Core / SQL / MudDialog infrastructure.
        // Default impls throw / auto-confirm so missed wire-up is loud, not
        // silently wrong.
        sb.AppendLine("    // -- runtime escape hatches (override in .Custom.cs) -----------------");
        sb.AppendLine("    protected virtual Task<dynamic?> ExecuteRequestAsync(string requestName)");
        sb.AppendLine("    {");
        sb.AppendLine("        throw new NotImplementedException(");
        sb.AppendLine($"            $\"ExecuteRequestAsync for {{requestName}} is not implemented. \"");
        sb.AppendLine("            + \"Override in the partial class .Custom.cs to wire up your named-query runner.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected virtual Task<bool> OnConfirmAsync(string message)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Default: auto-confirm so flows progress without a prompt.");
        sb.AppendLine("        // Override in .Custom.cs to show a real MudDialog confirmation.");
        sb.AppendLine("        return Task.FromResult(true);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Custom-method escape hatches (CallMethod actions). The partial-class
        // pattern means these stubs are declared here and the user implements
        // them in <Class>.Custom.cs.
        var customMethods = CollectCallMethodTargets();
        if (customMethods.Count > 0)
        {
            sb.AppendLine("    // -- custom escape hatches (implement in .Custom.cs) -----------------");
            foreach (var m in customMethods)
            {
                sb.AppendLine($"    protected partial void {m}();");
            }
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // -- layout emission -----------------------------------------------------

    private void EmitBlock(StringBuilder sb, JsonElement block, int indent)
    {
        var type = block.GetProperty("type").GetString();
        var pad = new string(' ', indent);

        switch (type)
        {
            case "Section":
            case "Group":
                if (block.TryGetProperty("title", out var sTitle))
                    sb.AppendLine($"{pad}<MudText Typo=\"Typo.subtitle1\" Class=\"mt-2\">{Escape(sTitle.GetString())}</MudText>");
                sb.AppendLine($"{pad}<MudStack>");
                EmitChildren(sb, block, indent + 4);
                sb.AppendLine($"{pad}</MudStack>");
                break;

            case "Row":
                sb.AppendLine($"{pad}<MudStack Row=\"true\" AlignItems=\"AlignItems.Center\" Class=\"mb-2\">");
                EmitChildren(sb, block, indent + 4);
                sb.AppendLine($"{pad}</MudStack>");
                break;

            case "Column":
                sb.AppendLine($"{pad}<MudStack Row=\"false\">");
                EmitChildren(sb, block, indent + 4);
                sb.AppendLine($"{pad}</MudStack>");
                break;

            case "Label":
                sb.AppendLine($"{pad}<MudText>{Escape(block.GetProperty("text").GetString())}</MudText>");
                break;

            case "Line":
                {
                    var orientation = block.TryGetProperty("orientation", out var o)
                        ? o.GetString() : "horizontal";
                    sb.AppendLine(orientation == "vertical"
                        ? $"{pad}<MudDivider Vertical=\"true\" />"
                        : $"{pad}<MudDivider />");
                    break;
                }

            case "TextBox":
                EmitTextBox(sb, block, pad);
                break;

            case "ComboBox":
                EmitComboBox(sb, block, pad);
                break;

            case "CheckBox":
                EmitCheckBox(sb, block, pad);
                break;

            case "Button":
                var bText = block.GetProperty("text").GetString();
                var bClick = MapEventNameToMethod(block.GetProperty("onClick").GetString()!);
                sb.AppendLine($"{pad}<MudButton OnClick=\"{bClick}\" Variant=\"Variant.Filled\" Color=\"Color.Primary\">{Escape(bText)}</MudButton>");
                break;

            default:
                sb.AppendLine($"{pad}<!-- unknown block type: {type} -->");
                break;
        }
    }

    private void EmitChildren(StringBuilder sb, JsonElement block, int indent)
    {
        if (!block.TryGetProperty("blocks", out var blocks)) return;
        foreach (var child in blocks.EnumerateArray())
            EmitBlock(sb, child, indent);
    }

    private void EmitTextBox(StringBuilder sb, JsonElement block, string pad)
    {
        var label = block.TryGetProperty("label", out var l) ? l.GetString() : null;
        var readOnly = block.TryGetProperty("readOnly", out var ro) && JsonBool(ro);
        var binding = block.GetProperty("binding");
        var readExpr = ExpressionToCSharp(binding);
        var onChanged = block.TryGetProperty("onValueChanged", out var oc) ? oc.GetString() : null;

        sb.Append($"{pad}<MudTextField T=\"string\"");
        if (label is not null) sb.Append($" Label=\"{Escape(label)}\"");
        // Single-quote the attribute so the C# body can contain "string" literals.
        // Convert.ToString handles object?, value-type, and null uniformly.
        sb.Append($" Value='@(System.Convert.ToString({readExpr}))'");
        if (readOnly)
        {
            sb.Append(" ReadOnly=\"true\"");
        }
        else
        {
            // Two-way: write back into the request row's indexer slot. We
            // require the binding to be a single `_request: "name.field"`
            // (schema enforces this for non-readOnly controls via if/then).
            // Razor attributes use single quotes when the C# body contains
            // double-quoted string literals (Razor does not honor \"-escapes).
            var (rowVar, field) = ParseRequestRef(binding);
            sb.Append($" ValueChanged='@((string __v) => {rowVar}![\"{field}\"] = __v)'");
            if (onChanged is not null) sb.Append($" OnBlur=\"{MapEventNameToMethod(onChanged)}\"");
        }
        sb.AppendLine(" />");
    }

    private void EmitComboBox(StringBuilder sb, JsonElement block, string pad)
    {
        var label = block.TryGetProperty("label", out var l) ? l.GetString() : null;
        var binding = block.GetProperty("binding");
        var readExpr = ExpressionToCSharp(binding);
        var (rowVar, field) = ParseRequestRef(binding);
        var optionsRequest = block.GetProperty("optionsRequest").GetString();
        var valueField = block.GetProperty("valueField").GetString();
        var textField = block.GetProperty("textField").GetString();

        sb.AppendLine($"{pad}<MudSelect T=\"object\" Label=\"{Escape(label)}\" Value='@({readExpr})' ValueChanged='@((object __v) => {rowVar}![\"{field}\"] = __v)'>");
        sb.AppendLine($"{pad}    @if ({optionsRequest} != null)");
        sb.AppendLine($"{pad}    {{");
        sb.AppendLine($"{pad}        @foreach (var __row in {optionsRequest})");
        sb.AppendLine($"{pad}        {{");
        sb.AppendLine($"{pad}            <MudSelectItem T=\"object\" Value='@(__row[\"{valueField}\"])'>@__row[\"{textField}\"]</MudSelectItem>");
        sb.AppendLine($"{pad}        }}");
        sb.AppendLine($"{pad}    }}");
        sb.AppendLine($"{pad}</MudSelect>");
    }

    private void EmitCheckBox(StringBuilder sb, JsonElement block, string pad)
    {
        var label = block.TryGetProperty("label", out var l) ? l.GetString() : null;
        var readOnly = block.TryGetProperty("readOnly", out var ro) && JsonBool(ro);
        var binding = block.GetProperty("binding");
        var readExpr = ExpressionToCSharp(binding);
        sb.Append($"{pad}<MudCheckBox T=\"bool\" Label=\"{Escape(label)}\"");
        sb.Append($" Value='@(({readExpr}) is bool __b ? __b : false)'");
        if (readOnly)
        {
            sb.Append(" ReadOnly=\"true\"");
        }
        else
        {
            var (rowVar, field) = ParseRequestRef(binding);
            sb.Append($" ValueChanged='@((bool __v) => {rowVar}![\"{field}\"] = __v)'");
        }
        sb.AppendLine(" />");
    }

    // -- event handler emission ---------------------------------------------

    private void EmitEventHandler(StringBuilder sb, string eventName, JsonElement evt)
    {
        var methodName = MapEventNameToMethod(eventName);

        // onInit -> override OnInitializedAsync; other events -> protected async Task <name>
        if (eventName == "onInit")
        {
            sb.AppendLine("    protected override async Task OnInitializedAsync()");
        }
        else
        {
            sb.AppendLine($"    protected async Task {methodName}()");
        }
        sb.AppendLine("    {");

        if (evt.TryGetProperty("tryCatch", out var tryCatch))
        {
            var tryActions = tryCatch.TryGetProperty("tryActions", out var ta) ? ta : evt.GetProperty("actions");
            var catchActions = tryCatch.GetProperty("catchActions");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            foreach (var a in tryActions.EnumerateArray()) EmitAction(sb, a, 12);
            sb.AppendLine("        }");
            sb.AppendLine("        catch");
            sb.AppendLine("        {");
            foreach (var a in catchActions.EnumerateArray()) EmitAction(sb, a, 12);
            sb.AppendLine("        }");
        }
        else if (evt.TryGetProperty("actions", out var actions))
        {
            foreach (var a in actions.EnumerateArray()) EmitAction(sb, a, 8);
        }

        sb.AppendLine("        await Task.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private void EmitAction(StringBuilder sb, JsonElement action, int indent)
    {
        var pad = new string(' ', indent);
        var type = action.GetProperty("type").GetString();
        var p = action.TryGetProperty("params", out var pp) ? pp : default;

        switch (type)
        {
            case "Request":
                {
                    var name = p.GetProperty("requestName").GetString();
                    var req = _ir.GetProperty("requests").GetProperty(name!);
                    var queryName = req.GetProperty("queryName").GetString();
                    sb.AppendLine($"{pad}// TODO: wire request runner. Invokes named query \"{queryName}\".");
                    sb.AppendLine($"{pad}{name} = await ExecuteRequestAsync(\"{name}\");");
                    break;
                }
            case "RefreshRequest":
                {
                    var name = p.GetProperty("requestName").GetString();
                    sb.AppendLine($"{pad}{name} = await ExecuteRequestAsync(\"{name}\");");
                    break;
                }
            case "SetState":
                {
                    var key = p.GetProperty("key").GetString();
                    var val = ExpressionToCSharp(p.GetProperty("value"));
                    sb.AppendLine($"{pad}{key} = {val};");
                    break;
                }
            case "CallMethod":
                {
                    var m = p.GetProperty("methodName").GetString();
                    sb.AppendLine($"{pad}{m}();");
                    break;
                }
            case "CloseModal":
                {
                    var result = p.ValueKind == JsonValueKind.Object && p.TryGetProperty("result", out var r)
                        ? ExpressionToCSharp(r) : "null";
                    sb.AppendLine($"{pad}MudDialog?.Close(DialogResult.Ok<object?>({result}));");
                    break;
                }
            case "ShowError":
                {
                    var msg = ExpressionToCSharp(p.GetProperty("message"));
                    sb.AppendLine($"{pad}Snackbar.Add({msg}, Severity.Error);");
                    break;
                }
            case "Navigate":
                {
                    sb.AppendLine($"{pad}// TODO: inject NavigationManager and call NavigateTo({ExpressionToCSharp(p.GetProperty("url"))});");
                    break;
                }
            case "Validate":
                {
                    sb.AppendLine($"{pad}var __errors = new List<string>();");
                    foreach (var rule in p.GetProperty("rules").EnumerateArray())
                    {
                        var expr = ExpressionToCSharp(rule.GetProperty("expression"));
                        var msg = JsonStringLiteral(rule.GetProperty("message").GetString()!);
                        sb.AppendLine($"{pad}if (!({expr})) __errors.Add({msg});");
                    }
                    sb.AppendLine($"{pad}if (__errors.Count > 0)");
                    sb.AppendLine($"{pad}{{");
                    sb.AppendLine($"{pad}    foreach (var e in __errors) Snackbar.Add(e, Severity.Warning);");
                    sb.AppendLine($"{pad}    return;");
                    sb.AppendLine($"{pad}}}");
                    break;
                }
            case "Confirm":
                {
                    var msg = ExpressionToCSharp(p.GetProperty("message"));
                    sb.AppendLine($"{pad}// TODO: implement confirmation prompt (MudBlazor IDialogService).");
                    sb.AppendLine($"{pad}// Auto-confirming for now -- override OnConfirmAsync in .Custom.cs for real prompts.");
                    sb.AppendLine($"{pad}bool __confirmed = await OnConfirmAsync({msg});");
                    sb.AppendLine($"{pad}if (__confirmed)");
                    sb.AppendLine($"{pad}{{");
                    foreach (var a in p.GetProperty("onConfirm").EnumerateArray())
                        EmitAction(sb, a, indent + 4);
                    sb.AppendLine($"{pad}}}");
                    if (p.TryGetProperty("onCancel", out var oc) && oc.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine($"{pad}else");
                        sb.AppendLine($"{pad}{{");
                        foreach (var a in oc.EnumerateArray())
                            EmitAction(sb, a, indent + 4);
                        sb.AppendLine($"{pad}}}");
                    }
                    break;
                }
            default:
                sb.AppendLine($"{pad}// TODO: unsupported action type {type}");
                break;
        }
    }

    // -- expression emitter --------------------------------------------------

    private string ExpressionToCSharp(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String:
                // bare string is treated as a literal (JsonString operator branch
                // of the expression def)
                return JsonStringLiteral(e.GetString()!);
            case JsonValueKind.Number:
                return e.GetRawText();
            case JsonValueKind.True: return "true";
            case JsonValueKind.False: return "false";
            case JsonValueKind.Null: return "null";
            case JsonValueKind.Object:
                if (e.EnumerateObject().Count() != 1)
                    return "/* malformed expression: not single-key operator object */";
                var prop = e.EnumerateObject().Single();
                return EmitOperator(prop.Name, prop.Value);
            default:
                return "/* unsupported expression kind */";
        }
    }

    private string EmitOperator(string op, JsonElement arg)
    {
        // _state / _request / _const / _op take string-or-element args directly
        switch (op)
        {
            case "_state":   return arg.GetString()!;
            case "_request":
                {
                    var parts = arg.GetString()!.Split('.', 2);
                    return $"{parts[0]}?[\"{parts[1]}\"]";
                }
            case "_const":   return ExpressionToCSharp(arg);
            case "_op":
                // The top-level `operators` section is not yet emitted as a
                // generated class. Emit a TODO so the gap is visible and the
                // C# at least compiles (`default!` returns the type's default).
                // See schema-refinements.md SR-I.
                return $"/* TODO: _op '{arg.GetString()}' -- operators section not yet processed */ default!";
        }

        // n-ary operators: arg is an array
        var argv = arg.EnumerateArray().Select(ExpressionToCSharp).ToArray();
        return op switch
        {
            "_eq"  => $"(Equals({argv[0]}, {argv[1]}))",
            "_ne"  => $"(!Equals({argv[0]}, {argv[1]}))",
            "_lt"  => $"(System.Convert.ToDouble({argv[0]}) < System.Convert.ToDouble({argv[1]}))",
            "_le"  => $"(System.Convert.ToDouble({argv[0]}) <= System.Convert.ToDouble({argv[1]}))",
            "_gt"  => $"(System.Convert.ToDouble({argv[0]}) > System.Convert.ToDouble({argv[1]}))",
            "_ge"  => $"(System.Convert.ToDouble({argv[0]}) >= System.Convert.ToDouble({argv[1]}))",
            // Boolean-context operators need their operands as `bool`. If
            // an operand is itself a bool-producing expression (_isnull, _eq,
            // etc.) the coercion is a no-op; if it's an `object?` rvalue
            // (a _request / _state ref), the coercion is load-bearing.
            // See schema-refinements.md SR-J.
            "_and" => "(" + string.Join(" && ", argv.Select(a => $"System.Convert.ToBoolean({a})")) + ")",
            "_or"  => "(" + string.Join(" || ", argv.Select(a => $"System.Convert.ToBoolean({a})")) + ")",
            "_not" => $"(!System.Convert.ToBoolean({argv[0]}))",
            "_isnull" => $"({argv[0]} == null)",
            // Arithmetic: coerce through double. Operands are object? since
            // request rows are Dictionary<string, object?>; direct + / - / *
            // would fail to bind. A future iteration emitting typed DTOs can
            // drop the coercion and rely on native operators.
            "_add" => "(" + string.Join(" + ", argv.Select(a => $"System.Convert.ToDouble({a})")) + ")",
            "_sub" => $"(System.Convert.ToDouble({argv[0]}) - System.Convert.ToDouble({argv[1]}))",
            "_mul" => "(" + string.Join(" * ", argv.Select(a => $"System.Convert.ToDouble({a})")) + ")",
            "_div" => $"(System.Convert.ToDouble({argv[0]}) / System.Convert.ToDouble({argv[1]}))",
            "_mod" => $"(System.Convert.ToDouble({argv[0]}) % System.Convert.ToDouble({argv[1]}))",
            "_nz"  => $"({argv[0]} ?? {argv[1]})",
            "_iif" => $"(System.Convert.ToBoolean({argv[0]}) ? {argv[1]} : {argv[2]})",
            "_format"  => $"(({argv[0]}).ToString({argv[1]}))",
            "_dlookup" => "/* TODO: _dlookup */ default!",
            "_dcount"  => "/* TODO: _dcount */ 0",
            "_dsum"    => "/* TODO: _dsum */ 0",
            _ => $"/* unknown operator {op} */"
        };
    }

    // -- helpers -------------------------------------------------------------

    private List<string> CollectCallMethodTargets()
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (_ir.TryGetProperty("events", out var events))
        {
            foreach (var evt in events.EnumerateObject())
                CollectCallMethodFromActions(evt.Value, found);
        }
        return found.OrderBy(s => s).ToList();
    }

    private static void CollectCallMethodFromActions(JsonElement evt, HashSet<string> found)
    {
        if (evt.TryGetProperty("actions", out var actions))
            ScanActions(actions, found);
        if (evt.TryGetProperty("tryCatch", out var tc))
        {
            if (tc.TryGetProperty("tryActions", out var ta)) ScanActions(ta, found);
            if (tc.TryGetProperty("catchActions", out var ca)) ScanActions(ca, found);
        }
    }

    private static void ScanActions(JsonElement actions, HashSet<string> found)
    {
        foreach (var a in actions.EnumerateArray())
        {
            if (a.TryGetProperty("type", out var t) && t.GetString() == "CallMethod"
                && a.TryGetProperty("params", out var p)
                && p.TryGetProperty("methodName", out var m))
            {
                found.Add(m.GetString()!);
            }
            if (a.TryGetProperty("params", out var pp))
            {
                if (pp.TryGetProperty("onConfirm", out var oc)) ScanActions(oc, found);
                if (pp.TryGetProperty("onCancel", out var oca) && oca.ValueKind == JsonValueKind.Array)
                    ScanActions(oca, found);
            }
        }
    }

    private static string ClrTypeForState(JsonElement initial) => initial.ValueKind switch
    {
        JsonValueKind.String => "string?",
        JsonValueKind.Number => initial.GetRawText().Contains('.') ? "double?" : "long?",
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.Null => "object?",
        _ => "object?"
    };

    private static string MapEventNameToMethod(string eventName) =>
        char.ToUpperInvariant(eventName[0]) + eventName[1..];

    private static string Escape(string? s) =>
        (s ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

    private static string JsonStringLiteral(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static bool JsonBool(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => string.Equals(e.GetString(), "true", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private bool IsListRequest(string requestName)
    {
        // A request is list-typed if any layout block references it via
        // `optionsRequest`. Walk the layout tree to find out.
        if (!_ir.TryGetProperty("layout", out var layout)) return false;
        return ContainsOptionsRequest(layout, requestName);
    }

    private static bool ContainsOptionsRequest(JsonElement block, string name)
    {
        if (block.ValueKind != JsonValueKind.Object) return false;
        if (block.TryGetProperty("optionsRequest", out var or)
            && or.ValueKind == JsonValueKind.String
            && or.GetString() == name)
        {
            return true;
        }
        if (block.TryGetProperty("blocks", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in children.EnumerateArray())
                if (ContainsOptionsRequest(c, name)) return true;
        }
        return false;
    }

    /// <summary>
    /// Parses a binding expression that the schema (post Session 1 verifier
    /// fix) guarantees is `{ _request: "row.field" }`. Returns the row
    /// variable name and the field name. Used to emit dictionary-indexer
    /// writeback in ValueChanged lambdas.
    /// </summary>
    private static (string rowVar, string field) ParseRequestRef(JsonElement binding)
    {
        if (binding.ValueKind == JsonValueKind.Object
            && binding.TryGetProperty("_request", out var r))
        {
            var parts = r.GetString()!.Split('.', 2);
            return (parts[0], parts[1]);
        }
        // Fallback: not a request ref (would only happen on a readOnly
        // computed binding, which won't actually invoke this helper).
        return ("__unknownRow", "__unknownField");
    }
}
