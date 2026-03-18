using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MqttDashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMqttDashboardServices();

// Add HttpClient for API calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add SignalRService (HTTP WebSocket client, runs in browser)
builder.Services.AddScoped<ISignalRService, SignalRService>();

// Add DiagramService (needs HttpClient)
builder.Services.AddScoped<IDiagramService, DiagramService>();

// Add AuthService (needs HttpClient)
builder.Services.AddScoped<IAuthService, AuthService>();

await builder.Build().RunAsync();
