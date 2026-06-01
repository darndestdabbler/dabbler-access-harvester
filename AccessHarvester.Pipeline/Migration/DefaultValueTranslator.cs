using System;
using System.Text.RegularExpressions;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>
/// Translates an Access column <c>DefaultValue</c> expression into a SQL Server
/// default expression (the inner expression only — the caller wraps it as
/// <c>DEFAULT (...)</c>). Only the safe, well-understood cases are translated;
/// anything else returns <c>false</c> so the caller can omit the default and
/// flag an <see cref="MigrationDegradationKind.UntranslatedDefault"/> rather than
/// fabricate one.
/// </summary>
public static class DefaultValueTranslator
{
    private static readonly Regex NumericLiteral =
        new(@"^[+-]?\d+(\.\d+)?$", RegexOptions.Compiled);

    // Date/time function with optional whitespace and optional parentheses.
    private static readonly Regex DateFn = new(@"^date\s*\(\s*\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NowFn  = new(@"^now\s*\(\s*\)$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TimeFn = new(@"^time\s*\(\s*\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Strict, locale-unambiguous date/time literal: yyyy-MM-dd or yyyy/MM/dd, optionally with a time.
    private static readonly Regex IsoDateLiteral =
        new(@"^\d{4}[-/]\d{2}[-/]\d{2}([ T]\d{2}:\d{2}(:\d{2})?)?$", RegexOptions.Compiled);

    /// <summary>
    /// Attempts to translate <paramref name="accessDefault"/> to a SQL Server
    /// default expression.
    /// </summary>
    /// <returns><c>true</c> with <paramref name="sqlDefault"/> set on success.</returns>
    public static bool TryTranslate(string? accessDefault, out string sqlDefault)
    {
        sqlDefault = "";
        if (string.IsNullOrWhiteSpace(accessDefault))
            return false;

        string v = accessDefault.Trim();

        // Date/time functions.
        if (DateFn.IsMatch(v)) { sqlDefault = "CAST(GETDATE() AS date)"; return true; }
        if (NowFn.IsMatch(v))  { sqlDefault = "SYSDATETIME()"; return true; }
        if (TimeFn.IsMatch(v)) { sqlDefault = "CAST(SYSDATETIME() AS time)"; return true; }

        // Boolean keywords (Access stores Yes/No fields' defaults as words).
        switch (v.ToLowerInvariant())
        {
            case "true":
            case "yes":
            case "on":
                sqlDefault = "1"; return true;
            case "false":
            case "no":
            case "off":
                sqlDefault = "0"; return true;
        }

        // Numeric literal — pass through verbatim.
        if (NumericLiteral.IsMatch(v))
        {
            sqlDefault = v;
            return true;
        }

        // Double-quoted string literal -> single-quoted, with Access's doubled
        // "" un-escaped and SQL's single quotes doubled.
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"')
        {
            string inner = v.Substring(1, v.Length - 2).Replace("\"\"", "\"");
            sqlDefault = "'" + inner.Replace("'", "''") + "'";
            return true;
        }

        // #...# date literal -> single-quoted, but ONLY when the inner value is a
        // strict, locale-unambiguous ISO date/time. A locale-shaped literal such as
        // #1/2/2020# would be interpreted differently depending on the SQL Server
        // session language, so it is left untranslated (omitted + flagged) rather
        // than silently changing meaning.
        if (v.Length >= 2 && v[0] == '#' && v[^1] == '#')
        {
            string inner = v.Substring(1, v.Length - 2).Trim();
            if (IsoDateLiteral.IsMatch(inner))
            {
                sqlDefault = "'" + inner + "'";
                return true;
            }
            return false;
        }

        // Anything else (arbitrary VBA/Jet expression) is not translated.
        return false;
    }
}
