using System.Collections.Generic;
using System.Linq;

namespace BlazorAddAnOrder.Services.Domain;

public interface IProductCatalog
{
    decimal LookupUnitPrice(long productId);

    int PhysicalAvailable(long productId);

    int OnOrderAvailable(long productId);
}

public sealed class OrderLineItemService
{
    private readonly IProductCatalog _products;

    public OrderLineItemService(IProductCatalog products)
    {
        _products = products;
    }

    public void BeginLineItem(OrderLineItemDraft lineItem)
    {
        lineItem.Status = OrderDetailStatus.New;
    }

    public void ApplyProductSelection(OrderLineItemDraft lineItem)
    {
        lineItem.UnitPrice = _products.LookupUnitPrice(lineItem.ProductId);
    }

    public void PrepareForSave(OrderLineItemDraft lineItem)
    {
        if (lineItem.ProductId == 0 || lineItem.Quantity == 0)
        {
            return;
        }

        lineItem.Status = _products.PhysicalAvailable(lineItem.ProductId) >= lineItem.Quantity
            ? OrderDetailStatus.Allocated
            : OrderDetailStatus.NoStock;
    }

    public bool CanSave(OrderLineItemDraft lineItem)
    {
        return lineItem.OrderId != 0
            && lineItem.ProductId != 0
            && lineItem.Quantity > 0
            && lineItem.UnitPrice > 0m;
    }

    public bool CanAddProductToOrder(long productId, IEnumerable<long> existingProductIds)
    {
        return !existingProductIds.Contains(productId);
    }
}
