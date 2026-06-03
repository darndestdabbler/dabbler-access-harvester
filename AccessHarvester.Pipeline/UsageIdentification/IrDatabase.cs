using System.Collections.Generic;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// In-memory representation of all IR files for one harvested database.
/// Consumed by signal collectors and the classifier. All fields are populated
/// by <see cref="IrLoader"/> at runtime; unit tests build instances in-memory.
/// </summary>
public sealed record IrDatabase
{
    public required string Name { get; init; }
    public required IReadOnlyList<IrFormOrReport> Forms { get; init; }
    public required IReadOnlyList<IrFormOrReport> Reports { get; init; }
    public required IReadOnlyList<IrQuery> Queries { get; init; }
    public required IReadOnlyList<IrTable> Tables { get; init; }
    public required IReadOnlyList<IrRelationship> Relationships { get; init; }
    public required IReadOnlyList<IrMacro> Macros { get; init; }
    public required IReadOnlyList<IrModule> Modules { get; init; }
}

/// <summary>
/// Minimal parsed representation of a form or report IR file.
/// </summary>
public sealed record IrFormOrReport
{
    public required string Name { get; init; }
    public required string ObjectType { get; init; }   // "Form" or "Report"
    public string? RecordSource { get; init; }
    public string? VbaCode { get; init; }
    public required IReadOnlyList<IrControl> Controls { get; init; }
    public bool HasBeforeUpdate { get; init; }
    public bool HasAfterUpdate { get; init; }
    public bool HasOnDelete { get; init; }
    public required IReadOnlyList<IrMacroAction> MacroActions { get; init; }
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

/// <summary>One control node found in a form or report's design tree.</summary>
public sealed record IrControl
{
    public required string Type { get; init; }   // "ComboBox", "ListBox", etc.
    public string? Name { get; init; }
    public string? RowSource { get; init; }
    public string? RowSourceType { get; init; }
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

/// <summary>Minimal parsed representation of a query IR file.</summary>
public sealed record IrQuery
{
    public required string Name { get; init; }
    public required int QueryTypeCode { get; init; }
    public string? Sql { get; init; }

    /// <summary>
    /// Saved-query parameters carried through from the query SQL at load time
    /// (set 001 Session 2): declared <c>PARAMETERS</c>-clause entries and implicit
    /// <c>[Forms]!</c>/<c>[Reports]!</c> control references. Null when none were
    /// detected. Populated by <see cref="IrLoader"/> via the shared
    /// <c>AccessHarvester.Access</c> parameter scan so this metadata survives load
    /// into the classifier instead of being dropped — the usage classifier reads
    /// it rather than re-implementing parameter parsing.
    /// </summary>
    public IReadOnlyList<IrQueryParameter>? Parameters { get; init; }
}

/// <summary>
/// One saved-query parameter (set 001 Session 2). Mirrors the declared /
/// form-reference / report-reference provenance of the Access-layer
/// <c>QueryParameter</c>, kept as a usage-side type so the IR model does not
/// leak the extractor type.
/// </summary>
public sealed record IrQueryParameter
{
    public required string Name { get; init; }
    public required string Source { get; init; }   // "declared" | "form-reference" | "report-reference"
    public string? Path { get; init; }
}

/// <summary>Minimal parsed representation of a table IR file.</summary>
public sealed record IrTable
{
    public required string Name { get; init; }
    public required int ColumnCount { get; init; }
    public System.DateTimeOffset? LastUpdated { get; init; }
    public string? Connect { get; init; }
    public IReadOnlyList<string> DateTimeColumnNames { get; init; } = [];
}

/// <summary>Parsed FK relationship from a relationship IR file.</summary>
public sealed record IrRelationship
{
    public required string Name { get; init; }
    public required string ParentTable { get; init; }
    public required string ChildTable { get; init; }
}

/// <summary>
/// One action entry found in a macro IR file's Children array.
/// </summary>
public sealed record IrMacroAction
{
    public string? MacroName { get; init; }
    public required string Action { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
}

/// <summary>Parsed macro IR file.</summary>
public sealed record IrMacro
{
    public required string Name { get; init; }
    public required IReadOnlyList<IrMacroAction> Actions { get; init; }
}

/// <summary>Parsed module IR file (standalone VBA module or class).</summary>
public sealed record IrModule
{
    public required string Name { get; init; }
    public string? VbaCode { get; init; }
}
