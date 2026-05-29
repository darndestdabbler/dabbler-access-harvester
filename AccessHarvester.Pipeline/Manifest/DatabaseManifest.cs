using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Root document for a full database export per the v1.1 schema's
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
/// <c>harvester-schema.v1.1.json</c> is exercised by
/// <c>ManifestSchemaConformanceTests</c>; any in-memory-type-vs-schema
/// mismatch surfaces there.
/// </para>
/// <para>
/// v1.1 (Session 2 of
/// <c>harvester-manifest-index-and-batch-readiness</c>) adds two
/// additive top-level fields — <see cref="Documentation"/> and
/// <see cref="FileMap"/> — so a binary-only consumer can find the
/// interpretation guide and the per-object IR files without prior
/// knowledge of the repo layout. Both are nullable on the record so
/// existing test surfaces that construct a manifest without them keep
/// compiling; the production pipeline always populates them.
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
    /// <c>$.SchemaVersion</c>. Always <c>"1.1"</c> for documents
    /// produced against <c>harvester-schema.v1.1.json</c>. Pinned via
    /// <see cref="SchemaVersionLiteral"/> so the CLI's <c>--version</c>
    /// emission and any other code path that needs the version string
    /// can read it from one place.
    /// </summary>
    [JsonPropertyOrder(-99)]
    public string SchemaVersion { get; } = SchemaVersionLiteral;

    /// <summary>
    /// The schema version literal emitted into <c>$.SchemaVersion</c>.
    /// Mirrors the schema file's <c>SchemaVersion</c> const. Bumped in
    /// lockstep when the schema bumps.
    /// </summary>
    public const string SchemaVersionLiteral = "1.1";

    /// <summary>
    /// Canonical harvester version string emitted into
    /// <c>$.HarvesterVersion</c> and into the CLI's <c>--json-status</c>
    /// envelope + <c>--version</c> output. Sourced from this assembly's
    /// <see cref="AssemblyInformationalVersionAttribute"/> so every
    /// surface that reports a version is reading from the same place.
    /// The SourceLink commit-sha decoration the SDK appends as
    /// <c>+&lt;sha&gt;</c> is stripped — consumers want the semver, not
    /// the build provenance.
    /// </summary>
    public static string ResolveHarvesterVersion()
    {
        var asm = typeof(DatabaseManifest).Assembly;
        string version =
            asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";
        int plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    /// <summary>
    /// Version of the harvester tool that produced this document.
    /// Caller-supplied; the aggregator does not synthesise it.
    /// </summary>
    [JsonPropertyOrder(-98)]
    public required string HarvesterVersion { get; init; }

    [JsonPropertyOrder(-97)]
    public required DatabaseInfo Database { get; init; }

    /// <summary>
    /// v1.1 additive: pointer to the public interpretation guide + the
    /// schema version this document conforms to. Null in legacy / test
    /// fixtures; the production pipeline always emits it.
    /// </summary>
    [JsonPropertyOrder(-96)]
    public DocumentationPointer? Documentation { get; init; }

    /// <summary>
    /// v1.1 additive: per-category navigation index pointing at the
    /// on-disk <c>ir/</c> tier. Null in hermetic aggregator unit tests
    /// that exercise pure assembly contracts; the production pipeline
    /// always emits it after the per-object files have been written
    /// (so counts reflect what is actually on disk).
    /// </summary>
    [JsonPropertyOrder(-95)]
    public FileMap? FileMap { get; init; }

    public DatabaseSummary Summary { get; init; } = new();

    public IReadOnlyList<FormExport> Forms { get; init; }
        = System.Array.Empty<FormExport>();

    public IReadOnlyList<FormExport> Reports { get; init; }
        = System.Array.Empty<FormExport>();

    public IReadOnlyList<JsonNode> Queries { get; init; }
        = System.Array.Empty<JsonNode>();

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
