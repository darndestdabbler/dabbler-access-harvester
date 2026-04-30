using System;

namespace AccessHarvester.CharacterizationTests.AddAnOrder;

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

internal sealed class OrderDraft
{
    public long EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public float TaxRate { get; set; }

    public byte? TaxStatusId { get; set; }

    public OrderStatus? Status { get; set; }

    public DateTime? OrderDate { get; set; }
}

internal sealed class OrderLineItemDraft
{
    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public short Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public OrderDetailStatus Status { get; set; }
}

internal sealed record PendingOrderDetail(
    long OrderId,
    DateTime OrderDate,
    short Quantity,
    OrderDetailStatus Status);
