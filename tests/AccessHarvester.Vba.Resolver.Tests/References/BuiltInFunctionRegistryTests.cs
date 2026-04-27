using AccessHarvester.Vba.Resolver.References;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.References;

// Spec anchors: BuiltInFunctionRegistry must contain every built-in
// the parser-migration plan calls out by name (Format, IIf, Nz) plus
// the canonical seed list documented in the registry source.
public sealed class BuiltInFunctionRegistryTests
{
    [Fact]
    public void TryGet_ReturnsHit_ForCanonicalPlanCalledOutNames()
    {
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("Format"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("IIf"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("Nz"));
    }

    [Theory]
    [InlineData("FORMAT")]
    [InlineData("format")]
    [InlineData("Format")]
    [InlineData("FoRmAt")]
    public void TryGet_IsCaseInsensitive(string spelling)
    {
        var hit = BuiltInFunctionRegistry.TryGet(spelling);
        Assert.NotNull(hit);
        Assert.Equal("Format", hit!.Name);
    }

    [Fact]
    public void TryGet_ReturnsNull_ForUnknownName()
    {
        Assert.Null(BuiltInFunctionRegistry.TryGet("ThisIsDefinitelyNotABuiltIn"));
    }

    [Fact]
    public void Format_HasStringReturnType()
    {
        var hit = BuiltInFunctionRegistry.TryGet("Format");
        Assert.NotNull(hit);
        Assert.Equal(BuiltInCategory.String, hit!.Category);
        Assert.Equal("String", hit.ReturnType);
    }

    [Fact]
    public void IIf_HasNullReturnType_ToSignalPolymorphic()
    {
        var hit = BuiltInFunctionRegistry.TryGet("IIf");
        Assert.NotNull(hit);
        Assert.Null(hit!.ReturnType);
    }

    [Fact]
    public void AccessSpecific_BareNamesArePresent()
    {
        // Without these, fixtures like 08-unresolvable would emit
        // Unresolved("not-in-scope") for canonical Access surface.
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("DoCmd"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("CurrentDb"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("DLookup"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("Forms"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("Reports"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("Application"));
        Assert.NotNull(BuiltInFunctionRegistry.TryGet("Eval"));
    }

    [Fact]
    public void AllPropertyEnumeratesAllEntries()
    {
        var all = BuiltInFunctionRegistry.All;
        // Lower bound — exact count is implementation-defined, but we
        // expect at least the sum of String + Math + DateTime + ...
        // categories. The canonical plan calls out 3 names; the
        // curated list expands these substantially.
        Assert.True(all.Count >= 50,
            $"Expected at least 50 built-ins, got {all.Count}.");
    }
}
