using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Deterministic classification of one Access event handler procedure
/// (Form_BeforeUpdate, btnFoo_Click, GroupHeader0_Format, ...) by the
/// effects its body produces. Implements Phase 6.3 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Categorization is rule-based pattern matching against the parsed VBA
/// AST — never AI-decided per
/// <c>docs/planning/project-guidance.md</c> -&gt; VBA Parser Strategy.
/// The classifier walks the procedure body and emits one
/// <see cref="EventHandlerClassificationSignal"/> per recognized pattern;
/// <see cref="Categories"/> is the deduplicated list of categories
/// observed, in order of first appearance, and <see cref="Mixed"/> is
/// true when the body carries signals across more than one category.
/// </para>
/// <para>
/// Per the Lean By Default principle, mixed handlers are not
/// pre-decomposed. Every recognized signal stays in
/// <see cref="Signals"/>, and downstream consumers (translation, doc
/// generation, human review) can either summarize via
/// <see cref="Categories"/> or drill into the per-pattern evidence.
/// Patterns the classifier does not deterministically model (helper
/// procedure calls, MsgBox dialogs, SetFocus) emit no signal — silence
/// is explicit, not a guess.
/// </para>
/// <para>
/// Population of the
/// <see cref="EventHandlerReference.Classification"/> string slot on
/// <see cref="FormComponentSpec"/> is the responsibility of a
/// downstream pipeline stage (Session 4 of this set wires the
/// classifier through the form-component pipeline).
/// </para>
/// </remarks>
public sealed record EventHandlerClassification
{
    /// <summary>
    /// The VBA procedure name (e.g., <c>Form_BeforeUpdate</c>,
    /// <c>btnNewCase_Click</c>). Mirrors the
    /// <see cref="AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration.Name"/>
    /// verbatim.
    /// </summary>
    public required string ProcedureName { get; init; }

    /// <summary>
    /// Distinct categories observed in the body, in order of first
    /// appearance. Empty when the body contains no recognized patterns
    /// (e.g., a stub procedure or a body composed entirely of
    /// unmodeled calls like MsgBox).
    /// </summary>
    public required IReadOnlyList<EventHandlerClassificationCategory> Categories { get; init; }

    /// <summary>
    /// True when <see cref="Categories"/> has more than one entry. Mixed
    /// handlers are emitted as-is — see the type remarks for the
    /// "no pre-decomposition" rule.
    /// </summary>
    public required bool Mixed { get; init; }

    /// <summary>
    /// The evidence trail — every recognized pattern in source order.
    /// Each signal carries its category, its kind, the source
    /// line/column, and a stringified rendering of the AST target so the
    /// verifier and human reviewers can audit the classification.
    /// </summary>
    public required IReadOnlyList<EventHandlerClassificationSignal> Signals { get; init; }
}

/// <summary>
/// The taxonomy from Phase 6.3. Names are PascalCase; the
/// <see cref="JsonStringEnumConverter"/> serializes the enum-value name
/// verbatim, matching the existing convention used by
/// <see cref="EventHandlerHostKind"/> on
/// <see cref="EventHandlerReference"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventHandlerClassificationCategory
{
    /// <summary>
    /// Modifies the <c>Cancel</c> by-ref parameter of a cancellable
    /// event (BeforeUpdate, BeforeDelConfirm, KeyPress, Open, ...) to
    /// abort the operation when input does not pass validation.
    /// </summary>
    Validation,

    /// <summary>
    /// Modifies another control's value, RowSource, or single-control
    /// Requery state. Distinguished from
    /// <see cref="FilterSearch"/> by scope: cascading updates change
    /// individual controls, filter/search changes change which records
    /// the form (or one of its subforms) shows.
    /// </summary>
    CascadingUpdate,

    /// <summary>
    /// Modifies the form's (or a subform's) record-set selection —
    /// <c>Filter</c>, <c>FilterOn</c>, <c>RecordSource</c>,
    /// <c>OrderBy</c>, <c>OrderByOn</c>, <c>ServerFilter</c>,
    /// <c>ServerFilterOn</c>, or <c>Me.Requery</c> /
    /// <c>...Form.Requery</c>.
    /// </summary>
    FilterSearch,

    /// <summary>
    /// Opens or closes a different form/report, runs a saved macro,
    /// invokes a built-in command (<c>DoCmd.RunCommand</c>,
    /// <c>RunCommand</c>), navigates record-set position
    /// (<c>GoToRecord</c>, <c>GoToControl</c>), follows a hyperlink, or
    /// quits the application.
    /// </summary>
    Navigation,

    /// <summary>
    /// Persists data through DAO/ADO (<c>db.Execute</c>,
    /// <c>CurrentDb.Execute</c>, recordset edits) or invokes
    /// declared external procedures. The classifier currently models
    /// the <c>.Execute</c> family; richer DAO/ADO modeling is a
    /// future-work extension, not a Session 1 commitment.
    /// </summary>
    SideEffect,

    /// <summary>
    /// Computes derived values into local or module-level state without
    /// writing back to controls — e.g., a report's running total
    /// (<c>m_GroupTotal = m_GroupTotal + Nz(Me.txtCaseValue, 0)</c>).
    /// </summary>
    Calculation,

    /// <summary>
    /// Modifies a control's appearance — <c>Visible</c>, <c>Enabled</c>,
    /// <c>Caption</c>, <c>ForeColor</c>, <c>BackColor</c>,
    /// <c>BorderColor</c>, <c>FontBold</c>, <c>FontItalic</c>,
    /// <c>FontUnderline</c>, <c>FontSize</c>, <c>FontName</c>,
    /// <c>FontWeight</c>, <c>BorderStyle</c>, <c>BorderWidth</c>,
    /// <c>SpecialEffect</c>, <c>Locked</c>, <c>BackStyle</c>.
    /// </summary>
    FormattingDisplay,
}

/// <summary>
/// One observed pattern in an event-handler body — the building block
/// of <see cref="EventHandlerClassification.Signals"/>.
/// </summary>
public sealed record EventHandlerClassificationSignal
{
    /// <summary>The category this signal contributes to.</summary>
    public required EventHandlerClassificationCategory Category { get; init; }

    /// <summary>
    /// The specific AST pattern that fired. Multiple kinds may map to
    /// the same category (e.g., <see cref="EventHandlerSignalKind.FilterAssignment"/>
    /// and <see cref="EventHandlerSignalKind.FormRequeryCall"/> both
    /// classify as <see cref="EventHandlerClassificationCategory.FilterSearch"/>).
    /// </summary>
    public required EventHandlerSignalKind Kind { get; init; }

    /// <summary>
    /// One-based source line of the originating statement, copied from
    /// the AST node's <c>Provenance.Line</c>.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// Zero-based source column of the originating statement, copied
    /// from the AST node's <c>Provenance.Column</c>.
    /// </summary>
    public required int Column { get; init; }

    /// <summary>
    /// Source-shape rendering of the assignment target or call target
    /// the signal was extracted from — e.g., <c>Cancel</c>,
    /// <c>Me!cboClient.RowSource</c>,
    /// <c>Me.sfrmOpenCases.Form.Filter</c>, <c>DoCmd.OpenForm</c>. Not
    /// guaranteed to round-trip the source verbatim — bang and dot
    /// access are preserved, parentheses and indexer arguments are
    /// stripped — but readable enough for verifier audit.
    /// </summary>
    public string? TargetExpression { get; init; }
}

/// <summary>
/// The narrow AST patterns the classifier recognizes. Names are
/// stable and serialize via <see cref="JsonStringEnumConverter"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventHandlerSignalKind
{
    /// <summary>Bare-identifier assignment to <c>Cancel</c>.</summary>
    CancelAssignment,

    /// <summary>
    /// Bare-identifier assignment to any name other than <c>Cancel</c>
    /// (local Dim variable or module-level field).
    /// </summary>
    BareVariableAssignment,

    /// <summary>
    /// Member-access assignment whose final member is not in the
    /// FilterSearch or appearance lists — i.e., writes a control's
    /// data value, RowSource, or any non-cosmetic property.
    /// </summary>
    ControlAssignment,

    /// <summary>Member-access assignment whose final member is in the
    /// appearance list (<c>Visible</c>, <c>Caption</c>, etc.).</summary>
    AppearanceAssignment,

    /// <summary>
    /// Member-access assignment whose final member is in the
    /// FilterSearch list (<c>Filter</c>, <c>FilterOn</c>,
    /// <c>RecordSource</c>, <c>OrderBy</c>, <c>OrderByOn</c>,
    /// <c>ServerFilter</c>, <c>ServerFilterOn</c>).
    /// </summary>
    FilterAssignment,

    /// <summary>Bare <c>Me.Requery</c> — refreshes the form itself.</summary>
    FormRequeryCall,

    /// <summary>
    /// <c>...Form.Requery</c> — the chain ends with <c>.Form.Requery</c>,
    /// which is the canonical subform-refresh pattern.
    /// </summary>
    SubformRequeryCall,

    /// <summary>
    /// <c>...&lt;control&gt;.Requery</c> — single-control refresh,
    /// almost always paired with a preceding RowSource assignment.
    /// </summary>
    ControlRequeryCall,

    /// <summary>
    /// <c>DoCmd.&lt;X&gt;</c> where X is one of the navigation members
    /// (<c>OpenForm</c>, <c>OpenReport</c>, <c>OpenQuery</c>,
    /// <c>OpenTable</c>, <c>OpenModule</c>, <c>Close</c>,
    /// <c>RunMacro</c>, <c>RunCommand</c>, <c>GoToRecord</c>,
    /// <c>GoToControl</c>, <c>NavigateTo</c>, <c>ShowAllRecords</c>,
    /// <c>FindRecord</c>, <c>FindNext</c>, <c>ApplyFilter</c>).
    /// </summary>
    DoCmdNavigationCall,

    /// <summary><c>Application.Quit</c> or bare <c>Quit</c>.</summary>
    ApplicationQuitCall,

    /// <summary>Bare <c>FollowHyperlink</c> call.</summary>
    FollowHyperlinkCall,

    /// <summary>Bare <c>RunCommand</c> call (the global form, separate from <c>DoCmd.RunCommand</c>).</summary>
    RunCommandCall,

    /// <summary>
    /// Member call whose final member is <c>Execute</c> — the canonical
    /// shape for DAO/ADO database execution
    /// (<c>db.Execute</c>, <c>CurrentDb.Execute</c>,
    /// <c>conn.Execute</c>).
    /// </summary>
    DatabaseExecuteCall,
}
