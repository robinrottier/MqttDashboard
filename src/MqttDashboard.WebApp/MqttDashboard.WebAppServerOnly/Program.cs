using MqttDashboard.WebAppServerOnly.Components;
using MqttDashboard.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add all MqttDashboard services with InteractiveServer render mode
builder.AddMqttDashboard(BlazorRenderMode.InteractiveServer);

var app = builder.Build();

// Configure the HTTP request pipeline with InteractiveServer render mode
app.UseMqttDashboard<App>(BlazorRenderMode.InteractiveServer);

// Enable status code pages for not found routes (optional, specific to this project)
// app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.Run();
