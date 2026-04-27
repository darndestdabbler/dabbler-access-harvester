using System.Collections.Generic;
using System.Text.Json.Serialization;
using AccessHarvester.Access;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Translation-ready specification for a single Access form, shaped for a
/// downstream Blazor-component generator. Implements Phase 6.2 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>: a populated
/// <see cref="AccessHarvester.Access.FormMetadata"/> tree is flattened into
/// named facets the consumer can read directly without re-walking the
/// design hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// The abstraction is deliberately *not* an opinion about Blazor rendering —
/// per the session-set spec's Standards: "do not pre-compute every
/// Blazor-rendering decision; capture the structure and let the consumer
/// decide." Each facet captures structure (sections as descriptors, controls
/// flat with explicit section attribution, subforms as relationships, event
/// handlers as references) so the consumer can render however it wishes.
/// Facets the underlying form did not exercise (e.g.,
/// <see cref="Subforms"/> when no subform control is present,
/// <see cref="EventHandlers"/> when the form has no code-behind) are left
/// null rather than emitted as empty lists, matching the
/// only-emit-what-Access-wrote rule from
/// <c>docs/planning/project-guidance.md</c> -&gt; Output JSON Conventions.
/// </para>
/// <para>
/// Interpretive slots (<see cref="Purpose"/>, <see cref="Intent"/>,
/// <see cref="Notes"/>) are reserved for the AI consumer to fill downstream
/// and remain null at the <c>ir/</c> tier per
/// <c>project-guidance.md</c> -&gt; Output Architecture.
/// </para>
/// <para>
/// Builders for this record live in
/// <see cref="FormComponentSpecBuilder"/>, which walks a
/// <see cref="FormMetadata"/> tree to populate as many facets as the input
/// unambiguously determines. Facets that require resolved cross-source
/// information are deliberately left null at this tier:
/// <list type="bullet">
///   <item><see cref="ControlBinding.QueryAbstractionRef"/> and
///   <see cref="FormDataSourceBinding.QueryAbstractionRef"/> — wired to
///   built <see cref="QueryAbstraction"/> objects by a downstream pipeline
///   stage once the symbol table is resolved.</item>
///   <item><see cref="EventHandlerReference.Classification"/> — populated by
///   the next enrichment set (events + validation; tree row #15).</item>
///   <item><see cref="ValidationRuleReference.Classification"/> — populated
///   by the same set as the event-handler classification.</item>
/// </list>
/// </para>
/// </remarks>
public sealed record FormComponentSpec
{
    /// <summary>
    /// The form's source name as Access stores it (e.g., <c>frmCaseManagement</c>).
    /// Required.
    /// </summary>
    public required string SourceFormName { get; init; }

    /// <summary>
    /// The VBA class module name backing the form (typically
    /// <c>Form_&lt;SourceFormName&gt;</c>). Mirrors
    /// <see cref="FormMetadata.ModuleName"/> verbatim. Null when the form has
    /// no code-behind.
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>
    /// PascalCase component-name suggestion derived from
    /// <see cref="SourceFormName"/> (e.g., <c>frmCaseManagement</c> -&gt;
    /// <c>CaseManagementComponent</c>). Null when the source name yields no
    /// usable suggestion (e.g., empty, punctuation-only). The suggestion is a
    /// hint; downstream consumers may override.
    /// </summary>
    public string? SuggestedComponentName { get; init; }

    /// <summary>
    /// Data-source binding for the form as a whole — record source, filter,
    /// order. Null only when the source form did not declare a record source
    /// (an unbound dialog form).
    /// </summary>
    public FormDataSourceBinding? DataSource { get; init; }

    /// <summary>
    /// The decoded <c>DefaultView</c> label (<c>Single Form</c>,
    /// <c>Continuous Forms</c>, <c>Datasheet</c>, ...). Mirrors
    /// <see cref="FormMetadata.DefaultView"/> verbatim. Null when Access did
    /// not record a default view.
    /// </summary>
    public string? DefaultView { get; init; }

    /// <summary>
    /// Inch-converted form-level geometry. Mirrors
    /// <see cref="FormMetadata.GeometryIn"/> verbatim. Null when Access did
    /// not record dimensions.
    /// </summary>
    public GeometryInches? GeometryIn { get; init; }

    /// <summary>
    /// Top-level sections of the form (<c>FormHeader</c>, <c>Detail</c>,
    /// <c>FormFooter</c>, <c>PageHeader</c>, <c>PageFooter</c>) in source
    /// order. Each section descriptor records the section's name, kind, and
    /// the names of the controls it contains so the consumer can preserve
    /// section-relative grouping when emitting Blazor markup. Null when the
    /// form has no design tree at all.
    /// </summary>
    public IReadOnlyList<FormSectionDescriptor>? Sections { get; init; }

    /// <summary>
    /// Flattened list of named, leaf-level controls on the form, in
    /// source-tree document order. Each entry carries its
    /// <see cref="FormControlSpec.SectionName"/> so the consumer can
    /// re-group by section without re-walking. Anonymous <c>Begin</c>
    /// wrappers and section nodes themselves are not represented here.
    /// Null when the form has no controls.
    /// </summary>
    public IReadOnlyList<FormControlSpec>? Controls { get; init; }

    /// <summary>
    /// Subform relationships (subform-control name, contained-form name,
    /// link-master / link-child fields) in source order. Null when the form
    /// has no subform controls.
    /// </summary>
    public IReadOnlyList<SubformRelationship>? Subforms { get; init; }

    /// <summary>
    /// Event-handler references — one entry per Access event property whose
    /// value is the <c>[Event Procedure]</c> sentinel — covering both
    /// form-level events and per-control events. The
    /// <see cref="EventHandlerReference.Classification"/> slot is null at
    /// this tier; the next enrichment set populates it.
    /// </summary>
    public IReadOnlyList<EventHandlerReference>? EventHandlers { get; init; }

    /// <summary>
    /// Validation-rule references aggregated from the form's properties bag
    /// and per-control properties bags (table-level rules surfaced via
    /// <see cref="FormMetadata"/> are referenced too). The
    /// <see cref="ValidationRuleReference.Classification"/> slot is null at
    /// this tier; the next enrichment set populates it.
    /// </summary>
    public IReadOnlyList<ValidationRuleReference>? ValidationRules { get; init; }

    /// <summary>
    /// Reserved interpretive slot — null at the <c>ir/</c> tier; populated
    /// downstream by the consumer AI per
    /// <c>project-guidance.md</c> -&gt; Output Architecture.
    /// </summary>
    public string? Purpose { get; init; }

    /// <summary>Reserved interpretive slot. See <see cref="Purpose"/>.</summary>
    public string? Intent { get; init; }

    /// <summary>Reserved interpretive slot. See <see cref="Purpose"/>.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Form-level data-source binding: the record source plus the optional
/// filter / sort overlays Access applies to it.
/// </summary>
public sealed record FormDataSourceBinding
{
    /// <summary>
    /// Discriminates how the record source is recorded (a bareword name that
    /// Access treats as either a table or a saved query without resolution,
    /// inline SQL, or no source at all).
    /// </summary>
    public required FormDataSourceKind Kind { get; init; }

    /// <summary>
    /// The bareword target name when <see cref="Kind"/> is
    /// <see cref="FormDataSourceKind.NamedReference"/>, e.g.,
    /// <c>tblCases</c> or <c>qryActiveCases</c>. Null otherwise. The
    /// table-vs-query disambiguation is left to a resolved schema and is
    /// not done at this tier.
    /// </summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// The verbatim SQL text when <see cref="Kind"/> is
    /// <see cref="FormDataSourceKind.InlineSql"/>. Null otherwise.
    /// </summary>
    public string? InlineSql { get; init; }

    /// <summary>
    /// The <c>Filter</c> clause Access applies on top of the record source,
    /// in JET <c>WHERE</c>-fragment form (without the <c>WHERE</c> keyword).
    /// Mirrors <see cref="FormMetadata.Filter"/> verbatim. Null when no
    /// filter is set.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// The <c>OrderBy</c> clause, in JET <c>ORDER BY</c>-fragment form.
    /// Mirrors <see cref="FormMetadata.OrderBy"/> verbatim. Null when no
    /// sort is set.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Reference to a built <see cref="QueryAbstraction"/> when the record
    /// source resolves to one (a saved query, or inline SQL the pipeline
    /// has parsed). Null at this tier; populated by a downstream pipeline
    /// stage once the symbol table is resolved.
    /// </summary>
    public string? QueryAbstractionRef { get; init; }
}

/// <summary>Discriminator for <see cref="FormDataSourceBinding.Kind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormDataSourceKind
{
    /// <summary>The record source is a bareword name — table or query — and
    /// disambiguation is deferred to a resolved schema.</summary>
    NamedReference,

    /// <summary>The record source is inline SQL (<c>SELECT</c> / <c>PARAMETERS</c> ...).</summary>
    InlineSql,

    /// <summary>The form is unbound — no record source was declared.</summary>
    None,

    /// <summary>The record source could not be classified into one of the
    /// above (e.g., contains characters neither a bareword nor recognised
    /// SQL would have).</summary>
    Unknown,
}

/// <summary>
/// One section of a <see cref="FormComponentSpec"/>: form / page header,
/// detail, footer, group level. Carries layout hints and the names of the
/// controls in source order.
/// </summary>
public sealed record FormSectionDescriptor
{
    /// <summary>
    /// The section's name as Access wrote it (e.g., <c>FormHeader</c>,
    /// <c>Detail</c>, <c>FormFooter</c>). Required even when Access used a
    /// well-known section type rather than a custom name — the well-known
    /// name (e.g., <c>Detail</c>) is reproduced here.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The section kind, classified from <see cref="Name"/>.</summary>
    public required FormSectionKind Kind { get; init; }

    /// <summary>
    /// Inch-converted geometry for the section, when Access recorded it.
    /// Null when no dimensions were written.
    /// </summary>
    public GeometryInches? GeometryIn { get; init; }

    /// <summary>
    /// Names of the controls this section directly contains, in source
    /// order. The control specifications themselves are flattened on
    /// <see cref="FormComponentSpec.Controls"/>; consumers that want
    /// section-grouped rendering join on
    /// <see cref="FormControlSpec.SectionName"/>. Null when the section has
    /// no named controls (e.g., a header section that is geometry-only).
    /// </summary>
    public IReadOnlyList<string>? ControlNames { get; init; }
}

/// <summary>Discriminator for <see cref="FormSectionDescriptor.Kind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormSectionKind
{
    /// <summary><c>FormHeader</c>.</summary>
    FormHeader,

    /// <summary><c>Detail</c> (sometimes also written as <c>Section</c> with
    /// no specific name in <c>SaveAsText</c> output).</summary>
    Detail,

    /// <summary><c>FormFooter</c>.</summary>
    FormFooter,

    /// <summary><c>PageHeader</c> (reports / forms in print layout).</summary>
    PageHeader,

    /// <summary><c>PageFooter</c>.</summary>
    PageFooter,

    /// <summary>A group-level header (<c>GroupLevel0Header</c> et al.).</summary>
    GroupLevelHeader,

    /// <summary>A group-level footer (<c>GroupLevel0Footer</c> et al.).</summary>
    GroupLevelFooter,

    /// <summary>Section kind could not be classified.</summary>
    Other,
}

/// <summary>
/// One leaf control on a <see cref="FormComponentSpec"/>. Carries the
/// control's source type, suggested Blazor component kind, binding, render
/// hints, geometry, and label.
/// </summary>
public sealed record FormControlSpec
{
    /// <summary>The control's name as Access stores it (e.g., <c>txtCustomerName</c>).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The Access control class (e.g., <c>TextBox</c>, <c>ComboBox</c>,
    /// <c>CommandButton</c>, <c>Subform</c>). Mirrors
    /// <see cref="AccessControl.Type"/> verbatim.
    /// </summary>
    public required string SourceType { get; init; }

    /// <summary>
    /// Categorical Blazor-component-kind suggestion derived from
    /// <see cref="SourceType"/>. The mapping is conservative — controls the
    /// builder cannot map to a known kind receive
    /// <see cref="FormControlComponentKind.Other"/>.
    /// </summary>
    public required FormControlComponentKind ComponentKind { get; init; }

    /// <summary>
    /// The name of the section that contains this control (e.g.,
    /// <c>Detail</c>, <c>FormHeader</c>). Null when the control's section
    /// could not be determined.
    /// </summary>
    public string? SectionName { get; init; }

    /// <summary>
    /// Resolved label provenance (<c>attached</c>, <c>proximity</c>,
    /// <c>from-name</c>, <c>from-binding</c>, <c>unresolved</c>). Mirrors
    /// <see cref="AccessControl.Label"/>. Null for ineligible control types
    /// (Label, Rectangle, Image, ...).
    /// </summary>
    public LabelInfo? Label { get; init; }

    /// <summary>
    /// Inch-converted geometry. Mirrors <see cref="AccessControl.GeometryIn"/>.
    /// Null when Access recorded no dimensions.
    /// </summary>
    public GeometryInches? GeometryIn { get; init; }

    /// <summary>
    /// Data binding for this control (<c>ControlSource</c> / <c>RowSource</c>
    /// / etc.). Null for unbound controls (Labels, decorative shapes,
    /// command buttons that only fire events).
    /// </summary>
    public ControlBinding? Binding { get; init; }

    /// <summary>
    /// Render-affecting hints lifted from the control's property bag
    /// (<c>Caption</c>, <c>Format</c>, <c>InputMask</c>, visibility,
    /// enablement, lock, tab index, default value). All sub-fields are
    /// nullable; only-emit-what-Access-wrote applies. Null when no
    /// render-affecting properties were recorded.
    /// </summary>
    public ControlRenderHints? RenderHints { get; init; }

    /// <summary>
    /// Properties the developer flagged as deviating from the Access
    /// default. Mirrors <see cref="AccessControl.NonDefaultProperties"/>.
    /// Null when no such markers were recorded.
    /// </summary>
    public IReadOnlyList<string>? NonDefaultProperties { get; init; }
}

/// <summary>Discriminator for <see cref="FormControlSpec.ComponentKind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormControlComponentKind
{
    /// <summary>Plain single-line text input (Access <c>TextBox</c>).</summary>
    TextInput,

    /// <summary>Multi-line text input (an Access <c>TextBox</c> whose render
    /// hints indicate multi-line — currently treated identically to
    /// <see cref="TextInput"/> at this tier).</summary>
    MultilineTextInput,

    /// <summary>Drop-down selector (Access <c>ComboBox</c>).</summary>
    Dropdown,

    /// <summary>List-box selector (Access <c>ListBox</c>).</summary>
    ListBox,

    /// <summary>Boolean toggle (Access <c>CheckBox</c>, <c>OptionButton</c>,
    /// <c>ToggleButton</c>).</summary>
    Toggle,

    /// <summary>Action button (Access <c>CommandButton</c>).</summary>
    Button,

    /// <summary>Static label (Access <c>Label</c>).</summary>
    Label,

    /// <summary>Image (Access <c>Image</c>).</summary>
    Image,

    /// <summary>Subform / subreport host.</summary>
    SubformHost,

    /// <summary>Tab control (a container with Page children).</summary>
    TabControl,

    /// <summary>Decorative-only shape (rectangle, line). Carries no binding.</summary>
    Decoration,

    /// <summary>Could not be mapped to a known component kind.</summary>
    Other,
}

/// <summary>
/// Data binding for a <see cref="FormControlSpec"/>: the bound field /
/// expression, plus row-source information for combo / list controls.
/// </summary>
public sealed record ControlBinding
{
    /// <summary>
    /// Discriminates how the binding is recorded — bound to a table column,
    /// a computed expression, a row-source list, or unbound.
    /// </summary>
    public required ControlBindingKind Kind { get; init; }

    /// <summary>
    /// The control's <c>ControlSource</c> as Access wrote it. May be a
    /// column name, an expression starting with <c>=</c>, or null when the
    /// control is unbound.
    /// </summary>
    public string? ControlSource { get; init; }

    /// <summary>
    /// The control's <c>RowSource</c> for combo / list controls — an inline
    /// SQL string, a saved query name, or a value list. Null otherwise.
    /// </summary>
    public string? RowSource { get; init; }

    /// <summary>
    /// The control's <c>RowSourceType</c> — <c>Table/Query</c>,
    /// <c>Value List</c>, or <c>Field List</c>. Null otherwise.
    /// </summary>
    public string? RowSourceType { get; init; }

    /// <summary>
    /// The bound-column index for combo / list controls (1-based, matching
    /// Access's convention). Null when not specified.
    /// </summary>
    public int? BoundColumn { get; init; }

    /// <summary>
    /// The total column count for combo / list controls. Null when not
    /// specified.
    /// </summary>
    public int? ColumnCount { get; init; }

    /// <summary>
    /// The <c>ColumnWidths</c> string for combo / list controls (semicolon-
    /// separated twip widths). Null when not specified.
    /// </summary>
    public string? ColumnWidths { get; init; }

    /// <summary>
    /// Reference to a built <see cref="QueryAbstraction"/> when the
    /// <see cref="RowSource"/> or <see cref="ControlSource"/> resolves to
    /// one. Null at this tier; populated by a downstream pipeline stage
    /// once the symbol table is resolved.
    /// </summary>
    public string? QueryAbstractionRef { get; init; }
}

/// <summary>Discriminator for <see cref="ControlBinding.Kind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControlBindingKind
{
    /// <summary><see cref="ControlBinding.ControlSource"/> is a bareword
    /// column name from the form's record source.</summary>
    TableField,

    /// <summary><see cref="ControlBinding.ControlSource"/> begins with
    /// <c>=</c>: a computed VBA expression.</summary>
    ComputedExpression,

    /// <summary><see cref="ControlBinding.RowSource"/> drives a list of
    /// values (combo / list box).</summary>
    RowSourceList,

    /// <summary>The control has no <see cref="ControlBinding.ControlSource"/>
    /// or <see cref="ControlBinding.RowSource"/>.</summary>
    Unbound,

    /// <summary>Binding kind could not be classified.</summary>
    Unknown,
}

/// <summary>
/// Render-affecting hints lifted from a control's property bag. All
/// sub-fields are nullable; absent fields mean Access did not write the
/// property (consumer reads omission as Access default).
/// </summary>
public sealed record ControlRenderHints
{
    /// <summary>The control's <c>Caption</c> (button label, label text,
    /// frame caption). Null when not specified.</summary>
    public string? Caption { get; init; }

    /// <summary>The display <c>Format</c> string (e.g., <c>Currency</c>,
    /// <c>mm/dd/yyyy</c>). Null when not specified.</summary>
    public string? Format { get; init; }

    /// <summary>The <c>InputMask</c> string (e.g., <c>(999) 000-0000</c>).
    /// Null when not specified.</summary>
    public string? InputMask { get; init; }

    /// <summary>The <c>DefaultValue</c> expression. Null when not specified.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>The <c>Visible</c> flag. Null when not specified.</summary>
    public bool? IsVisible { get; init; }

    /// <summary>The <c>Enabled</c> flag. Null when not specified.</summary>
    public bool? IsEnabled { get; init; }

    /// <summary>The <c>Locked</c> flag. Null when not specified.</summary>
    public bool? IsLocked { get; init; }

    /// <summary>The <c>TabIndex</c> integer. Null when not specified.</summary>
    public int? TabIndex { get; init; }
}

/// <summary>
/// Subform / subreport relationship: the host control on this form, the
/// contained form / report, and the link-master / link-child field
/// correspondence Access uses to drive the parent / child join.
/// </summary>
public sealed record SubformRelationship
{
    /// <summary>
    /// The name of the subform / subreport control on the parent form
    /// (e.g., <c>sfrmOpenCases</c>).
    /// </summary>
    public required string SubformControlName { get; init; }

    /// <summary>
    /// The contained form / report's name as referenced by the
    /// <c>SourceObject</c> property (e.g., <c>frmOpenCasesEmbed</c>). May
    /// carry the <c>Form.</c> or <c>Report.</c> prefix Access stamps in
    /// some versions; the prefix is preserved verbatim. Null when the
    /// <c>SourceObject</c> property was not recorded.
    /// </summary>
    public string? ContainedObjectName { get; init; }

    /// <summary>
    /// The fields on the contained object that match
    /// <see cref="LinkMasterFields"/>, in the order Access wrote them.
    /// Null when no link-child fields were recorded.
    /// </summary>
    public IReadOnlyList<string>? LinkChildFields { get; init; }

    /// <summary>
    /// The fields on the parent form that match
    /// <see cref="LinkChildFields"/>, in the order Access wrote them.
    /// Null when no link-master fields were recorded.
    /// </summary>
    public IReadOnlyList<string>? LinkMasterFields { get; init; }
}

/// <summary>
/// Reference to one event handler — either a form-level event or a
/// per-control event — whose corresponding property in the Access source
/// was the <c>[Event Procedure]</c> sentinel. The
/// <see cref="Classification"/> slot is reserved for the next enrichment
/// set (events + validation; tree row #15) to fill.
/// </summary>
public sealed record EventHandlerReference
{
    /// <summary>
    /// Discriminates which scope owns this event handler — the form itself
    /// or one of its controls.
    /// </summary>
    public required EventHandlerHostKind HostKind { get; init; }

    /// <summary>
    /// The owning control's name when <see cref="HostKind"/> is
    /// <see cref="EventHandlerHostKind.Control"/>; null when the handler is
    /// form-level.
    /// </summary>
    public string? ControlName { get; init; }

    /// <summary>
    /// The Access property name where the handler was declared (e.g.,
    /// <c>OnClick</c>, <c>AfterUpdate</c>, <c>OnLoad</c>). Required.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// The event name as it appears in the VBA procedure-name convention
    /// (e.g., <c>OnClick</c> -&gt; <c>Click</c>;
    /// <c>AfterUpdate</c> -&gt; <c>AfterUpdate</c> — the
    /// <c>After</c> / <c>Before</c> family preserves its prefix).
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// The expected VBA procedure name in the form's class module
    /// (<c>&lt;ControlName&gt;_&lt;EventName&gt;</c> for control events;
    /// <c>Form_&lt;EventName&gt;</c> for form-level events). The
    /// <see cref="VbaResolver"/> downstream binds this to the actual
    /// procedure declaration. Null when the convention could not be
    /// applied (e.g., no control name and no form name).
    /// </summary>
    public string? VbaProcedureName { get; init; }

    /// <summary>
    /// Reserved classification slot — null at this tier. The next
    /// enrichment set (events + validation; tree row #15) classifies each
    /// handler as <c>validation</c>, <c>cascadingUpdate</c>,
    /// <c>filterSearch</c>, <c>navigation</c>, <c>sideEffect</c>,
    /// <c>calculation</c>, <c>formattingDisplay</c>, or <c>mixed</c>.
    /// </summary>
    public string? Classification { get; init; }
}

/// <summary>Discriminator for <see cref="EventHandlerReference.HostKind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventHandlerHostKind
{
    /// <summary>The handler is bound to the form itself (<c>Form_Load</c>,
    /// <c>Form_Current</c>, ...).</summary>
    Form,

    /// <summary>The handler is bound to one of the form's controls.</summary>
    Control,
}

/// <summary>
/// Reference to one validation rule — either a form-level rule or a
/// per-control rule — surfaced from the design-time properties bag. The
/// <see cref="Classification"/> slot is reserved for the next enrichment
/// set (events + validation; tree row #15) to fill, by which time
/// <c>BeforeUpdate</c>-handler patterns have been parsed and table-level
/// rules have been merged.
/// </summary>
public sealed record ValidationRuleReference
{
    /// <summary>
    /// Discriminates which scope owns this validation rule.
    /// </summary>
    public required ValidationRuleHostKind HostKind { get; init; }

    /// <summary>
    /// The owning control's name when <see cref="HostKind"/> is
    /// <see cref="ValidationRuleHostKind.Control"/>; null when the rule is
    /// form-level.
    /// </summary>
    public string? ControlName { get; init; }

    /// <summary>
    /// Discriminates the rule's source: a design-time
    /// <c>ValidationRule</c> property, a <c>Required</c> flag, a
    /// <c>LimitToList</c> flag, or a deferred reference to a
    /// <c>BeforeUpdate</c> handler whose body Session #15 will parse for
    /// validation patterns.
    /// </summary>
    public required ValidationRuleSource Source { get; init; }

    /// <summary>
    /// The rule expression Access wrote, when <see cref="Source"/> is
    /// <see cref="ValidationRuleSource.ControlValidationRule"/> or
    /// <see cref="ValidationRuleSource.RecordValidationRule"/>. Verbatim
    /// JET-expression text. Null otherwise.
    /// </summary>
    public string? RuleExpression { get; init; }

    /// <summary>
    /// The <c>ValidationText</c> companion message (the message shown to
    /// the user when the rule fails). Null when not specified.
    /// </summary>
    public string? ValidationText { get; init; }

    /// <summary>
    /// Reserved classification slot — null at this tier. Session #15
    /// classifies each rule as <c>required</c>, <c>range</c>,
    /// <c>pattern</c>, or <c>customPredicate</c>.
    /// </summary>
    public string? Classification { get; init; }
}

/// <summary>Discriminator for <see cref="ValidationRuleReference.HostKind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationRuleHostKind
{
    /// <summary>Form-level rule (record validation).</summary>
    Form,

    /// <summary>Per-control rule.</summary>
    Control,
}

/// <summary>Discriminator for <see cref="ValidationRuleReference.Source"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationRuleSource
{
    /// <summary>The control's <c>ValidationRule</c> property is non-empty.</summary>
    ControlValidationRule,

    /// <summary>The form's <c>ValidationRule</c> (record-level) property is non-empty.</summary>
    RecordValidationRule,

    /// <summary>The control's <c>Required</c> property is <c>True</c>.</summary>
    RequiredField,

    /// <summary>A combo box's <c>LimitToList</c> property is <c>True</c>.</summary>
    LimitToList,

    /// <summary>
    /// A reference to a <c>BeforeUpdate</c> event handler whose body the
    /// next enrichment set will parse for validation patterns
    /// (<c>If IsNull(field) Then Cancel = True</c>, etc.). The handler is
    /// also surfaced via <see cref="EventHandlerReference"/>; this entry is
    /// the validation-side cross-reference so consumers can find rules
    /// without scanning every handler.
    /// </summary>
    BeforeUpdateHandler,
}
