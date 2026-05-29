using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>Context passed to every <see cref="IMaskingHandler"/> invocation.</summary>
public sealed record MaskingContext(
    string TableName,
    ClassifiedColumn Column,
    RemapRegistry Registry,
    DateTimeOffset SanitizationEpoch);

/// <summary>
/// One handler per <see cref="MaskingStrategy"/>. Pure — no I/O, no
/// randomness; outputs are deterministic functions of the original value
/// (and, where applicable, the column metadata and the row index threaded
/// through <see cref="MaskingEngine"/>).
/// </summary>
public interface IMaskingHandler
{
    object? Apply(object? originalValue, MaskingContext ctx, long rowIndex);
}

/// <summary>Returns the original value unchanged. NULL stays NULL.</summary>
public sealed class PreserveHandler : IMaskingHandler
{
    public object? Apply(object? originalValue, MaskingContext ctx, long rowIndex) => originalValue;
}

/// <summary>
/// Replaces the original with a deterministic mock value. When the column
/// has a <c>valueFormatPattern</c>, the pattern is rendered with digits
/// (`#`) and letters (`?`) substituted from a per-row deterministic stream
/// seeded by <c>(table, column, rowIndex)</c>. When the column has no
/// pattern, falls back to a label of the form
/// <c>MOCK-&lt;ColumnName&gt;-&lt;rowIndex&gt;</c>. NULL stays NULL.
/// </summary>
public sealed class MockReplaceHandler : IMaskingHandler
{
    public object? Apply(object? originalValue, MaskingContext ctx, long rowIndex)
    {
        if (originalValue is null) return null;
        if (!string.IsNullOrEmpty(ctx.Column.ValueFormatPattern))
            return FormatPatternMocker.Render(
                pattern: ctx.Column.ValueFormatPattern!,
                seed: DeterministicSeed(ctx.TableName, ctx.Column.Name, rowIndex));
        return string.Create(CultureInfo.InvariantCulture, $"MOCK-{ctx.Column.Name}-{rowIndex}");
    }

    internal static int DeterministicSeed(string table, string column, long rowIndex)
    {
        // FNV-1a 32-bit hash of (table, column, rowIndex). Stable across
        // platforms; far better than relying on string.GetHashCode()
        // (randomized per process in .NET Core+).
        const uint Fnv1aOffset = 2166136261U;
        const uint Fnv1aPrime  = 16777619U;
        uint hash = Fnv1aOffset;
        foreach (char c in table)  hash = (hash ^ c) * Fnv1aPrime;
        foreach (char c in column) hash = (hash ^ c) * Fnv1aPrime;
        for (int i = 0; i < 8; i++)
        {
            hash = (hash ^ (byte)(rowIndex >> (i * 8))) * Fnv1aPrime;
        }
        return unchecked((int)hash);
    }
}

/// <summary>
/// Generalises the original value to a lower-resolution shape. Recognises
/// DOB-like columns (renders just the year) and US ZIP-like 5-digit
/// columns (renders ZIP3 + "00"). Otherwise falls back to a coarse
/// length-bucket label. NULL stays NULL.
/// </summary>
public sealed class GeneralizeHandler : IMaskingHandler
{
    public object? Apply(object? originalValue, MaskingContext ctx, long rowIndex)
    {
        if (originalValue is null) return null;

        string colName = ctx.Column.Name;

        if (IsDateLike(originalValue, ctx.Column.DataType)
         && TryGetDateTime(originalValue, out var dt))
        {
            return new DateTime(dt.Year, 1, 1);
        }

        string s = originalValue.ToString() ?? string.Empty;

        if (colName.Contains("Zip", StringComparison.OrdinalIgnoreCase)
         && s.Length >= 3
         && AllDigits(s, 5))
        {
            return s.Substring(0, 3) + "00";
        }

        // Fallback: keep the first character (or a single underscore) plus
        // a length bucket. Strictly lower-resolution than the original.
        if (s.Length == 0) return string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{char.ToUpperInvariant(s[0])}-len{s.Length}");
    }

    private static bool IsDateLike(object value, string dataType) =>
        value is DateTime
     || value is DateTimeOffset
     || dataType.Contains("Date", StringComparison.OrdinalIgnoreCase)
     || dataType.Contains("Time", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetDateTime(object value, out DateTime dt)
    {
        switch (value)
        {
            case DateTime d:        dt = d; return true;
            case DateTimeOffset o:  dt = o.UtcDateTime; return true;
            case string s when DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed):
                dt = parsed; return true;
            default: dt = default; return false;
        }
    }

    private static bool AllDigits(string s, int maxLen)
    {
        if (s.Length > maxLen) return false;
        for (int i = 0; i < s.Length; i++) if (!char.IsDigit(s[i])) return false;
        return true;
    }
}

/// <summary>Replaces the original with the constant <c>[REDACTED]</c>. NULL stays NULL.</summary>
public sealed class RedactHandler : IMaskingHandler
{
    public const string ConstantValue = "[REDACTED]";
    public object? Apply(object? originalValue, MaskingContext ctx, long rowIndex)
        => originalValue is null ? null : ConstantValue;
}

/// <summary>
/// Replaces a free-text / memo value with a deterministic Lorem-style
/// placeholder. The placeholder length matches the original length bucket
/// so downstream consumers do not see e.g. a 10,000-character memo replaced
/// by a 5-character string. NULL stays NULL.
/// </summary>
public sealed class ScrubFreeTextHandler : IMaskingHandler
{
    private static readonly string Lorem =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor " +
        "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
        "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. ";

    public object? Apply(object? originalValue, MaskingContext ctx, long rowIndex)
    {
        if (originalValue is null) return null;
        string s = originalValue.ToString() ?? string.Empty;
        if (s.Length == 0) return string.Empty;
        if (s.Length <= Lorem.Length) return Lorem.Substring(0, s.Length);
        var sb = new StringBuilder(s.Length);
        while (sb.Length < s.Length)
            sb.Append(Lorem, 0, Math.Min(Lorem.Length, s.Length - sb.Length));
        return sb.ToString();
    }
}

/// <summary>
/// Produces a deterministic regenerated value for system / audit columns.
/// Date-like columns get the sanitization epoch; everything else gets the
/// constant <c>[AUDIT]</c>. Preserves a NOT-NULL invariant because the
/// scanner classifies columns as <c>system-audit</c> only when they appear
/// across multiple tables (plan § 3.2), and required system columns must
/// stay populated.
/// </summary>
public sealed class RegenerateHandler : IMaskingHandler
{
    public const string ConstantValue = "[AUDIT]";

    public object? Apply(object? originalValue, MaskingContext ctx, long rowIndex)
    {
        if (originalValue is null && !ctx.Column.IsRequired) return null;
        if (IsDateLike(ctx.Column.DataType)) return ctx.SanitizationEpoch.UtcDateTime;
        return ConstantValue;
    }

    private static bool IsDateLike(string dataType) =>
        dataType.Contains("Date", StringComparison.OrdinalIgnoreCase)
     || dataType.Contains("Time", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Replaces the original with the registry-tracked new value for the
/// column's referenced (table, column). For PK columns the target is the
/// column itself; for FK columns the target is <c>FkTarget</c>. The
/// invariant: any two columns whose remap target keys are equal share a
/// single <see cref="Dictionary{TKey, TValue}"/> inside the registry, so
/// the same original always produces the same new value. NULL stays NULL.
/// </summary>
public sealed class RemapIdHandler : IMaskingHandler
{
    public object? Apply(object? originalValue, MaskingContext ctx, long rowIndex)
    {
        if (originalValue is null) return null;
        TableColumn target = ctx.Column.IsForeignKey && ctx.Column.FkTarget is not null
            ? new TableColumn(ctx.Column.FkTarget.Table, ctx.Column.FkTarget.Column)
            : new TableColumn(ctx.TableName, ctx.Column.Name);
        return ctx.Registry.Remap(target, originalValue);
    }
}
