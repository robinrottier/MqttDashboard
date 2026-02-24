# Service Registration Extension Method

## Overview
Created a centralized service registration extension method to eliminate code duplication across multiple Program.cs files.

## Implementation

### Extension Method Location
**File:** `BlazorApp1\Extensions\ServiceCollectionExtensions.cs`

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorApp1Services(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddScoped<ApplicationState>();
        services.AddScoped<LocalStorageService>();
        services.AddScoped<SignalRService>();

        return services;
    }
}
```

### Benefits

1. **Single Source of Truth** - All BlazorApp1 services are registered in one place
2. **Reduced Code Duplication** - No need to repeat service registrations in multiple projects
3. **Easier Maintenance** - Add/remove services in one location
4. **Consistency** - Ensures all projects use the same service configuration
5. **Cleaner Program.cs** - Simplified startup code

### Usage

All Program.cs files now use the extension method:

```csharp
builder.Services.AddBlazorApp1Services();
```

### Updated Files

1. **BlazorWebAppWasmOnly.Client\Program.cs** - WebAssembly client
2. **BlazorWebAppWasmOnly\Program.cs** - Server with MQTT services
3. **BlazorWebAppServerOnly\Program.cs** - Server-only project
4. **BlazorWasmStandalone\Program.cs** - Standalone WASM project

### Services Registered

- **MudBlazor Services** - UI component library
- **ApplicationState** (Scoped) - Shared application state with MQTT support
- **LocalStorageService** (Scoped) - Browser local storage access
- **SignalRService** (Scoped) - Real-time communication for MQTT

### Before vs After

**Before:**
```csharp
using MudBlazor.Services;
using BlazorApp1.Services;

builder.Services.AddMudServices();
builder.Services.AddScoped<ApplicationState>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<SignalRService>();
```

**After:**
```csharp
using BlazorApp1.Extensions;

builder.Services.AddBlazorApp1Services();
```

## Future Enhancements

Consider adding overloads or additional extension methods for:
- Configuration options (e.g., `AddBlazorApp1Services(Action<BlazorApp1Options> configure)`)
- Environment-specific registrations
- Feature flags to enable/disable specific services
- MQTT-specific services in a separate extension method

## Notes

- The extension method returns `IServiceCollection` to support method chaining
- Server projects can still add additional services (MQTT, SignalR Hub, etc.) after calling this method
- This follows the ASP.NET Core convention for service registration extensions
