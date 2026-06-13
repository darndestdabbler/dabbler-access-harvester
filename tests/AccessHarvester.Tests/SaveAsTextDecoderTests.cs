using System.Collections.Generic;
using System.Linq;
using System.Text;
using AccessHarvester.Core.Translation;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Pins the shared SaveAsText decode (set 010): BOM → strict UTF-8 → cp1252 fallback.
/// Covers both historical failure modes — cp1252 bytes mis-read as UTF-8 → U+FFFD
/// (006 F-1-1) and UTF-8-without-BOM bytes mis-read as cp1252 → mojibake (the "§"→"Â§"
/// regression the 010 golden regen surfaced).
/// </summary>
public class SaveAsTextDecoderTests
{
    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    private static byte[] A(string s) => Encoding.ASCII.GetBytes(s);

    [Fact]
    public void Utf8_without_bom_decodes_as_utf8_not_cp1252()
    {
        // The real corpus case: a section sign authored as UTF-8 (bytes C2 A7), no BOM.
        byte[] bytes = Concat(A("design.md "), new byte[] { 0xC2, 0xA7 }, A("0)."));

        string decoded = SaveAsTextDecoder.Decode(bytes);

        Assert.Equal("design.md §0).", decoded);        // §, not Â§
        Assert.DoesNotContain("Â§", decoded);      // not the mojibake
    }

    [Fact]
    public void Cp1252_high_byte_decodes_as_cp1252_not_replacement_char()
    {
        // An em-dash authored as the cp1252 byte 0x97 (not valid UTF-8): must fall
        // back to cp1252 → U+2014, never the U+FFFD the old UTF-8-default read produced.
        byte[] bytes = Concat(A("nothing "), new byte[] { 0x97 }, A(" silent"));

        string decoded = SaveAsTextDecoder.Decode(bytes);

        Assert.Equal("nothing — silent", decoded);      // em dash
        Assert.DoesNotContain("�", decoded);
    }

    [Fact]
    public void Plain_ascii_round_trips()
    {
        byte[] bytes = A("Option Explicit\r\nSub Foo()\r\nEnd Sub\r\n");
        Assert.Equal("Option Explicit\r\nSub Foo()\r\nEnd Sub\r\n", SaveAsTextDecoder.Decode(bytes));
    }

    [Fact]
    public void Utf8_bom_is_honoured_and_stripped()
    {
        byte[] bytes = Concat(new byte[] { 0xEF, 0xBB, 0xBF }, Encoding.UTF8.GetBytes("café")); // café
        string decoded = SaveAsTextDecoder.Decode(bytes);
        Assert.Equal("café", decoded);                  // BOM stripped, é preserved
    }
}
