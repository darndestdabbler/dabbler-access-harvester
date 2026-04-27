using System;
using System.Linq;
using AccessHarvester.Vba.Grammar.Generated;
using AccessHarvester.Vba.Parser;
using Antlr4.Runtime.Tree;
using Xunit;

namespace AccessHarvester.Vba.Parser.Tests;

// Validates the contract laid out in
// docs/session-sets/parser-infrastructure/error-recovery-strategy.md §8.
// Strict mode is the existing harness behavior, exposed for completeness;
// Recovering and Lossy modes are the new surface this session lands.
public class VbaParserPipelineTests
{
    private const string CleanModule = """
        Option Explicit

        Public Sub Greet()
            Dim msg As String
            msg = "Hello"
            Debug.Print msg
        End Sub
        """;

    // A clean module with one mid-procedure garbage line. The injected
    // line is line 5 (1-based); the diagnostic should report the same.
    private const string MidProcedureGarbage = """
        Option Explicit

        Public Sub Greet()
            Dim msg As String
            @@@ this is not VBA @@@
            msg = "Hello"
            Debug.Print msg
        End Sub
        """;

    // Missing End Sub before the next procedure header. Asserts that the
    // statement-boundary sync (§4 of the strategy doc) anchors recovery
    // on the next procedure-header keyword instead of treating it as a
    // continuation of the open body.
    private const string MissingEndSub = """
        Public Sub First()
            Dim a As Long
            a = 1

        Public Sub Second()
            Dim b As Long
            b = 2
        End Sub
        """;

    // Same shape as MissingEndSub but the next procedure leads with
    // `Friend` / `Static` modifiers — confirms the sync set covers
    // every legal procedure-header lead token (issue raised by Round 1
    // verifier: PUBLIC/PRIVATE alone leave FRIEND/STATIC unprotected).
    private const string MissingEndSub_FriendNext = """
        Public Sub First()
            Dim a As Long
            a = 1

        Friend Sub Second()
            Dim b As Long
            b = 2
        End Sub
        """;

    // Note: a `Static Sub Second()` regression case is intentionally NOT
    // included. STATIC is ambiguous between a module-level header lead
    // (`procedureModifier? STATIC? SUB`) and a procedure-local
    // declaration head (`localDeclStatement : DIM | STATIC ...`). Adding
    // STATIC to the sync set anchors recovery on it but does not exit
    // the open body context, so the bare-Static next procedure is still
    // absorbed as a local Static declaration. Documented limitation;
    // resolving it requires context-aware recovery beyond the scope of
    // Session 1's lean error-recovery strategy.

    // Lexer-only fixture: the `~` character lacks a dedicated grammar
    // rule and lexes as ERROR_TOKEN via the catch-all. The pipeline's
    // ScanForLexerErrors phase must surface this as VBA0001 at line 3.
    private const string LexerError = """
        Public Sub Demo()
            Dim x As Long
            x = ~
        End Sub
        """;

    [Fact]
    public void Strict_OnCleanInput_ReturnsTreeWithNoDiagnostics()
    {
        var result = VbaParserPipeline.Parse(
            CleanModule,
            new ParseOptions(RequestedMode: ParseMode.Strict));

        Assert.Equal(ParseMode.Strict, result.ModeUsed);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.ParseTree);
    }

    [Fact]
    public void Strict_OnMalformedInput_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            VbaParserPipeline.Parse(
                MidProcedureGarbage,
                new ParseOptions(RequestedMode: ParseMode.Strict)));
    }

    [Fact]
    public void Strict_SourcePathPropagatesToExceptionMessage()
    {
        // Strict mode pairs ThrowingErrorListener with BailErrorStrategy.
        // BailErrorStrategy may wrap the listener's exception in a
        // ParseCancellationException, so walk the chain.
        var ex = Assert.ThrowsAny<Exception>(() =>
            VbaParserPipeline.Parse(
                MidProcedureGarbage,
                new ParseOptions(RequestedMode: ParseMode.Strict, SourcePath: "Foo.bas")));

        bool foundSource = false;
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur.Message.Contains("[Foo.bas]"))
            {
                foundSource = true;
                break;
            }
        }
        Assert.True(foundSource, $"Expected '[Foo.bas]' in exception chain. Top: {ex.Message}");
    }

    [Fact]
    public void Recovering_OnCleanInput_ReturnsTreeWithNoDiagnostics()
    {
        var result = VbaParserPipeline.Parse(CleanModule);

        Assert.Equal(ParseMode.Recovering, result.ModeUsed);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Recovering_OnMidProcedureGarbage_ReturnsBoundedDiagnosticSet()
    {
        var result = VbaParserPipeline.Parse(MidProcedureGarbage);

        Assert.Equal(ParseMode.Recovering, result.ModeUsed);
        Assert.NotEmpty(result.Diagnostics);

        // Bound the noise: this fixture has exactly one garbage region
        // (line 5). Recovery may emit up to 5 diagnostics for `@@@ ... @@@`
        // (each `@` is an ERROR_TOKEN plus the parser-level mismatches),
        // but should not cascade through the rest of the module. A
        // regression that breaks sync-token anchoring would surface as
        // a much larger count.
        Assert.True(
            result.Diagnostics.Count <= 10,
            $"Expected <=10 diagnostics on a single-region garbage fixture, got {result.Diagnostics.Count}.");

        // The primary diagnostic must point at line 5 (the garbage line)
        // and carry one of the parser- or lexer-error codes.
        Assert.Contains(result.Diagnostics, d => d.Line == 5);
        Assert.All(result.Diagnostics, d =>
            Assert.True(
                d.Code is "VBA0001" or "VBA0002" or "VBA0003" or "VBA0004",
                $"Unexpected diagnostic code '{d.Code}'."));
    }

    [Fact]
    public void Recovering_SourcePathPropagatesToDiagnosticMessages()
    {
        var result = VbaParserPipeline.Parse(
            MidProcedureGarbage,
            new ParseOptions(SourcePath: "Forms/Garbage.bas"));

        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, d =>
            Assert.StartsWith("[Forms/Garbage.bas] ", d.Message));
    }

    [Theory]
    [InlineData(nameof(MissingEndSub))]
    [InlineData(nameof(MissingEndSub_FriendNext))]
    public void Recovering_OnMissingEndSub_KeepsNextProcedureAsOwnTopLevelDeclaration(
        string fixtureName)
    {
        string source = fixtureName switch
        {
            nameof(MissingEndSub) => MissingEndSub,
            nameof(MissingEndSub_FriendNext) => MissingEndSub_FriendNext,
            _ => throw new ArgumentOutOfRangeException(nameof(fixtureName)),
        };

        var result = VbaParserPipeline.Parse(source);

        Assert.Equal(ParseMode.Recovering, result.ModeUsed);
        Assert.NotEmpty(result.Diagnostics);

        // Structural assertion: walk the parse tree's top-level moduleItem
        // children and confirm that BOTH `First` and `Second` surface as
        // their own procedureDeclaration contexts. A regression that
        // absorbed `Second` into `First`'s body would leave only one
        // procedureDeclaration in the moduleItem list.
        var procedureNames = CollectTopLevelProcedureNames(result.ParseTree);

        Assert.Contains("First", procedureNames);
        Assert.Contains("Second", procedureNames);
    }

    [Fact]
    public void Recovering_AutoPromotesToLossy_WhenDiagnosticsExceedThreshold()
    {
        // The threshold-comparison contract: when diagnostic count exceeds
        // LossyFallbackErrorThreshold, the result is labeled Lossy and a
        // VBA0050 diagnostic is appended. Threshold=0 means *any* error
        // triggers fallback, which is the cleanest way to assert the
        // comparison logic without depending on ANTLR's per-fixture
        // diagnostic count (which varies with how aggressively
        // recovery sync absorbs the error).
        var result = VbaParserPipeline.Parse(
            MidProcedureGarbage,
            new ParseOptions(LossyFallbackErrorThreshold: 0));

        Assert.Equal(ParseMode.Lossy, result.ModeUsed);
        Assert.Contains(result.Diagnostics, d => d.Code == "VBA0050");
    }

    [Fact]
    public void Recovering_OnUnrecognizedCharacter_ReportsVBA0001()
    {
        var result = VbaParserPipeline.Parse(LexerError);

        Assert.Equal(ParseMode.Recovering, result.ModeUsed);
        Assert.Contains(result.Diagnostics, d => d.Code == "VBA0001" && d.Line == 3);
    }

    [Fact]
    public void Lossy_RequestedExplicitly_AlwaysReturnsLossyMode()
    {
        var result = VbaParserPipeline.Parse(
            CleanModule,
            new ParseOptions(RequestedMode: ParseMode.Lossy));

        Assert.Equal(ParseMode.Lossy, result.ModeUsed);
        Assert.Contains(result.Diagnostics, d => d.Code == "VBA0050");
    }

    // Walks the immediate moduleItem children of `module` and pulls out
    // the procedure name token from each ProcedureItem (Sub/Function/
    // Property*). Returns names in source order. Only top-level
    // procedure declarations are collected — nested context lookup
    // would defeat the whole point of the regression check.
    private static System.Collections.Generic.List<string> CollectTopLevelProcedureNames(
        VbaParser.ModuleContext module)
    {
        var names = new System.Collections.Generic.List<string>();

        foreach (var moduleItem in module.moduleItem())
        {
            if (moduleItem is not VbaParser.ProcedureItemContext procItem)
                continue;

            // procedureDeclaration -> {sub|function|property*}Declaration
            // Each accessor variant has the same shape: header tokens
            // followed by IDENTIFIER. Pull the first IDENTIFIER terminal
            // beneath the proc-decl context.
            var procDecl = procItem.procedureDeclaration();
            if (procDecl is null) continue;

            string? name = FindFirstIdentifierUnder(procDecl);
            if (name is not null) names.Add(name);
        }

        return names;
    }

    private static string? FindFirstIdentifierUnder(IParseTree node)
    {
        if (node is ITerminalNode terminal && terminal.Symbol.Type == VbaLexer.IDENTIFIER)
            return terminal.Symbol.Text;

        for (int i = 0; i < node.ChildCount; i++)
        {
            string? found = FindFirstIdentifierUnder(node.GetChild(i));
            if (found is not null) return found;
        }
        return null;
    }
}
