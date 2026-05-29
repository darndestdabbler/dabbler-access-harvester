using System;
using System.Collections.Generic;
using System.Globalization;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Deterministic old-value -> new-value memoization keyed by the referenced
/// (table, column). Drives the FK consistency invariant: a PK column's
/// remap and every FK column pointing at it consult the same per-key map,
/// so the same original value always produces the same new value across
/// every appearance in every table.
///
/// <para>
/// The map's value type is <see cref="long"/> because every surrogate PK
/// remap in scope is an Access AutoNumber (Long/Integer), and the sanitizer
/// writes the new value into a CSV column the downstream consumer will load
/// with the original type. Non-numeric remap targets fall outside the v1.0
/// surrogate-PK scope.
/// </para>
///
/// <para>
/// Determinism is achieved by assigning new values in FIRST-SEEN order
/// starting from <see cref="StartValue"/> (default 1,000,000 — high enough
/// to keep sanitized rows clearly distinguishable from any "low ID" the
/// downstream consumer might hand-author). Within one
/// <see cref="MaskingEngine"/> run the iteration order over each table's
/// rows is fixed by the <see cref="IRowSource"/>, so repeat runs on the same
/// classification + same source produce byte-identical output.
/// </para>
///
/// <para>
/// The registry is not thread-safe. The sanitizer drives it single-threaded
/// (one table at a time, one row at a time within a table).
/// </para>
/// </summary>
public sealed class RemapRegistry
{
    private readonly Dictionary<TableColumn, Dictionary<string, long>> _maps;
    private readonly Dictionary<TableColumn, long> _nextValue;

    public long StartValue { get; }

    public RemapRegistry(long startValue = 1_000_000L)
    {
        if (startValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(startValue), "Start value must be positive.");
        StartValue = startValue;
        _maps      = new Dictionary<TableColumn, Dictionary<string, long>>(TableColumn.CaseInsensitiveComparer);
        _nextValue = new Dictionary<TableColumn, long>(TableColumn.CaseInsensitiveComparer);
    }

    /// <summary>
    /// Returns the remapped value for <paramref name="originalValue"/> under
    /// the given target column. Repeated calls with the same original value
    /// return the same new value. <c>null</c> originals are passed through
    /// as <c>null</c> (the caller should not invoke this for required PKs).
    /// </summary>
    public long? Remap(TableColumn target, object? originalValue)
    {
        if (originalValue is null) return null;

        if (!_maps.TryGetValue(target, out var map))
        {
            map = new Dictionary<string, long>(StringComparer.Ordinal);
            _maps[target] = map;
            _nextValue[target] = StartValue;
        }

        string canonical = CanonicalKey(originalValue);
        if (map.TryGetValue(canonical, out long mapped)) return mapped;
        long next = _nextValue[target];
        map[canonical] = next;
        _nextValue[target] = next + 1;
        return next;
    }

    /// <summary>The current size of every registered map; useful for the sanitization report.</summary>
    public IReadOnlyDictionary<TableColumn, int> Sizes()
    {
        var sizes = new Dictionary<TableColumn, int>(TableColumn.CaseInsensitiveComparer);
        foreach (var (key, map) in _maps) sizes[key] = map.Count;
        return sizes;
    }

    private static string CanonicalKey(object value)
    {
        // Numeric originals are normalized to a culture-invariant string so
        // an int 1 and a long 1 collide as the same key — important when the
        // source reader returns the OLE DB-typed value (often boxed as
        // various integer sizes depending on the column type).
        return value switch
        {
            long   l => l.ToString(CultureInfo.InvariantCulture),
            int    i => i.ToString(CultureInfo.InvariantCulture),
            short  s => s.ToString(CultureInfo.InvariantCulture),
            byte   b => b.ToString(CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            string s => s,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}
