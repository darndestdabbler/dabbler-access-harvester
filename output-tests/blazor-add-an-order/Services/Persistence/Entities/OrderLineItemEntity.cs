namespace BlazorAddAnOrder.Services.Persistence.Entities;

public sealed class OrderLineItemEntity
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public short Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public int StatusId { get; set; }
}
