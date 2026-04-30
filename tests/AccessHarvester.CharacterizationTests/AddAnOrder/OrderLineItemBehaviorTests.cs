using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAnOrder;

/// <summary>
/// Characterization tests for Add-an-Order line-item behavior.
/// Sources:
/// - sfrmOrderLineItems::Form_BeforeInsert
/// - sfrmOrderLineItems::ProductID_AfterUpdate
/// - sfrmOrderLineItems::SetOrderDetailStatus
/// </summary>
public sealed class OrderLineItemBehaviorTests
{
    [Fact]
    public void BeginLineItem_SetsInitialStatusToNew()
    {
        var lineItem = new OrderLineItemDraft();
        var workflow = new OrderLineItemWorkflow(
            productLookup: _ => 0m,
            productAvailable: _ => 0,
            lineItemValidator: _ => true);

        workflow.BeginLineItem(lineItem);

        Assert.Equal(OrderDetailStatus.New, lineItem.Status);
    }

    [Fact]
    public void ApplyProductSelection_CopiesUnitPriceFromProductCatalog()
    {
        var lineItem = new OrderLineItemDraft { ProductId = 7 };
        var workflow = new OrderLineItemWorkflow(
            productLookup: productId =>
            {
                Assert.Equal(7L, productId);
                return 12.34m;
            },
            productAvailable: _ => 0,
            lineItemValidator: _ => true);

        workflow.ApplyProductSelection(lineItem);

        Assert.Equal(12.34m, lineItem.UnitPrice);
    }

    [Fact]
    public void PrepareForSave_MarksLineAllocatedWhenFullQuantityIsAvailable()
    {
        var lineItem = new OrderLineItemDraft { ProductId = 7, Quantity = 5 };
        var workflow = new OrderLineItemWorkflow(
            productLookup: _ => 12.34m,
            productAvailable: _ => 5,
            lineItemValidator: _ => true);

        workflow.PrepareForSave(lineItem);

        Assert.Equal(OrderDetailStatus.Allocated, lineItem.Status);
    }

    [Fact]
    public void PrepareForSave_MarksLineNoStockWhenQuantityExceedsAvailability()
    {
        var lineItem = new OrderLineItemDraft { ProductId = 7, Quantity = 6 };
        var workflow = new OrderLineItemWorkflow(
            productLookup: _ => 12.34m,
            productAvailable: _ => 5,
            lineItemValidator: _ => true);

        workflow.PrepareForSave(lineItem);

        Assert.Equal(OrderDetailStatus.NoStock, lineItem.Status);
    }

    [Fact]
    public void CanSave_ReturnsFalseWhenRequiredFieldsAreMissingOrQuantityIsNonPositive()
    {
        var lineItem = new OrderLineItemDraft
        {
            OrderId = 10,
            ProductId = 0,
            Quantity = 0,
            UnitPrice = 0m,
            Status = OrderDetailStatus.New,
        };
        var workflow = new OrderLineItemWorkflow(
            productLookup: _ => 12.34m,
            productAvailable: _ => 5,
            lineItemValidator: draft =>
                draft.OrderId != 0 &&
                draft.ProductId != 0 &&
                draft.Quantity > 0 &&
                draft.UnitPrice > 0m);

        Assert.False(workflow.CanSave(lineItem));
    }

    [Fact]
    public void CanSave_ReturnsTrueWhenLineItemIsComplete()
    {
        var lineItem = new OrderLineItemDraft
        {
            OrderId = 10,
            ProductId = 7,
            Quantity = 3,
            UnitPrice = 12.34m,
            Status = OrderDetailStatus.Allocated,
        };
        var workflow = new OrderLineItemWorkflow(
            productLookup: _ => 12.34m,
            productAvailable: _ => 5,
            lineItemValidator: draft =>
                draft.OrderId != 0 &&
                draft.ProductId != 0 &&
                draft.Quantity > 0 &&
                draft.UnitPrice > 0m);

        Assert.True(workflow.CanSave(lineItem));
    }

    [Fact]
    public void CanAddProductToOrder_ReturnsFalseWhenSameProductAlreadyExistsForOrder()
    {
        var workflow = new OrderLineItemWorkflow(
            productLookup: _ => 12.34m,
            productAvailable: _ => 5,
            lineItemValidator: _ => true);

        Assert.False(workflow.CanAddProductToOrder(productId: 7, existingProductIds: [2, 7, 9]));
    }

    [Fact]
    public void CanAddProductToOrder_ReturnsTrueWhenProductIsNotAlreadyOnOrder()
    {
        var workflow = new OrderLineItemWorkflow(
            productLookup: _ => 12.34m,
            productAvailable: _ => 5,
            lineItemValidator: _ => true);

        Assert.True(workflow.CanAddProductToOrder(productId: 7, existingProductIds: [2, 9]));
    }

    private sealed class OrderLineItemWorkflow
    {
        private readonly Func<long, decimal> _productLookup;
        private readonly Func<long, int> _productAvailable;
        private readonly Func<OrderLineItemDraft, bool> _lineItemValidator;

        public OrderLineItemWorkflow(
            Func<long, decimal> productLookup,
            Func<long, int> productAvailable,
            Func<OrderLineItemDraft, bool> lineItemValidator)
        {
            _productLookup = productLookup;
            _productAvailable = productAvailable;
            _lineItemValidator = lineItemValidator;
        }

        public void BeginLineItem(OrderLineItemDraft lineItem)
        {
            lineItem.Status = OrderDetailStatus.New;
        }

        public void ApplyProductSelection(OrderLineItemDraft lineItem)
        {
            lineItem.UnitPrice = _productLookup(lineItem.ProductId);
        }

        public void PrepareForSave(OrderLineItemDraft lineItem)
        {
            if (lineItem.ProductId == 0 || lineItem.Quantity == 0)
            {
                return;
            }

            lineItem.Status = _productAvailable(lineItem.ProductId) >= lineItem.Quantity
                ? OrderDetailStatus.Allocated
                : OrderDetailStatus.NoStock;
        }

        public bool CanSave(OrderLineItemDraft lineItem)
        {
            return _lineItemValidator(lineItem);
        }

        public bool CanAddProductToOrder(long productId, IEnumerable<long> existingProductIds)
        {
            return !existingProductIds.Contains(productId);
        }
    }
}
