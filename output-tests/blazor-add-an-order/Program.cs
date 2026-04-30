using BlazorAddAnOrder.Services.Domain;
using BlazorAddAnOrder.Services.Stubs;
using BlazorAddAnOrder.Ui;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

builder.Services.AddMudServices();

builder.Services.AddSingleton<InMemoryCatalog>();
builder.Services.AddSingleton<ICustomerCatalog>(sp => sp.GetRequiredService<InMemoryCatalog>());
builder.Services.AddSingleton<IProductCatalog>(sp => sp.GetRequiredService<InMemoryCatalog>());
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton(new OrderCreationOptions { SystemTaxRate = 0.0825f });
builder.Services.AddScoped<OrderCreationService>();
builder.Services.AddScoped<OrderLineItemService>();
builder.Services.AddSingleton<InventoryAllocationService>();

var servicesBaseAddress = builder.Configuration["Services:BaseAddress"]
    ?? throw new InvalidOperationException(
        "Services:BaseAddress is required in appsettings.json — " +
        "the UI must reach the in-process REST boundary through HttpClient, " +
        "not through a hard-coded URL.");

builder.Services.AddHttpClient("services", client =>
{
    client.BaseAddress = new Uri(servicesBaseAddress);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

namespace BlazorAddAnOrder
{
    public partial class Program;
}
