using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

public sealed class ProductDeletePolicyTests
{
    [Fact]
    public void CanDeleteProduct_AllowsDeleteOnlyWhenNoOrderOrPurchaseOrderReferencesExist()
    {
        Assert.True(UpdateProductWorkflow.CanDeleteProduct(0, 0));
        Assert.False(UpdateProductWorkflow.CanDeleteProduct(1, 0));
        Assert.False(UpdateProductWorkflow.CanDeleteProduct(0, 1));
        Assert.False(UpdateProductWorkflow.CanDeleteProduct(3, 4));
    }

    [Fact]
    public void DeleteProductCascade_ReturnsStockTakeThenProductVendorsThenProducts_WhenAllowedAndConfirmed()
    {
        var statements = UpdateProductWorkflow.DeleteProductCascade(
            productId: 17,
            confirmed: true,
            orderDetailReferenceCount: 0,
            purchaseOrderDetailReferenceCount: 0);

        Assert.Equal(new[]
        {
            "DELETE FROM StockTake WHERE ProductID = 17",
            "DELETE FROM ProductVendors WHERE ProductID = 17",
            "DELETE FROM Products WHERE ProductID = 17",
        }, statements);
    }

    [Fact]
    public void DeleteProductCascade_DoesNothingWhenNotConfirmed()
    {
        var statements = UpdateProductWorkflow.DeleteProductCascade(
            productId: 17,
            confirmed: false,
            orderDetailReferenceCount: 0,
            purchaseOrderDetailReferenceCount: 0);

        Assert.Empty(statements);
    }

    [Fact]
    public void DeleteProductCascade_DoesNothingWhenAnyReferenceCountIsNonZero()
    {
        var withOrderHistory = UpdateProductWorkflow.DeleteProductCascade(
            productId: 17,
            confirmed: true,
            orderDetailReferenceCount: 2,
            purchaseOrderDetailReferenceCount: 0);
        Assert.Empty(withOrderHistory);

        var withPurchaseHistory = UpdateProductWorkflow.DeleteProductCascade(
            productId: 17,
            confirmed: true,
            orderDetailReferenceCount: 0,
            purchaseOrderDetailReferenceCount: 1);
        Assert.Empty(withPurchaseHistory);
    }
}
