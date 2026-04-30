using System.Collections.Generic;
using System.Linq;
using BlazorAddAnOrder.Services.Domain;
using BlazorAddAnOrder.Services.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorAddAnOrder.Services.Persistence;

public sealed class PersistenceCatalog : ICustomerCatalog, IProductCatalog
{
    private readonly OrdersDbContext _db;

    public PersistenceCatalog(OrdersDbContext db)
    {
        _db = db;
    }

    public IReadOnlyCollection<CustomerEntity> ListCustomers()
        => _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToList();

    public CustomerEntity? FindCustomer(long id)
        => _db.Customers.AsNoTracking().FirstOrDefault(c => c.Id == id);

    public IReadOnlyCollection<ProductEntity> ListProducts()
        => _db.Products.AsNoTracking().OrderBy(p => p.Name).ToList();

    public ProductEntity? FindProduct(long id)
        => _db.Products.AsNoTracking().FirstOrDefault(p => p.Id == id);

    public byte? LookupStandardTaxStatus(long customerId)
        => _db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => (byte?)c.StandardTaxStatusId)
            .FirstOrDefault();

    public decimal LookupUnitPrice(long productId)
        => _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.UnitPrice)
            .FirstOrDefault();

    public int PhysicalAvailable(long productId)
        => _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.PhysicalAvailable)
            .FirstOrDefault();

    public int OnOrderAvailable(long productId)
        => _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.OnOrderAvailable)
            .FirstOrDefault();
}
