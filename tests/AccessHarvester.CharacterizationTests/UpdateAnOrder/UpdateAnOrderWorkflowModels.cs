using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.CharacterizationTests.UpdateAnOrder;

internal enum OrderStatus
{
    Closed = 1,
    Invoiced = 2,
    New = 3,
    Shipped = 4,
    Paid = 5,
}

internal enum OrderDetailStatus
{
    Allocated = 1,
    Invoiced = 2,
    New = 3,
    NoStock = 4,
    OnOrder = 5,
    Shipped = 6,
}

internal sealed class ExistingOrderDraft
{
    public long? EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public DateTime? OrderDate { get; set; }

    public float? TaxRate { get; set; }

    public byte? TaxStatusId { get; set; }

    public OrderStatus? Status { get; set; }

    public DateTime? InvoiceDate { get; set; }

    public DateTime? ShippedDate { get; set; }

    public long? ShipperId { get; set; }

    public decimal? ShippingFee { get; set; }

    public string? PaymentMethod { get; set; }

    public DateTime? PaidDate { get; set; }
}

internal sealed class ExistingOrderLineItem
{
    public long ProductId { get; set; }

    public OrderDetailStatus Status { get; set; }
}

internal static class UpdateOrderWorkflow
{
    public static bool CanSave(ExistingOrderDraft order)
    {
        return order.EmployeeId.HasValue
            && order.CustomerId.HasValue
            && order.OrderDate.HasValue
            && order.TaxRate.HasValue
            && order.TaxStatusId.HasValue
            && order.Status.HasValue;
    }

    public static bool AllInvoiceLineItemsHaveStatus(
        IEnumerable<ExistingOrderLineItem> lineItems,
        OrderDetailStatus expectedStatus)
    {
        return lineItems.All(lineItem => lineItem.Status == expectedStatus);
    }

    public static bool CanCreateInvoice(
        ExistingOrderDraft order,
        ICollection<ExistingOrderLineItem> lineItems)
    {
        return order.Status == OrderStatus.New
            && lineItems.Count > 0
            && AllInvoiceLineItemsHaveStatus(lineItems, OrderDetailStatus.Allocated)
            && order.ShippingFee.HasValue;
    }

    public static bool CreateInvoice(
        ExistingOrderDraft order,
        IList<ExistingOrderLineItem> lineItems,
        Func<DateTime> nowFactory)
    {
        if (!CanCreateInvoice(order, lineItems))
        {
            return false;
        }

        order.Status = OrderStatus.Invoiced;
        order.InvoiceDate = nowFactory();
        SetLineItemsStatus(lineItems, OrderDetailStatus.Invoiced);
        return true;
    }

    public static bool CanShip(ExistingOrderDraft order)
    {
        return order.Status == OrderStatus.Invoiced
            && order.ShippedDate.HasValue
            && order.ShipperId.HasValue
            && order.ShippingFee.HasValue;
    }

    public static bool ShipOrder(
        ExistingOrderDraft order,
        IList<ExistingOrderLineItem> lineItems)
    {
        if (!CanShip(order))
        {
            return false;
        }

        order.Status = OrderStatus.Shipped;
        SetLineItemsStatus(lineItems, OrderDetailStatus.Shipped);
        return true;
    }

    public static bool CanReceivePayment(ExistingOrderDraft order)
    {
        return order.Status == OrderStatus.Shipped
            && !string.IsNullOrWhiteSpace(order.PaymentMethod)
            && order.PaidDate.HasValue;
    }

    public static bool ReceivePayment(ExistingOrderDraft order)
    {
        if (!CanReceivePayment(order))
        {
            return false;
        }

        order.Status = OrderStatus.Paid;
        return true;
    }

    public static bool CanClose(ExistingOrderDraft order)
    {
        return order.Status == OrderStatus.Paid;
    }

    public static bool CloseOrder(ExistingOrderDraft order, bool confirmed)
    {
        if (!confirmed || !CanClose(order))
        {
            return false;
        }

        order.Status = OrderStatus.Closed;
        return true;
    }

    public static bool CanDelete(ExistingOrderDraft order)
    {
        return order.Status is OrderStatus.New or OrderStatus.Invoiced;
    }

    public static IReadOnlyList<long> DeleteOrder(
        ExistingOrderDraft order,
        IReadOnlyCollection<ExistingOrderLineItem> lineItems,
        bool confirmed)
    {
        if (!confirmed || !CanDelete(order))
        {
            return Array.Empty<long>();
        }

        return lineItems.Select(lineItem => lineItem.ProductId).ToArray();
    }

    public static bool CanEditLineItems(OrderStatus? status, bool isNewRecord)
    {
        if (isNewRecord)
        {
            return true;
        }

        return status == OrderStatus.New;
    }

    public static ISet<string> LockedFieldsFor(OrderStatus? status)
    {
        return status switch
        {
            OrderStatus.New => FieldSet(
                "OrderID",
                "CustomerID",
                "EmployeeID",
                "OrderStatusName",
                "OrderDate",
                "InvoiceDate",
                "ShippedDate",
                "ShipperID",
                "PaymentMethod",
                "PaidDate",
                "txtTaxAmount",
                "txtTotal"),
            OrderStatus.Invoiced => FieldSet(
                "OrderID",
                "CustomerID",
                "EmployeeID",
                "OrderStatusName",
                "OrderDate",
                "InvoiceDate",
                "ShippingFee",
                "PaymentMethod",
                "PaidDate",
                "TaxRate",
                "TaxStatusID",
                "txtTaxAmount",
                "txtTotal"),
            OrderStatus.Shipped => FieldSet(
                "OrderID",
                "CustomerID",
                "EmployeeID",
                "OrderStatusName",
                "OrderDate",
                "InvoiceDate",
                "ShippedDate",
                "ShipperID",
                "ShippingFee",
                "TaxRate",
                "TaxStatusID",
                "txtTaxAmount",
                "txtTotal"),
            OrderStatus.Paid => FieldSet(
                "OrderID",
                "CustomerID",
                "EmployeeID",
                "OrderStatusName",
                "OrderDate",
                "InvoiceDate",
                "ShippedDate",
                "ShipperID",
                "ShippingFee",
                "PaymentMethod",
                "PaidDate",
                "TaxRate",
                "TaxStatusID",
                "txtTaxAmount",
                "txtTotal"),
            OrderStatus.Closed => FieldSet(
                "OrderID",
                "CustomerID",
                "EmployeeID",
                "Notes",
                "OrderStatusName",
                "OrderDate",
                "InvoiceDate",
                "ShippedDate",
                "ShipperID",
                "ShippingFee",
                "PaymentMethod",
                "PaidDate",
                "TaxRate",
                "TaxStatusID",
                "txtTaxAmount",
                "txtTotal"),
            _ => FieldSet(
                "OrderID",
                "OrderStatusName",
                "OrderDate",
                "InvoiceDate",
                "ShippedDate",
                "ShipperID",
                "PaymentMethod",
                "PaidDate",
                "txtTaxAmount",
                "txtTotal"),
        };
    }

    private static void SetLineItemsStatus(
        IEnumerable<ExistingOrderLineItem> lineItems,
        OrderDetailStatus targetStatus)
    {
        foreach (var lineItem in lineItems)
        {
            lineItem.Status = targetStatus;
        }
    }

    private static ISet<string> FieldSet(params string[] fields)
    {
        return new HashSet<string>(fields, StringComparer.Ordinal);
    }
}
