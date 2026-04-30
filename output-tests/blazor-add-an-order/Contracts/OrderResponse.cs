using System;
using System.Collections.Generic;

namespace BlazorAddAnOrder.Contracts;

public sealed class OrderResponse
{
    public long Id { get; set; }

    public long EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public float TaxRate { get; set; }

    public byte? TaxStatusId { get; set; }

    public string? Status { get; set; }

    public DateTime? OrderDate { get; set; }

    public List<LineItemResponse> LineItems { get; set; } = new();
}

public sealed class LineItemResponse
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public short Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string Status { get; set; } = string.Empty;
}
