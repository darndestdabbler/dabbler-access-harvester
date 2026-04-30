using System.IO;
using System.Linq;
using BlazorAddAnOrder.Services.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlazorAddAnOrder.Services.Persistence;

public static class DatabaseSeeder
{
    public static void EnsureCreatedAndSeeded(OrdersDbContext db)
    {
        // SQLite needs the parent directory to exist before it can create the
        // database file. Resolve the relative Data Source path from the
        // connection string and create the directory if needed.
        var connectionString = db.Database.GetDbConnection().ConnectionString;
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (!Path.IsPathRooted(dataSource))
        {
            dataSource = Path.Combine(Directory.GetCurrentDirectory(), dataSource);
        }
        var dataDir = Path.GetDirectoryName(dataSource);
        if (!string.IsNullOrEmpty(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        db.Database.EnsureCreated();

        if (!db.Customers.Any())
        {
            db.Customers.AddRange(
                new CustomerEntity { Id = 1, Name = "Adventure Outfitters", StandardTaxStatusId = 1 },
                new CustomerEntity { Id = 2, Name = "Beachcomber Sundries", StandardTaxStatusId = 0 },
                new CustomerEntity { Id = 3, Name = "Cascade Trading Co.", StandardTaxStatusId = 1 });
        }

        if (!db.Products.Any())
        {
            db.Products.AddRange(
                new ProductEntity { Id = 10, Name = "Trail Mix (1lb)", UnitPrice = 7.50m, PhysicalAvailable = 25, OnOrderAvailable = 0 },
                new ProductEntity { Id = 11, Name = "Stainless Bottle", UnitPrice = 18.00m, PhysicalAvailable = 4, OnOrderAvailable = 6 },
                new ProductEntity { Id = 12, Name = "Camp Stove", UnitPrice = 89.95m, PhysicalAvailable = 0, OnOrderAvailable = 2 },
                new ProductEntity { Id = 13, Name = "Sleeping Bag", UnitPrice = 64.99m, PhysicalAvailable = 8, OnOrderAvailable = 0 });
        }

        db.SaveChanges();
    }
}
