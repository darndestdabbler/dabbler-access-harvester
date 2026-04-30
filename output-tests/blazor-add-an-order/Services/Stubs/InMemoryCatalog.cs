using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BlazorAddAnOrder.Services.Domain;

namespace BlazorAddAnOrder.Services.Stubs;

public sealed class CatalogCustomer
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte StandardTaxStatusId { get; set; }
}

public sealed class CatalogProduct
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int PhysicalAvailable { get; set; }

    public int OnOrderAvailable { get; set; }
}

public sealed class CatalogOrder
{
    public long Id { get; set; }

    public OrderDraft Draft { get; set; } = new();

    public List<OrderLineItemDraft> LineItems { get; set; } = new();
}

public sealed class InMemoryCatalog : ICustomerCatalog, IProductCatalog
{
    private readonly Dictionary<long, CatalogCustomer> _customers = new();
    private readonly Dictionary<long, CatalogProduct> _products = new();
    private readonly ConcurrentDictionary<long, CatalogOrder> _orders = new();
    private long _nextOrderId;
    private long _nextLineItemId;

    public InMemoryCatalog()
    {
        Seed();
    }

    public IReadOnlyCollection<CatalogCustomer> ListCustomers() => _customers.Values.OrderBy(c => c.Name).ToList();

    public CatalogCustomer? FindCustomer(long id) => _customers.TryGetValue(id, out var c) ? c : null;

    public IReadOnlyCollection<CatalogProduct> ListProducts() => _products.Values.OrderBy(p => p.Name).ToList();

    public CatalogProduct? FindProduct(long id) => _products.TryGetValue(id, out var p) ? p : null;

    public byte? LookupStandardTaxStatus(long customerId)
        => _customers.TryGetValue(customerId, out var c) ? c.StandardTaxStatusId : (byte?)null;

    public decimal LookupUnitPrice(long productId)
        => _products.TryGetValue(productId, out var p) ? p.UnitPrice : 0m;

    public int PhysicalAvailable(long productId)
        => _products.TryGetValue(productId, out var p) ? p.PhysicalAvailable : 0;

    public int OnOrderAvailable(long productId)
        => _products.TryGetValue(productId, out var p) ? p.OnOrderAvailable : 0;

    public CatalogOrder PersistOrder(OrderDraft draft, IEnumerable<OrderLineItemDraft> lineItems)
    {
        var orderId = System.Threading.Interlocked.Increment(ref _nextOrderId);
        draft.Id = orderId;
        var stored = new CatalogOrder
        {
            Id = orderId,
            Draft = draft,
            LineItems = lineItems.Select(li =>
            {
                li.Id = System.Threading.Interlocked.Increment(ref _nextLineItemId);
                li.OrderId = orderId;
                return li;
            }).ToList(),
        };
        _orders[orderId] = stored;
        return stored;
    }

    public CatalogOrder? FindOrder(long id) => _orders.TryGetValue(id, out var o) ? o : null;

    private void Seed()
    {
        _customers[1] = new CatalogCustomer { Id = 1, Name = "Adventure Outfitters", StandardTaxStatusId = 1 };
        _customers[2] = new CatalogCustomer { Id = 2, Name = "Beachcomber Sundries", StandardTaxStatusId = 0 };
        _customers[3] = new CatalogCustomer { Id = 3, Name = "Cascade Trading Co.", StandardTaxStatusId = 1 };

        _products[10] = new CatalogProduct { Id = 10, Name = "Trail Mix (1lb)", UnitPrice = 7.50m, PhysicalAvailable = 25, OnOrderAvailable = 0 };
        _products[11] = new CatalogProduct { Id = 11, Name = "Stainless Bottle", UnitPrice = 18.00m, PhysicalAvailable = 4, OnOrderAvailable = 6 };
        _products[12] = new CatalogProduct { Id = 12, Name = "Camp Stove", UnitPrice = 89.95m, PhysicalAvailable = 0, OnOrderAvailable = 2 };
        _products[13] = new CatalogProduct { Id = 13, Name = "Sleeping Bag", UnitPrice = 64.99m, PhysicalAvailable = 8, OnOrderAvailable = 0 };
    }
}
