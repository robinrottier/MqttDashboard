using BlazorWebAppServerOnly.Components;
using BlazorApp1.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add all BlazorApp1 services with InteractiveServer render mode
builder.AddBlazorApp1(BlazorRenderMode.InteractiveServer);

var app = builder.Build();

// Configure the HTTP request pipeline with InteractiveServer render mode
app.UseBlazorApp1<App>(BlazorRenderMode.InteractiveServer);

// Enable status code pages for not found routes (optional, specific to this project)
// app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.Run();
