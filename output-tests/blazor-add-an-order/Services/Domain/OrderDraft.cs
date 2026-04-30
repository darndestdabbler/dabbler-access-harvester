namespace BlazorAddAnOrder.Services.Domain;

public sealed class OrderDraft
{
    public long? Id { get; set; }

    public long EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public float TaxRate { get; set; }

    public byte? TaxStatusId { get; set; }

    public OrderStatus? Status { get; set; }

    public System.DateTime? OrderDate { get; set; }
}

public sealed class OrderLineItemDraft
{
    public long? Id { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public short Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public OrderDetailStatus Status { get; set; }
}

public sealed record PendingOrderDetail(
    long OrderId,
    System.DateTime OrderDate,
    short Quantity,
    OrderDetailStatus Status);
