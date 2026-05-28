using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// Platform-neutral contract for reading an Access database's structural
/// metadata into a <see cref="RawSchema"/>. The production implementation
/// (<c>AccessHarvester.Core.Com.OleDbDaoSchemaReader</c>) drives OLE DB
/// schema rowsets plus DAO interop; tests pass in hand-built
/// <see cref="RawSchema"/> instances to exercise the heuristics engine
/// hermetically.
/// </summary>
public interface ISchemaReader
{
    /// <summary>Returns the schema snapshot for <paramref name="databasePath"/>.</summary>
    RawSchema Read(string databasePath);
}

/// <summary>
/// Platform-neutral contract for sampling column values + computing
/// per-column statistics (cardinality, null count, PII value-match,
/// canonical format pattern). The production implementation
/// (<c>AccessHarvester.Core.Com.OleDbValueSampler</c>) runs Jet/ACE-
/// compatible SQL; tests pass in pre-built <see cref="ColumnStatistics"/>.
/// </summary>
public interface IValueSampler
{
    /// <summary>Returns one <see cref="ColumnStatistics"/> per non-key data column the sampler chose to inspect.</summary>
    IReadOnlyList<ColumnStatistics> Sample(string databasePath, RawSchema schema, ScanConfig config);
}
