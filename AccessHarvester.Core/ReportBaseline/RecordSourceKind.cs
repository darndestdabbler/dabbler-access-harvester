namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// How a report's <c>RecordSource</c> string is interpreted before it can
/// be executed. Resolved from the raw string by
/// <see cref="RecordSourceResolver"/>.
/// </summary>
public enum RecordSourceKind
{
    /// <summary>The RecordSource is an object name (table or saved query).</summary>
    TableOrQueryName,

    /// <summary>The RecordSource is an inline SQL <c>SELECT</c> statement.</summary>
    InlineSql,
}
