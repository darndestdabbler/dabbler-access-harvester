namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// The on-disk contract of the <c>sanitize</c> subcommand's CSV-per-table output,
/// confirmed against the writer (<c>AccessHarvester.Pipeline.Sanitization.CsvWriter</c>
/// and <c>SanitizationRunner</c>) as the migration generator's data input contract.
/// <para>
/// Session 1 records this contract; the rows are consumed in Session 2 (data-load).
/// Pinning it here as named constants keeps the producer (sanitize) and the future
/// consumer (generate-migration data-load) from drifting silently — a hermetic
/// test asserts these values against the writer.
/// </para>
/// </summary>
public static class SanitizeCsvContract
{
    /// <summary>One CSV file per table, named <c>&lt;TableName&gt;.csv</c>.</summary>
    public const string FileExtension = ".csv";

    /// <summary>UTF-8 without a byte-order mark.</summary>
    public const bool Utf8WithoutBom = true;

    /// <summary>RFC 4180 quoting: fields containing <c>, " CR LF</c> are double-quoted; embedded quotes are doubled.</summary>
    public const bool Rfc4180Quoting = true;

    /// <summary>The first row is a header of unquoted-unless-necessary column names, in source column order.</summary>
    public const bool HasHeaderRow = true;

    /// <summary>SQL NULL is written as an empty (zero-length) field.</summary>
    public const string NullToken = "";

    /// <summary>Date/time values are ISO-8601 <c>yyyy-MM-ddTHH:mm:ss</c>.</summary>
    public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss";

    /// <summary>Boolean values are the lowercase tokens <c>true</c> / <c>false</c>.</summary>
    public const string BoolTrue = "true";

    /// <summary>Boolean values are the lowercase tokens <c>true</c> / <c>false</c>.</summary>
    public const string BoolFalse = "false";

    /// <summary>Numeric values use the invariant culture (<c>.</c> decimal separator, no group separators).</summary>
    public const bool InvariantNumerics = true;
}
