namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// The result of classifying a report's raw RecordSource string and
/// shaping it into something executable. Carries both the original string
/// (for provenance) and the SQL the executor will actually run.
/// </summary>
public sealed record ResolvedRecordSource
{
    /// <summary>The raw RecordSource string as it came from <c>ReportMetadata.RecordSource</c>.</summary>
    public required string Original { get; init; }

    /// <summary>How the original string was classified.</summary>
    public required RecordSourceKind Kind { get; init; }

    /// <summary>
    /// The SQL the executor will run. For <see cref="RecordSourceKind.InlineSql"/>
    /// this is identical to <see cref="Original"/>; for
    /// <see cref="RecordSourceKind.TableOrQueryName"/> this is
    /// <c>SELECT * FROM [Original]</c> (bracket-quoted).
    /// </summary>
    public required string SqlToExecute { get; init; }
}
