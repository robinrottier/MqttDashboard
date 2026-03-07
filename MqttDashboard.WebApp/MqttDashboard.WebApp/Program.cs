using MqttDashboard.WebApp.Components;
using MqttDashboard.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add all MqttDashboard services with InteractiveAuto render mode
builder.AddMqttDashboard(BlazorRenderMode.InteractiveAuto);

var app = builder.Build();

// Configure the HTTP request pipeline with InteractiveAuto render mode
app.UseMqttDashboard<App>(BlazorRenderMode.InteractiveAuto);

app.Run();
