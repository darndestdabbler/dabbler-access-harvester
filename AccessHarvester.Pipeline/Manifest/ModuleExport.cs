using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// VBA standard or class module export per the v1.0 schema's
/// <c>ModuleExport</c> definition: identity + parser output.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ParserOutput"/> is modelled as <see cref="JsonNode"/>?
/// for the same reason as <see cref="FormExport.ParserOutput"/> —
/// the manifest aggregator passes whatever the caller produced
/// through verbatim, leaving parser-output projection to the parser
/// assembly.
/// </para>
/// </remarks>
public sealed record ModuleExport
{
    public required string ModuleName { get; init; }
    public required ModuleKind ModuleKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? ParserOutput { get; init; }
}

/// <summary>
/// Discriminator for <see cref="ModuleExport.ModuleKind"/>. Matches
/// the v1.0 schema's <c>ModuleExport.ModuleKind</c> enum string
/// values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModuleKind
{
    Standard,
    ClassModule,
}
