namespace BlazorAddAnOrder.Services.Persistence.Entities;

public sealed class ProductEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int PhysicalAvailable { get; set; }

    public int OnOrderAvailable { get; set; }
}
