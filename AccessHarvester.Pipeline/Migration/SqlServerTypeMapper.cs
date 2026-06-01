using System.Globalization;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// The result of mapping one Access column type to a SQL Server type.
/// When <see cref="Mappable"/> is <c>false</c> the column has no honest scalar
/// SQL Server representation and must be flagged + omitted (never fabricated).
/// A <see cref="DegradationKind"/> may also be present on a <em>mappable</em>
/// column to carry an informational note (e.g. an assumed decimal precision).
/// </summary>
public sealed record SqlTypeMapping(
    bool Mappable,
    string SqlType,
    bool IsIdentity,
    MigrationDegradationKind? DegradationKind,
    string? DegradationReason);

/// <summary>
/// Pure-logic Access (DAO <c>dbType*</c>) → SQL Server type map. The mapping
/// follows the SSMA-for-Access conventions documented in the data-classification
/// plan §8 (Text→nvarchar, Memo→nvarchar(max), Long→int, Double→float,
/// Currency→money, Date/Time→datetime2, Yes/No→bit, AutoNumber→int IDENTITY,
/// OLE Object→varbinary(max)). Multi-value / Attachment fields are flagged as
/// unmappable degradations rather than given a lossy scalar mapping.
/// </summary>
public static class SqlServerTypeMapper
{
    // DAO dbType* constants (the same set TableDdlBuilder switches on).
    private const int DbBoolean      = 1;
    private const int DbByte         = 2;
    private const int DbInteger      = 3;   // 16-bit ("Short")
    private const int DbLong         = 4;   // 32-bit; AutoNumber when dbAutoIncrField is set
    private const int DbCurrency     = 5;
    private const int DbSingle       = 6;
    private const int DbDouble       = 7;
    private const int DbDate         = 8;
    private const int DbBinary       = 9;
    private const int DbText         = 10;
    private const int DbLongBinary   = 11;  // OLE Object
    private const int DbMemo         = 12;  // Long Text
    private const int DbGuid         = 15;
    private const int DbBigInt       = 16;
    private const int DbVarBinary    = 17;
    private const int DbChar         = 18;
    private const int DbNumeric      = 19;
    private const int DbDecimal      = 20;
    private const int DbFloat        = 21;
    private const int DbAttachment   = 101; // multi-value / Attachment

    /// <summary>Access Text columns default to 255 chars when no size is recorded.</summary>
    private const int DefaultTextLength = 255;

    /// <summary>SQL Server <c>nvarchar(n)</c> caps at 4000 before <c>nvarchar(max)</c>.</summary>
    private const int MaxNVarCharLength = 4000;

    public static SqlTypeMapping Map(MigrationField field)
    {
        switch (field.TypeCode)
        {
            case DbBoolean:    return Ok("bit");
            case DbByte:       return Ok("tinyint");
            case DbInteger:    return Ok("smallint");
            case DbLong:       return Ok("int", isIdentity: field.IsAutoIncrement);
            case DbBigInt:     return Ok("bigint");
            case DbCurrency:   return Ok("money");
            case DbSingle:     return Ok("real");
            case DbDouble:
            case DbFloat:      return Ok("float");
            case DbDate:       return Ok("datetime2");
            case DbGuid:       return Ok("uniqueidentifier");
            case DbMemo:       return Ok("nvarchar(max)");
            case DbLongBinary: return Ok("varbinary(max)");

            case DbText:       return Ok($"nvarchar({TextLength(field.Size)})");
            case DbChar:       return Ok($"nchar({TextLength(field.Size)})");

            case DbBinary:
                return Ok($"binary({(field.Size is > 0 ? field.Size.Value : 1)})");
            case DbVarBinary:
                return field.Size is > 0
                    ? Ok($"varbinary({field.Size.Value})")
                    : Ok("varbinary(max)");

            case DbNumeric:
            case DbDecimal:
                // The IR carries a byte width, not SQL precision/scale, so we use a
                // documented default and surface it for a reviewer to tighten.
                return new SqlTypeMapping(
                    Mappable: true,
                    SqlType: "decimal(18,4)",
                    IsIdentity: false,
                    DegradationKind: MigrationDegradationKind.AssumedTypePrecision,
                    DegradationReason:
                        $"Access {field.TypeName} (type {field.TypeCode}) does not expose precision/scale; " +
                        "mapped to decimal(18,4). Review and tighten if the source defined a narrower precision.");

            case DbAttachment:
                return Unmappable(
                    $"Access multi-value/Attachment field '{field.Name}' has no scalar SQL Server column " +
                    "(it is a hidden child table in Access); flagged for human attention.");

            default:
                return Unmappable(
                    $"Unmapped Access type code {field.TypeCode.ToString(CultureInfo.InvariantCulture)} " +
                    $"('{field.TypeName}') for column '{field.Name}'.");
        }
    }

    private static SqlTypeMapping Ok(string sqlType, bool isIdentity = false) =>
        new(Mappable: true, SqlType: sqlType, IsIdentity: isIdentity,
            DegradationKind: null, DegradationReason: null);

    private static SqlTypeMapping Unmappable(string reason) =>
        new(Mappable: false, SqlType: "", IsIdentity: false,
            DegradationKind: MigrationDegradationKind.UnmappableColumnType,
            DegradationReason: reason);

    private static int TextLength(int? size)
    {
        int n = size is > 0 ? size.Value : DefaultTextLength;
        return n > MaxNVarCharLength ? MaxNVarCharLength : n;
    }
}
