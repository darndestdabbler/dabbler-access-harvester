using System;
using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

public sealed class ProductReorderPolicyTests
{
    [Fact]
    public void IsNonClosedPurchaseOrderStatus_TreatsApproveNewSubmittedAsOpen()
    {
        Assert.True(UpdateProductWorkflow.IsNonClosedPurchaseOrderStatus(PurchaseOrderStatus.Approve));
        Assert.True(UpdateProductWorkflow.IsNonClosedPurchaseOrderStatus(PurchaseOrderStatus.New));
        Assert.True(UpdateProductWorkflow.IsNonClosedPurchaseOrderStatus(PurchaseOrderStatus.Submitted));
    }

    [Fact]
    public void IsNonClosedPurchaseOrderStatus_TreatsClosedAndReceivedAsTerminal()
    {
        Assert.False(UpdateProductWorkflow.IsNonClosedPurchaseOrderStatus(PurchaseOrderStatus.Closed));
        Assert.False(UpdateProductWorkflow.IsNonClosedPurchaseOrderStatus(PurchaseOrderStatus.Received));
    }

    [Fact]
    public void ReorderIsBlockedByOpenPurchaseOrder_RequiresBothQuantityOnOrderAndAnExistingOpenPo()
    {
        Assert.True(UpdateProductWorkflow.ReorderIsBlockedByOpenPurchaseOrder(5, 9001));
        Assert.False(UpdateProductWorkflow.ReorderIsBlockedByOpenPurchaseOrder(0, 9001));
        Assert.False(UpdateProductWorkflow.ReorderIsBlockedByOpenPurchaseOrder(5, null));
    }

    [Fact]
    public void ExecuteReorder_OpensExistingOpenPurchaseOrderWhenStockIsAlreadyOnOrder()
    {
        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = ProductWithMinimum(minimum: 10),
            OnOrderQuantity = 25,
            CalculatedReorderQuantity = 0,
            AvailableVendorIds = Array.Empty<long>(),
            ExistingOpenPurchaseOrderId = 5050,
        });

        Assert.Equal(ReorderOutcomeKind.OpenedExistingOpenPurchaseOrder, outcome.Kind);
        Assert.Equal(5050, outcome.PurchaseOrderId);
    }

    [Fact]
    public void ExecuteReorder_CancelsWhenProductHasNoVendors()
    {
        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = ProductWithMinimum(minimum: 10),
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 12,
            AvailableVendorIds = Array.Empty<long>(),
        });

        Assert.Equal(ReorderOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal("vendor-required", outcome.CancelReason);
    }

    [Fact]
    public void ExecuteReorder_CancelsWhenMultipleVendorsAreOfferedAndUserDoesNotPick()
    {
        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = ProductWithMinimum(minimum: 10),
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 12,
            AvailableVendorIds = new long[] { 11, 22 },
            UserSelectedVendorId = null,
        });

        Assert.Equal(ReorderOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal("vendor-not-selected", outcome.CancelReason);
    }

    [Fact]
    public void ExecuteReorder_CancelsWhenResolvedQuantityIsZero()
    {
        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = ProductWithMinimum(minimum: 0),
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 0,
            AvailableVendorIds = new long[] { 11 },
            UserEnteredQuantity = 0,
        });

        Assert.Equal(ReorderOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal("quantity-zero", outcome.CancelReason);
    }

    [Fact]
    public void ExecuteReorder_CancelsWhenProductIsDiscontinued()
    {
        var product = ProductWithMinimum(minimum: 10);
        product.Discontinued = true;

        var outcome = UpdateProductWorkflow.ExecuteReorder(new ReorderRequest
        {
            Product = product,
            OnOrderQuantity = 0,
            CalculatedReorderQuantity = 12,
            AvailableVendorIds = new long[] { 11 },
        });

        Assert.Equal(ReorderOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal("discontinued", outcome.CancelReason);
    }

    private static ProductDraft ProductWithMinimum(int minimum)
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
            MinimumReorderQuantity = minimum,
        };
    }
}
