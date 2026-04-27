using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using AccessHarvester.Access;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds a <see cref="FormComponentSpec"/> from an extracted
/// <see cref="FormMetadata"/> tree. Walks the design hierarchy, lifts
/// section / control / subform / event-handler / validation-rule facets,
/// and leaves slots that require resolved cross-source information
/// (symbol-table-driven query references, event-handler classification,
/// validation classification) null for downstream pipeline stages.
/// </summary>
/// <remarks>
/// The builder is intentionally pure (no I/O, no symbol-table resolution)
/// so it stays unit-testable per the <c>project-guidance.md</c> Testing
/// Hierarchy. Reads only the input <see cref="FormMetadata"/>; the output
/// is a value record with no shared mutable state.
/// </remarks>
public static class FormComponentSpecBuilder
{
    /// <summary>
    /// Build a <see cref="FormComponentSpec"/> from a populated
    /// <see cref="FormMetadata"/>. Required-slot values are taken from the
    /// metadata (form name, section names); deferred slots are left null.
    /// </summary>
    public static FormComponentSpec Build(FormMetadata form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var sections = new List<FormSectionDescriptor>();
        var controls = new List<FormControlSpec>();
        var subforms = new List<SubformRelationship>();
        var events = new List<EventHandlerReference>();
        var validations = new List<ValidationRuleReference>();

        if (form.Children is { Count: > 0 })
        {
            foreach (var topNode in EnumerateBeginUnwrapped(form.Children))
            {
                if (IsSectionType(topNode.Type))
                {
                    var section = BuildSection(topNode, controls, subforms, events, validations);
                    sections.Add(section);
                }
                else if (IsAnonymousBegin(topNode))
                {
                    // Already unwrapped above; defensive no-op for nested Begin wrappers.
                    continue;
                }
                else
                {
                    // A leaf control directly under the form root with no
                    // section wrapper. The control is emitted with
                    // SectionName = null; consumers grouping by section
                    // surface the orphans directly rather than seeing them
                    // attributed to a synthetic Detail bucket they did not
                    // declare.
                    AddLeaf(topNode, sectionName: null, controls, subforms, events, validations);
                }
            }
        }

        AddFormLevelEvents(form, events);
        AddFormLevelValidation(form, validations);

        var dataSource = BuildDataSource(form);

        return new FormComponentSpec
        {
            SourceFormName = form.Name,
            ModuleName = form.ModuleName,
            SuggestedComponentName = SuggestComponentName(form.Name),
            DataSource = dataSource,
            DefaultView = form.DefaultView,
            GeometryIn = form.GeometryIn,
            Sections = sections.Count == 0 ? null : sections,
            Controls = controls.Count == 0 ? null : controls,
            Subforms = subforms.Count == 0 ? null : subforms,
            EventHandlers = events.Count == 0 ? null : events,
            ValidationRules = validations.Count == 0 ? null : validations,
        };
    }

    // ── Component-name suggestion ──────────────────────────────────────

    /// <summary>
    /// Derive a PascalCase component-name suggestion from a form name.
    /// Strips Hungarian-style prefixes (<c>frm</c>, <c>sfrm</c>) and
    /// non-identifier characters, then appends the <c>Component</c>
    /// suffix. Returns null when the source yields no usable letters or
    /// digits.
    /// </summary>
    public static string? SuggestComponentName(string formName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formName);

        var alnum = new StringBuilder(formName.Length);
        foreach (var ch in formName)
        {
            if (char.IsLetterOrDigit(ch))
                alnum.Append(ch);
            else if (ch is '_' or '-' or ' ')
                alnum.Append(' ');
        }
        var stripped = alnum.ToString().Trim();
        if (stripped.Length == 0) return null;

        stripped = StripFormPrefix(stripped);

        var pascal = ToPascalCase(stripped);
        if (pascal.Length == 0) return null;

        return $"{pascal}Component";
    }

    private static string StripFormPrefix(string s)
    {
        // Common Access-author conventions: frm, sfrm (subform), frmsub.
        string[] prefixes = ["sfrm", "frmsub", "frm"];
        foreach (var p in prefixes)
        {
            if (s.Length > p.Length &&
                s.StartsWith(p, StringComparison.OrdinalIgnoreCase) &&
                char.IsUpper(s[p.Length]))
            {
                return s[p.Length..];
            }
        }
        return s;
    }

    private static string ToPascalCase(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool capitaliseNext = true;
        foreach (var ch in s)
        {
            if (ch == ' ')
            {
                capitaliseNext = true;
                continue;
            }
            sb.Append(capitaliseNext ? char.ToUpperInvariant(ch) : ch);
            capitaliseNext = false;
        }
        return sb.ToString();
    }

    // ── Section walk ───────────────────────────────────────────────────

    private static FormSectionDescriptor BuildSection(
        AccessControl sectionNode,
        List<FormControlSpec> controls,
        List<SubformRelationship> subforms,
        List<EventHandlerReference> events,
        List<ValidationRuleReference> validations)
    {
        // Many SaveAsText outputs name the detail-section node "Section"
        // rather than "Detail" — preserve the verbatim Type / Name pair
        // and classify on the more descriptive of the two.
        var sourceName = sectionNode.Name ?? sectionNode.Type;
        var kind = ClassifySection(sectionNode.Type, sectionNode.Name);

        var controlNames = new List<string>();
        if (sectionNode.Children is { Count: > 0 })
        {
            foreach (var child in EnumerateBeginUnwrapped(sectionNode.Children))
            {
                AddLeaf(child, sourceName, controls, subforms, events, validations, controlNames);
            }
        }

        return new FormSectionDescriptor
        {
            Name = sourceName,
            Kind = kind,
            GeometryIn = sectionNode.GeometryIn,
            ControlNames = controlNames.Count == 0 ? null : controlNames,
        };
    }

    private static FormSectionKind ClassifySection(string type, string? name)
    {
        // Access SaveAsText emits the section as e.g. "Section" with a
        // Name like "Detail", or as the literal "FormHeader" type.
        var key = !string.IsNullOrEmpty(name) ? name! : type;
        return key switch
        {
            "FormHeader" => FormSectionKind.FormHeader,
            "FormFooter" => FormSectionKind.FormFooter,
            "PageHeader" => FormSectionKind.PageHeader,
            "PageFooter" => FormSectionKind.PageFooter,
            "Detail" => FormSectionKind.Detail,
            _ when key.StartsWith("GroupLevel", StringComparison.Ordinal) &&
                   key.EndsWith("Header", StringComparison.Ordinal)
                => FormSectionKind.GroupLevelHeader,
            _ when key.StartsWith("GroupLevel", StringComparison.Ordinal) &&
                   key.EndsWith("Footer", StringComparison.Ordinal)
                => FormSectionKind.GroupLevelFooter,
            "Section" => FormSectionKind.Detail,
            _ => FormSectionKind.Other,
        };
    }

    // ── Leaf walk ──────────────────────────────────────────────────────

    private static void AddLeaf(
        AccessControl node,
        string? sectionName,
        List<FormControlSpec> controls,
        List<SubformRelationship> subforms,
        List<EventHandlerReference> events,
        List<ValidationRuleReference> validations,
        List<string>? sectionControlNames = null)
    {
        if (IsAnonymousBegin(node))
        {
            // Recurse through the wrapper, attributing children to the
            // current section.
            if (node.Children is { Count: > 0 })
            {
                foreach (var inner in node.Children)
                {
                    AddLeaf(inner, sectionName, controls, subforms, events, validations, sectionControlNames);
                }
            }
            return;
        }

        // A nested Section under another section is unusual but possible
        // (e.g., GroupLevel inside Detail in a report). We do not recurse
        // structural sections inside other sections — that is reserved for
        // future report-specific work. Instead we surface unnamed
        // controls only.
        if (string.IsNullOrEmpty(node.Name))
        {
            // Anonymous structural / decorative node — drop.
            return;
        }

        var control = new FormControlSpec
        {
            Name = node.Name!,
            SourceType = node.Type,
            ComponentKind = ClassifyComponentKind(node.Type),
            SectionName = sectionName,
            Label = node.Label,
            GeometryIn = node.GeometryIn,
            Binding = BuildBinding(node),
            RenderHints = BuildRenderHints(node),
            NonDefaultProperties = node.NonDefaultProperties,
        };
        controls.Add(control);
        sectionControlNames?.Add(node.Name!);

        AddControlLevelEvents(node, events);
        AddControlLevelValidation(node, validations);

        if (IsSubformType(node.Type))
        {
            subforms.Add(BuildSubformRelationship(node));
        }
    }

    private static FormControlComponentKind ClassifyComponentKind(string sourceType)
        => sourceType switch
        {
            "TextBox" => FormControlComponentKind.TextInput,
            "ComboBox" => FormControlComponentKind.Dropdown,
            "ListBox" => FormControlComponentKind.ListBox,
            "CheckBox" or "OptionButton" or "ToggleButton" => FormControlComponentKind.Toggle,
            "CommandButton" => FormControlComponentKind.Button,
            "Label" => FormControlComponentKind.Label,
            "Image" => FormControlComponentKind.Image,
            "Subform" or "Subreport" => FormControlComponentKind.SubformHost,
            "TabCtl" or "TabControl" => FormControlComponentKind.TabControl,
            "Rectangle" or "Line" => FormControlComponentKind.Decoration,
            _ => FormControlComponentKind.Other,
        };

    // ── Binding lift ───────────────────────────────────────────────────

    private static ControlBinding? BuildBinding(AccessControl node)
    {
        var props = node.Properties;
        if (props is null) return null;

        var controlSource = ReadString(props, "ControlSource");
        var rowSource = ReadString(props, "RowSource");
        var rowSourceType = ReadString(props, "RowSourceType");
        var boundColumn = ReadInt(props, "BoundColumn");
        var columnCount = ReadInt(props, "ColumnCount");
        var columnWidths = ReadString(props, "ColumnWidths");

        if (controlSource is null && rowSource is null && rowSourceType is null &&
            boundColumn is null && columnCount is null && columnWidths is null)
        {
            return null;
        }

        var kind = ClassifyBindingKind(controlSource, rowSource);

        return new ControlBinding
        {
            Kind = kind,
            ControlSource = controlSource,
            RowSource = rowSource,
            RowSourceType = rowSourceType,
            BoundColumn = boundColumn,
            ColumnCount = columnCount,
            ColumnWidths = columnWidths,
        };
    }

    private static ControlBindingKind ClassifyBindingKind(string? controlSource, string? rowSource)
    {
        if (!string.IsNullOrEmpty(controlSource))
        {
            return controlSource!.StartsWith('=')
                ? ControlBindingKind.ComputedExpression
                : ControlBindingKind.TableField;
        }
        if (!string.IsNullOrEmpty(rowSource))
        {
            return ControlBindingKind.RowSourceList;
        }
        return ControlBindingKind.Unbound;
    }

    // ── Render hints ───────────────────────────────────────────────────

    private static ControlRenderHints? BuildRenderHints(AccessControl node)
    {
        var props = node.Properties;
        if (props is null) return null;

        var caption = ReadString(props, "Caption");
        var format = ReadString(props, "Format");
        var inputMask = ReadString(props, "InputMask");
        var defaultValue = ReadString(props, "DefaultValue");
        var visible = ReadBool(props, "Visible");
        var enabled = ReadBool(props, "Enabled");
        var locked = ReadBool(props, "Locked");
        var tabIndex = ReadInt(props, "TabIndex");

        if (caption is null && format is null && inputMask is null && defaultValue is null &&
            visible is null && enabled is null && locked is null && tabIndex is null)
        {
            return null;
        }

        return new ControlRenderHints
        {
            Caption = caption,
            Format = format,
            InputMask = inputMask,
            DefaultValue = defaultValue,
            IsVisible = visible,
            IsEnabled = enabled,
            IsLocked = locked,
            TabIndex = tabIndex,
        };
    }

    // ── Subform relationship ───────────────────────────────────────────

    private static SubformRelationship BuildSubformRelationship(AccessControl node)
    {
        var props = node.Properties;
        var sourceObject = props is null ? null : ReadString(props, "SourceObject");
        var linkChild = props is null ? null : ReadString(props, "LinkChildFields");
        var linkMaster = props is null ? null : ReadString(props, "LinkMasterFields");

        return new SubformRelationship
        {
            SubformControlName = node.Name!,
            ContainedObjectName = sourceObject,
            LinkChildFields = SplitSemicolonList(linkChild),
            LinkMasterFields = SplitSemicolonList(linkMaster),
        };
    }

    // ── Event handler references ───────────────────────────────────────

    private static void AddControlLevelEvents(AccessControl node, List<EventHandlerReference> events)
    {
        var props = node.Properties;
        if (props is null) return;

        foreach (var (propertyName, eventName) in EnumerateEventProperties(props))
        {
            events.Add(new EventHandlerReference
            {
                HostKind = EventHandlerHostKind.Control,
                ControlName = node.Name,
                PropertyName = propertyName,
                EventName = eventName,
                VbaProcedureName = node.Name is null
                    ? null
                    : $"{node.Name}_{eventName}",
            });
        }
    }

    private static void AddFormLevelEvents(FormMetadata form, List<EventHandlerReference> events)
    {
        var props = form.Properties;
        if (props is null) return;

        foreach (var (propertyName, eventName) in EnumerateEventProperties(props))
        {
            events.Add(new EventHandlerReference
            {
                HostKind = EventHandlerHostKind.Form,
                ControlName = null,
                PropertyName = propertyName,
                EventName = eventName,
                VbaProcedureName = $"Form_{eventName}",
            });
        }
    }

    private static IEnumerable<(string PropertyName, string EventName)> EnumerateEventProperties(
        IReadOnlyDictionary<string, JsonElement> props)
    {
        foreach (var (key, value) in props)
        {
            if (!IsEventProperty(key)) continue;
            if (!IsEventProcedureSentinel(value)) continue;
            yield return (key, EventNameFromPropertyName(key));
        }
    }

    private static bool IsEventProperty(string propertyName)
    {
        if (propertyName.StartsWith("On", StringComparison.Ordinal) && propertyName.Length > 2 &&
            char.IsUpper(propertyName[2]))
        {
            return true;
        }
        // Access also uses bare "AfterUpdate", "BeforeUpdate", "AfterInsert",
        // "BeforeInsert", "BeforeDelConfirm", "AfterDelConfirm", "AfterFinalRender"
        // (chart) family without an "On" prefix.
        return propertyName.StartsWith("After", StringComparison.Ordinal) ||
               propertyName.StartsWith("Before", StringComparison.Ordinal);
    }

    private static string EventNameFromPropertyName(string propertyName)
    {
        // OnClick -> Click; OnDblClick -> DblClick; AfterUpdate -> AfterUpdate.
        if (propertyName.StartsWith("On", StringComparison.Ordinal) && propertyName.Length > 2 &&
            char.IsUpper(propertyName[2]))
        {
            return propertyName[2..];
        }
        return propertyName;
    }

    private static bool IsEventProcedureSentinel(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) return false;
        var s = value.GetString();
        return string.Equals(s, "[Event Procedure]", StringComparison.Ordinal);
    }

    // ── Validation rule references ─────────────────────────────────────

    private static void AddControlLevelValidation(AccessControl node, List<ValidationRuleReference> validations)
    {
        var props = node.Properties;
        if (props is null) return;

        var rule = ReadString(props, "ValidationRule");
        var text = ReadString(props, "ValidationText");
        if (!string.IsNullOrEmpty(rule))
        {
            validations.Add(new ValidationRuleReference
            {
                HostKind = ValidationRuleHostKind.Control,
                ControlName = node.Name,
                Source = ValidationRuleSource.ControlValidationRule,
                RuleExpression = rule,
                ValidationText = text,
            });
        }

        if (ReadBool(props, "Required") == true)
        {
            validations.Add(new ValidationRuleReference
            {
                HostKind = ValidationRuleHostKind.Control,
                ControlName = node.Name,
                Source = ValidationRuleSource.RequiredField,
            });
        }

        if (ReadBool(props, "LimitToList") == true)
        {
            validations.Add(new ValidationRuleReference
            {
                HostKind = ValidationRuleHostKind.Control,
                ControlName = node.Name,
                Source = ValidationRuleSource.LimitToList,
            });
        }

        // If a BeforeUpdate event procedure is present, surface a
        // cross-reference so consumers find validation without scanning
        // every event handler.
        if (props.TryGetValue("BeforeUpdate", out var beforeUpdate) &&
            IsEventProcedureSentinel(beforeUpdate))
        {
            validations.Add(new ValidationRuleReference
            {
                HostKind = ValidationRuleHostKind.Control,
                ControlName = node.Name,
                Source = ValidationRuleSource.BeforeUpdateHandler,
            });
        }
    }

    private static void AddFormLevelValidation(FormMetadata form, List<ValidationRuleReference> validations)
    {
        var props = form.Properties;
        if (props is null) return;

        var rule = ReadString(props, "ValidationRule");
        var text = ReadString(props, "ValidationText");
        if (!string.IsNullOrEmpty(rule))
        {
            validations.Add(new ValidationRuleReference
            {
                HostKind = ValidationRuleHostKind.Form,
                ControlName = null,
                Source = ValidationRuleSource.RecordValidationRule,
                RuleExpression = rule,
                ValidationText = text,
            });
        }

        if (props.TryGetValue("BeforeUpdate", out var beforeUpdate) &&
            IsEventProcedureSentinel(beforeUpdate))
        {
            validations.Add(new ValidationRuleReference
            {
                HostKind = ValidationRuleHostKind.Form,
                ControlName = null,
                Source = ValidationRuleSource.BeforeUpdateHandler,
            });
        }
    }

    // ── Form-level data source ─────────────────────────────────────────

    private static FormDataSourceBinding? BuildDataSource(FormMetadata form)
    {
        var rs = form.RecordSource;
        var filter = form.Filter;
        var orderBy = form.OrderBy;

        if (rs is null && filter is null && orderBy is null)
        {
            return null;
        }

        var (kind, named, sql) = ClassifyRecordSource(rs);
        return new FormDataSourceBinding
        {
            Kind = kind,
            TargetName = named,
            InlineSql = sql,
            Filter = filter,
            OrderBy = orderBy,
        };
    }

    private static (FormDataSourceKind Kind, string? Named, string? Sql) ClassifyRecordSource(string? recordSource)
    {
        if (string.IsNullOrWhiteSpace(recordSource))
        {
            return (FormDataSourceKind.None, null, null);
        }

        var trimmed = recordSource.TrimStart();
        if (LooksLikeSql(trimmed))
        {
            return (FormDataSourceKind.InlineSql, null, recordSource);
        }

        if (LooksLikeBareword(recordSource))
        {
            return (FormDataSourceKind.NamedReference, recordSource, null);
        }

        return (FormDataSourceKind.Unknown, null, null);
    }

    private static bool LooksLikeSql(string s)
    {
        // JET DML keywords that can start a record source. The leading
        // token comparison is case-insensitive; keywords are followed by
        // whitespace or end-of-string.
        string[] starters = ["SELECT", "PARAMETERS", "TRANSFORM", "INSERT", "UPDATE", "DELETE"];
        foreach (var k in starters)
        {
            if (s.Length >= k.Length &&
                s.StartsWith(k, StringComparison.OrdinalIgnoreCase) &&
                (s.Length == k.Length || char.IsWhiteSpace(s[k.Length])))
            {
                return true;
            }
        }
        return false;
    }

    private static bool LooksLikeBareword(string s)
    {
        // A bareword is a single identifier or bracket-quoted name. We
        // accept identifiers built from letters / digits / underscore, plus
        // bracketed names that may contain whitespace internally — Access
        // permits "[My Table]" as a record source.
        var t = s.Trim();
        if (t.Length == 0) return false;

        if (t[0] == '[' && t[^1] == ']')
        {
            return t.Length > 2;
        }

        foreach (var ch in t)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
            {
                return false;
            }
        }
        return true;
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static IEnumerable<AccessControl> EnumerateBeginUnwrapped(IReadOnlyList<AccessControl> nodes)
    {
        foreach (var n in nodes)
        {
            if (IsAnonymousBegin(n))
            {
                if (n.Children is { Count: > 0 })
                {
                    foreach (var inner in EnumerateBeginUnwrapped(n.Children))
                    {
                        yield return inner;
                    }
                }
            }
            else
            {
                yield return n;
            }
        }
    }

    private static bool IsAnonymousBegin(AccessControl node)
        => string.Equals(node.Type, "Begin", StringComparison.Ordinal) && node.Name is null;

    private static bool IsSectionType(string type)
        => type is "Section" or "FormHeader" or "FormFooter" or "PageHeader" or "PageFooter"
           || (type.StartsWith("GroupLevel", StringComparison.Ordinal) &&
               (type.EndsWith("Header", StringComparison.Ordinal) ||
                type.EndsWith("Footer", StringComparison.Ordinal)));

    private static bool IsSubformType(string type)
        => type is "Subform" or "Subreport";

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "True",
            JsonValueKind.False => "False",
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => v.GetRawText(),
        };
    }

    private static int? ReadInt(IReadOnlyDictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            _ => null,
        };
    }

    private static bool? ReadBool(IReadOnlyDictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => v.GetString() switch
            {
                "True" or "true" or "-1" => true,
                "False" or "false" or "0" => false,
                _ => (bool?)null,
            },
            JsonValueKind.Number when v.TryGetInt32(out var n) => n != 0,
            _ => null,
        };
    }

    private static IReadOnlyList<string>? SplitSemicolonList(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts.ToArray();
    }
}
