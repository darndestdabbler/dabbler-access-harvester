namespace BlazorAddAnOrder.Contracts;

public sealed class CustomerLookupResponse
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte StandardTaxStatusId { get; set; }
}

public sealed class ProductLookupResponse
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int PhysicalAvailable { get; set; }

    public int OnOrderAvailable { get; set; }
}
