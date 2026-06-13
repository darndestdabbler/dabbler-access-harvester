using System.IO;
using AccessHarvester.Pipeline.Migration;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// Hermetic tests for the data-load primitives: the RFC 4180
/// <see cref="CsvTableReader"/> (round-trips the <c>sanitize</c> CSV shape,
/// including the unquoted-empty = NULL vs quoted-empty = "" distinction) and the
/// type-driven <see cref="SqlValueFormatter"/>.
/// </summary>
public class CsvTableReaderAndValueTests
{
    private static CsvTable Read(string text) => CsvTableReader.Read(new StringReader(text));

    // ── CSV reader ────────────────────────────────────────────────────────────

    [Fact]
    public void Reads_header_and_rows()
    {
        var csv = Read("Id,Name\r\n1,Alice\r\n2,Bob\r\n");

        Assert.Equal(new[] { "Id", "Name" }, csv.Header);
        Assert.Equal(2, csv.Rows.Count);
        Assert.Equal(new string?[] { "1", "Alice" }, csv.Rows[0]);
        Assert.Equal(new string?[] { "2", "Bob" }, csv.Rows[1]);
    }

    [Fact]
    public void Distinguishes_unquoted_empty_null_from_quoted_empty_string()
    {
        var csv = Read("A,B\r\n,\"\"\r\n");

        Assert.Single(csv.Rows);
        Assert.Null(csv.Rows[0][0]);          // unquoted empty -> NULL
        Assert.Equal("", csv.Rows[0][1]);     // quoted empty -> empty string
    }

    [Fact]
    public void Handles_quoted_commas_and_doubled_quotes()
    {
        var csv = Read("A,B\r\n\"a,b\",\"She said \"\"hi\"\"\"\r\n");

        Assert.Equal("a,b", csv.Rows[0][0]);
        Assert.Equal("She said \"hi\"", csv.Rows[0][1]);
    }

    [Fact]
    public void Handles_embedded_newline_inside_quotes()
    {
        var csv = Read("A\r\n\"line1\r\nline2\"\r\n");

        Assert.Single(csv.Rows);
        Assert.Equal("line1\r\nline2", csv.Rows[0][0]);
    }

    [Fact]
    public void Handles_lf_only_line_endings()
    {
        var csv = Read("A,B\n1,2\n");

        Assert.Equal(new[] { "A", "B" }, csv.Header);
        Assert.Equal(new string?[] { "1", "2" }, csv.Rows[0]);
    }

    [Fact]
    public void Header_only_yields_no_rows()
    {
        var csv = Read("A,B,C\r\n");

        Assert.Equal(3, csv.Header.Count);
        Assert.Empty(csv.Rows);
    }

    [Fact]
    public void Empty_input_yields_empty_table()
    {
        var csv = Read("");

        Assert.Empty(csv.Header);
        Assert.Empty(csv.Rows);
    }

    [Fact]
    public void Trailing_comma_yields_a_trailing_null_field()
    {
        var csv = Read("A,B\r\nx,\r\n");

        Assert.Equal("x", csv.Rows[0][0]);
        Assert.Null(csv.Rows[0][1]);
    }

    [Fact]
    public void Real_csvwriter_roundtrip_collapses_null_and_empty_string_to_null()
    {
        // Pins the cross-component reality the reader docs describe: the production
        // CsvWriter emits BOTH SQL NULL and a genuine empty string as an unquoted
        // empty field, so a round-trip through the sanitize CSV reads both back as
        // NULL. This guards against the contract silently changing under us.
        var sw = new StringWriter();
        using (var w = new CsvWriter(sw, ownsWriter: false))
        {
            w.WriteHeader(new[] { "A", "B" });
            w.WriteRow(new object?[] { null, "" }); // SQL NULL, then a genuine empty string
        }

        var csv = Read(sw.ToString());

        Assert.Single(csv.Rows);
        Assert.Null(csv.Rows[0][0]); // NULL -> empty field -> NULL
        Assert.Null(csv.Rows[0][1]); // "" also -> empty field -> NULL (the documented collapse)
    }

    // ── Value formatter (Facts, to keep theory pre-enumeration out of discovery) ─

    [Fact]
    public void Formats_character_types_with_n_prefix_and_quote_escaping()
    {
        Assert.Equal("N'hello'", SqlValueFormatter.Format(10, "hello"));    // Text
        Assert.Equal("N'a memo'", SqlValueFormatter.Format(12, "a memo"));  // Memo
        Assert.Equal("N'fixed'", SqlValueFormatter.Format(18, "fixed"));    // Char
        Assert.Equal("N'O''Brien'", SqlValueFormatter.Format(10, "O'Brien")); // escaping
    }

    [Fact]
    public void Formats_bit_datetime_and_guid()
    {
        Assert.Equal("1", SqlValueFormatter.Format(1, "true"));
        Assert.Equal("0", SqlValueFormatter.Format(1, "false"));
        Assert.Equal("'2020-01-02T13:45:00'", SqlValueFormatter.Format(8, "2020-01-02T13:45:00"));
        Assert.Equal("'6F9619FF-8B86-D011-B42D-00C04FC964FF'",
            SqlValueFormatter.Format(15, "6F9619FF-8B86-D011-B42D-00C04FC964FF"));
    }

    [Fact]
    public void Formats_numeric_families_as_bare_tokens()
    {
        Assert.Equal("123", SqlValueFormatter.Format(4, "123"));   // Long
        Assert.Equal("1.5", SqlValueFormatter.Format(7, "1.5"));   // Double
        Assert.Equal("9.99", SqlValueFormatter.Format(5, "9.99")); // Currency
    }

    [Fact]
    public void Null_field_is_always_NULL_regardless_of_type()
    {
        Assert.Equal("NULL", SqlValueFormatter.Format(10, null));
        Assert.Equal("NULL", SqlValueFormatter.Format(4, null));
        Assert.Equal("NULL", SqlValueFormatter.Format(8, null));
    }

    [Fact]
    public void IsBinary_flags_only_binary_families()
    {
        Assert.True(SqlValueFormatter.IsBinary(9));    // binary
        Assert.True(SqlValueFormatter.IsBinary(11));   // OLE Object / long binary
        Assert.True(SqlValueFormatter.IsBinary(17));   // varbinary
        Assert.False(SqlValueFormatter.IsBinary(10));  // text
        Assert.False(SqlValueFormatter.IsBinary(4));   // long
    }

    // ── F-2-7: numeric/bit tokens are validated, not interpolated bare ───────────

    [Theory]
    [InlineData(4, "123")]            // Long, plain integer
    [InlineData(7, "-1.5")]           // Double, signed fraction
    [InlineData(5, "9.99")]           // Currency
    [InlineData(7, "1.2e10")]         // Double, exponent
    [InlineData(4, "+7")]             // signed
    [InlineData(7, ".5")]             // leading-dot fraction (010 review) — valid T-SQL
    [InlineData(7, "1.")]             // trailing-dot (010 review) — valid T-SQL
    [InlineData(7, "-.25")]           // signed leading-dot
    public void Valid_numeric_tokens_pass_through_bare(int typeCode, string raw)
    {
        Assert.Equal(raw, SqlValueFormatter.Format(typeCode, raw));
        Assert.False(SqlValueFormatter.IsInvalidDataToken(typeCode, raw));
    }

    [Theory]
    [InlineData(7, "1D2")]            // VB D-exponent is NOT valid T-SQL → still rejected
    [InlineData(7, ".")]             // lone dot
    [InlineData(7, "1.2.3")]          // multiple dots
    public void Still_rejects_non_tsql_numeric_forms(int typeCode, string raw)
    {
        Assert.Equal("NULL", SqlValueFormatter.Format(typeCode, raw));
        Assert.True(SqlValueFormatter.IsInvalidDataToken(typeCode, raw));
    }

    [Theory]
    [InlineData(4, "12; DROP TABLE x")]   // injection attempt
    [InlineData(7, "abc")]                // non-numeric
    [InlineData(5, "1,234")]              // thousands separator (not invariant)
    [InlineData(4, "0x1F")]               // hex literal
    public void Invalid_numeric_tokens_degrade_to_NULL(int typeCode, string raw)
    {
        // Rather than interpolate a raw/arbitrary token into the INSERT (malformed
        // or injected SQL), the formatter emits NULL and the generator records an
        // InvalidDataToken degradation (F-2-7).
        Assert.Equal("NULL", SqlValueFormatter.Format(typeCode, raw));
        Assert.True(SqlValueFormatter.IsInvalidDataToken(typeCode, raw));
    }

    [Fact]
    public void Bit_tokens_outside_the_known_set_degrade_to_NULL()
    {
        Assert.Equal("1", SqlValueFormatter.Format(1, "1"));
        Assert.Equal("0", SqlValueFormatter.Format(1, "False"));
        Assert.Equal("NULL", SqlValueFormatter.Format(1, "maybe"));
        Assert.True(SqlValueFormatter.IsInvalidDataToken(1, "maybe"));
        Assert.False(SqlValueFormatter.IsInvalidDataToken(1, "true"));
    }

    [Fact]
    public void Quoted_families_and_null_are_never_invalid_tokens()
    {
        // Text/date/GUID are quoted-and-escaped, so they are never "invalid" in the
        // injection sense; a null cell is a clean NULL, not a degradation.
        Assert.False(SqlValueFormatter.IsInvalidDataToken(10, "anything '; --"));
        Assert.False(SqlValueFormatter.IsInvalidDataToken(8, "not-a-date"));
        Assert.False(SqlValueFormatter.IsInvalidDataToken(4, null));
    }

    [Theory]
    [InlineData(4, "")]      // Long, empty cell (quoted-empty CSV)
    [InlineData(7, "   ")]   // Double, whitespace
    [InlineData(1, "")]      // Bit, empty
    public void Blank_or_whitespace_numeric_bit_cells_are_clean_NULL_not_invalid(int typeCode, string raw)
    {
        // An empty/whitespace numeric or bit cell is a missing value → clean NULL, not
        // an "invalid token" — it must NOT raise a degradation (verifier R1 fix).
        Assert.Equal("NULL", SqlValueFormatter.Format(typeCode, raw));
        Assert.False(SqlValueFormatter.IsInvalidDataToken(typeCode, raw));
    }

    [Theory]
    [InlineData(9, "DEADBEEF")]   // binary
    [InlineData(11, "0102FF")]    // OLE / long binary
    [InlineData(17, "AB")]        // varbinary
    public void Binary_tokens_are_not_invalid_tokens(int typeCode, string raw)
    {
        // Binary/OLE columns are omitted from the INSERT upstream (the generator skips
        // them before the row loop), so IsInvalidDataToken must never flag a binary
        // payload — its hex digits would otherwise miss the numeric pattern (verifier R2).
        Assert.False(SqlValueFormatter.IsInvalidDataToken(typeCode, raw));
    }
}
