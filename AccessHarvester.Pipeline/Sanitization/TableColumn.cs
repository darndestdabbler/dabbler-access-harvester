using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Case-insensitive (Table, Column) pair used as the FK graph and remap
/// registry key. Access identifiers are case-insensitive in JET/ACE, so the
/// sanitizer must treat <c>person.PersonID</c> and <c>Person.personID</c> as
/// the same column when matching an FK source to its referenced PK.
/// </summary>
public readonly record struct TableColumn(string Table, string Column)
{
    public static IEqualityComparer<TableColumn> CaseInsensitiveComparer { get; } =
        new CaseInsensitiveTableColumnComparer();

    private sealed class CaseInsensitiveTableColumnComparer : IEqualityComparer<TableColumn>
    {
        public bool Equals(TableColumn x, TableColumn y) =>
            string.Equals(x.Table,  y.Table,  StringComparison.OrdinalIgnoreCase)
         && string.Equals(x.Column, y.Column, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(TableColumn obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Table),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Column));
    }
}
