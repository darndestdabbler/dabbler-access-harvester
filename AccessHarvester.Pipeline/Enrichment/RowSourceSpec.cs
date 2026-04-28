using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Translation-ready specification for one combo box / list box on an
/// Access form. Implements Phase 6.5 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>: the design-time
/// <c>RowSource</c>, <c>RowSourceType</c>, <c>BoundColumn</c>,
/// <c>ColumnCount</c>, <c>ColumnWidths</c>, and <c>LimitToList</c>
/// properties are flattened, and any cascading-update event handlers that
/// dynamically rewrite the row source from another control are
/// cross-referenced via <see cref="CascadingDrivers"/>.
/// </summary>
/// <remarks>
/// <para>
/// The builder (<see cref="RowSourceSpecBuilder"/>) is deterministic given
/// the same input — no AI judgment is applied per
/// <c>docs/planning/project-guidance.md</c> -&gt; VBA Parser Strategy.
/// Source SQL is preserved verbatim in <see cref="InlineSql"/>; parsing
/// the SQL into a <see cref="QueryAbstraction"/> is a downstream pipeline
/// stage's job per the spec's Standards: "RowSource SQL points at the
/// parsed SQL AST from #12, never re-parses." The
/// <see cref="QueryAbstractionRef"/> slot is the join key between the
/// two stages.
/// </para>
/// <para>
/// Per the only-emit-what-Access-wrote rule from the project's Output
/// JSON Conventions, optional sub-records and lists stay null when they
/// do not apply (a saved-query reference has no <see cref="ValueListItems"/>;
/// a static row source has no <see cref="CascadingDrivers"/>; a control
/// the form did not configure with <c>ColumnWidths</c> has no
/// <see cref="Columns"/>).
/// </para>
/// <para>
/// Interpretive slots (<see cref="Purpose"/>, <see cref="Notes"/>) are
/// reserved for the AI consumer to fill downstream and remain null at the
/// <c>ir/</c> tier per <c>project-guidance.md</c> -&gt; Output Architecture.
/// </para>
/// </remarks>
public sealed record RowSourceSpec
{
    /// <summary>The owning form's source name (e.g., <c>frmCaseManagement</c>).</summary>
    public required string SourceFormName { get; init; }

    /// <summary>The combo / list control's name (e.g., <c>cboCaseworker</c>).</summary>
    public required string ControlName { get; init; }

    /// <summary>
    /// The Access control class — <c>ComboBox</c> or <c>ListBox</c>. Mirrors
    /// <see cref="AccessHarvester.Access.AccessControl.Type"/> verbatim.
    /// </summary>
    public required string ControlType { get; init; }

    /// <summary>How the row source is recorded.</summary>
    public required RowSourceKind Kind { get; init; }

    /// <summary>
    /// The verbatim <c>RowSource</c> string Access wrote, regardless of
    /// <see cref="Kind"/>. Null when no <c>RowSource</c> was recorded.
    /// </summary>
    public string? RowSourceText { get; init; }

    /// <summary>
    /// The verbatim <c>RowSourceType</c> string Access wrote
    /// (<c>Table/Query</c>, <c>Value List</c>, <c>Field List</c>, ...).
    /// Null when not specified.
    /// </summary>
    public string? RowSourceType { get; init; }

    /// <summary>
    /// The bareword target name when <see cref="Kind"/> is
    /// <see cref="RowSourceKind.TableOrQueryNamed"/> — a saved query or
    /// table name (resolution is left to the symbol table). Null otherwise.
    /// </summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// The verbatim SQL text when <see cref="Kind"/> is
    /// <see cref="RowSourceKind.TableOrQueryInlineSql"/>. Null otherwise.
    /// Parsing this text into a <see cref="QueryAbstraction"/> is the job
    /// of a downstream pipeline stage; this record only carries the
    /// string.
    /// </summary>
    public string? InlineSql { get; init; }

    /// <summary>
    /// Items parsed from a <c>Value List</c> row source — the
    /// semicolon-separated list, trimmed of surrounding quotes. Null when
    /// <see cref="Kind"/> is not <see cref="RowSourceKind.ValueList"/> or
    /// when the list was empty.
    /// </summary>
    public IReadOnlyList<string>? ValueListItems { get; init; }

    /// <summary>
    /// The table name referenced when <see cref="Kind"/> is
    /// <see cref="RowSourceKind.FieldList"/> (the row source enumerates
    /// the fields of a table). Null otherwise.
    /// </summary>
    public string? FieldListSourceTable { get; init; }

    /// <summary>The <c>BoundColumn</c> index (1-based, Access convention). Null when not specified.</summary>
    public int? BoundColumn { get; init; }

    /// <summary>The <c>ColumnCount</c> integer. Null when not specified.</summary>
    public int? ColumnCount { get; init; }

    /// <summary>
    /// Per-column descriptors derived from <c>ColumnWidths</c>, in
    /// 1-based source order. Null when <c>ColumnWidths</c> was not
    /// specified.
    /// </summary>
    public IReadOnlyList<RowSourceColumnDescriptor>? Columns { get; init; }

    /// <summary>
    /// The <c>LimitToList</c> flag, mirrored verbatim. The corresponding
    /// validation rule is emitted by
    /// <see cref="ValidationRuleExtractor"/> with
    /// <see cref="ValidationRuleOrigin.ControlLimitToList"/>; this slot is
    /// here so the consumer can read it without joining tables. Null when
    /// not specified.
    /// </summary>
    public bool? LimitToList { get; init; }

    /// <summary>
    /// Other controls on the same form whose event handlers dynamically
    /// rewrite this control's <c>RowSource</c> (the cascading-lookup
    /// pattern from Phase 6.5). Each entry is one observed
    /// <c>ControlAssignment</c> signal whose target is
    /// <c>Me!&lt;this&gt;.RowSource</c>. Null when no such drivers were
    /// detected.
    /// </summary>
    public IReadOnlyList<RowSourceCascadingDriver>? CascadingDrivers { get; init; }

    /// <summary>
    /// Reference to a built <see cref="QueryAbstraction"/> when
    /// <see cref="InlineSql"/> or <see cref="TargetName"/> resolves to a
    /// saved-query abstraction. Null at this tier; populated by a
    /// downstream pipeline stage once the symbol table is resolved.
    /// </summary>
    public string? QueryAbstractionRef { get; init; }

    /// <summary>Reserved interpretive slot — null at the <c>ir/</c> tier.</summary>
    public string? Purpose { get; init; }

    /// <summary>Reserved interpretive slot. See <see cref="Purpose"/>.</summary>
    public string? Notes { get; init; }
}

/// <summary>Discriminator for <see cref="RowSourceSpec.Kind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RowSourceKind
{
    /// <summary>The row source is a bareword name — a saved query or
    /// table — under <c>RowSourceType = "Table/Query"</c>.</summary>
    TableOrQueryNamed,

    /// <summary>The row source is inline SQL
    /// (<c>SELECT ...</c>, <c>PARAMETERS ...</c>) under
    /// <c>RowSourceType = "Table/Query"</c>.</summary>
    TableOrQueryInlineSql,

    /// <summary>The row source is a literal semicolon-separated list of
    /// values (<c>RowSourceType = "Value List"</c>).</summary>
    ValueList,

    /// <summary>The row source enumerates the fields of a table
    /// (<c>RowSourceType = "Field List"</c>).</summary>
    FieldList,

    /// <summary>The control declared row-source-related properties (e.g.,
    /// <c>BoundColumn</c>, <c>ColumnCount</c>) but no
    /// <c>RowSource</c> string. The row source is filled at runtime by
    /// VBA before the control is shown — see
    /// <see cref="RowSourceSpec.CascadingDrivers"/>.</summary>
    Empty,

    /// <summary>The row source could not be classified into any of the
    /// above shapes. The verbatim text is preserved in
    /// <see cref="RowSourceSpec.RowSourceText"/> per the "unresolvable
    /// constructs are explicit, not silent" rule.</summary>
    Unknown,
}

/// <summary>
/// One column of a combo / list row source, derived from the
/// <c>ColumnWidths</c> property. Consumers join on
/// <see cref="Index"/> against the row source's SELECT-list (when the
/// SELECT has been parsed downstream) to learn the column's bound field.
/// </summary>
public sealed record RowSourceColumnDescriptor
{
    /// <summary>1-based column index (matches Access's <c>Column(N)</c>
    /// convention used by <see cref="EventHandlerSignalKind.ControlAssignment"/>
    /// targets like <c>Me!cbo.Column(2)</c>).</summary>
    public required int Index { get; init; }

    /// <summary>Column width in twips, as Access wrote it. Null when the
    /// width entry was malformed or absent at this index.</summary>
    public int? WidthTwips { get; init; }

    /// <summary>Column width in inches (twips / 1440). Null when
    /// <see cref="WidthTwips"/> is null.</summary>
    public double? WidthInches { get; init; }

    /// <summary>True when <see cref="WidthTwips"/> is 0 — Access hides
    /// columns with zero width. Null when the width itself is null.</summary>
    public bool? Hidden { get; init; }
}

/// <summary>
/// One observed cascading-update signal: an event handler on another
/// control assigns this control's <c>RowSource</c>. The target combo
/// is identified by <see cref="RowSourceSpec.ControlName"/>; this record
/// names the driver and where the assignment happens.
/// </summary>
public sealed record RowSourceCascadingDriver
{
    /// <summary>
    /// The driving control's name (e.g., <c>cboCaseworker</c>), parsed
    /// from the host procedure name's <c>&lt;control&gt;_&lt;event&gt;</c>
    /// pattern. Null when the host is the form itself
    /// (<c>Form_&lt;event&gt;</c>) or the procedure name does not follow
    /// that convention.
    /// </summary>
    public string? DriverControlName { get; init; }

    /// <summary>The host procedure name (e.g., <c>cboCaseworker_AfterUpdate</c>).</summary>
    public required string DriverProcedureName { get; init; }

    /// <summary>1-based source line of the assignment.</summary>
    public required int Line { get; init; }

    /// <summary>Zero-based source column of the assignment.</summary>
    public required int Column { get; init; }
}
