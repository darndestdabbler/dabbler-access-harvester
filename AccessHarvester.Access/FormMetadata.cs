using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Structured metadata for a single Access form, covering the deliverables of
/// Plan Phase 4.1. Holds the form's bound data source, hidden VBA-class-module
/// name, raw property bag, geometry, design tree of controls and sections, and
/// optional code-behind. Subforms appear inside <see cref="Children"/> as
/// <c>Subform</c> control nodes whose <see cref="AccessControl.Properties"/>
/// carry the <c>SourceObject</c> reference.
/// </summary>
/// <remarks>
/// One designed-once schema covers both forms and reports — see also
/// <see cref="ReportMetadata"/>, which adds report-only fields
/// (<see cref="ReportMetadata.GroupLevels"/>, <see cref="ReportMetadata.PageSetup"/>)
/// but otherwise mirrors this shape.
/// <para>
/// Output JSON conventions (<c>docs/planning/project-guidance.md</c> -> Output
/// JSON Conventions) apply: only-emit-what-Access-wrote (every field except
/// <see cref="Name"/> is nullable; serializer should configure
/// <c>JsonIgnoreCondition.WhenWritingNull</c>); raw twip dimensions live in
/// <see cref="Properties"/>, inch-converted geometry in <see cref="GeometryIn"/>;
/// the <c>{ source, ... }</c> provenance shape is reused on labels via
/// <see cref="LabelInfo"/> attached to controls.
/// </para>
/// <para>
/// Phase 4.1 lifts a small number of bound-data-source properties out of the
/// raw <see cref="Properties"/> bag into typed fields so downstream consumers
/// can read them without dictionary scanning. The promotion rule is:
/// <list type="bullet">
///   <item><b>Exact-value lifts</b> — <see cref="RecordSource"/>,
///   <see cref="Filter"/>, <see cref="OrderBy"/>: the typed field carries
///   the same string Access wrote. Extractors remove the corresponding key
///   from <see cref="Properties"/> when promoting it, to avoid
///   double-emitting the same value through two paths.</item>
///   <item><b>Decoded lifts</b> — <see cref="DefaultView"/>: the typed field
///   carries a resolved label (e.g. <c>Single Form</c>) decoded from
///   Access's numeric code. The raw numeric code is preserved in
///   <see cref="Properties"/> so round-trip fidelity to the underlying
///   IR JSON is not lost.</item>
/// </list>
/// </para>
/// </remarks>
public sealed record FormMetadata
{
    /// <summary>The form's name as Access stores it (without any container prefix).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The VBA class module backing this form (typically <c>Form_&lt;Name&gt;</c>
    /// for forms with code-behind). Null when the form has no class module.
    /// Required for the symbol-table integration in Plan Phase 4.4 — the
    /// class scope of this module is populated with synthetic declarations
    /// for every control on the form so VBA references like <c>Me.txtFoo</c>
    /// resolve.
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>
    /// The form's bound data source: a table name, a saved query name, or a
    /// SQL string. Null for unbound forms.
    /// </summary>
    public string? RecordSource { get; init; }

    /// <summary>
    /// The Filter clause Access applies on top of <see cref="RecordSource"/>,
    /// in JET SQL <c>WHERE</c>-fragment form (without the <c>WHERE</c>
    /// keyword). Null when no filter is set.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// The Order By clause, in JET SQL <c>ORDER BY</c>-fragment form (without
    /// the <c>ORDER BY</c> keywords). Null when no sort is set.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// The view Access opens this form in by default — typically one of
    /// <c>Single Form</c>, <c>Continuous Forms</c>, <c>Datasheet</c>,
    /// <c>Split Form</c>, <c>PivotTable</c>, <c>PivotChart</c>. Decoded from
    /// Access's numeric code; the raw code is preserved in
    /// <see cref="Properties"/> per the decoded-lift rule above.
    /// </summary>
    public string? DefaultView { get; init; }

    /// <summary>
    /// The form-level property bag as Access wrote it, with exact-value lifts
    /// removed (<see cref="RecordSource"/>, <see cref="Filter"/>,
    /// <see cref="OrderBy"/>) but decoded lifts left intact (the raw numeric
    /// <c>DefaultView</c> code remains here alongside the decoded
    /// <see cref="DefaultView"/> label). Values preserved as
    /// <see cref="JsonElement"/> for faithful round-trip; see
    /// <see cref="AccessControl.Properties"/> for rationale.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>
    /// Inch-converted form-level geometry. The form's overall <c>Width</c> and
    /// the implicit page extent are converted; absent dimensions remain null.
    /// </summary>
    public GeometryInches? GeometryIn { get; init; }

    /// <summary>
    /// Form-level properties that were the literal <c>"NotDefault"</c>
    /// sentinel in the source — see <see cref="AccessControl.NonDefaultProperties"/>.
    /// </summary>
    public IReadOnlyList<string>? NonDefaultProperties { get; init; }

    /// <summary>
    /// The form's design tree: typically the anonymous default-property
    /// <c>Begin</c> wrapper followed by <c>FormHeader</c> / <c>Section</c>
    /// (Detail) / <c>FormFooter</c> sections, each holding their controls.
    /// Subforms appear here as <c>Subform</c> control nodes referencing their
    /// contained form by <c>SourceObject</c> — see
    /// <see cref="AccessControl.Children"/> for the acyclic-by-reference
    /// contract that single-sources this rule.
    /// </summary>
    public IReadOnlyList<AccessControl>? Children { get; init; }

    /// <summary>
    /// The complete VBA code-behind module text, exactly as Access wrote it.
    /// Null when the form has no code-behind. Procedure-level structure is
    /// surfaced separately by <c>VbaSignatureEnricher</c> (the
    /// <c>Procedures</c> array on the IR JSON); it is not re-modelled in the
    /// metadata schema for Session 1.
    /// </summary>
    public string? VbaCode { get; init; }
}
