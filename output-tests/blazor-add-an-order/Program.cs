using BlazorAddAnOrder.Ui;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

builder.Services.AddMudServices();

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
