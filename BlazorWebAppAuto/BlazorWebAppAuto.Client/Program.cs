using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorApp1.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddBlazorApp1Services();

// Add HttpClient for API calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add DiagramService (needs HttpClient)
builder.Services.AddScoped<DiagramService>();

// Add ApplicationStateService (needs HttpClient)
builder.Services.AddScoped<ApplicationStateService>();

await builder.Build().RunAsync();
