namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Counts derived from the populated per-category arrays in a
/// <see cref="DatabaseManifest"/>. Computed by the aggregator at
/// build time so the manifest is internally consistent (i.e.
/// <c>Forms.Length == Summary.FormCount</c>).
/// </summary>
/// <remarks>
/// Every count is non-negative; an absent category surfaces as 0
/// rather than null. The summary is purely advisory — consumers may
/// derive these counts themselves from the arrays — but emitting it
/// inline lets a downstream AI scan a manifest's shape without
/// deserialising the per-category payloads.
/// </remarks>
public sealed record DatabaseSummary
{
    public int FormCount { get; init; }
    public int ReportCount { get; init; }
    public int QueryCount { get; init; }
    public int ModuleCount { get; init; }
    public int TableCount { get; init; }
    public int UnresolvedItemCount { get; init; }
}
