using AccessHarvester.Vba.Resolver.AccessIntegration;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.AccessIntegration;

public sealed class AccessControlClassMapTests
{
    [Theory]
    [InlineData("TextBox", "Access.TextBox")]
    [InlineData("ComboBox", "Access.ComboBox")]
    [InlineData("ListBox", "Access.ListBox")]
    [InlineData("CommandButton", "Access.CommandButton")]
    [InlineData("Label", "Access.Label")]
    [InlineData("Subform", "Access.SubForm")]
    [InlineData("Subreport", "Access.SubReport")]
    public void Resolve_KnownClasses_ReturnsMappedTypeName(string klass, string expected)
    {
        Assert.Equal(expected, AccessControlClassMap.Resolve(klass));
    }

    [Theory]
    [InlineData("textbox")]
    [InlineData("TEXTBOX")]
    [InlineData("TextbOX")]
    public void Resolve_IsCaseInsensitive(string klass)
    {
        Assert.Equal("Access.TextBox", AccessControlClassMap.Resolve(klass));
    }

    [Theory]
    [InlineData("Rectangle")]
    [InlineData("Image")]
    [InlineData("Tab")]
    [InlineData("Page")]
    [InlineData("OptionGroup")]
    [InlineData("CompletelyUnknown")]
    public void Resolve_UnknownClass_FallsBackToAccessControl(string klass)
    {
        Assert.Equal(AccessControlClassMap.Fallback, AccessControlClassMap.Resolve(klass));
        Assert.Equal("Access.Control", AccessControlClassMap.Resolve(klass));
    }

    [Fact]
    public void Resolve_NullOrEmpty_ReturnsFallback()
    {
        Assert.Equal(AccessControlClassMap.Fallback, AccessControlClassMap.Resolve(""));
        Assert.Equal(AccessControlClassMap.Fallback, AccessControlClassMap.Resolve(null!));
    }

    [Fact]
    public void IsMapped_DistinguishesExplicitFromFallback()
    {
        Assert.True(AccessControlClassMap.IsMapped("TextBox"));
        Assert.True(AccessControlClassMap.IsMapped("textbox"));
        Assert.False(AccessControlClassMap.IsMapped("Rectangle"));
        Assert.False(AccessControlClassMap.IsMapped(""));
    }
}
