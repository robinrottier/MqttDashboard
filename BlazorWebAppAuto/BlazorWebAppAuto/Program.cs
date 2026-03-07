using BlazorWebAppAuto.Components;
using BlazorApp1.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add all BlazorApp1 services with InteractiveAuto render mode
builder.AddBlazorApp1(BlazorRenderMode.InteractiveAuto);

var app = builder.Build();

// Configure the HTTP request pipeline with InteractiveAuto render mode
app.UseBlazorApp1<App>(BlazorRenderMode.InteractiveAuto);

app.Run();
