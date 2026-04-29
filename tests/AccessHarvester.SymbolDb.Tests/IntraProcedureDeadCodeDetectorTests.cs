using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Hermetic tests for IntraProcedureDeadCodeDetector. Drives one-procedure
// VBA fragments through the parser pipeline and asserts the detector flags
// the three Session 1 shapes the spec calls out: statements after an
// unconditional Exit, statements after an unconditional Goto, and a literal
// `If False Then` block. A negative case ensures a healthy procedure does
// not get flagged.
public sealed class IntraProcedureDeadCodeDetectorTests
{
    [Fact]
    public void HasDeadCode_StatementAfterUnconditionalExitSub_ReturnsTrue()
    {
        var procedure = ParseFirstProcedure("""
            Public Sub Foo()
                Exit Sub
                Debug.Print "unreachable"
            End Sub
            """);

        bool dead = IntraProcedureDeadCodeDetector.HasDeadCode(procedure, out var reason);
        Assert.True(dead);
        Assert.Equal("after-unconditional-exit", reason);
    }

    [Fact]
    public void HasDeadCode_StatementAfterUnconditionalGoto_ReturnsTrue()
    {
        var procedure = ParseFirstProcedure("""
            Public Sub Foo()
                GoTo Done
                Debug.Print "unreachable"
            Done:
            End Sub
            """);

        bool dead = IntraProcedureDeadCodeDetector.HasDeadCode(procedure, out var reason);
        Assert.True(dead);
        Assert.Equal("after-unconditional-goto", reason);
    }

    [Fact]
    public void HasDeadCode_IfFalseThen_ReturnsTrue()
    {
        var procedure = ParseFirstProcedure("""
            Public Sub Foo()
                If False Then
                    Debug.Print "unreachable"
                End If
            End Sub
            """);

        bool dead = IntraProcedureDeadCodeDetector.HasDeadCode(procedure, out var reason);
        Assert.True(dead);
        Assert.Equal("if-false-then-block", reason);
    }

    [Fact]
    public void HasDeadCode_LabelAfterUnconditionalExit_NotFlagged()
    {
        // A labelled section after an unconditional Exit is the canonical
        // VBA error-handler shape and must NOT be flagged dead.
        var procedure = ParseFirstProcedure("""
            Public Sub Foo()
            10        On Error GoTo Err_Handler
            20        Debug.Print "work"
            Exit_Handler:
            30        Exit Sub
            Err_Handler:
            40        Resume Exit_Handler
            End Sub
            """);

        bool dead = IntraProcedureDeadCodeDetector.HasDeadCode(procedure, out var reason);
        Assert.False(dead);
        Assert.Null(reason);
    }

    [Fact]
    public void HasDeadCode_HealthyProcedure_NotFlagged()
    {
        var procedure = ParseFirstProcedure("""
            Public Sub Foo()
                Dim x As Long
                x = 1
                Debug.Print x
            End Sub
            """);

        bool dead = IntraProcedureDeadCodeDetector.HasDeadCode(procedure, out var reason);
        Assert.False(dead);
        Assert.Null(reason);
    }

    private static ProcedureDeclaration ParseFirstProcedure(string source)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        var ast = parseResult.BuildAst("dead-code-fixture.bas");
        return (ProcedureDeclaration)ast.Items.First(i => i is ProcedureDeclaration);
    }
}
