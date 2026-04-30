using System;
using System.Linq;
using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAnOrder;

public sealed class OrderEditPolicyBehaviorTests
{
    [Fact]
    public void CanSave_ReturnsFalseWhenRequiredOrderFieldsAreMissing()
    {
        var order = new ExistingOrderDraft
        {
            EmployeeId = 2,
            CustomerId = null,
            OrderDate = new DateTime(2026, 4, 30),
            TaxRate = 0.07f,
            TaxStatusId = 1,
            Status = OrderStatus.New,
        };

        var result = UpdateOrderWorkflow.CanSave(order);

        Assert.False(result);
    }

    [Fact]
    public void CanSave_ReturnsTrueWhenRequiredOrderFieldsArePresent()
    {
        var order = new ExistingOrderDraft
        {
            EmployeeId = 2,
            CustomerId = 42,
            OrderDate = new DateTime(2026, 4, 30),
            TaxRate = 0.07f,
            TaxStatusId = 1,
            Status = OrderStatus.Invoiced,
        };

        var result = UpdateOrderWorkflow.CanSave(order);

        Assert.True(result);
    }

    [Fact]
    public void CanDelete_ReturnsTrueOnlyForNewOrInvoicedOrders()
    {
        Assert.True(UpdateOrderWorkflow.CanDelete(CreateOrder(OrderStatus.New)));
        Assert.True(UpdateOrderWorkflow.CanDelete(CreateOrder(OrderStatus.Invoiced)));
        Assert.False(UpdateOrderWorkflow.CanDelete(CreateOrder(OrderStatus.Shipped)));
        Assert.False(UpdateOrderWorkflow.CanDelete(CreateOrder(OrderStatus.Paid)));
        Assert.False(UpdateOrderWorkflow.CanDelete(CreateOrder(OrderStatus.Closed)));
    }

    [Fact]
    public void DeleteOrder_ReturnsEveryLineItemProductForReallocationWhenConfirmed()
    {
        var order = CreateOrder(OrderStatus.Invoiced);
        var lineItems = new[]
        {
            new ExistingOrderLineItem { ProductId = 10, Status = OrderDetailStatus.Invoiced },
            new ExistingOrderLineItem { ProductId = 11, Status = OrderDetailStatus.Invoiced },
            new ExistingOrderLineItem { ProductId = 10, Status = OrderDetailStatus.Invoiced },
        };

        var reallocatedProducts = UpdateOrderWorkflow.DeleteOrder(
            order,
            lineItems,
            confirmed: true);

        Assert.Equal(new long[] { 10, 11, 10 }, reallocatedProducts);
    }

    [Fact]
    public void DeleteOrder_DoesNothingWhenNotConfirmedOrNotDeletable()
    {
        var newOrder = CreateOrder(OrderStatus.New);
        var shippedOrder = CreateOrder(OrderStatus.Shipped);
        var lineItems = new[]
        {
            new ExistingOrderLineItem { ProductId = 10, Status = OrderDetailStatus.Allocated },
        };

        Assert.Empty(UpdateOrderWorkflow.DeleteOrder(newOrder, lineItems, confirmed: false));
        Assert.Empty(UpdateOrderWorkflow.DeleteOrder(shippedOrder, lineItems, confirmed: true));
    }

    [Fact]
    public void CanEditLineItems_ReturnsTrueForNewRecordOrNewStatusOnly()
    {
        Assert.True(UpdateOrderWorkflow.CanEditLineItems(status: null, isNewRecord: true));
        Assert.True(UpdateOrderWorkflow.CanEditLineItems(OrderStatus.New, isNewRecord: false));
        Assert.False(UpdateOrderWorkflow.CanEditLineItems(OrderStatus.Invoiced, isNewRecord: false));
        Assert.False(UpdateOrderWorkflow.CanEditLineItems(OrderStatus.Shipped, isNewRecord: false));
    }

    [Fact]
    public void LockedFieldsFor_NewOrderLocksIdentityDatesAndPaymentFieldsButNotShippingFeeOrTax()
    {
        var lockedFields = UpdateOrderWorkflow.LockedFieldsFor(OrderStatus.New);

        Assert.Contains("CustomerID", lockedFields);
        Assert.Contains("EmployeeID", lockedFields);
        Assert.Contains("OrderDate", lockedFields);
        Assert.Contains("PaymentMethod", lockedFields);
        Assert.DoesNotContain("ShippingFee", lockedFields);
        Assert.DoesNotContain("TaxRate", lockedFields);
        Assert.DoesNotContain("TaxStatusID", lockedFields);
    }

    [Fact]
    public void LockedFieldsFor_InvoicedOrderLocksShippingFeeAndTaxSettings()
    {
        var lockedFields = UpdateOrderWorkflow.LockedFieldsFor(OrderStatus.Invoiced);

        Assert.Contains("ShippingFee", lockedFields);
        Assert.Contains("TaxRate", lockedFields);
        Assert.Contains("TaxStatusID", lockedFields);
        Assert.DoesNotContain("ShippedDate", lockedFields);
        Assert.DoesNotContain("ShipperID", lockedFields);
    }

    [Fact]
    public void LockedFieldsFor_ShippedOrderLeavesPaymentFieldsEditableUntilPaid()
    {
        var lockedFields = UpdateOrderWorkflow.LockedFieldsFor(OrderStatus.Shipped);

        Assert.Contains("ShippedDate", lockedFields);
        Assert.Contains("ShipperID", lockedFields);
        Assert.DoesNotContain("PaymentMethod", lockedFields);
        Assert.DoesNotContain("PaidDate", lockedFields);
    }

    [Fact]
    public void LockedFieldsFor_PaidOrderLocksPaymentFieldsButNotNotes()
    {
        var lockedFields = UpdateOrderWorkflow.LockedFieldsFor(OrderStatus.Paid);

        Assert.Contains("PaymentMethod", lockedFields);
        Assert.Contains("PaidDate", lockedFields);
        Assert.DoesNotContain("Notes", lockedFields);
    }

    [Fact]
    public void LockedFieldsFor_ClosedOrderAlsoLocksNotes()
    {
        var lockedFields = UpdateOrderWorkflow.LockedFieldsFor(OrderStatus.Closed);

        Assert.Contains("Notes", lockedFields);
    }

    [Fact]
    public void LockedFieldsFor_NullStatusMatchesPreInsertLockSet()
    {
        var lockedFields = UpdateOrderWorkflow.LockedFieldsFor(status: null);

        Assert.Equal(
            new[]
            {
                "InvoiceDate",
                "OrderDate",
                "OrderID",
                "OrderStatusName",
                "PaidDate",
                "PaymentMethod",
                "ShippedDate",
                "ShipperID",
                "txtTaxAmount",
                "txtTotal",
            },
            lockedFields.OrderBy(field => field).ToArray());
    }

    private static ExistingOrderDraft CreateOrder(OrderStatus status)
    {
        return new ExistingOrderDraft
        {
            EmployeeId = 2,
            CustomerId = 42,
            OrderDate = new DateTime(2026, 4, 30),
            TaxRate = 0.07f,
            TaxStatusId = 1,
            Status = status,
        };
    }
}
