namespace BlazorAddAnOrder.Services.Domain;

public enum OrderStatus
{
    Closed = 1,
    Invoiced = 2,
    New = 3,
    Shipped = 4,
    Paid = 5,
}

public enum OrderDetailStatus
{
    Allocated = 1,
    Invoiced = 2,
    New = 3,
    NoStock = 4,
    OnOrder = 5,
    Shipped = 6,
}
