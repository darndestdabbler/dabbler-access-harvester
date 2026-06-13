using System.Text.RegularExpressions;

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

    // Invariant numeric literal: optional sign; digits with an optional fraction
    // (`1`, `1.`, `1.5`) OR a leading-dot fraction (`.5`); optional `e`/`E` exponent.
    // Both `1.` and `.5` are valid SQL Server numeric literals, so accepting them
    // avoids degrading legitimate values to NULL (010 devil's-advocate review); a
    // VB `D`-exponent (`1D2`) is intentionally NOT accepted — it is not valid T-SQL.
    // Anything else interpolated bare into an INSERT would be malformed or arbitrary
    // SQL, so it is degraded to NULL (F-2-7).
    private static readonly Regex NumericToken =
        new(@"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$", RegexOptions.Compiled);

    /// <summary>Formats <paramref name="raw"/> as a SQL literal for a column of <paramref name="typeCode"/>.</summary>
    public static string Format(int typeCode, string? raw)
    {
        if (raw is null) return "NULL";

        return typeCode switch
        {
            DbText or DbChar or DbMemo => "N'" + raw.Replace("'", "''") + "'",
            DbDate or DbGuid           => "'" + raw.Replace("'", "''") + "'",
            DbBoolean                  => FormatBit(raw),
            // numeric families: emit the invariant token bare, but only when it is a
            // valid numeric literal; otherwise degrade to NULL rather than inject it.
            _                          => NumericToken.IsMatch(raw) ? raw : "NULL",
        };
    }

    /// <summary>
    /// True when a non-null CSV token cannot be emitted as a safe literal for this
    /// column type and <see cref="Format"/> therefore substitutes <c>NULL</c>: an
    /// out-of-pattern numeric token, or a bit token outside the known set. Text /
    /// date / GUID values are always quoted-and-escaped, so they are never "invalid"
    /// in this sense. The generator uses this to record a single
    /// <see cref="MigrationDegradationKind.InvalidDataToken"/> per affected column (F-2-7).
    /// </summary>
    public static bool IsInvalidDataToken(int typeCode, string? raw)
    {
        // null / empty / whitespace cells are a clean NULL (Format emits NULL for them),
        // not an invalid token — never flag them as a degradation.
        if (string.IsNullOrWhiteSpace(raw)) return false;
        // Binary/OLE columns are omitted from the INSERT upstream (the generator skips
        // them via IsBinary before the row loop), so they never reach this predicate in
        // practice; guard explicitly so a direct caller cannot mis-flag a hex/binary
        // token through the numeric branch below.
        if (IsBinary(typeCode)) return false;
        return typeCode switch
        {
            DbBoolean                  => !IsKnownBit(raw),
            DbText or DbChar or DbMemo
                or DbDate or DbGuid    => false,
            _                          => !NumericToken.IsMatch(raw),
        };
    }

    private static string FormatBit(string raw) => raw switch
    {
        "true" or "True" or "TRUE" or "1"  => "1",
        "false" or "False" or "FALSE" or "0" => "0",
        _ => "NULL", // not a recognized bit token; degrade to NULL rather than inject (F-2-7)
    };

    private static bool IsKnownBit(string raw) => raw is
        "true" or "True" or "TRUE" or "1" or "false" or "False" or "FALSE" or "0";
}
