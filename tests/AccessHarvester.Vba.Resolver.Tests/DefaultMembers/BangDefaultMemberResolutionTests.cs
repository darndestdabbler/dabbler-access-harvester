using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.DefaultMembers;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.DefaultMembers;

// F-3-4 / Phase 3.5: DecideMemberOutcome now consults DefaultMemberRegistry
// for a bang access on a resolved source-typed target whose type carries a
// PARAMETERIZED default member (`rs!Notes` ≡ `rs.Fields("Notes")`). These
// pins prove the consultation fires where the registry resolves, stays
// deferred where it does not, and — the cross-provider design consensus's
// chief risk — does NOT fabricate a symbol named after the bang key.
public sealed class BangDefaultMemberResolutionTests
{
    private static IReadOnlyList<ResolvedReference> Resolve(string source)
    {
        var parsed = VbaParserPipeline.Parse(source);
        Module ast = parsed.BuildAst("modDemo");
        var project = new ProjectScope();
        var scope = new ScopeBuildingVisitor()
            .BuildModuleScope(project, ast, "modDemo", ModuleKind.Standard);
        // Null Access registry on purpose: bang default-member resolution
        // uses the static DefaultMemberRegistry, not the Access registry.
        return new ReferenceResolver().ResolveModule(scope, ast);
    }

    private static ResolvedReference BangRef(IReadOnlyList<ResolvedReference> refs, string member)
        => refs.First(r => r.Expression is BangAccessExpression b && b.MemberName == member);

    [Fact]
    public void BangOnRecordset_ResolvesViaParameterizedDefaultMember()
    {
        var refs = Resolve(@"
Sub Demo()
    Dim rs As DAO.Recordset
    Dim v As Variant
    v = rs!Notes
End Sub");

        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(
            BangRef(refs, "Notes").Outcome);
        var dm = Assert.IsType<SyntheticDefaultMemberDeclaration>(outcome.Declaration);

        // The binding target is the DEFAULT MEMBER (Recordset.Fields),
        // returning a Field — NOT a fabricated field named "Notes".
        Assert.Equal("Fields", dm.DefaultMember.MemberName);
        Assert.Equal("Recordset", dm.DefaultMember.TypeName);
        Assert.Equal("Field", dm.DefaultMember.ReturnType);
        Assert.Equal("Notes", dm.AccessedName);
        // Declaration Name is the default member, not the bang key.
        Assert.Equal("Fields", dm.Name);
        Assert.NotEqual("Notes", dm.Name);
    }

    [Fact]
    public void BangWithArbitraryUnvalidatableName_DoesNotFabricateAFieldSymbol()
    {
        // Consensus's chief risk: never invent a resolved symbol for the
        // bang key. An arbitrary (even typo'd) name binds the INDIRECTION
        // only; the key survives solely as AccessedName provenance.
        var refs = Resolve(@"
Sub Demo()
    Dim rs As DAO.Recordset
    Dim v As Variant
    v = rs!TypoedNameThatCannotBeValidated
End Sub");

        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(
            BangRef(refs, "TypoedNameThatCannotBeValidated").Outcome);
        var dm = Assert.IsType<SyntheticDefaultMemberDeclaration>(outcome.Declaration);
        Assert.Equal("TypoedNameThatCannotBeValidated", dm.AccessedName);
        Assert.Equal("Fields", dm.Name);

        // No reference anywhere resolved to a declaration NAMED after the
        // bang key — the key is provenance, never a bound symbol.
        Assert.DoesNotContain(refs, r =>
            r.Outcome is ResolutionOutcome.Resolved res
            && res.Declaration.Name == "TypoedNameThatCannotBeValidated");
    }

    [Fact]
    public void BangOnScalarDefaultType_StaysDeferred()
    {
        // TextBox's default member is Value (NOT parameterized): the bang
        // cannot supply a key argument, so it must not resolve through the
        // default member — it stays default-member-not-yet-resolved.
        var refs = Resolve(@"
Sub Demo()
    Dim t As TextBox
    Dim v As Variant
    v = t!Foo
End Sub");

        var u = Assert.IsType<ResolutionOutcome.Unresolved>(BangRef(refs, "Foo").Outcome);
        Assert.Equal(UnresolvedReasons.DefaultMemberNotYetResolved, u.Reason);
    }

    [Fact]
    public void BangOnUnregisteredType_StaysDeferred()
    {
        // A type with no DefaultMemberRegistry row (Object) leaves the
        // bang access deferred — the registry is conservative-add.
        var refs = Resolve(@"
Sub Demo()
    Dim o As Object
    Dim v As Variant
    v = o!Foo
End Sub");

        var u = Assert.IsType<ResolutionOutcome.Unresolved>(BangRef(refs, "Foo").Outcome);
        Assert.Equal(UnresolvedReasons.DefaultMemberNotYetResolved, u.Reason);
    }

    [Fact]
    public void DotAccessOnRecordset_DoesNotApplyDefaultMember()
    {
        // The default-member fallback is gated on the bang operator. A dot
        // access is a direct named-member access; with no Access registry
        // an unknown dot member stays deferred rather than being
        // reinterpreted as a default-member application.
        var refs = Resolve(@"
Sub Demo()
    Dim rs As DAO.Recordset
    Dim v As Variant
    v = rs.SomeUnknownMember
End Sub");

        var dotRef = refs.First(r =>
            r.Expression is MemberAccessExpression m && m.MemberName == "SomeUnknownMember");
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(dotRef.Outcome);
        Assert.Equal(UnresolvedReasons.DefaultMemberNotYetResolved, u.Reason);
    }
}
