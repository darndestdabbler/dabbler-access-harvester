using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AccessHarvester.Access;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds <see cref="RowSourceSpec"/> records for combo and list controls
/// on an Access form. Walks the form's design tree, lifts the row-source
/// property bag for each combo / list, and — when an
/// <see cref="EventHandlerClassification"/> map is supplied — annotates
/// each spec with cascading drivers harvested from event-handler signals.
/// </summary>
/// <remarks>
/// The builder is pure (no I/O, no symbol-table resolution) so it stays
/// unit-testable per the project's Testing Hierarchy. SQL parsing is
/// deliberately deferred per the spec's Standards: the SQL string is
/// preserved verbatim and a downstream pipeline stage parses it into a
/// <see cref="QueryAbstraction"/>.
/// </remarks>
public static class RowSourceSpecBuilder
{
    private static readonly string[] _eligibleControlTypes = ["ComboBox", "ListBox"];

    /// <summary>
    /// Build a single <see cref="RowSourceSpec"/> from one combo / list
    /// control's properties. Returns null when <paramref name="control"/>
    /// is not a combo or list, or when the control declared no
    /// row-source-related properties at all (an unbound combo whose
    /// row source is filled at runtime is still emitted with
    /// <see cref="RowSourceKind.Empty"/> when any of <c>BoundColumn</c>,
    /// <c>ColumnCount</c>, or <c>LimitToList</c> were written).
    /// </summary>
    public static RowSourceSpec? Build(AccessControl control, string formName)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(formName);

        if (!_eligibleControlTypes.Contains(control.Type)) return null;
        if (string.IsNullOrEmpty(control.Name)) return null;

        var props = control.Properties;
        var rowSource = ReadString(props, "RowSource");
        var rowSourceType = ReadString(props, "RowSourceType");
        var boundColumn = ReadInt(props, "BoundColumn");
        var columnCount = ReadInt(props, "ColumnCount");
        var columnWidths = ReadString(props, "ColumnWidths");
        var limitToList = ReadBool(props, "LimitToList");

        if (rowSource is null && rowSourceType is null && boundColumn is null &&
            columnCount is null && columnWidths is null && limitToList is null)
        {
            return null;
        }

        var (kind, targetName, inlineSql, valueListItems, fieldListSourceTable) =
            ClassifyRowSource(rowSource, rowSourceType);

        var columns = ParseColumnWidths(columnWidths);

        return new RowSourceSpec
        {
            SourceFormName = formName,
            ControlName = control.Name!,
            ControlType = control.Type,
            Kind = kind,
            RowSourceText = rowSource,
            RowSourceType = rowSourceType,
            TargetName = targetName,
            InlineSql = inlineSql,
            ValueListItems = valueListItems,
            FieldListSourceTable = fieldListSourceTable,
            BoundColumn = boundColumn,
            ColumnCount = columnCount,
            Columns = columns,
            LimitToList = limitToList,
        };
    }

    /// <summary>
    /// Walk an entire form, building one <see cref="RowSourceSpec"/> per
    /// combo / list control. When <paramref name="handlerClassifications"/>
    /// is supplied, each spec is annotated with the cascading drivers
    /// harvested from event-handler signals whose target is
    /// <c>Me!&lt;control&gt;.RowSource</c>. Returns an empty list when the
    /// form has no eligible controls.
    /// </summary>
    public static IReadOnlyList<RowSourceSpec> BuildAll(
        FormMetadata form,
        IReadOnlyList<EventHandlerClassification>? handlerClassifications = null)
    {
        ArgumentNullException.ThrowIfNull(form);

        var specs = new List<RowSourceSpec>();
        if (form.Children is { Count: > 0 })
        {
            foreach (var node in EnumerateAllControls(form.Children))
            {
                var spec = Build(node, form.Name);
                if (spec is not null) specs.Add(spec);
            }
        }

        if (specs.Count == 0 || handlerClassifications is null || handlerClassifications.Count == 0)
        {
            return specs;
        }

        return AnnotateCascadingDrivers(specs, handlerClassifications);
    }

    /// <summary>
    /// Annotate an existing list of <see cref="RowSourceSpec"/> records
    /// with cascading-driver references discovered in
    /// <paramref name="handlerClassifications"/>. Useful when the specs
    /// were built earlier (e.g., from a cached design-tree pass) and
    /// event-handler classification ran later in the pipeline. Returns a
    /// new list — input records are not mutated.
    /// </summary>
    public static IReadOnlyList<RowSourceSpec> AnnotateCascadingDrivers(
        IReadOnlyList<RowSourceSpec> specs,
        IReadOnlyList<EventHandlerClassification> handlerClassifications)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(handlerClassifications);

        var driversByControl = CollectRowSourceDrivers(handlerClassifications);

        if (driversByControl.Count == 0) return specs;

        var annotated = new List<RowSourceSpec>(specs.Count);
        foreach (var spec in specs)
        {
            if (driversByControl.TryGetValue(spec.ControlName, out var drivers) && drivers.Count > 0)
            {
                annotated.Add(spec with { CascadingDrivers = drivers });
            }
            else
            {
                annotated.Add(spec);
            }
        }
        return annotated;
    }

    // ── Row-source classification ──────────────────────────────────────

    private static (RowSourceKind Kind, string? Target, string? Sql,
                    IReadOnlyList<string>? ValueList, string? FieldListTable)
        ClassifyRowSource(string? rowSource, string? rowSourceType)
    {
        var trimmedType = rowSourceType?.Trim();
        var trimmedSource = rowSource?.Trim();

        if (string.Equals(trimmedType, "Value List", StringComparison.OrdinalIgnoreCase))
        {
            var items = SplitValueList(rowSource);
            return (RowSourceKind.ValueList, null, null, items, null);
        }
        if (string.Equals(trimmedType, "Field List", StringComparison.OrdinalIgnoreCase))
        {
            return (RowSourceKind.FieldList, null, null, null,
                    string.IsNullOrEmpty(trimmedSource) ? null : trimmedSource);
        }

        if (string.IsNullOrEmpty(trimmedSource))
        {
            return (RowSourceKind.Empty, null, null, null, null);
        }

        if (LooksLikeSql(trimmedSource))
        {
            return (RowSourceKind.TableOrQueryInlineSql, null, rowSource, null, null);
        }

        if (IsBareword(trimmedSource))
        {
            return (RowSourceKind.TableOrQueryNamed, trimmedSource, null, null, null);
        }

        return (RowSourceKind.Unknown, null, null, null, null);
    }

    private static IReadOnlyList<string>? SplitValueList(string? rowSource)
    {
        if (string.IsNullOrEmpty(rowSource)) return null;
        var raw = rowSource.Split(';');
        var items = new List<string>(raw.Length);
        foreach (var part in raw)
        {
            var trimmed = part.Trim();
            if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }
            items.Add(trimmed);
        }
        // Access often has a trailing semicolon producing a final empty
        // entry. Drop only that single trailing empty so leading or
        // intentional middle blanks survive.
        if (items.Count > 0 && items[^1].Length == 0)
        {
            items.RemoveAt(items.Count - 1);
        }
        return items.Count == 0 ? null : items;
    }

    private static readonly HashSet<string> _sqlLeadingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "PARAMETERS", "TRANSFORM", "WITH",
    };

    private static bool LooksLikeSql(string trimmed)
    {
        var firstWord = FirstWord(trimmed);
        return firstWord is not null && _sqlLeadingKeywords.Contains(firstWord);
    }

    private static string? FirstWord(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var i = 0;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        var start = i;
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
        return i == start ? null : text.Substring(start, i - start);
    }

    private static bool IsBareword(string trimmed)
    {
        foreach (var ch in trimmed)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == ' ' || ch == '[' || ch == ']'))
            {
                return false;
            }
        }
        return trimmed.Length > 0;
    }

    // ── ColumnWidths parsing ───────────────────────────────────────────

    private static IReadOnlyList<RowSourceColumnDescriptor>? ParseColumnWidths(string? columnWidths)
    {
        if (string.IsNullOrEmpty(columnWidths)) return null;

        var parts = columnWidths.Split(';');
        var descriptors = new List<RowSourceColumnDescriptor>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            var raw = parts[i].Trim();
            int? twips = null;
            if (raw.Length > 0 &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                twips = parsed;
            }
            descriptors.Add(new RowSourceColumnDescriptor
            {
                Index = i + 1,
                WidthTwips = twips,
                WidthInches = twips is null ? null : Math.Round(twips.Value / 1440.0, 4),
                Hidden = twips is null ? null : twips.Value == 0,
            });
        }
        return descriptors.Count == 0 ? null : descriptors;
    }

    // ── Cascading-driver detection ─────────────────────────────────────

    private static readonly Regex _meRowSourceTarget = new(
        @"^Me\s*[!\.]\s*(?<control>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*RowSource$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static Dictionary<string, List<RowSourceCascadingDriver>>
        CollectRowSourceDrivers(IReadOnlyList<EventHandlerClassification> handlers)
    {
        var byTargetControl = new Dictionary<string, List<RowSourceCascadingDriver>>(StringComparer.Ordinal);

        foreach (var handler in handlers)
        {
            foreach (var signal in handler.Signals)
            {
                if (signal.Kind != EventHandlerSignalKind.ControlAssignment) continue;
                if (signal.TargetExpression is null) continue;

                var match = _meRowSourceTarget.Match(signal.TargetExpression);
                if (!match.Success) continue;

                var targetControl = match.Groups["control"].Value;
                var driverControl = ParseDriverControlFromProcedureName(handler.ProcedureName);

                if (!byTargetControl.TryGetValue(targetControl, out var list))
                {
                    list = new List<RowSourceCascadingDriver>();
                    byTargetControl[targetControl] = list;
                }
                list.Add(new RowSourceCascadingDriver
                {
                    DriverControlName = driverControl,
                    DriverProcedureName = handler.ProcedureName,
                    Line = signal.Line,
                    Column = signal.Column,
                });
            }
        }

        return byTargetControl;
    }

    private static string? ParseDriverControlFromProcedureName(string procedureName)
    {
        if (string.IsNullOrEmpty(procedureName)) return null;
        var underscore = procedureName.LastIndexOf('_');
        if (underscore <= 0) return null;
        var head = procedureName.Substring(0, underscore);
        if (string.Equals(head, "Form", StringComparison.OrdinalIgnoreCase)) return null;
        return head;
    }

    // ── Tree walking ───────────────────────────────────────────────────

    private static IEnumerable<AccessControl> EnumerateAllControls(IReadOnlyList<AccessControl> roots)
    {
        var stack = new Stack<AccessControl>(roots.Count);
        for (var i = roots.Count - 1; i >= 0; i--) stack.Push(roots[i]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            if (node.Children is { Count: > 0 })
            {
                for (var i = node.Children.Count - 1; i >= 0; i--) stack.Push(node.Children[i]);
            }
        }
    }

    // ── Property readers ───────────────────────────────────────────────

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement>? props, string key)
    {
        if (props is null || !props.TryGetValue(key, out var elem)) return null;
        return elem.ValueKind == JsonValueKind.String ? elem.GetString() : null;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, JsonElement>? props, string key)
    {
        if (props is null || !props.TryGetValue(key, out var elem)) return null;
        return elem.ValueKind switch
        {
            JsonValueKind.Number when elem.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(elem.GetString(),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null,
        };
    }

    private static bool? ReadBool(IReadOnlyDictionary<string, JsonElement>? props, string key)
    {
        if (props is null || !props.TryGetValue(key, out var elem)) return null;
        return elem.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when elem.TryGetInt32(out var n) => n != 0,
            JsonValueKind.String =>
                bool.TryParse(elem.GetString(), out var b) ? b
                    : (string.Equals(elem.GetString(), "Yes", StringComparison.OrdinalIgnoreCase) ? true
                       : string.Equals(elem.GetString(), "No", StringComparison.OrdinalIgnoreCase) ? false
                       : (bool?)null),
            _ => null,
        };
    }
}
