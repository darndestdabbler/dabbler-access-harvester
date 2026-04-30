using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

public sealed class ProductSaveValidationTests
{
    [Fact]
    public void CanSave_AcceptsAFullyPopulatedProduct()
    {
        var product = new ProductDraft
        {
            ProductId = 17,
            ProductCode = "NWTBV-17",
            ProductName = "Northwind Traders Cocoa",
            StandardUnitCost = 5.25m,
            UnitPrice = 12.49m,
            ProductCategoryId = 4,
            Discontinued = false,
        };

        Assert.True(UpdateProductWorkflow.CanSave(product));
    }

    [Theory]
    [InlineData("ProductCode")]
    [InlineData("ProductName")]
    [InlineData("UnitPrice")]
    [InlineData("ProductCategoryId")]
    [InlineData("Discontinued")]
    public void CanSave_RejectsWhenAnyRequiredFieldIsMissing(string omittedField)
    {
        var product = BaseProduct();
        Clear(product, omittedField);

        Assert.False(UpdateProductWorkflow.CanSave(product));
    }

    [Fact]
    public void CanSave_RejectsWhenStandardUnitCostIsZero()
    {
        var product = BaseProduct();
        product.StandardUnitCost = 0m;

        Assert.False(UpdateProductWorkflow.CanSave(product));
    }

    [Fact]
    public void CanSave_RejectsWhenStandardUnitCostIsNegative()
    {
        var product = BaseProduct();
        product.StandardUnitCost = -0.01m;

        Assert.False(UpdateProductWorkflow.CanSave(product));
    }

    [Fact]
    public void CanSave_RejectsWhenStandardUnitCostIsMissing()
    {
        var product = BaseProduct();
        product.StandardUnitCost = null;

        Assert.False(UpdateProductWorkflow.CanSave(product));
    }

    [Fact]
    public void CanSave_RejectsWhenProductNameIsBlank()
    {
        var product = BaseProduct();
        product.ProductName = "   ";

        Assert.False(UpdateProductWorkflow.CanSave(product));
    }

    private static ProductDraft BaseProduct()
    {
        return new ProductDraft
        {
            ProductId = 17,
            ProductCode = "NWTBV-17",
            ProductName = "Northwind Traders Cocoa",
            StandardUnitCost = 5.25m,
            UnitPrice = 12.49m,
            ProductCategoryId = 4,
            Discontinued = false,
        };
    }

    private static void Clear(ProductDraft product, string field)
    {
        switch (field)
        {
            case "ProductCode":
                product.ProductCode = null;
                break;
            case "ProductName":
                product.ProductName = null;
                break;
            case "UnitPrice":
                product.UnitPrice = null;
                break;
            case "ProductCategoryId":
                product.ProductCategoryId = null;
                break;
            case "Discontinued":
                product.Discontinued = null;
                break;
            default:
                throw new System.ArgumentException(field, nameof(field));
        }
    }
}
