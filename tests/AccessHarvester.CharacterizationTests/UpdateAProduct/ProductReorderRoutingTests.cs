using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

public sealed class ProductReorderRoutingTests
{
    [Fact]
    public void ExecuteReorder_AppendsToExistingNewPurchaseOrderForVendor_WhenOnePresent()
    {
        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = StandardProduct(),
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 8,
            AvailableVendorIds = new long[] { 99 },
            ExistingNewPurchaseOrderForVendorId = 7777,
        });

        Assert.Equal(ReorderOutcomeKind.AppendedToExistingNewPurchaseOrder, outcome.Kind);
        Assert.Equal(7777, outcome.PurchaseOrderId);
        Assert.Equal(8, outcome.Quantity);
        Assert.Equal(99, outcome.VendorId);
    }

    [Fact]
    public void ExecuteReorder_CreatesNewPurchaseOrder_WhenNoExistingNewPoForVendor()
    {
        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = StandardProduct(),
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 12,
            AvailableVendorIds = new long[] { 11 },
        });

        Assert.Equal(ReorderOutcomeKind.CreatedNewPurchaseOrder, outcome.Kind);
        Assert.Null(outcome.PurchaseOrderId);
        Assert.Equal(12, outcome.Quantity);
        Assert.Equal(11, outcome.VendorId);
    }

    [Fact]
    public void ExecuteReorder_PrefersUserSelectedVendor_WhenMultipleVendorsAvailable()
    {
        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = StandardProduct(),
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 6,
            AvailableVendorIds = new long[] { 11, 22, 33 },
            UserSelectedVendorId = 22,
        });

        Assert.Equal(ReorderOutcomeKind.CreatedNewPurchaseOrder, outcome.Kind);
        Assert.Equal(22, outcome.VendorId);
    }

    [Fact]
    public void ExecuteReorder_FallsBackToMinimumReorderQuantity_WhenCalculatedIsZero()
    {
        var product = StandardProduct();
        product.MinimumReorderQuantity = 24;

        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = product,
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 0,
            AvailableVendorIds = new long[] { 11 },
        });

        Assert.Equal(ReorderOutcomeKind.CreatedNewPurchaseOrder, outcome.Kind);
        Assert.Equal(24, outcome.Quantity);
    }

    [Fact]
    public void ExecuteReorder_FallsBackToUserEnteredQuantity_WhenCalculatedAndMinimumAreBothZero()
    {
        var product = StandardProduct();
        product.MinimumReorderQuantity = 0;

        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = product,
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 0,
            AvailableVendorIds = new long[] { 11 },
            UserEnteredQuantity = 7,
        });

        Assert.Equal(ReorderOutcomeKind.CreatedNewPurchaseOrder, outcome.Kind);
        Assert.Equal(7, outcome.Quantity);
    }

    [Fact]
    public void AddPurchaseOrderDetail_AddsNewLineItemWhenNoMatchingProductExists()
    {
        var lines = new List<PurchaseOrderLineItem>
        {
            new() { PurchaseOrderId = 1, ProductId = 50, Quantity = 4, UnitCost = 1m },
        };

        var updated = UpdateProductWorkflow.AddPurchaseOrderDetail(
            lines,
            purchaseOrderId: 1,
            productId: 99,
            quantityToAdd: 6,
            unitCost: 2.5m);

        Assert.Equal(2, updated.Count);
        var added = updated.Single(line => line.ProductId == 99);
        Assert.Equal(1, added.PurchaseOrderId);
        Assert.Equal(6, added.Quantity);
        Assert.Equal(2.5m, added.UnitCost);
    }

    [Fact]
    public void AddPurchaseOrderDetail_SumsQuantityIntoExistingLineWhenProductAlreadyPresent()
    {
        var lines = new List<PurchaseOrderLineItem>
        {
            new() { PurchaseOrderId = 1, ProductId = 99, Quantity = 4, UnitCost = 2.5m },
        };

        var updated = UpdateProductWorkflow.AddPurchaseOrderDetail(
            lines,
            purchaseOrderId: 1,
            productId: 99,
            quantityToAdd: 6,
            unitCost: 9.99m);

        var only = Assert.Single(updated);
        Assert.Equal(99, only.ProductId);
        Assert.Equal(10, only.Quantity);

        // The cost on the existing line is intentionally not overwritten by
        // AddPurchaseOrderDetail; the Edit branch only updates Quantity.
        Assert.Equal(2.5m, only.UnitCost);
    }

    private static ProductDraft StandardProduct()
    {
        return new ProductDraft
        {
            ProductId = 17,
            ProductCode = "NWTBV-17",
            ProductName = "Sample",
            StandardUnitCost = 5m,
            UnitPrice = 9m,
            ProductCategoryId = 4,
            Discontinued = false,
            MinimumReorderQuantity = 5,
        };
    }
}
