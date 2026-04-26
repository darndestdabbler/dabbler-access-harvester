using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// One node in the form/report design tree. Covers controls (TextBox, ComboBox,
/// CommandButton, ...), sections (Detail, FormHeader, GroupLevel0Header, ...),
/// and the anonymous <c>Begin</c> wrappers SaveAsText emits around default-
/// property templates inside a section.
/// </summary>
/// <remarks>
/// Shared by <see cref="FormMetadata"/> and <see cref="ReportMetadata"/> so the
/// design-tree shape is committed once and reused. The structure mirrors the
/// existing IR JSON produced by <c>AccessHarvester.Core</c> enrichers
/// (<c>GeometryEnricher</c>, <c>LabelResolver</c>, <c>PropertyDecoder</c>) — see
/// <c>docs/planning/project-guidance.md</c> -> Output JSON Conventions.
/// <para>
/// Only-emit-what-Access-wrote: <see cref="Type"/> is the sole required field.
/// Every other field is nullable, including <see cref="Name"/> — anonymous
/// <c>Begin</c> wrappers and nodes Access did not name carry <c>Name = null</c>.
/// Consumers serializing to JSON should configure
/// <c>JsonSerializerOptions.DefaultIgnoreCondition =
/// JsonIgnoreCondition.WhenWritingNull</c> so absent fields are omitted rather
/// than emitted as <c>null</c>. The exception is <see cref="Label"/> on eligible
/// controls, which is always emitted (even as <c>{ source: "unresolved" }</c>)
/// so consumers do not have to special-case absence.
/// </para>
/// </remarks>
public sealed record AccessControl
{
    /// <summary>
    /// The Access object class for this node — e.g. <c>Form</c>, <c>Report</c>,
    /// <c>Section</c>, <c>FormHeader</c>, <c>TextBox</c>, <c>ComboBox</c>,
    /// <c>CommandButton</c>, <c>Subform</c>, <c>Subreport</c>. The literal
    /// <c>Begin</c> appears for anonymous wrappers SaveAsText emits.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The control's name as Access stores it (e.g. <c>txtCustomerName</c>,
    /// <c>cmdSave</c>, <c>Detail</c> for the detail section). Null for the
    /// anonymous <c>Begin</c> wrappers SaveAsText emits and for any node Access
    /// did not give a name. Lifted from the raw <c>Name</c> property — when
    /// non-null, the corresponding <c>Name</c> key is removed from
    /// <see cref="Properties"/> by the populating extractor to avoid
    /// double-emission.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The Access property bag for this node, with values preserved as
    /// <see cref="JsonElement"/> so heterogeneous shapes (string scalar,
    /// repeated-key arrays — see <c>AccessTextParser.SetOrAccumulate</c>)
    /// survive round-trip. Twip dimensions (<c>Left</c>, <c>Top</c>,
    /// <c>Width</c>, <c>Height</c>) live here as strings; the inch-converted
    /// view is <see cref="GeometryIn"/>.
    /// <para>
    /// Two sanctioned mutations apply on top of the raw bag:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Promoted exact-value keys</b> are removed when non-null.
    ///   For <see cref="AccessControl"/> this is the <c>Name</c> key (lifted
    ///   to <see cref="Name"/>); the form/report root types remove
    ///   <c>RecordSource</c>, <c>Filter</c>, and <c>OrderBy</c> in addition.</item>
    ///   <item><b>Decoded property values</b> are rewritten in place. The
    ///   property-decoding enrichment pass replaces a literal <c>"NotDefault"</c>
    ///   value with the resolved <c>"True"</c> / <c>"False"</c> (or otherwise
    ///   decoded) value and lists the affected keys in
    ///   <see cref="NonDefaultProperties"/>. The raw numeric code for
    ///   decoded-lift fields like <c>DefaultView</c> is retained here
    ///   alongside the decoded label on the parent record.</item>
    /// </list>
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>
    /// Inch-converted geometry produced by the geometry enrichment pass.
    /// Sparse: a section with only Height yields <c>{ H: 1.0 }</c>. Raw twips
    /// remain in <see cref="Properties"/>.
    /// </summary>
    public GeometryInches? GeometryIn { get; init; }

    /// <summary>
    /// Resolved visible label for data-bound / interactive controls
    /// (TextBox, ComboBox, ListBox, CheckBox, OptionButton, ToggleButton,
    /// SubForm, Subreport). Always emitted for eligible controls — unresolved
    /// labels carry <c>{ source: "unresolved" }</c> rather than being omitted.
    /// Null for ineligible types (Label, Rectangle, Image, ...).
    /// </summary>
    public LabelInfo? Label { get; init; }

    /// <summary>
    /// Names of properties whose value in the Access source was the literal
    /// <c>"NotDefault"</c> sentinel — i.e. the developer explicitly deviated
    /// from the control / form / report default for this property. Populated
    /// by the property-decoding enrichment pass; the actual decoded value is
    /// rewritten in place inside <see cref="Properties"/>. Useful for
    /// downstream consumers that care about explicit deviations.
    /// </summary>
    public IReadOnlyList<string>? NonDefaultProperties { get; init; }

    /// <summary>
    /// Child nodes of this scope: a section's controls, a tab control's pages,
    /// the anonymous <c>Begin</c> wrapper's contents. <c>Subform</c> /
    /// <c>Subreport</c> control nodes do NOT inline their contained form /
    /// report tree here — that contained object is extracted separately as its
    /// own <see cref="FormMetadata"/> / <see cref="ReportMetadata"/>, and the
    /// subform/subreport node references it by name via its <c>SourceObject</c>
    /// property. <see cref="Children"/> on a subform/subreport node should
    /// therefore be null or empty in the typed schema, keeping the design
    /// acyclic.
    /// </summary>
    public IReadOnlyList<AccessControl>? Children { get; init; }

}

/// <summary>
/// Geometry expressed in inches (1 inch = 1440 twips), rounded to three
/// decimal places to keep fixture diffs stable. The unit is encoded in the
/// containing record / field name (<c>GeometryIn</c>, this type's name) — the
/// coordinate fields themselves stay short. Raw twip values remain in the
/// node's <c>Properties</c> bag. See <c>docs/planning/project-guidance.md</c>
/// -> Output JSON Conventions.
/// </summary>
/// <remarks>
/// Sparse by design: any dimension Access did not write is null and should be
/// omitted from the serialized output.
/// </remarks>
public sealed record GeometryInches
{
    /// <summary>Left edge in inches (from <c>Left</c> twips).</summary>
    public double? X { get; init; }

    /// <summary>Top edge in inches (from <c>Top</c> twips).</summary>
    public double? Y { get; init; }

    /// <summary>Width in inches (from <c>Width</c> twips).</summary>
    public double? W { get; init; }

    /// <summary>Height in inches (from <c>Height</c> twips).</summary>
    public double? H { get; init; }
}

/// <summary>
/// A resolved label with provenance. The four producing rounds, in order, are:
/// <c>attached</c> (a Label child's Caption), <c>proximity</c> (a peer Label
/// satisfying the conservative 2x rule), <c>from-name</c> (camelCase split of a
/// non-generic control Name), <c>from-binding</c> (camelCase split of a simple
/// ControlSource identifier). Controls that survive all four rounds receive
/// <c>{ source: "unresolved" }</c> so consumers do not have to special-case
/// absence. See <c>AccessHarvester.Core.Enrichment.LabelResolver</c>.
/// </summary>
public sealed record LabelInfo
{
    /// <summary>
    /// One of <c>attached</c>, <c>proximity</c>, <c>from-name</c>,
    /// <c>from-binding</c>, <c>unresolved</c>.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// The resolved label text. Null when <see cref="Source"/> is
    /// <c>unresolved</c>; non-null otherwise.
    /// </summary>
    public string? Text { get; init; }
}
