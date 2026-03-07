using MqttDashboard.WebApp.Components;
using MqttDashboard.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

var renderModeConfig = builder.Configuration["RenderMode"] ?? "Auto";
var renderMode = renderModeConfig.Equals("WebAssembly", StringComparison.OrdinalIgnoreCase)
    ? BlazorRenderMode.InteractiveWebAssembly
    : BlazorRenderMode.InteractiveAuto;

builder.AddMqttDashboard(renderMode);

var app = builder.Build();
app.UseMqttDashboard<App>(renderMode);
app.Run();
