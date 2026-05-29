using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Platform-neutral row reader. The production implementation
/// (<c>AccessHarvester.Core.Com.OleDbRowSource</c>) streams rows from the
/// source <c>.accdb</c> via OLE DB; unit tests supply in-memory rows so the
/// masking engine and FK invariants can be exercised hermetically.
/// </summary>
public interface IRowSource
{
    /// <summary>
    /// Streams every row of <paramref name="tableName"/> in source order.
    /// Each row is a column-name -> value dictionary; missing columns
    /// (e.g., a memo that the source skipped) appear with value <c>null</c>.
    /// The keys MUST match the column names in the document
    /// case-insensitively.
    /// </summary>
    IEnumerable<IReadOnlyDictionary<string, object?>> ReadRows(
        string tableName,
        IReadOnlyList<string> columnNames);
}
