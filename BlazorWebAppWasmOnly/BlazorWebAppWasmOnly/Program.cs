using BlazorWebAppWasmOnly.Components;
using BlazorApp1.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add all BlazorApp1 services with InteractiveWebAssembly render mode
builder.AddBlazorApp1(BlazorRenderMode.InteractiveWebAssembly);

var app = builder.Build();

// Configure the HTTP request pipeline with InteractiveWebAssembly render mode
app.UseBlazorApp1<App>(BlazorRenderMode.InteractiveWebAssembly);

app.Run();
