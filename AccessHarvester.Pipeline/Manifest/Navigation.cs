namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// v1.1 additive: public pointer to the interpretation guide + the
/// schema version this document conforms to. Surfaces at
/// <c>$.Documentation</c> on a <see cref="DatabaseManifest"/>.
/// </summary>
/// <remarks>
/// The URL is intentionally a public HTTPS link rather than a relative
/// repo path. The harvester ships as a self-contained binary; consumers
/// receive the output zip without the source repo, so a relative path
/// resolves to nothing on their machine. The schema version is
/// duplicated here (mirrors the root <c>SchemaVersion</c>) so the
/// pointer is self-contained — an agent that pulls just this object
/// from a larger document still knows which schema to read against.
/// </remarks>
public sealed record DocumentationPointer
{
    /// <summary>Public URL of the interpretation guide.</summary>
    public required string Url { get; init; }

    /// <summary>
    /// The schema version this document conforms to (e.g. <c>"1.1"</c>).
    /// </summary>
    public required string SchemaVersion { get; init; }
}

/// <summary>
/// v1.1 additive: per-category navigation index pointing at the
/// on-disk <c>ir/</c> tier. Surfaces at <c>$.FileMap</c> on a
/// <see cref="DatabaseManifest"/>.
/// </summary>
/// <remarks>
/// Globs are relative to the directory containing
/// <c>manifest.json</c>. Modules surface at <c>manifest.Modules</c>
/// but live on disk under <c>ir/ModulesAndClasses/</c>; the
/// <see cref="Modules"/> entry's <see cref="FileMapEntry.Glob"/>
/// reflects the on-disk path. Counts reflect the number of per-object
/// files actually written, so <c>FileMap.&lt;X&gt;.Count</c> stays
/// equal to <c>manifest.&lt;X&gt;.Length</c> and to the on-disk file
/// count — the invariant <c>ManifestCompletenessTests</c> pins.
/// </remarks>
public sealed record FileMap
{
    public required FileMapEntry Forms         { get; init; }
    public required FileMapEntry Reports       { get; init; }
    public required FileMapEntry Queries       { get; init; }
    public required FileMapEntry Modules       { get; init; }
    public required FileMapEntry Tables        { get; init; }
    public required FileMapEntry Relationships { get; init; }
}

/// <summary>
/// One per-category entry inside a <see cref="FileMap"/>.
/// </summary>
public sealed record FileMapEntry
{
    /// <summary>
    /// Number of per-object IR files on disk for this category.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Relative glob (relative to the directory containing
    /// <c>manifest.json</c>) that locates the per-object IR files.
    /// </summary>
    public required string Glob { get; init; }
}
