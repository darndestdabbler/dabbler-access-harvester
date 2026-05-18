using System.Collections.Generic;
using System.Text.Json.Serialization;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Root document for a full database export per the v1.0 schema's
/// <c>DatabaseManifest</c> definition. Wraps all harvested metadata
/// and enrichment output for one Access database.
/// </summary>
/// <remarks>
/// <para>
/// Build instances via
/// <see cref="DatabaseManifestAggregator.Build"/> — the constructor
/// surface is intentionally bare so callers cannot bypass the
/// summary-count consistency the aggregator enforces.
/// </para>
/// <para>
/// The per-category collections carry existing in-memory record
/// types from <c>AccessHarvester.Access</c> /
/// <c>AccessHarvester.Pipeline.Enrichment</c> rather than
/// re-modelled wrappers. JSON property-name conformance against
/// <c>harvester-schema.v1.0.json</c> is exercised by the
/// schema-conformance test landing in Session 2 of the
/// <c>harvester-finish-canonical-golden-output</c> set; any
/// in-memory-type-vs-schema mismatch surfaces there.
/// </para>
/// </remarks>
public sealed record DatabaseManifest
{
    /// <summary>
    /// Constant discriminator the schema requires at
    /// <c>$.SchemaType</c>. Always <c>"DatabaseManifest"</c>.
    /// </summary>
    [JsonPropertyOrder(-100)]
    public string SchemaType { get; } = "DatabaseManifest";

    /// <summary>
    /// Constant schema version the schema requires at
    /// <c>$.SchemaVersion</c>. Always <c>"1.0"</c> for documents
    /// produced against <c>harvester-schema.v1.0.json</c>.
    /// </summary>
    [JsonPropertyOrder(-99)]
    public string SchemaVersion { get; } = "1.0";

    /// <summary>
    /// Version of the harvester tool that produced this document.
    /// Caller-supplied; the aggregator does not synthesise it.
    /// </summary>
    [JsonPropertyOrder(-98)]
    public required string HarvesterVersion { get; init; }

    [JsonPropertyOrder(-97)]
    public required DatabaseInfo Database { get; init; }

    public DatabaseSummary Summary { get; init; } = new();

    public IReadOnlyList<FormExport> Forms { get; init; }
        = System.Array.Empty<FormExport>();

    public IReadOnlyList<FormExport> Reports { get; init; }
        = System.Array.Empty<FormExport>();

    public IReadOnlyList<QueryAbstraction> Queries { get; init; }
        = System.Array.Empty<QueryAbstraction>();

    public IReadOnlyList<ModuleExport> Modules { get; init; }
        = System.Array.Empty<ModuleExport>();

    public IReadOnlyList<TableMetadata> Tables { get; init; }
        = System.Array.Empty<TableMetadata>();

    public IReadOnlyList<RelationshipMetadata> Relationships { get; init; }
        = System.Array.Empty<RelationshipMetadata>();

    public IReadOnlyList<ValidationRule> ValidationRules { get; init; }
        = System.Array.Empty<ValidationRule>();

    public IReadOnlyList<RowSourceSpec> RowSourceSpecs { get; init; }
        = System.Array.Empty<RowSourceSpec>();

    public IReadOnlyList<FunctionClassification> FunctionClassifications { get; init; }
        = System.Array.Empty<FunctionClassification>();

    public IReadOnlyList<EventHandlerClassification> EventHandlerClassifications { get; init; }
        = System.Array.Empty<EventHandlerClassification>();

    public NavigationGraph? NavigationGraph { get; init; }

    public IReadOnlyList<UnresolvedItem> UnresolvedItems { get; init; }
        = System.Array.Empty<UnresolvedItem>();

    public IReadOnlyList<HarvesterDiagnostic> Diagnostics { get; init; }
        = System.Array.Empty<HarvesterDiagnostic>();
}
