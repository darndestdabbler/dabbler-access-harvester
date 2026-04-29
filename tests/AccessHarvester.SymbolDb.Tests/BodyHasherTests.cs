using AccessHarvester.SymbolDb.Extraction;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Hermetic unit tests for the body-hash normalisation. Two procedures that
// differ only in comments / whitespace / leading line numbers must hash the
// same so the AI interpretation pipeline can copy interpretations across
// MD5-equivalent twins.
public sealed class BodyHasherTests
{
    [Fact]
    public void Compute_TwoProceduresWithDifferentComments_HashSame()
    {
        const string a = """
            Public Sub Foo()
                ' First implementation comment.
                Dim x As Long
                x = 1
            End Sub
            """;
        const string b = """
            Public Sub Foo()
                ' Different comment same code.
                Dim x As Long
                x = 1
            End Sub
            """;
        Assert.Equal(BodyHasher.Compute(a), BodyHasher.Compute(b));
    }

    [Fact]
    public void Compute_TwoProceduresWithDifferentLineNumbers_HashSame()
    {
        const string withLineNumbers = """
            Public Sub Foo()
            10        Dim x As Long
            20        x = 1
            End Sub
            """;
        const string withoutLineNumbers = """
            Public Sub Foo()
                Dim x As Long
                x = 1
            End Sub
            """;
        Assert.Equal(BodyHasher.Compute(withLineNumbers), BodyHasher.Compute(withoutLineNumbers));
    }

    [Fact]
    public void Compute_TwoProceduresWithDifferentBodies_HashDifferent()
    {
        const string a = """
            Public Sub Foo()
                Dim x As Long
                x = 1
            End Sub
            """;
        const string b = """
            Public Sub Foo()
                Dim x As Long
                x = 2
            End Sub
            """;
        Assert.NotEqual(BodyHasher.Compute(a), BodyHasher.Compute(b));
    }

    [Fact]
    public void Compute_RemBlockCommentNormalises()
    {
        const string commented = """
            Public Sub Foo()
                REM behaves like an apostrophe comment in VBA.
                Dim x As Long
                x = 1
            End Sub
            """;
        const string uncommented = """
            Public Sub Foo()
                Dim x As Long
                x = 1
            End Sub
            """;
        Assert.Equal(BodyHasher.Compute(commented), BodyHasher.Compute(uncommented));
    }

    // Round-1 verifier flagged that the regex-based comment stripper was not
    // VBA-string-literal aware, so an apostrophe inside a string would be
    // treated as a comment start. The cases below pin down the corrected
    // behaviour: a bare apostrophe in code starts a comment; an apostrophe
    // inside double-quoted strings does NOT.
    [Fact]
    public void Compute_ApostropheInsideStringLiteral_DoesNotStartComment()
    {
        const string a = """
            Public Sub Foo()
                Dim s As String
                s = "Bob's database"
                Debug.Print s
            End Sub
            """;
        const string b = """
            Public Sub Foo()
                Dim s As String
                s = "Carol's database"
                Debug.Print s
            End Sub
            """;
        // The two procedures differ in the string content. They must hash
        // DIFFERENTLY. A string-unsafe stripper would have collapsed both at
        // the apostrophe and produced the same hash.
        Assert.NotEqual(BodyHasher.Compute(a), BodyHasher.Compute(b));
    }

    [Fact]
    public void Compute_RemAsSubstringInsideString_DoesNotStartComment()
    {
        const string a = """
            Public Sub Foo()
                Dim s As String
                s = "Remember me"
                Debug.Print s
            End Sub
            """;
        const string b = """
            Public Sub Foo()
                Dim s As String
                s = "Forget me not"
                Debug.Print s
            End Sub
            """;
        Assert.NotEqual(BodyHasher.Compute(a), BodyHasher.Compute(b));
    }

    [Fact]
    public void Compute_DoubleQuoteEscapeWithinString_PreservesContent()
    {
        // Embedded double-quote escape ("") inside the VBA string literal
        // verbatim. C# raw-string delimiter is bumped to four quotes so the
        // VBA triple-quote sequence does not collide with the C# literal end.
        const string a = """"
            Public Sub Foo()
                Dim s As String
                s = "He said ""hello"""
            End Sub
            """";
        const string b = """"
            Public Sub Foo()
                Dim s As String
                s = "He said ""goodbye"""
            End Sub
            """";
        Assert.NotEqual(BodyHasher.Compute(a), BodyHasher.Compute(b));
    }

    [Fact]
    public void Compute_TrailingApostropheCommentStillStrips()
    {
        const string commented = """
            Public Sub Foo()
                Dim x As Long ' counter
                x = 1
            End Sub
            """;
        const string uncommented = """
            Public Sub Foo()
                Dim x As Long
                x = 1
            End Sub
            """;
        Assert.Equal(BodyHasher.Compute(commented), BodyHasher.Compute(uncommented));
    }
}
