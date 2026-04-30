using System.Collections.Generic;

namespace BlazorAddAnOrder.Contracts;

public sealed class OrderRequest
{
    public long EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public List<OrderLineItemRequest> LineItems { get; set; } = new();
}

public sealed class OrderLineItemRequest
{
    public long ProductId { get; set; }

    public short Quantity { get; set; }
}
