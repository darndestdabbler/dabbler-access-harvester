using System;
using System.Collections.Generic;
using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAnOrder;

public sealed class OrderStatusTransitionBehaviorTests
{
    [Fact]
    public void AllInvoiceLineItemsHaveStatus_ReturnsTrueWhenOrderHasNoLineItems()
    {
        var lineItems = Array.Empty<ExistingOrderLineItem>();

        var result = UpdateOrderWorkflow.AllInvoiceLineItemsHaveStatus(
            lineItems,
            OrderDetailStatus.Allocated);

        Assert.True(result);
    }

    [Fact]
    public void AllInvoiceLineItemsHaveStatus_ReturnsFalseWhenAnyLineItemDiffers()
    {
        var lineItems = new[]
        {
            new ExistingOrderLineItem { ProductId = 1, Status = OrderDetailStatus.Allocated },
            new ExistingOrderLineItem { ProductId = 2, Status = OrderDetailStatus.NoStock },
        };

        var result = UpdateOrderWorkflow.AllInvoiceLineItemsHaveStatus(
            lineItems,
            OrderDetailStatus.Allocated);

        Assert.False(result);
    }

    [Fact]
    public void CanCreateInvoice_ReturnsFalseWhenOrderIsNotNew()
    {
        var order = CreateEditableOrder(OrderStatus.Invoiced);
        var lineItems = AllocatedLineItems();

        var result = UpdateOrderWorkflow.CanCreateInvoice(order, lineItems);

        Assert.False(result);
    }

    [Fact]
    public void CanCreateInvoice_ReturnsFalseWhenOrderHasNoLineItems()
    {
        var order = CreateEditableOrder(OrderStatus.New);

        var result = UpdateOrderWorkflow.CanCreateInvoice(order, Array.Empty<ExistingOrderLineItem>());

        Assert.False(result);
    }

    [Fact]
    public void CanCreateInvoice_ReturnsFalseWhenAnyLineItemIsNotAllocated()
    {
        var order = CreateEditableOrder(OrderStatus.New);
        var lineItems = new List<ExistingOrderLineItem>
        {
            new() { ProductId = 10, Status = OrderDetailStatus.Allocated },
            new() { ProductId = 11, Status = OrderDetailStatus.OnOrder },
        };

        var result = UpdateOrderWorkflow.CanCreateInvoice(order, lineItems);

        Assert.False(result);
    }

    [Fact]
    public void CanCreateInvoice_ReturnsFalseWhenShippingFeeIsMissing()
    {
        var order = CreateEditableOrder(OrderStatus.New);
        order.ShippingFee = null;

        var result = UpdateOrderWorkflow.CanCreateInvoice(order, AllocatedLineItems());

        Assert.False(result);
    }

    [Fact]
    public void CreateInvoice_TransitionsOrderAndLineItemsToInvoiced()
    {
        var invoiceDate = new DateTime(2026, 4, 30, 8, 45, 0, DateTimeKind.Local);
        var order = CreateEditableOrder(OrderStatus.New);
        var lineItems = AllocatedLineItems();

        var succeeded = UpdateOrderWorkflow.CreateInvoice(order, lineItems, () => invoiceDate);

        Assert.True(succeeded);
        Assert.Equal(OrderStatus.Invoiced, order.Status);
        Assert.Equal(invoiceDate, order.InvoiceDate);
        Assert.All(lineItems, lineItem => Assert.Equal(OrderDetailStatus.Invoiced, lineItem.Status));
    }

    [Fact]
    public void CanShip_ReturnsFalseWhenOrderIsNotInvoiced()
    {
        var order = CreateEditableOrder(OrderStatus.New);
        order.ShippedDate = new DateTime(2026, 5, 1);
        order.ShipperId = 6;

        var result = UpdateOrderWorkflow.CanShip(order);

        Assert.False(result);
    }

    [Fact]
    public void CanShip_ReturnsFalseWhenShippingFieldsAreMissing()
    {
        var order = CreateEditableOrder(OrderStatus.Invoiced);
        order.ShippedDate = null;
        order.ShipperId = null;

        var result = UpdateOrderWorkflow.CanShip(order);

        Assert.False(result);
    }

    [Fact]
    public void ShipOrder_TransitionsOrderAndLineItemsToShipped()
    {
        var order = CreateEditableOrder(OrderStatus.Invoiced);
        order.ShippedDate = new DateTime(2026, 5, 1);
        order.ShipperId = 4;
        var lineItems = new List<ExistingOrderLineItem>
        {
            new() { ProductId = 10, Status = OrderDetailStatus.Invoiced },
            new() { ProductId = 11, Status = OrderDetailStatus.Invoiced },
        };

        var succeeded = UpdateOrderWorkflow.ShipOrder(order, lineItems);

        Assert.True(succeeded);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.All(lineItems, lineItem => Assert.Equal(OrderDetailStatus.Shipped, lineItem.Status));
    }

    [Fact]
    public void CanReceivePayment_ReturnsFalseWhenOrderIsNotShipped()
    {
        var order = CreateEditableOrder(OrderStatus.Invoiced);
        order.PaymentMethod = "Wire";
        order.PaidDate = new DateTime(2026, 5, 2);

        var result = UpdateOrderWorkflow.CanReceivePayment(order);

        Assert.False(result);
    }

    [Fact]
    public void CanReceivePayment_ReturnsFalseWhenPaidFieldsAreMissing()
    {
        var order = CreateEditableOrder(OrderStatus.Shipped);
        order.PaymentMethod = null;
        order.PaidDate = null;

        var result = UpdateOrderWorkflow.CanReceivePayment(order);

        Assert.False(result);
    }

    [Fact]
    public void ReceivePayment_TransitionsOrderToPaid()
    {
        var order = CreateEditableOrder(OrderStatus.Shipped);
        order.PaymentMethod = "ACH";
        order.PaidDate = new DateTime(2026, 5, 3);

        var succeeded = UpdateOrderWorkflow.ReceivePayment(order);

        Assert.True(succeeded);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void CloseOrder_ReturnsFalseUnlessOrderIsPaidAndConfirmed()
    {
        var unpaidOrder = CreateEditableOrder(OrderStatus.Shipped);
        var paidOrder = CreateEditableOrder(OrderStatus.Paid);

        Assert.False(UpdateOrderWorkflow.CloseOrder(unpaidOrder, confirmed: true));
        Assert.False(UpdateOrderWorkflow.CloseOrder(paidOrder, confirmed: false));
        Assert.Equal(OrderStatus.Shipped, unpaidOrder.Status);
        Assert.Equal(OrderStatus.Paid, paidOrder.Status);
    }

    [Fact]
    public void CloseOrder_TransitionsPaidOrderToClosedWhenConfirmed()
    {
        var order = CreateEditableOrder(OrderStatus.Paid);

        var succeeded = UpdateOrderWorkflow.CloseOrder(order, confirmed: true);

        Assert.True(succeeded);
        Assert.Equal(OrderStatus.Closed, order.Status);
    }

    private static ExistingOrderDraft CreateEditableOrder(OrderStatus status)
    {
        return new ExistingOrderDraft
        {
            EmployeeId = 2,
            CustomerId = 42,
            OrderDate = new DateTime(2026, 4, 30),
            TaxRate = 0.07f,
            TaxStatusId = 1,
            Status = status,
            ShippingFee = 12.50m,
        };
    }

    private static List<ExistingOrderLineItem> AllocatedLineItems()
    {
        return new List<ExistingOrderLineItem>
        {
            new() { ProductId = 10, Status = OrderDetailStatus.Allocated },
            new() { ProductId = 11, Status = OrderDetailStatus.Allocated },
        };
    }
}
