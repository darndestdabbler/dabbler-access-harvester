namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// Formats one sanitized CSV field into a SQL Server <c>INSERT</c> value literal,
/// typed by the column's DAO <c>dbType*</c> code. A <c>null</c> field (an unquoted
/// empty CSV cell, the writer's form for SQL <c>NULL</c>) always becomes
/// <c>NULL</c>; otherwise the literal is shaped per the column's type family.
/// </summary>
public static class SqlValueFormatter
{
    // DAO dbType* codes that the type mapper resolves to a SQL Server binary type
    // (binary / varbinary / OLE Object). Their bytes do not round-trip through the
    // text CSV, so the data-load omits them rather than fabricating a literal.
    private const int DbBinary     = 9;
    private const int DbLongBinary = 11;
    private const int DbVarBinary  = 17;

    private const int DbBoolean = 1;
    private const int DbDate    = 8;
    private const int DbText    = 10;
    private const int DbMemo    = 12;
    private const int DbGuid    = 15;
    private const int DbChar    = 18;

    /// <summary>
    /// True when a column of this type cannot have its data loaded from the text
    /// CSV (binary/OLE). Such columns survive in the schema but are omitted from
    /// the generated <c>INSERT</c> column list and flagged.
    /// </summary>
    public static bool IsBinary(int typeCode) =>
        typeCode is DbBinary or DbLongBinary or DbVarBinary;

    /// <summary>Formats <paramref name="raw"/> as a SQL literal for a column of <paramref name="typeCode"/>.</summary>
    public static string Format(int typeCode, string? raw)
    {
        if (raw is null) return "NULL";

        return typeCode switch
        {
            DbText or DbChar or DbMemo => "N'" + raw.Replace("'", "''") + "'",
            DbDate or DbGuid           => "'" + raw.Replace("'", "''") + "'",
            DbBoolean                  => FormatBit(raw),
            _                          => raw, // numeric families: emit the invariant token bare
        };
    }

    private static string FormatBit(string raw) => raw switch
    {
        "true" or "True" or "TRUE" or "1"  => "1",
        "false" or "False" or "FALSE" or "0" => "0",
        _ => raw, // already a 0/1-ish token from upstream; pass through unquoted
    };
}
