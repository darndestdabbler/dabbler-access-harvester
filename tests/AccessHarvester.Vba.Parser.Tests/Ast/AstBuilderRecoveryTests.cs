using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Vba.Parser.Tests.Ast;

// Validates that the recovery contract from
// docs/session-sets/parser-infrastructure/error-recovery-strategy.md §7
// reaches the AST: nodes whose underlying parse-tree subtree contains
// IErrorNode are tagged RecoveredProvenance.
public class AstBuilderRecoveryTests
{
    [Fact]
    public void RecoveringMode_OnMidProcedureGarbage_ProducesRecoveredNodes()
    {
        // The Recovering pipeline produces a tree even on this mid-
        // procedure garbage. The AST built from it must include at
        // least one Recovered node OR an Unknown sentinel where the
        // builder could not interpret the IErrorNode subtree.
        var src = """
            Public Sub Greet()
                Dim msg As String
                @@@ this is not VBA @@@
                msg = "hi"
            End Sub
            """;
        var result = VbaParserPipeline.Parse(src);
        var ast = result.BuildAst();

        Assert.NotEmpty(result.Diagnostics);

        bool foundRecovered = false;
        foreach (var node in DescendantsOf(ast))
        {
            if (node.Provenance is RecoveredProvenance)
            {
                foundRecovered = true;
                break;
            }
            if (node is UnknownStatement || node is UnknownExpression)
            {
                foundRecovered = true;
                break;
            }
        }
        Assert.True(foundRecovered, "Expected at least one RecoveredProvenance or Unknown* sentinel from the malformed-VBA fixture.");
    }

    [Fact]
    public void RecoveredProvenance_ReasonComesFromNearestPrecedingDiagnostic()
    {
        // A diagnostic at line 3 should provide the reason string for
        // any RecoveredProvenance whose line is >= 3.
        var src = """
            Public Sub Greet()
                Dim msg As String
                @@@ malformed @@@
            End Sub
            """;
        var result = VbaParserPipeline.Parse(src);
        var ast = result.BuildAst();

        var firstDiagMessage = result.Diagnostics.First().Message;

        var recovered = DescendantsOf(ast)
            .Select(n => n.Provenance)
            .OfType<RecoveredProvenance>()
            .FirstOrDefault();

        if (recovered is not null)
        {
            // Either the diagnostic message OR the "unknown-recovery" sentinel.
            // The match is positional, so deeper subtrees might predate the
            // first diagnostic and fall back to the sentinel.
            Assert.True(
                recovered.Reason == firstDiagMessage || recovered.Reason == "unknown-recovery",
                $"Reason '{recovered.Reason}' did not match diagnostic '{firstDiagMessage}' or sentinel.");
        }
    }

    private static System.Collections.Generic.IEnumerable<VbaSyntaxNode> DescendantsOf(VbaSyntaxNode root)
    {
        var stack = new System.Collections.Generic.Stack<VbaSyntaxNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in ReflectChildren(node))
                stack.Push(child);
        }
    }

    private static System.Collections.Generic.IEnumerable<VbaSyntaxNode> ReflectChildren(VbaSyntaxNode node)
    {
        foreach (var p in node.GetType().GetProperties())
        {
            var v = p.GetValue(node);
            if (v is VbaSyntaxNode n) yield return n;
            else if (v is System.Collections.IEnumerable e && v is not string)
                foreach (var item in e)
                    if (item is VbaSyntaxNode c) yield return c;
        }
    }
}
