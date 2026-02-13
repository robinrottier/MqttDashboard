# Moving SignalR Hub to Shared Library

## Summary

Successfully moved the SignalR Hub and MQTT Topic Subscription Manager from the server-specific project (`BlazorWebAppWasmOnly`) to the shared `BlazorApp1` library, making them reusable across multiple server projects.

## Changes Made

### 1. Updated BlazorApp1.csproj

**Removed:**
- `<SupportedPlatform Include="browser" />` restriction
- `Microsoft.AspNetCore.Components` package (redundant)

**Added:**
- `Microsoft.AspNetCore.SignalR.Core` (v1.1.0) - Provides Hub base class
- `DefineConstants` with `SERVER_SIDE` for conditional compilation

**Final Configuration:**
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <PropertyGroup>
    <DefineConstants>$(DefineConstants);SERVER_SIDE</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Core" Version="1.1.0" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.3" />
    <PackageReference Include="MudBlazor" Version="8.15.0" />
  </ItemGroup>
</Project>
```

### 2. Moved Files to BlazorApp1\Hubs\

**From:** `BlazorWebAppWasmOnly\BlazorWebAppWasmOnly\Hubs\`  
**To:** `BlazorApp1\Hubs\`

Files moved:
- `MqttDataHub.cs` - SignalR Hub for MQTT topic subscriptions
- `MqttTopicSubscriptionManager.cs` - Reference-counted topic subscription manager

**Namespace Changes:**
- Old: `BlazorWebAppWasmOnly.Hubs`
- New: `BlazorApp1.Hubs`

### 3. Updated Server Project References

**File:** `BlazorWebAppWasmOnly\BlazorWebAppWasmOnly\Program.cs`

```csharp
// Before
using BlazorWebAppWasmOnly.Hubs;
using BlazorWebAppWasmOnly.Services;

// After
using BlazorApp1.Hubs;
using BlazorWebAppWasmOnly.Services;
```

**File:** `BlazorWebAppWasmOnly\BlazorWebAppWasmOnly\Services\MqttClientService.cs`

```csharp
// Before
using BlazorWebAppWasmOnly.Hubs;

// After
using BlazorApp1.Hubs;
```

### 4. Removed Duplicate Files

Deleted from `BlazorWebAppWasmOnly\BlazorWebAppWasmOnly\`:
- `Hubs\MqttDataHub.cs`
- `Services\MqttTopicSubscriptionManager.cs`

## Architecture

### BlazorApp1 - Shared Library

Now contains both client-side and server-side components:

**Client-Side:**
- Blazor components (`.razor` files)
- `SignalRService` (SignalR client)
- `ApplicationState`
- `LocalStorageService`

**Server-Side:**
- `MqttDataHub` (SignalR Hub)
- `MqttTopicSubscriptionManager`

### BlazorWebAppWasmOnly - Server Project

**Retains:**
- `MqttClientService` (BackgroundService for MQTT broker connection)
- Server-specific configuration and startup

**References:**
- BlazorApp1 library for Hub and shared components

## Benefits

1. **Code Reusability** - Hub can be used by multiple server projects
2. **Single Source of Truth** - Hub logic in one location
3. **Easier Maintenance** - Update Hub in one place
4. **Consistency** - All servers use the same Hub implementation
5. **Type Safety** - Shared types between client and server

## Compatibility

✅ **WebAssembly Client** - Can still reference BlazorApp1 for components and client services  
✅ **Server Projects** - Can reference BlazorApp1 for Hub and shared components  
✅ **SignalR Client** - Uses `Microsoft.AspNetCore.SignalR.Client` (works in WASM)  
✅ **SignalR Hub** - Uses `Microsoft.AspNetCore.SignalR.Core` (works on server)

## Package Strategy

- **SignalR.Client** (10.0.3) - For WebAssembly clients to connect to hubs
- **SignalR.Core** (1.1.0) - For Hub base class (server-side)
- No framework reference needed - Package approach works cross-platform

## Important Notes

⚠️ **Do Not Add** `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to BlazorApp1  
- This causes "browser-wasm" runtime pack errors when WebAssembly projects reference it
- Use package references instead

⚠️ **SERVER_SIDE Constant**  
- Defined for potential conditional compilation
- Can be used to exclude server-only code from client builds if needed

## Testing Checklist

- [x] Build successful
- [ ] Server project can inject MqttDataHub
- [ ] SignalR Hub works correctly
- [ ] MQTT topic subscriptions work
- [ ] WebAssembly client can connect to Hub
- [ ] Multiple clients can subscribe to topics
- [ ] Reference counting works correctly

## Future Considerations

1. **Multi-Targeting** - Could target `net10.0` and `net10.0-browser` separately
2. **Conditional Compilation** - Use `#if SERVER_SIDE` if needed
3. **Separate Projects** - Consider splitting into BlazorApp1.Client and BlazorApp1.Server if complexity grows
