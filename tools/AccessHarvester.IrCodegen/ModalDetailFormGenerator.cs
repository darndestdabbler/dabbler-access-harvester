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
    private readonly string? _modalTitle;        // SR-L: title to suppress on root Section when modalType=modal
    private readonly bool _hasFieldErrors;       // SR-1: any rule carries a `field`? then emit SetFieldError stub
    private readonly bool _emitsOperators;       // SR-I: top-level `operators` non-empty?

    public ModalDetailFormGenerator(JsonElement ir, string className, string ns)
    {
        _ir = ir;
        _className = className;
        _namespace = ns;
        _modalTitle = (ir.GetProperty("metadata").GetProperty("modalType").GetString() == "modal")
            ? ir.GetProperty("metadata").GetProperty("title").GetString()
            : null;
        _hasFieldErrors = AnyValidateRuleHasField();
        _emitsOperators = ir.TryGetProperty("operators", out var ops)
            && ops.ValueKind == JsonValueKind.Object
            && ops.EnumerateObject().Any();
    }

    private bool AnyValidateRuleHasField()
    {
        if (!_ir.TryGetProperty("events", out var events)) return false;
        foreach (var evt in events.EnumerateObject())
        {
            if (HasFieldRule(evt.Value)) return true;
        }
        return false;
    }

    private static bool HasFieldRule(JsonElement evt)
    {
        var actionsSources = new List<JsonElement>();
        if (evt.TryGetProperty("actions", out var a)) actionsSources.Add(a);
        if (evt.TryGetProperty("tryCatch", out var tc))
        {
            if (tc.TryGetProperty("tryActions", out var ta)) actionsSources.Add(ta);
            if (tc.TryGetProperty("catchActions", out var ca)) actionsSources.Add(ca);
        }
        foreach (var src in actionsSources)
        {
            if (ActionsHaveFieldRule(src)) return true;
        }
        return false;
    }

    /// <summary>
    /// SR-1 + post-cleanup verifier fix: scan an action array recursively
    /// for Validate rules carrying a `field`. Recurses into Confirm's
    /// onConfirm / onCancel branches because EmitAction itself recurses there.
    /// Without this recursion, a field-scoped Validate buried in a Confirm
    /// branch would emit `SetFieldError(...)` calls without the stub method,
    /// producing a compile error.
    /// </summary>
    private static bool ActionsHaveFieldRule(JsonElement actions)
    {
        if (actions.ValueKind != JsonValueKind.Array) return false;
        foreach (var act in actions.EnumerateArray())
        {
            if (!act.TryGetProperty("type", out var t)) continue;
            var type = t.GetString();
            if (type == "Validate"
                && act.TryGetProperty("params", out var p)
                && p.TryGetProperty("rules", out var rules))
            {
                foreach (var rule in rules.EnumerateArray())
                {
                    if (rule.TryGetProperty("field", out _)) return true;
                }
            }
            else if (type == "Confirm" && act.TryGetProperty("params", out var cp))
            {
                if (cp.TryGetProperty("onConfirm", out var oc) && ActionsHaveFieldRule(oc)) return true;
                if (cp.TryGetProperty("onCancel", out var oca) && ActionsHaveFieldRule(oca)) return true;
            }
        }
        return false;
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

        // Page-level state fields. SR-F: emit initial values from `state`.
        if (_ir.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("    // -- state -----------------------------------------------------------");
            foreach (var kv in state.EnumerateObject())
            {
                var clrType = ClrTypeForState(kv.Value);
                var initLiteral = StateInitLiteral(kv.Value, clrType);
                sb.AppendLine(initLiteral is null
                    ? $"    protected {clrType} {kv.Name};"
                    : $"    protected {clrType} {kv.Name} = {initLiteral};");
            }
            sb.AppendLine();
        }

        // Request result rows. SR-A: read explicit cardinality from each
        // request definition. single -> Dictionary<>; list -> List<Dict<>>;
        // command -> no row field at all (fire-and-return).
        if (_ir.TryGetProperty("requests", out var requests) && requests.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("    // -- request result rows ---------------------------------------------");
            foreach (var kv in requests.EnumerateObject())
            {
                var cardinality = GetCardinality(kv.Value);
                if (cardinality == "command") continue; // no field for fire-and-return
                var typeName = cardinality == "list"
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

        // SR-I: emit named-operator methods from the top-level `operators`
        // section. Each entry becomes an instance method on this partial class
        // (so it can read state / request rows). Referenced from
        // `_op: "<name>"` expressions elsewhere in the IR.
        if (_emitsOperators)
        {
            sb.AppendLine("    // -- named operators (SR-I; referenced by `_op` expressions) ---------");
            foreach (var op in _ir.GetProperty("operators").EnumerateObject())
            {
                var methodName = "Op" + char.ToUpperInvariant(op.Name[0]) + op.Name[1..];
                var body = ExpressionToCSharp(op.Value);
                sb.AppendLine($"    protected object? {methodName}() => {body};");
            }
            sb.AppendLine();
        }

        // Stubs for the named-query runner / confirmation prompt / per-field
        // validation. SR-A: split the runner into three signatures so the
        // generator can dispatch by request cardinality without runtime sniffing.
        // SR-1: SetFieldError is only emitted when a Validate rule carries a
        // `field` reference.
        sb.AppendLine("    // -- runtime escape hatches (override in .Custom.cs) -----------------");
        sb.AppendLine("    protected virtual Task<Dictionary<string, object?>?> ExecuteRequestAsync(string requestName)");
        sb.AppendLine("    {");
        sb.AppendLine("        throw new NotImplementedException(");
        sb.AppendLine($"            $\"ExecuteRequestAsync for {{requestName}} is not implemented. \"");
        sb.AppendLine("            + \"Override in the partial class .Custom.cs to wire up your named-query runner.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected virtual Task<List<Dictionary<string, object?>>?> ExecuteRequestListAsync(string requestName)");
        sb.AppendLine("    {");
        sb.AppendLine("        throw new NotImplementedException(");
        sb.AppendLine($"            $\"ExecuteRequestListAsync for {{requestName}} is not implemented. \"");
        sb.AppendLine("            + \"Override in the partial class .Custom.cs.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected virtual Task ExecuteCommandAsync(string commandName)");
        sb.AppendLine("    {");
        sb.AppendLine("        throw new NotImplementedException(");
        sb.AppendLine($"            $\"ExecuteCommandAsync for {{commandName}} is not implemented. \"");
        sb.AppendLine("            + \"Override in the partial class .Custom.cs.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected virtual Task<bool> OnConfirmAsync(string message)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Default: auto-confirm so flows progress without a prompt.");
        sb.AppendLine("        // Override in .Custom.cs to show a real MudDialog confirmation.");
        sb.AppendLine("        return Task.FromResult(true);");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (_hasFieldErrors)
        {
            sb.AppendLine("    protected virtual void SetFieldError(string fieldId, string message)");
            sb.AppendLine("    {");
            sb.AppendLine("        // Default: surface via snackbar with a field-id hint.");
            sb.AppendLine("        // Override in .Custom.cs to anchor errors on the specific MudBlazor control.");
            sb.AppendLine("        Snackbar.Add($\"[{fieldId}] {message}\", Severity.Warning);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Date helpers backing the SR-3 _dateadd / _datediff / _datepart
        // operators. Access's date-interval string ("d", "ww", "m", "yyyy",
        // etc.) is mapped to .NET DateTime arithmetic. Unknown intervals
        // fall back to day granularity.
        sb.AppendLine("    // -- Access date helpers (SR-3 _dateadd / _datediff / _datepart) ----");
        sb.AppendLine("    protected static System.DateTime DateAddAccess(string interval, int number, System.DateTime date) => interval switch");
        sb.AppendLine("    {");
        sb.AppendLine("        \"yyyy\" => date.AddYears(number),");
        sb.AppendLine("        \"q\"    => date.AddMonths(number * 3),");
        sb.AppendLine("        \"m\"    => date.AddMonths(number),");
        sb.AppendLine("        \"ww\"   => date.AddDays(number * 7),");
        sb.AppendLine("        \"w\"    => date.AddDays(number),");
        sb.AppendLine("        \"d\"    => date.AddDays(number),");
        sb.AppendLine("        \"h\"    => date.AddHours(number),");
        sb.AppendLine("        \"n\"    => date.AddMinutes(number),");
        sb.AppendLine("        \"s\"    => date.AddSeconds(number),");
        sb.AppendLine("        _      => date.AddDays(number),");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    protected static long DateDiffAccess(string interval, System.DateTime a, System.DateTime b) => interval switch");
        sb.AppendLine("    {");
        sb.AppendLine("        \"yyyy\" => b.Year - a.Year,");
        sb.AppendLine("        \"q\"    => ((b.Year - a.Year) * 4) + ((b.Month - 1) / 3) - ((a.Month - 1) / 3),");
        sb.AppendLine("        \"m\"    => ((b.Year - a.Year) * 12) + (b.Month - a.Month),");
        sb.AppendLine("        \"ww\"   => (long)(b.Date - a.Date).TotalDays / 7,");
        sb.AppendLine("        \"w\"    => (long)(b.Date - a.Date).TotalDays / 7,");
        sb.AppendLine("        \"d\"    => (long)(b.Date - a.Date).TotalDays,");
        sb.AppendLine("        \"h\"    => (long)(b - a).TotalHours,");
        sb.AppendLine("        \"n\"    => (long)(b - a).TotalMinutes,");
        sb.AppendLine("        \"s\"    => (long)(b - a).TotalSeconds,");
        sb.AppendLine("        _      => (long)(b.Date - a.Date).TotalDays,");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    protected static int DatePartAccess(string interval, System.DateTime date) => interval switch");
        sb.AppendLine("    {");
        sb.AppendLine("        \"yyyy\" => date.Year,");
        sb.AppendLine("        \"q\"    => ((date.Month - 1) / 3) + 1,");
        sb.AppendLine("        \"m\"    => date.Month,");
        sb.AppendLine("        \"d\"    => date.Day,");
        sb.AppendLine("        \"h\"    => date.Hour,");
        sb.AppendLine("        \"n\"    => date.Minute,");
        sb.AppendLine("        \"s\"    => date.Second,");
        sb.AppendLine("        _      => date.Day,");
        sb.AppendLine("    };");
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

        // SR-H: if `visible` is set and not literal true, wrap the entire
        // block emission in @if (Convert.ToBoolean(<visible>)) { ... }.
        var visibleExpr = TryGetGuardExpr(block, "visible");
        if (visibleExpr is not null)
        {
            sb.AppendLine($"{pad}@if ({visibleExpr})");
            sb.AppendLine($"{pad}{{");
            EmitBlockBody(sb, block, type!, indent + 4);
            sb.AppendLine($"{pad}}}");
        }
        else
        {
            EmitBlockBody(sb, block, type!, indent);
        }
    }

    private void EmitBlockBody(StringBuilder sb, JsonElement block, string type, int indent)
    {
        var pad = new string(' ', indent);

        switch (type)
        {
            case "Section":
            case "Group":
                // SR-L: suppress redundant title when it matches the modal's metadata.title.
                if (block.TryGetProperty("title", out var sTitle)
                    && (_modalTitle is null || sTitle.GetString() != _modalTitle))
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
                {
                    var bText = block.GetProperty("text").GetString();
                    var bClick = MapEventNameToMethod(block.GetProperty("onClick").GetString()!);
                    var disabledAttr = EmitEnabledToDisabledAttr(block);
                    sb.AppendLine($"{pad}<MudButton OnClick=\"{bClick}\" Variant=\"Variant.Filled\" Color=\"Color.Primary\"{disabledAttr}>{Escape(bText)}</MudButton>");
                    break;
                }

            default:
                sb.AppendLine($"{pad}<!-- unknown block type: {type} -->");
                break;
        }
    }

    /// <summary>
    /// If `visible` is present and not literal true, returns the C# boolean
    /// expression to gate the block on. Returns null when visibility is
    /// unconstrained (no @if wrap needed).
    /// </summary>
    private string? TryGetGuardExpr(JsonElement block, string propName)
    {
        if (!block.TryGetProperty(propName, out var p)) return null;
        if (p.ValueKind == JsonValueKind.True) return null; // unconstrained
        // Anything else (including literal false, _state ref, operator tree)
        // needs to be evaluated at runtime.
        return $"System.Convert.ToBoolean({ExpressionToCSharp(p)})";
    }

    /// <summary>
    /// SR-H: returns a `Disabled="@(...)"` attribute fragment when the block
    /// declares `enabled` as something other than literal true. Empty string
    /// otherwise. Leading space included so callers can splice it inline.
    /// </summary>
    private string EmitEnabledToDisabledAttr(JsonElement block)
    {
        var enabledExpr = TryGetGuardExpr(block, "enabled");
        if (enabledExpr is null) return string.Empty;
        return $" Disabled='@(!({enabledExpr}))'";
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
        var disabledAttr = EmitEnabledToDisabledAttr(block);
        var dataType = block.TryGetProperty("dataType", out var dt) ? (dt.GetString() ?? "string") : "string";

        // SR-O: dispatch MudBlazor control class by dataType.
        // - string -> MudTextField T="string"
        // - int    -> MudNumericField T="int"
        // - decimal -> MudNumericField T="decimal"
        // - date   -> MudDatePicker
        // - bool   -> not valid on a TextBox block; emit as MudTextField with a comment.
        var (controlName, tParam, readerConvert) = dataType switch
        {
            "int"     => ("MudNumericField", "int", "System.Convert.ToInt32"),
            "decimal" => ("MudNumericField", "decimal", "System.Convert.ToDecimal"),
            "date"    => ("MudDatePicker", null, "(System.DateTime?)System.Convert.ToDateTime"),
            "bool"    => ("MudTextField", "string", "System.Convert.ToString"), // schema misuse; fall back
            _         => ("MudTextField", "string", "System.Convert.ToString"),
        };

        sb.Append($"{pad}<{controlName}");
        if (tParam is not null) sb.Append($" T=\"{tParam}\"");
        if (label is not null) sb.Append($" Label=\"{Escape(label)}\"");

        // MudDatePicker uses Date / DateChanged instead of Value / ValueChanged.
        var valueAttr = controlName == "MudDatePicker" ? "Date" : "Value";
        var changedAttr = controlName == "MudDatePicker" ? "DateChanged" : "ValueChanged";

        sb.Append($" {valueAttr}='@({readerConvert}({readExpr}))'");

        if (readOnly)
        {
            sb.Append(" ReadOnly=\"true\"");
        }
        else
        {
            var (rowVar, field) = ParseRequestRef(binding);
            var lambdaType = tParam ?? "System.DateTime?";
            sb.Append($" {changedAttr}='@(({lambdaType} __v) => {rowVar}![\"{field}\"] = __v)'");
            if (onChanged is not null) sb.Append($" OnBlur=\"{MapEventNameToMethod(onChanged)}\"");
        }
        sb.Append(disabledAttr);
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
        var disabledAttr = EmitEnabledToDisabledAttr(block);

        sb.AppendLine($"{pad}<MudSelect T=\"object\" Label=\"{Escape(label)}\" Value='@({readExpr})' ValueChanged='@((object __v) => {rowVar}![\"{field}\"] = __v)'{disabledAttr}>");
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
        var disabledAttr = EmitEnabledToDisabledAttr(block);
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
        sb.Append(disabledAttr);
        sb.AppendLine(" />");
    }

    // -- event handler emission ---------------------------------------------

    private void EmitEventHandler(StringBuilder sb, string eventName, JsonElement evt)
    {
        var methodName = MapEventNameToMethod(eventName);

        // SR-P: emit `async Task` only when the body actually awaits. Sync
        // handlers get a plain `Task` return and `return Task.CompletedTask;`
        // tail. Detection: any Request / RefreshRequest / Confirm action in
        // the action sequence forces async; other action types are sync.
        bool isAsync = EventNeedsAsync(evt);

        // onInit always overrides OnInitializedAsync (which is itself async).
        if (eventName == "onInit")
        {
            sb.AppendLine("    protected override async Task OnInitializedAsync()");
            isAsync = true;
        }
        else
        {
            sb.AppendLine(isAsync
                ? $"    protected async Task {methodName}()"
                : $"    protected Task {methodName}()");
        }
        sb.AppendLine("    {");

        // SR-K: surface debounce intent as a TODO if the IR declared one.
        if (evt.TryGetProperty("debounce", out var deb)
            && deb.TryGetProperty("delayMs", out var ms))
        {
            sb.AppendLine($"        // TODO: debounce {ms.GetInt32()}ms -- wire to CancellationTokenSource or JS interop delay.");
        }

        if (evt.TryGetProperty("tryCatch", out var tryCatch))
        {
            var tryActions = tryCatch.TryGetProperty("tryActions", out var ta)
                ? ta
                : (evt.TryGetProperty("actions", out var defaultActions) ? defaultActions : default);
            var catchActions = tryCatch.GetProperty("catchActions");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            if (tryActions.ValueKind == JsonValueKind.Array)
                foreach (var a in tryActions.EnumerateArray()) EmitAction(sb, a, 12, isAsync);
            sb.AppendLine("        }");
            sb.AppendLine("        catch");
            sb.AppendLine("        {");
            foreach (var a in catchActions.EnumerateArray()) EmitAction(sb, a, 12, isAsync);
            sb.AppendLine("        }");
        }
        else if (evt.TryGetProperty("actions", out var actions))
        {
            foreach (var a in actions.EnumerateArray()) EmitAction(sb, a, 8, isAsync);
        }

        // For sync handlers, return Task.CompletedTask. For async, no tail is
        // needed -- the method's awaits already produce a continuation.
        if (!isAsync)
        {
            sb.AppendLine("        return Task.CompletedTask;");
        }
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// SR-P: an event handler needs `async` only if it awaits something.
    /// Request / RefreshRequest / Confirm are the only async-producing
    /// actions in the catalog. Walks into Confirm's onConfirm/onCancel
    /// branches recursively.
    /// </summary>
    private static bool EventNeedsAsync(JsonElement evt)
    {
        if (evt.TryGetProperty("actions", out var a) && AnyAsyncAction(a)) return true;
        if (evt.TryGetProperty("tryCatch", out var tc))
        {
            if (tc.TryGetProperty("tryActions", out var ta) && AnyAsyncAction(ta)) return true;
            if (tc.TryGetProperty("catchActions", out var ca) && AnyAsyncAction(ca)) return true;
        }
        return false;
    }

    private static bool AnyAsyncAction(JsonElement actions)
    {
        foreach (var act in actions.EnumerateArray())
        {
            var type = act.GetProperty("type").GetString();
            if (type is "Request" or "RefreshRequest" or "Confirm") return true;
            if (type == "Confirm" && act.TryGetProperty("params", out var p))
            {
                if (p.TryGetProperty("onConfirm", out var oc) && AnyAsyncAction(oc)) return true;
                if (p.TryGetProperty("onCancel", out var oca) && oca.ValueKind == JsonValueKind.Array
                    && AnyAsyncAction(oca)) return true;
            }
        }
        return false;
    }

    private void EmitAction(StringBuilder sb, JsonElement action, int indent, bool isAsyncHandler)
    {
        var pad = new string(' ', indent);
        var type = action.GetProperty("type").GetString();
        var p = action.TryGetProperty("params", out var pp) ? pp : default;
        // SR-P + post-cleanup fix: `return;` only compiles inside `async Task`.
        // For non-async Task handlers, an early exit must `return Task.CompletedTask;`.
        var earlyReturn = isAsyncHandler ? "return;" : "return Task.CompletedTask;";

        switch (type)
        {
            case "Request":
                {
                    var name = p.GetProperty("requestName").GetString();
                    var req = _ir.GetProperty("requests").GetProperty(name!);
                    var queryName = req.GetProperty("queryName").GetString();
                    var cardinality = GetCardinality(req);
                    sb.AppendLine($"{pad}// Invokes named query / command \"{queryName}\" (cardinality={cardinality}).");
                    // SR-A: dispatch by cardinality. `command` requests don't
                    // assign a row variable -- there's no row to model.
                    switch (cardinality)
                    {
                        case "list":
                            sb.AppendLine($"{pad}{name} = await ExecuteRequestListAsync(\"{name}\");");
                            break;
                        case "command":
                            sb.AppendLine($"{pad}await ExecuteCommandAsync(\"{name}\");");
                            break;
                        default: // single
                            sb.AppendLine($"{pad}{name} = await ExecuteRequestAsync(\"{name}\");");
                            break;
                    }
                    break;
                }
            case "RefreshRequest":
                {
                    var name = p.GetProperty("requestName").GetString();
                    var req = _ir.GetProperty("requests").GetProperty(name!);
                    var cardinality = GetCardinality(req);
                    switch (cardinality)
                    {
                        case "list":
                            sb.AppendLine($"{pad}{name} = await ExecuteRequestListAsync(\"{name}\");");
                            break;
                        case "command":
                            sb.AppendLine($"{pad}await ExecuteCommandAsync(\"{name}\");");
                            break;
                        default:
                            sb.AppendLine($"{pad}{name} = await ExecuteRequestAsync(\"{name}\");");
                            break;
                    }
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
                        sb.AppendLine($"{pad}if (!({expr}))");
                        sb.AppendLine($"{pad}{{");
                        sb.AppendLine($"{pad}    __errors.Add({msg});");
                        // SR-1: per-field error anchor. Generator emits a
                        // SetFieldError(fieldId, message) call; the stub on
                        // the partial class falls back to snackbar with a
                        // [fieldId] prefix until the user overrides.
                        if (rule.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String)
                        {
                            var fid = JsonStringLiteral(f.GetString()!);
                            sb.AppendLine($"{pad}    SetFieldError({fid}, {msg});");
                        }
                        sb.AppendLine($"{pad}}}");
                    }
                    sb.AppendLine($"{pad}if (__errors.Count > 0)");
                    sb.AppendLine($"{pad}{{");
                    sb.AppendLine($"{pad}    foreach (var e in __errors) Snackbar.Add(e, Severity.Warning);");
                    sb.AppendLine($"{pad}    {earlyReturn}");
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
                        EmitAction(sb, a, indent + 4, isAsyncHandler);
                    sb.AppendLine($"{pad}}}");
                    if (p.TryGetProperty("onCancel", out var oc) && oc.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine($"{pad}else");
                        sb.AppendLine($"{pad}{{");
                        foreach (var a in oc.EnumerateArray())
                            EmitAction(sb, a, indent + 4, isAsyncHandler);
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

    private int _patternVarCounter = 0;
    private string NextPatternVar() => $"__s{_patternVarCounter++}";

    private string EmitLeft(string[] argv)
    {
        var s = NextPatternVar();
        return $"((System.Convert.ToString({argv[0]}) ?? string.Empty) is var {s} ? {s}.Substring(0, System.Math.Min({s}.Length, System.Math.Max(0, System.Convert.ToInt32({argv[1]})))) : string.Empty)";
    }

    private string EmitRight(string[] argv)
    {
        var s = NextPatternVar();
        return $"((System.Convert.ToString({argv[0]}) ?? string.Empty) is var {s} ? {s}.Substring(System.Math.Max(0, {s}.Length - System.Math.Max(0, System.Convert.ToInt32({argv[1]})))) : string.Empty)";
    }

    private string EmitMid(string[] argv)
    {
        // Access Mid(s, start[, length]): start is 1-based. If start > Len(s), Access returns "".
        // The emitted C# clamps the start index to the string length so Substring never throws.
        var s = NextPatternVar();
        var startClamp = $"System.Math.Min({s}.Length, System.Math.Max(0, System.Convert.ToInt32({argv[1]}) - 1))";
        if (argv.Length == 2)
            return $"((System.Convert.ToString({argv[0]}) ?? string.Empty) is var {s} ? {s}.Substring({startClamp}) : string.Empty)";
        return $"((System.Convert.ToString({argv[0]}) ?? string.Empty) is var {s} ? {s}.Substring({startClamp}, System.Math.Min({s}.Length - {startClamp}, System.Math.Max(0, System.Convert.ToInt32({argv[2]})))) : string.Empty)";
    }

    private string EmitInstr(string[] argv)
    {
        // Access InStr returns 1-based index, 0 if not found. The 3-arg form
        // takes (start, src, target); the 2-arg form takes (src, target).
        // Clamp start to string length so IndexOf never throws.
        if (argv.Length == 2)
            return $"((System.Convert.ToString({argv[0]}) ?? string.Empty).IndexOf(System.Convert.ToString({argv[1]}) ?? string.Empty) + 1)";
        var s = NextPatternVar();
        return $"((System.Convert.ToString({argv[1]}) ?? string.Empty) is var {s} ? {s}.IndexOf(System.Convert.ToString({argv[2]}) ?? string.Empty, System.Math.Min({s}.Length, System.Math.Max(0, System.Convert.ToInt32({argv[0]}) - 1))) + 1 : 0)";
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
                // SR-I: emit a call to the generated instance method. The
                // EmitRazorCs pass emits `protected object? OpFoo() => <expr>;`
                // for each entry in the IR's top-level `operators` section.
                {
                    var opName = arg.GetString()!;
                    var methodName = "Op" + char.ToUpperInvariant(opName[0]) + opName[1..];
                    return $"{methodName}()";
                }
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
            "_dlookup" => "/* TODO: _dlookup -- needs DB access */ default!",
            "_dcount"  => "/* TODO: _dcount -- needs DB access */ 0",
            "_dsum"    => "/* TODO: _dsum -- needs DB access */ 0",
            "_dmin"    => "/* TODO: _dmin -- needs DB access */ 0",
            "_dmax"    => "/* TODO: _dmax -- needs DB access */ 0",
            "_davg"    => "/* TODO: _davg -- needs DB access */ 0",
            // SR-3: string operators. Each pattern-bound emission uses a
            // unique `__sN` variable (via NextPatternVar) so nested usages of
            // the same operator don't collide on a duplicate local declaration.
            "_concat" => "(" + string.Join(" + ", argv.Select(a => $"System.Convert.ToString({a})")) + ")",
            "_trim"   => $"(System.Convert.ToString({argv[0]})?.Trim() ?? string.Empty)",
            "_left"   => EmitLeft(argv),
            "_right"  => EmitRight(argv),
            "_mid"    => EmitMid(argv),
            "_len"    => $"((System.Convert.ToString({argv[0]}) ?? string.Empty).Length)",
            "_instr"  => EmitInstr(argv),
            "_ucase"  => $"(System.Convert.ToString({argv[0]})?.ToUpperInvariant() ?? string.Empty)",
            "_lcase"  => $"(System.Convert.ToString({argv[0]})?.ToLowerInvariant() ?? string.Empty)",
            "_replace" => $"((System.Convert.ToString({argv[0]}) ?? string.Empty).Replace(System.Convert.ToString({argv[1]}) ?? string.Empty, System.Convert.ToString({argv[2]}) ?? string.Empty))",
            // SR-3: date operators
            "_now"      => "System.DateTime.Now",
            "_date"     => "System.DateTime.Today",
            "_dateadd"  => $"DateAddAccess(System.Convert.ToString({argv[0]}) ?? \"d\", System.Convert.ToInt32({argv[1]}), System.Convert.ToDateTime({argv[2]}))",
            "_datediff" => $"DateDiffAccess(System.Convert.ToString({argv[0]}) ?? \"d\", System.Convert.ToDateTime({argv[1]}), System.Convert.ToDateTime({argv[2]}))",
            "_datepart" => $"DatePartAccess(System.Convert.ToString({argv[0]}) ?? \"d\", System.Convert.ToDateTime({argv[1]}))",
            // SR-3: type coercion
            "_cstr"  => $"System.Convert.ToString({argv[0]})",
            "_cint"  => $"System.Convert.ToInt32({argv[0]})",
            "_cdate" => $"System.Convert.ToDateTime({argv[0]})",
            "_cdbl"  => $"System.Convert.ToDouble({argv[0]})",
            "_ccur"  => $"System.Convert.ToDecimal({argv[0]})",
            // SR-3: comparison
            "_like"  => $"/* TODO: _like -- VBA Like is not a direct C# operator; needs regex or wildcard helper */ (System.Convert.ToString({argv[0]}) == System.Convert.ToString({argv[1]}))",
            _ => $"/* unknown operator {op} */ default!"
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

    /// <summary>
    /// SR-A: returns the explicit `cardinality` declared on a request, defaulting
    /// to "single" for backward compat with pre-SR-A IR files. Recognized values:
    /// "single" (Dictionary&lt;string,object?&gt;), "list" (List of dict),
    /// "command" (no row payload).
    /// </summary>
    private static string GetCardinality(JsonElement request)
    {
        if (request.TryGetProperty("cardinality", out var c)
            && c.ValueKind == JsonValueKind.String)
        {
            return c.GetString()!;
        }
        return "single";
    }

    /// <summary>
    /// SR-F: produces a C# initializer literal for a state value, or null if
    /// no explicit initializer is needed (i.e. the JSON value is null AND the
    /// CLR type allows nullable default).
    /// </summary>
    private static string? StateInitLiteral(JsonElement value, string clrType) => value.ValueKind switch
    {
        JsonValueKind.String => "\"" + value.GetString()!.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null, // C# default() is null for nullable types
        _ => null,
    };

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
