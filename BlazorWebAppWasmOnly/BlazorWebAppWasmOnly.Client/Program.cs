using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorApp1.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddScoped<DiagramStateService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<SignalRService>();

await builder.Build().RunAsync();
