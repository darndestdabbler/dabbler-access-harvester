using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Header block for a <see cref="DatabaseManifest"/>: identifies the
/// source database file and when the harvest ran.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Name"/> is the only required field (matches the schema's
/// <c>DatabaseInfo.required</c>). <see cref="FilePath"/> and
/// <see cref="ExportedAt"/> are optional because golden-output
/// snapshots normalise both to stable placeholders — the regression
/// test does not need the absolute path or the harvest timestamp to
/// match.
/// </para>
/// </remarks>
public sealed record DatabaseInfo
{
    /// <summary>
    /// Database file name without path, e.g. <c>MyDatabase.accdb</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Absolute path to the <c>.accdb</c> / <c>.mdb</c> file as
    /// harvested. Null when the caller does not record absolute paths
    /// (normalised golden snapshots set this to a stable placeholder).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; init; }

    /// <summary>
    /// ISO-8601 timestamp of the harvest run. Null when the caller
    /// does not record timestamps (normalised golden snapshots set
    /// this to a stable placeholder).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExportedAt { get; init; }
}
