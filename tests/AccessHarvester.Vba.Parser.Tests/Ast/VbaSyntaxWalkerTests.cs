using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Vba.Parser.Tests.Ast;

// Validates that VbaSyntaxWalker dispatches every concrete VbaSyntaxNode
// type. Per ast-shape.md §5: the C# switch does not warn on missing
// arms, so this reflective coverage check stands in for that
// compile-time guarantee.
public class VbaSyntaxWalkerTests
{
    [Fact]
    public void Walker_HasVisitMethodForEveryConcreteAstNodeType()
    {
        var astAssembly = typeof(VbaSyntaxNode).Assembly;
        var concreteNodes = astAssembly.GetTypes()
            .Where(t => typeof(VbaSyntaxNode).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var walkerMethods = typeof(VbaSyntaxWalker)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("Visit") && m.Name != nameof(VbaSyntaxWalker.Visit) &&
                        m.GetParameters().Length == 1)
            .ToDictionary(m => m.GetParameters()[0].ParameterType, m => m);

        var missing = new List<string>();
        foreach (var node in concreteNodes)
        {
            if (!walkerMethods.ContainsKey(node))
                missing.Add(node.Name);
        }

        Assert.True(missing.Count == 0,
            "VbaSyntaxWalker is missing dispatch arms for: " + string.Join(", ", missing));
    }

    [Fact]
    public void Walker_DefaultVisit_ReachesEveryDescendant()
    {
        var src = """
            Public Sub Greet()
                Dim msg As String
                msg = "hi"
                Debug.Print msg
            End Sub
            """;
        var ast = VbaParserPipeline.Parse(src).BuildAst();

        var counter = new CountingWalker();
        counter.Visit(ast);

        Assert.True(counter.NodeCount >= 5, $"Walker visited only {counter.NodeCount} nodes — expected the Sub, its parameter list, and statements.");
    }

    private sealed class CountingWalker : VbaSyntaxWalker
    {
        public int NodeCount;
        public override void DefaultVisit(VbaSyntaxNode node)
        {
            NodeCount++;
            base.DefaultVisit(node);
        }
    }

    // Per verifier finding MAJ-2.1 (Round 1): the
    // Walker_HasVisitMethodForEveryConcreteAstNodeType test above confirms a
    // VisitX method exists for every concrete type, but does NOT confirm that
    // the central Visit dispatch switch actually routes to it. A new node type
    // could pass that test while silently falling through the default arm.
    //
    // The dispatch switch now throws InvalidOperationException on an unhandled
    // node (instead of falling through to DefaultVisit), so a missing arm
    // surfaces loudly. The two tests below confirm every concrete node type
    // in the assembly reaches a non-default arm — i.e., dispatch is exhaustive
    // for both the void Walker and the generic Visitor<T>.

    [Fact]
    public void Walker_DispatchSwitch_ReachesSpecificArmForEveryConcreteNodeType()
    {
        var astAssembly = typeof(VbaSyntaxNode).Assembly;
        var concreteNodes = astAssembly.GetTypes()
            .Where(t => typeof(VbaSyntaxNode).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var fallthroughs = new List<string>();
        foreach (var nodeType in concreteNodes)
        {
            // GetUninitializedObject yields a record with null fields. That is
            // fine — dispatch is type-driven, not field-driven. The NoOp walker
            // below overrides DefaultVisit to avoid recursing into the null
            // child collections.
            var node = (VbaSyntaxNode)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(nodeType);
            var walker = new NoOpWalker();
            try
            {
                walker.Visit(node);
            }
            catch (System.InvalidOperationException)
            {
                fallthroughs.Add(nodeType.Name);
            }
        }
        Assert.True(fallthroughs.Count == 0,
            "VbaSyntaxWalker dispatch is missing arms for: " + string.Join(", ", fallthroughs));
    }

    [Fact]
    public void Visitor_DispatchSwitch_ReachesSpecificArmForEveryConcreteNodeType()
    {
        var astAssembly = typeof(VbaSyntaxNode).Assembly;
        var concreteNodes = astAssembly.GetTypes()
            .Where(t => typeof(VbaSyntaxNode).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var fallthroughs = new List<string>();
        foreach (var nodeType in concreteNodes)
        {
            var node = (VbaSyntaxNode)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(nodeType);
            var visitor = new NoOpVisitor();
            try
            {
                visitor.Visit(node);
            }
            catch (System.InvalidOperationException)
            {
                fallthroughs.Add(nodeType.Name);
            }
        }
        Assert.True(fallthroughs.Count == 0,
            "VbaSyntaxVisitor<T> dispatch is missing arms for: " + string.Join(", ", fallthroughs));
    }

    private sealed class NoOpWalker : VbaSyntaxWalker
    {
        // Stop child traversal — uninitialized instances have null collections.
        public override void DefaultVisit(VbaSyntaxNode node) { }
    }

    private sealed class NoOpVisitor : VbaSyntaxVisitor<int>
    {
        public override int DefaultVisit(VbaSyntaxNode node) => 0;
    }
}
