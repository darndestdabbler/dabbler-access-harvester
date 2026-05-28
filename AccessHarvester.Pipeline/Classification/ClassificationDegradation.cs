using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// One degradation record produced during a classify run — a structural
/// gap (composite FK, unresolvable implicit FK, value-sampling SQL error,
/// COM property read failure, etc.) that the scanner could not fully
/// satisfy but did not want to abort over.
/// </summary>
/// <remarks>
/// Degradations are surfaced in the CLI's <c>--json-status</c> envelope and
/// drive a non-zero (exit code 3) partial-failure return so the operator
/// is aware the output is not authoritative for every column. They are
/// NOT written into <c>classification.json</c> — the on-disk artifact
/// stays clean for the editor and sanitizer; degradations travel through
/// the build / CI surface.
/// </remarks>
public sealed record ClassificationDegradation(
    DegradationKind Kind,
    string Location,
    string Detail);

public enum DegradationKind
{
    /// <summary>OLE DB / DAO read failure. Detail names the failed table/column.</summary>
    SchemaReadError,
    /// <summary>Implicit FK (combo-box <c>RowSource</c>) named a table but the target's PK could not be resolved.</summary>
    UnresolvedImplicitFk,
    /// <summary>Composite (multi-column) FK encountered. v1.0 of the schema does not yet model composite FKs.</summary>
    CompositeFkUnsupported,
    /// <summary>Cardinality / null-count / value sample query failed against a column.</summary>
    SamplingError,
}

/// <summary>
/// Implementing reader / sampler hands back a degradations list alongside
/// its primary output. The interface is opt-in so existing test doubles
/// keep working without change — the runner falls back to the base
/// <see cref="ISchemaReader"/> / <see cref="IValueSampler"/> call shape
/// when the implementation does not implement <see cref="IDegradationReporter"/>.
/// </summary>
public interface IDegradationReporter
{
    RawSchema ReadWithDegradations(string databasePath, IList<ClassificationDegradation> sink);
    IReadOnlyList<ColumnStatistics> SampleWithDegradations(
        string databasePath, RawSchema schema, ScanConfig config, IList<ClassificationDegradation> sink);
}
