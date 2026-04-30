using System;
using System.Collections.Generic;

namespace BlazorAddAnOrder.Services.Persistence.Entities;

public sealed class OrderEntity
{
    public long Id { get; set; }

    public long EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public float TaxRate { get; set; }

    public byte? TaxStatusId { get; set; }

    public int? StatusId { get; set; }

    public DateTime? OrderDate { get; set; }

    public List<OrderLineItemEntity> LineItems { get; set; } = new();
}
