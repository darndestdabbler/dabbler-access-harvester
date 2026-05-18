using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AccessHarvester.Pipeline.Enrichment;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Combined form (or report) enrichment export per the v1.0
/// schema's <c>FormExport</c> definition: a
/// <see cref="FormComponentSpec"/> plus the form's VBA parser output
/// when code-behind is present.
/// </summary>
/// <remarks>
/// <para>
/// The schema uses the same <c>FormExport</c> shape for both forms
/// and reports — the discriminator is which top-level array the
/// record lives in (<c>Forms[]</c> vs. <c>Reports[]</c>).
/// </para>
/// <para>
/// <see cref="ParserOutput"/> is modelled as
/// <see cref="JsonNode"/>? rather than a strongly-typed record so
/// the aggregator can pass through whatever parser-output shape the
/// caller already produced without forcing a re-projection here.
/// The schema's <c>ParserOutput</c> $def is structured, but its
/// fields are owned by the VBA parser, not by this assembly — a
/// future projection layer can land in <c>AccessHarvester.Vba.Parser</c>
/// without disturbing the manifest contract.
/// </para>
/// </remarks>
public sealed record FormExport
{
    public required FormComponentSpec Spec { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? ParserOutput { get; init; }
}
