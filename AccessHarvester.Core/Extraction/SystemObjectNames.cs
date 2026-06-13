using System;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// One definition of "is this an Access system/temporary object name" shared by the
/// extraction surfaces, so the raw tier and the DAO/OLE&#160;DB schema reader agree on
/// which objects to skip (F-1-7). The comparisons match
/// <c>Com/OleDbDaoSchemaReader.cs</c>: <c>MSys*</c> is matched case-insensitively
/// (a name surfacing as <c>MSYS*</c> is still a system table) while the <c>~</c>
/// temp/hidden prefix is matched ordinally. Using the explicit <see cref="StringComparison"/>
/// overloads avoids the culture-sensitive default of the bare <c>string.StartsWith(string)</c>
/// overload (CA1310), which could diverge under contraction-rule cultures.
/// </summary>
public static class SystemObjectNames
{
    /// <summary>True when <paramref name="name"/> is an Access system or temporary object name.</summary>
    public static bool IsSystemObjectName(string name) =>
        name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("~",    StringComparison.Ordinal);
}
