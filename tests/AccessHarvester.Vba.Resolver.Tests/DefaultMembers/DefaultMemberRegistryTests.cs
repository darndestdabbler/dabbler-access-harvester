using System.Collections.Generic;
using AccessHarvester.Vba.Resolver.DefaultMembers;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.DefaultMembers;

// Hermetic tests pinning the curated default-member registry's seeded
// coverage and lookup contract per
// docs/session-sets/resolver-default-members-and-inference/synthesis/
// session-1-default-member-registry.md §8.1.
public sealed class DefaultMemberRegistryTests
{
    [Theory]
    [InlineData("TextBox")]
    [InlineData("ComboBox")]
    [InlineData("ListBox")]
    [InlineData("CheckBox")]
    [InlineData("OptionButton")]
    [InlineData("ToggleButton")]
    [InlineData("Label")]
    [InlineData("SubForm")]
    [InlineData("SubReport")]
    [InlineData("Recordset")]
    [InlineData("Recordset2")]
    [InlineData("Field")]
    [InlineData("Field2")]
    [InlineData("Collection")]
    [InlineData("Dictionary")]
    [InlineData("Form")]
    [InlineData("Report")]
    [InlineData("Controls")]
    public void TryGet_ReturnsHit_ForCanonicalSeedTypes(string typeName)
    {
        Assert.NotNull(DefaultMemberRegistry.TryGet(typeName));
    }

    [Fact]
    public void TryGet_BareControl_NotInRegistry()
    {
        // Access.Control is a heterogeneous base — Label, CommandButton,
        // etc. do not share a `.Value`. Session 4 narrows to concrete
        // control types from form/report metadata before chaining.
        Assert.Null(DefaultMemberRegistry.TryGet("Control"));
    }

    [Theory]
    [InlineData("recordset")]
    [InlineData("RECORDSET")]
    [InlineData("Recordset")]
    [InlineData("ReCoRdSeT")]
    public void TryGet_IsCaseInsensitive(string spelling)
    {
        var hit = DefaultMemberRegistry.TryGet(spelling);
        Assert.NotNull(hit);
        Assert.Equal("Recordset", hit!.TypeName);
    }

    [Fact]
    public void TryGet_QualifiedName_MatchesByLeafSegment()
    {
        var qualified = new List<string> { "DAO", "Recordset" };
        var bare = DefaultMemberRegistry.TryGet("Recordset");
        var viaQn = DefaultMemberRegistry.TryGet(qualified);
        Assert.NotNull(viaQn);
        Assert.Equal(bare, viaQn);
    }

    [Fact]
    public void TryGet_QualifiedName_EmptyList_ReturnsNull()
    {
        Assert.Null(DefaultMemberRegistry.TryGet(new List<string>()));
    }

    [Fact]
    public void TryGet_Unknown_ReturnsNull()
    {
        Assert.Null(DefaultMemberRegistry.TryGet("ThisIsDefinitelyNotASeededType"));
    }

    [Fact]
    public void TryGet_NullTypeName_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            DefaultMemberRegistry.TryGet((string)null!));
    }

    [Fact]
    public void TryGet_NullQualifiedName_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            DefaultMemberRegistry.TryGet((IReadOnlyList<string>)null!));
    }

    [Fact]
    public void All_BackingMap_IsImmutable()
    {
        // The registry's contract is immutability. Casting `All` back
        // to a mutable Dictionary must not succeed — wrap in
        // ReadOnlyDictionary so this regression test stays meaningful.
        Assert.IsNotType<Dictionary<string, DefaultMember>>(DefaultMemberRegistry.All);
    }

    [Fact]
    public void Recordset_DefaultMember_IsParameterizedFields()
    {
        var hit = DefaultMemberRegistry.TryGet("Recordset");
        Assert.NotNull(hit);
        Assert.Equal("Fields", hit!.MemberName);
        Assert.Equal(DefaultMemberKind.Property, hit.Kind);
        Assert.Equal("Field", hit.ReturnType);
        Assert.True(hit.IsParameterized);
    }

    [Fact]
    public void Field_DefaultMember_IsValue()
    {
        var hit = DefaultMemberRegistry.TryGet("Field");
        Assert.NotNull(hit);
        Assert.Equal("Value", hit!.MemberName);
        Assert.Equal(DefaultMemberKind.Property, hit.Kind);
        Assert.Equal("Variant", hit.ReturnType);
        Assert.False(hit.IsParameterized);
    }

    [Theory]
    [InlineData("Collection")]
    [InlineData("Dictionary")]
    public void CollectionLikeTypes_DefaultMember_IsItem_Property(string typeName)
    {
        // Both Collection and Dictionary expose Item as the IDispatch
        // default member with DISPID_VALUE (PROPERTYGET in COM type
        // info). Modeled as Property so downstream consumers can pick
        // the right reader via the same dispatch path used for
        // .Value / .Fields.
        var hit = DefaultMemberRegistry.TryGet(typeName);
        Assert.NotNull(hit);
        Assert.Equal("Item", hit!.MemberName);
        Assert.Equal(DefaultMemberKind.Property, hit.Kind);
        Assert.True(hit.IsParameterized);
    }

    [Fact]
    public void TextBox_DefaultMember_IsValue_Variant_NotParameterized()
    {
        var hit = DefaultMemberRegistry.TryGet("TextBox");
        Assert.NotNull(hit);
        Assert.Equal("Value", hit!.MemberName);
        Assert.Equal(DefaultMemberKind.Property, hit.Kind);
        Assert.Equal("Variant", hit.ReturnType);
        Assert.False(hit.IsParameterized);
    }

    [Fact]
    public void SubForm_DefaultMember_IsForm_Form_NotParameterized()
    {
        var hit = DefaultMemberRegistry.TryGet("SubForm");
        Assert.NotNull(hit);
        Assert.Equal("Form", hit!.MemberName);
        Assert.Equal("Form", hit.ReturnType);
        Assert.False(hit.IsParameterized);
    }

    [Fact]
    public void All_EnumeratesEverySeed()
    {
        // Lower bound — exact count is implementation-defined and the
        // synthesis doc §4 row count drifts as seeded types come and go.
        // 15 leaves room to trim a couple of rows without rewriting the
        // assertion while still catching a wholesale-empty regression.
        Assert.True(DefaultMemberRegistry.All.Count >= 15,
            $"Expected at least 15 seeded entries, got {DefaultMemberRegistry.All.Count}.");
    }

    [Theory]
    [InlineData("Recordset")]
    [InlineData("Field")]
    [InlineData("TextBox")]
    [InlineData("Collection")]
    public void TryGet_ByName_AndByQualifiedName_Agree(string typeName)
    {
        var byName = DefaultMemberRegistry.TryGet(typeName);
        var byQn = DefaultMemberRegistry.TryGet(new List<string> { typeName });
        Assert.Equal(byName, byQn);
    }
}
