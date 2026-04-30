using System.Collections.Generic;
using System.Linq;
using BlazorAddAnOrder.Services.Domain;
using BlazorAddAnOrder.Services.Persistence.Entities;
using BlazorAddAnOrder.Services.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace BlazorAddAnOrder.Services.Persistence;

public sealed class OrdersStore
{
    private readonly OrdersDbContext _db;

    public OrdersStore(OrdersDbContext db)
    {
        _db = db;
    }

    public OrderEntity Persist(OrderDraft draft, IEnumerable<OrderLineItemDraft> lineItems)
    {
        var entity = OrderMappings.ToEntity(draft);
        foreach (var line in lineItems)
        {
            entity.LineItems.Add(OrderMappings.ToEntity(line));
        }

        _db.Orders.Add(entity);
        _db.SaveChanges();

        // The DbContext now holds the assigned identity values; reflect the
        // assigned order id back into the draft so callers see the persisted
        // identity without needing a re-read.
        draft.Id = entity.Id;
        return entity;
    }

    public OrderEntity? Find(long id)
        => _db.Orders
            .AsNoTracking()
            .Include(o => o.LineItems)
            .FirstOrDefault(o => o.Id == id);
}
