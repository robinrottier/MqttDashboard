using MqttDashboard.WebAppWasmOnly.Components;
using MqttDashboard.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add all MqttDashboard services with InteractiveWebAssembly render mode
builder.AddMqttDashboard(BlazorRenderMode.InteractiveWebAssembly);

var app = builder.Build();

// Configure the HTTP request pipeline with InteractiveWebAssembly render mode
app.UseMqttDashboard<App>(BlazorRenderMode.InteractiveWebAssembly);

app.Run();
