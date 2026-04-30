namespace BlazorAddAnOrder.Services.Persistence.Entities;

public sealed class CustomerEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte StandardTaxStatusId { get; set; }
}
