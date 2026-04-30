using BlazorAddAnOrder.Services.Domain;
using BlazorAddAnOrder.Services.Persistence;
using BlazorAddAnOrder.Ui;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

builder.Services.AddMudServices();

var ordersConnectionString = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Orders is required in appsettings.json.");

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlite(ordersConnectionString));

builder.Services.AddScoped<PersistenceCatalog>();
builder.Services.AddScoped<ICustomerCatalog>(sp => sp.GetRequiredService<PersistenceCatalog>());
builder.Services.AddScoped<IProductCatalog>(sp => sp.GetRequiredService<PersistenceCatalog>());
builder.Services.AddScoped<OrdersStore>();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    DatabaseSeeder.EnsureCreatedAndSeeded(db);
}

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
