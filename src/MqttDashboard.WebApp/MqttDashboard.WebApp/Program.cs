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
    var renderMode = renderModeConfig.ToLowerInvariant() switch
    {
        "webassembly" => BlazorRenderMode.InteractiveWebAssembly,
        "server"      => BlazorRenderMode.InteractiveServer,
        _             => BlazorRenderMode.InteractiveAuto
    };

    // Resolve data directory early (same logic as DashboardStorageService) so user settings
    // are stored in the volume-mounted data dir rather than the ephemeral container root.
    static string ResolveDataDir(IConfiguration config, string contentRoot)
    {
        var envDir = Environment.GetEnvironmentVariable("DIAGRAM_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envDir)) return envDir;
        var cfgDir = config["DiagramStorage:DataDirectory"];
        if (!string.IsNullOrWhiteSpace(cfgDir)) return Path.GetFullPath(cfgDir, contentRoot);
        return Path.Combine(contentRoot, "Data");
    }

    var dataDir = ResolveDataDir(builder.Configuration, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(dataDir);

    // One-time migration: move appsettings.user.json from ContentRoot to data dir
    var oldUserSettings = Path.Combine(builder.Environment.ContentRootPath, "appsettings.user.json");
    var newUserSettings = Path.Combine(dataDir, "appsettings.user.json");
    if (File.Exists(oldUserSettings) && !File.Exists(newUserSettings))
    {
        File.Copy(oldUserSettings, newUserSettings);
        Log.Information("Migrated appsettings.user.json to data directory {Dir}", dataDir);
    }

    // Load user-specific settings (e.g. admin password hash set via setup page) from data dir
    var settingsFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dataDir);
    builder.Configuration.AddJsonFile(settingsFileProvider, "appsettings.user.json", optional: true, reloadOnChange: true);

    // Home Assistant add-on support: /data/options.json is written by the HA supervisor
    // with add-on configuration. Map known keys to our environment variables.
    var haOptionsPath = "/data/options.json";
    if (File.Exists(haOptionsPath))
    {
        try
        {
            var json = File.ReadAllText(haOptionsPath);
            var haOptions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
            if (haOptions != null)
            {
                void SetIfPresent(string key, string envVar)
                {
                    if (haOptions.TryGetValue(key, out var val))
                        Environment.SetEnvironmentVariable(envVar, val.ToString());
                }
                SetIfPresent("mqtt_broker",   "MqttSettings__Broker");
                SetIfPresent("mqtt_port",     "MqttSettings__Port");
                SetIfPresent("mqtt_username", "MqttSettings__Username");
                SetIfPresent("mqtt_password", "MqttSettings__Password");
            }
            Log.Information("Home Assistant options loaded from {Path}", haOptionsPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read Home Assistant options from {Path}", haOptionsPath);
        }
    }

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
