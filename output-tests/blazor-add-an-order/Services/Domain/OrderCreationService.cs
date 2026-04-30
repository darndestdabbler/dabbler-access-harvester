using System;

namespace BlazorAddAnOrder.Services.Domain;

public interface ISystemClock
{
    DateTime UtcNow();
}

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow() => DateTime.UtcNow;
}

public interface ICustomerCatalog
{
    byte? LookupStandardTaxStatus(long customerId);
}

public sealed class OrderCreationService
{
    private readonly ISystemClock _clock;
    private readonly ICustomerCatalog _customers;
    private readonly OrderCreationOptions _options;

    public OrderCreationService(
        ISystemClock clock,
        ICustomerCatalog customers,
        OrderCreationOptions options)
    {
        _clock = clock;
        _customers = customers;
        _options = options;
    }

    public void BeginOrderEdit(OrderDraft order, long currentUserId)
    {
        order.EmployeeId = currentUserId;
        order.TaxRate = _options.SystemTaxRate;
        order.Status = OrderStatus.New;
        order.OrderDate = _clock.UtcNow();
    }

    public void ApplyCustomerSelection(OrderDraft order)
    {
        if (order.CustomerId is null)
        {
            throw new InvalidOperationException(
                "ApplyCustomerSelection requires a non-null CustomerId on the draft.");
        }

        order.TaxStatusId = _customers.LookupStandardTaxStatus(order.CustomerId.Value);
    }

    public bool CanSave(OrderDraft order)
    {
        return order.CustomerId is not null
            && order.OrderDate is not null
            && order.TaxStatusId is not null
            && order.Status is not null
            && order.EmployeeId != 0;
    }
}

public sealed class OrderCreationOptions
{
    public float SystemTaxRate { get; init; } = 0.0825f;
}
