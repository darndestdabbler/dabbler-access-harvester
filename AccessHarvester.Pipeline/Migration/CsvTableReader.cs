using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>One parsed CSV table: the header column names and the data rows.</summary>
/// <remarks>
/// A field is <c>null</c> when it was an <em>unquoted</em> empty field, and the
/// empty string when it was a <em>quoted</em> empty field (<c>""</c>). This is the
/// RFC 4180-faithful read of the two forms.
/// <para>
/// <b>Producer caveat (verified, Session 2).</b> The current
/// <see cref="Sanitization.CsvWriter"/> writes <em>both</em> SQL <c>NULL</c> and a
/// genuine empty string as an <em>unquoted</em> empty field (it quotes only fields
/// containing <c>, " CR LF</c>), so a value round-tripped through the sanitize CSV
/// collapses an empty string into <c>NULL</c> on read — the migration data-load
/// therefore loads an empty field as <c>NULL</c>, which is the only honest choice
/// given the producer (it never fabricates a value). The quoted-empty → <c>""</c>
/// branch below is forward-compatible (RFC 4180-correct, and what a NULL-sentinel or
/// quote-empties producer would rely on) but is not exercised by today's writer; it
/// is pinned by a real-writer round-trip test so the collapse cannot regress silently.
/// </para>
/// </remarks>
public sealed record CsvTable(
    IReadOnlyList<string> Header,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

/// <summary>
/// Minimal RFC 4180 CSV reader, the read-side counterpart to
/// <see cref="Sanitization.CsvWriter"/>. Handles quoted fields, doubled quotes,
/// and embedded commas / CR / LF; treats both <c>\r\n</c> and a lone <c>\n</c> as
/// record terminators. An unquoted empty field decodes to <c>null</c> (SQL NULL);
/// a quoted empty field decodes to the empty string.
/// </summary>
public static class CsvTableReader
{
    /// <summary>
    /// Parses the entire content of <paramref name="reader"/>. Returns an empty
    /// header and no rows for empty input.
    /// </summary>
    public static CsvTable Read(TextReader reader)
    {
        string text = reader.ReadToEnd();
        var records = ParseRecords(text);
        if (records.Count == 0)
            return new CsvTable(System.Array.Empty<string>(), System.Array.Empty<IReadOnlyList<string?>>());

        // The header row's fields are column names; an unquoted-empty header cell
        // becomes "" (a degenerate but representable name) rather than null.
        var header = new List<string>();
        foreach (var field in records[0])
            header.Add(field ?? "");

        var rows = new List<IReadOnlyList<string?>>(records.Count - 1);
        for (int i = 1; i < records.Count; i++)
            rows.Add(records[i]);

        return new CsvTable(header, rows);
    }

    private static List<List<string?>> ParseRecords(string text)
    {
        var records = new List<List<string?>>();
        if (text.Length == 0) return records;

        var record = new List<string?>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool fieldQuoted = false;   // this field opened with a quote

        void EndField()
        {
            // Quoted field -> its text (possibly ""); unquoted empty field -> null (SQL NULL).
            record!.Add(fieldQuoted ? field!.ToString() : (field!.Length > 0 ? field.ToString() : null));
            field.Clear();
            fieldQuoted = false;
        }

        void EndRecord()
        {
            EndField();
            records!.Add(record!);
            record = new List<string?>();
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    fieldQuoted = true;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    // Treat \r\n (and a lone \r) as one record terminator.
                    EndRecord();
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    break;
                case '\n':
                    EndRecord();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        // Flush a trailing record only if there is pending content (no spurious
        // empty record after a final newline).
        if (field.Length > 0 || fieldQuoted || record.Count > 0)
            EndRecord();

        return records;
    }
}
