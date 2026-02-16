using BlazorWebAppWasmOnly.Components;
using BlazorApp1.Services;
using BlazorApp1.Server.Hubs;
using BlazorApp1.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddBlazorApp1Services();
builder.Services.AddBlazorApp1ServerServices();

// Add HttpClient for server-side (self-referencing for API calls during SSR)
builder.Services.AddHttpClient("ServerAPI", client =>
{
    // This will be set to the server's own address at runtime
});

// Add DiagramService with a factory that creates HttpClient with proper base address
builder.Services.AddScoped<DiagramService>(sp =>
{
    var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
    var httpClient = new HttpClient();

    // Use the current request's base address if available (for server-side)
    if (httpContextAccessor?.HttpContext != null)
    {
        var request = httpContextAccessor.HttpContext.Request;
        httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
    }

    return new DiagramService(httpClient);
});

// Add HttpContextAccessor for the above
builder.Services.AddHttpContextAccessor();

// Add SignalR
builder.Services.AddSignalR();

// Add Controllers for API endpoints
builder.Services.AddControllers(options =>
{
    // Disable antiforgery validation for API controllers
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
})
.AddApplicationPart(typeof(BlazorApp1.Server.Controllers.DiagramController).Assembly)
.AddControllersAsServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
//app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Add antiforgery middleware (required by Blazor components)
// API controllers are exempt via the IgnoreAntiforgeryTokenAttribute global filter
app.UseAntiforgery();

app.MapStaticAssets();

// Map Controllers with antiforgery disabled via global filter
app.MapControllers();

// Map SignalR Hub
app.MapHub<MqttDataHub>("/mqttdatahub");

// Map Razor Components (handles its own antiforgery)
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorApp1._Imports).Assembly);

app.Run();
