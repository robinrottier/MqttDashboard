using BlazorWebAppWasmOnly.Components;
using MudBlazor.Services;
using BlazorApp1.Services;
using BlazorWebAppWasmOnly.Hubs;
using BlazorWebAppWasmOnly.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddScoped<DiagramStateService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<SignalRService>();

// Add SignalR
builder.Services.AddSignalR();

// Add MQTT service
builder.Services.AddHostedService<MqttClientService>();

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

app.UseAntiforgery();

app.MapStaticAssets();

// Map SignalR Hub
app.MapHub<MqttDataHub>("/mqttdatahub");

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorApp1._Imports).Assembly);

app.Run();
