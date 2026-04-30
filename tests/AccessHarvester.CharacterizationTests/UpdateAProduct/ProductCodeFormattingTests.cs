using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

public sealed class ProductCodeFormattingTests
{
    [Fact]
    public void FormatProductCode_ReturnsNwtPrefixWithCategoryCodeAndProductId()
    {
        var code = UpdateProductWorkflow.FormatProductCode(productId: 42, categoryCode: "BV");

        Assert.Equal("NWTBV-42", code);
    }

    [Fact]
    public void FormatProductCode_PreservesEmptyCategoryCode()
    {
        var code = UpdateProductWorkflow.FormatProductCode(productId: 7, categoryCode: string.Empty);

        Assert.Equal("NWT-7", code);
    }

    [Fact]
    public void FormatProductCode_AcceptsLargeProductIds()
    {
        var code = UpdateProductWorkflow.FormatProductCode(productId: 1234567, categoryCode: "PD");

        Assert.Equal("NWTPD-1234567", code);
    }
}
