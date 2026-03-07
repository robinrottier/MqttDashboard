using MqttDashboard.WebApp.Components;
using MqttDashboard.Server.Extensions;
using Serilog;

// Configure Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext();

        // Default console output format: JSON in Production, readable in Development
        if (ctx.HostingEnvironment.IsProduction())
            config.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
        else
            config.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    });

    var renderModeConfig = builder.Configuration["RenderMode"] ?? "Auto";
    var renderMode = renderModeConfig.Equals("WebAssembly", StringComparison.OrdinalIgnoreCase)
        ? BlazorRenderMode.InteractiveWebAssembly
        : BlazorRenderMode.InteractiveAuto;

    builder.AddMqttDashboard(renderMode);

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    app.UseMqttDashboard<App>(renderMode);
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
