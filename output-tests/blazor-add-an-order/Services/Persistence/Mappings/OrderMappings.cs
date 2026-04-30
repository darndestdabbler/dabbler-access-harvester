using System.Linq;
using BlazorAddAnOrder.Services.Domain;
using BlazorAddAnOrder.Services.Persistence.Entities;

namespace BlazorAddAnOrder.Services.Persistence.Mappings;

public static class OrderMappings
{
    public static OrderEntity ToEntity(OrderDraft draft)
    {
        return new OrderEntity
        {
            EmployeeId = draft.EmployeeId,
            CustomerId = draft.CustomerId,
            TaxRate = draft.TaxRate,
            TaxStatusId = draft.TaxStatusId,
            StatusId = draft.Status is null ? null : (int)draft.Status.Value,
            OrderDate = draft.OrderDate,
        };
    }

    public static OrderLineItemEntity ToEntity(OrderLineItemDraft line)
    {
        return new OrderLineItemEntity
        {
            ProductId = line.ProductId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            StatusId = (int)line.Status,
        };
    }

    public static OrderDraft ToDraft(OrderEntity entity)
    {
        return new OrderDraft
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            CustomerId = entity.CustomerId,
            TaxRate = entity.TaxRate,
            TaxStatusId = entity.TaxStatusId,
            Status = entity.StatusId is null ? null : (OrderStatus)entity.StatusId.Value,
            OrderDate = entity.OrderDate,
        };
    }

    public static OrderLineItemDraft ToDraft(OrderLineItemEntity entity)
    {
        return new OrderLineItemDraft
        {
            Id = entity.Id,
            OrderId = entity.OrderId,
            ProductId = entity.ProductId,
            Quantity = entity.Quantity,
            UnitPrice = entity.UnitPrice,
            Status = (OrderDetailStatus)entity.StatusId,
        };
    }
}
