using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Structured validation rule extracted from a single source — a table or
/// table-field declaration, a form or control properties bag, or a parsed
/// <c>BeforeUpdate</c> event-handler pattern. Implements Phase 6.4 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The extractor that produces these (<see cref="ValidationRuleExtractor"/>)
/// is deterministic given the same input — no AI judgment is applied per
/// <c>docs/planning/project-guidance.md</c> -&gt; VBA Parser Strategy.
/// Rules whose expression the extractor cannot structurally interpret are
/// emitted with <see cref="Kind"/> = <see cref="ValidationRuleKind.Unstructured"/>
/// and <see cref="Expression"/> set to the verbatim source text, per the
/// "unresolvable constructs are explicit, not silent" principle.
/// </para>
/// <para>
/// Custom predicates extracted from VBA <c>BeforeUpdate</c> handlers
/// preserve a string rendering of the original VBA condition in
/// <see cref="Expression"/>; downstream tooling may revise that
/// translation but never overwrites the verbatim source it was derived
/// from.
/// </para>
/// <para>
/// Per the only-emit-what-Access-wrote rule from the project's Output
/// JSON Conventions, optional sub-records (<see cref="Range"/>,
/// <see cref="Pattern"/>, <see cref="Membership"/>, <see cref="Location"/>)
/// stay null when they do not apply to the rule's <see cref="Kind"/>.
/// </para>
/// </remarks>
public sealed record ValidationRule
{
    /// <summary>
    /// Where the rule lives — a table field, a table record-level rule, a
    /// form record-level rule, or a single control on a form.
    /// </summary>
    public required ValidationRuleScope Scope { get; init; }

    /// <summary>
    /// The owning object's name — the table name when
    /// <see cref="Scope"/> is <see cref="ValidationRuleScope.TableField"/> or
    /// <see cref="ValidationRuleScope.Table"/>, the form name when
    /// <see cref="Scope"/> is <see cref="ValidationRuleScope.Form"/> or
    /// <see cref="ValidationRuleScope.Control"/>.
    /// </summary>
    public required string SourceObject { get; init; }

    /// <summary>
    /// The field or control name. Required when <see cref="Scope"/> is
    /// <see cref="ValidationRuleScope.TableField"/> or
    /// <see cref="ValidationRuleScope.Control"/>; null when the rule is
    /// scoped to the table or form as a whole (record-level rules).
    /// </summary>
    public string? FieldOrControlName { get; init; }

    /// <summary>
    /// Where in the source artefact the rule was harvested from. Mirrors
    /// the Phase 6.4 source enumeration plus the table-side counterparts.
    /// </summary>
    public required ValidationRuleOrigin Origin { get; init; }

    /// <summary>
    /// The structural classification of the rule. The four Phase 6.4
    /// categories (<see cref="ValidationRuleKind.Required"/>,
    /// <see cref="ValidationRuleKind.Range"/>,
    /// <see cref="ValidationRuleKind.Pattern"/>,
    /// <see cref="ValidationRuleKind.CustomPredicate"/>) are the primary
    /// outputs; <see cref="ValidationRuleKind.Membership"/> is the
    /// LimitToList specialisation, and
    /// <see cref="ValidationRuleKind.Unstructured"/> is the explicit
    /// "I-could-not-classify" emission per the "unresolvable constructs
    /// are explicit, not silent" principle.
    /// </summary>
    public required ValidationRuleKind Kind { get; init; }

    /// <summary>
    /// Verbatim source text the rule was derived from, when applicable:
    /// <list type="bullet">
    ///   <item>For <see cref="ValidationRuleKind.Range"/> /
    ///   <see cref="ValidationRuleKind.Pattern"/> /
    ///   <see cref="ValidationRuleKind.CustomPredicate"/> /
    ///   <see cref="ValidationRuleKind.Unstructured"/>: the original
    ///   JET-expression text (table / form / control rule) or the rendered
    ///   VBA condition (BeforeUpdate-handler rule).</item>
    ///   <item>For <see cref="ValidationRuleKind.Required"/> /
    ///   <see cref="ValidationRuleKind.Membership"/>: null. The rule is
    ///   self-describing; no expression text exists in the source.</item>
    /// </list>
    /// </summary>
    public string? Expression { get; init; }

    /// <summary>
    /// The user-visible message Access shows when this rule rejects input —
    /// the <c>ValidationText</c> companion property, or a literal extracted
    /// from a <c>MsgBox</c> call inside a BeforeUpdate handler. Null when
    /// no message was recorded.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Range bounds when <see cref="Kind"/> is
    /// <see cref="ValidationRuleKind.Range"/>; null otherwise.
    /// </summary>
    public ValidationRangeConstraint? Range { get; init; }

    /// <summary>
    /// Pattern descriptor when <see cref="Kind"/> is
    /// <see cref="ValidationRuleKind.Pattern"/>; null otherwise.
    /// </summary>
    public ValidationPatternConstraint? Pattern { get; init; }

    /// <summary>
    /// Membership descriptor when <see cref="Kind"/> is
    /// <see cref="ValidationRuleKind.Membership"/>; null otherwise.
    /// </summary>
    public ValidationMembershipConstraint? Membership { get; init; }

    /// <summary>
    /// One-based source line / zero-based source column for VBA-derived
    /// rules (BeforeUpdate-handler patterns). Null for rules sourced from
    /// design-time properties bags (no AST node to point at).
    /// </summary>
    public ValidationRuleLocation? Location { get; init; }
}

/// <summary>
/// Where a <see cref="ValidationRule"/> lives in the source artefact.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationRuleScope
{
    /// <summary>One column of one table.</summary>
    TableField,

    /// <summary>The whole row of one table — record-level constraint.</summary>
    Table,

    /// <summary>The whole record of one form — form record-level constraint.</summary>
    Form,

    /// <summary>One control on one form.</summary>
    Control,
}

/// <summary>
/// The specific source the rule was harvested from. Distinguishes, e.g.,
/// a table-field <c>Required</c> flag from a form-control <c>Required</c>
/// flag from a BeforeUpdate-handler-derived required rule — the three
/// share <see cref="ValidationRuleKind.Required"/> but downstream
/// tooling may want to apply different translation strategies.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationRuleOrigin
{
    /// <summary>
    /// <see cref="AccessHarvester.Access.TableField.ValidationRule"/> on a
    /// table column — JET expression evaluated over the candidate value.
    /// </summary>
    TableFieldValidationRule,

    /// <summary>
    /// <see cref="AccessHarvester.Access.TableField.Required"/> = true on
    /// a table column.
    /// </summary>
    TableFieldRequired,

    /// <summary>
    /// <see cref="AccessHarvester.Access.TableMetadata.ValidationRule"/> on
    /// the table — JET expression evaluated over the row.
    /// </summary>
    TableValidationRule,

    /// <summary>
    /// <c>ValidationRule</c> property on a form control — JET expression.
    /// </summary>
    ControlValidationRule,

    /// <summary>
    /// <c>Required</c> = true on a form control.
    /// </summary>
    ControlRequired,

    /// <summary>
    /// <c>LimitToList</c> = true on a combo box. The allowed-set is the
    /// row source the combo box is bound to.
    /// </summary>
    ControlLimitToList,

    /// <summary>
    /// <c>InputMask</c> on a form control — pattern constraint Access
    /// enforces character-by-character.
    /// </summary>
    ControlInputMask,

    /// <summary>
    /// <c>ValidationRule</c> on the form record — JET expression evaluated
    /// when the record is saved.
    /// </summary>
    FormValidationRule,

    /// <summary>
    /// Pattern extracted from a <c>BeforeUpdate</c> event handler body
    /// (<c>If IsNull(field) Then Cancel = True</c> et al.).
    /// </summary>
    BeforeUpdateHandler,
}

/// <summary>
/// The structural classification of a <see cref="ValidationRule"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationRuleKind
{
    /// <summary>The value must be non-null (and, for text inputs derived
    /// from VBA, non-empty).</summary>
    Required,

    /// <summary>The value must fall within a numeric / date range. The
    /// bounds live on <see cref="ValidationRule.Range"/>.</summary>
    Range,

    /// <summary>The value must conform to a character pattern. The
    /// pattern lives on <see cref="ValidationRule.Pattern"/>.</summary>
    Pattern,

    /// <summary>The value must come from a fixed allowed-set whose source
    /// is described on <see cref="ValidationRule.Membership"/>.</summary>
    Membership,

    /// <summary>The rule is a custom boolean predicate the extractor
    /// recognized but does not decompose further. The verbatim source
    /// text (or rendered VBA condition) is preserved in
    /// <see cref="ValidationRule.Expression"/>.</summary>
    CustomPredicate,

    /// <summary>The rule's source text was non-empty but the extractor
    /// could not classify it into any of the above shapes. The verbatim
    /// text is preserved in <see cref="ValidationRule.Expression"/> per
    /// the "unresolvable constructs are explicit, not silent" rule.</summary>
    Unstructured,
}

/// <summary>
/// Range bounds for a <see cref="ValidationRuleKind.Range"/> rule. Both
/// <see cref="Minimum"/> and <see cref="Maximum"/> are preserved as the
/// verbatim JET / VBA text the extractor saw — typing and evaluation are
/// downstream concerns.
/// </summary>
public sealed record ValidationRangeConstraint
{
    /// <summary>
    /// The lower bound, verbatim (e.g., <c>0</c>, <c>#1/1/2000#</c>,
    /// <c>Date()</c>). Null when the rule has no lower bound (e.g.,
    /// <c>&lt; 100</c>).
    /// </summary>
    public string? Minimum { get; init; }

    /// <summary>
    /// True when the lower bound itself is allowed (<c>&gt;=</c> or
    /// <c>Between</c>). Defaults to true; null only when there is no lower
    /// bound at all.
    /// </summary>
    public bool? MinimumInclusive { get; init; }

    /// <summary>
    /// The upper bound, verbatim. Null when the rule has no upper bound.
    /// </summary>
    public string? Maximum { get; init; }

    /// <summary>
    /// True when the upper bound itself is allowed (<c>&lt;=</c> or
    /// <c>Between</c>).
    /// </summary>
    public bool? MaximumInclusive { get; init; }
}

/// <summary>
/// Pattern descriptor for a <see cref="ValidationRuleKind.Pattern"/> rule.
/// The Access <c>InputMask</c> is preserved verbatim; translation to a
/// regex equivalent is a downstream concern (the per-character mask
/// language has multiple syntactic dialects, and a wrong translation
/// would silently break input).
/// </summary>
public sealed record ValidationPatternConstraint
{
    /// <summary>
    /// The Access <c>InputMask</c> string, verbatim.
    /// </summary>
    public required string InputMask { get; init; }
}

/// <summary>
/// Membership descriptor for a <see cref="ValidationRuleKind.Membership"/>
/// rule. The allowed-set itself is the combo box's row source — this
/// extractor does not resolve <c>RowSource</c> SQL; the
/// <see cref="QueryAbstraction"/> linkage is a Session 3 (RowSource
/// specification) deliverable. Until then, the consumer joins on
/// <see cref="SourceControlName"/> + the form's
/// <see cref="FormComponentSpec.Controls"/> entry to inspect the
/// row source.
/// </summary>
public sealed record ValidationMembershipConstraint
{
    /// <summary>
    /// The combo / list control whose row source defines the allowed-set.
    /// </summary>
    public required string SourceControlName { get; init; }
}

/// <summary>
/// One-based source line / zero-based source column for VBA-derived
/// rules. Mirrors the <see cref="AccessHarvester.Vba.Parser.Ast.Provenance"/>
/// shape used by other enrichers.
/// </summary>
public sealed record ValidationRuleLocation
{
    /// <summary>One-based source line of the originating statement.</summary>
    public required int Line { get; init; }

    /// <summary>Zero-based source column of the originating statement.</summary>
    public required int Column { get; init; }

    /// <summary>The procedure name the rule was extracted from
    /// (e.g., <c>Form_BeforeUpdate</c>, <c>txtAge_BeforeUpdate</c>).</summary>
    public required string ProcedureName { get; init; }
}
