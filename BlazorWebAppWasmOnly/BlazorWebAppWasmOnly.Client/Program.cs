using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorApp1.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddBlazorApp1Services();

await builder.Build().RunAsync();
