using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// RFC 4180-conformant CSV writer. Streaming: rows are written as supplied;
/// callers own the lifecycle of the underlying <see cref="TextWriter"/>.
///
/// <para>
/// Quoting rules: any field containing a comma, quote, CR, or LF is wrapped
/// in double quotes, and embedded quotes are doubled. NULL is emitted as
/// the empty string (RFC 4180 has no NULL — downstream consumers
/// distinguish "" from a quoted empty string only when the column type is
/// nullable on import). Numeric, boolean, and date values are formatted
/// culture-invariantly; date values use ISO 8601 (<c>yyyy-MM-ddTHH:mm:ss</c>)
/// so SQL Server <c>BULK INSERT</c> + Access text-link both parse them
/// unambiguously.
/// </para>
/// </summary>
public sealed class CsvWriter : IDisposable
{
    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;

    public CsvWriter(TextWriter writer, bool ownsWriter = true)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _ownsWriter = ownsWriter;
    }

    public void WriteHeader(IReadOnlyList<string> columnNames) => WriteRow(columnNames);

    public void WriteRow(IReadOnlyList<object?> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) _writer.Write(',');
            WriteField(values[i]);
        }
        _writer.Write("\r\n");
    }

    private void WriteRow(IReadOnlyList<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) _writer.Write(',');
            WriteField(values[i]);
        }
        _writer.Write("\r\n");
    }

    private void WriteField(object? value)
    {
        if (value is null) return; // empty field
        _writer.Write(Quote(Format(value)));
    }

    internal static string Format(object value) => value switch
    {
        string s              => s,
        DateTime dt           => dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto    => dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        bool b                => b ? "true" : "false",
        decimal d             => d.ToString(CultureInfo.InvariantCulture),
        double d              => d.ToString("R", CultureInfo.InvariantCulture),
        float  f              => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    internal static string Quote(string raw)
    {
        bool needsQuotes = false;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == ',' || c == '"' || c == '\r' || c == '\n') { needsQuotes = true; break; }
        }
        if (!needsQuotes) return raw;
        var sb = new StringBuilder(raw.Length + 2);
        sb.Append('"');
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '"') sb.Append('"');
            sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    public void Dispose()
    {
        _writer.Flush();
        if (_ownsWriter) _writer.Dispose();
    }
}
