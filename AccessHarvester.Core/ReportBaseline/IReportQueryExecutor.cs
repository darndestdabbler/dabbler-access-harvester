using System.Collections.Generic;
using System.Data;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Abstraction over "run this SQL against the report's source database".
/// The production implementation is the ACE OLE DB-backed
/// <c>AccessHarvester.Core.Com.OleDbReportQueryExecutor</c>; hermetic
/// tests pass a fake.
///
/// <para>
/// Parameter binding is positional — Access OLE DB text commands ignore
/// parameter names and bind <c>?</c> placeholders strictly in order.
/// Callers (typically <see cref="ReportBaselineService"/>) are responsible
/// for arranging the positional list to match the SQL's placeholder
/// order; the same list is also recorded as a name-keyed
/// <see cref="ReportAggregateInput.ParametersUsed"/> dictionary for
/// provenance.
/// </para>
/// </summary>
public interface IReportQueryExecutor
{
    /// <summary>
    /// Run <paramref name="sql"/> and return a fully-materialized
    /// <see cref="DataTable"/>. Pass <c>null</c> (or empty) for
    /// <paramref name="positionalParameters"/> when the SQL has no
    /// placeholders.
    /// </summary>
    DataTable Execute(string sql, IReadOnlyList<object?>? positionalParameters);
}
