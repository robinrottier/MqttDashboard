# Copilot Instructions for MqttDashboard

## Build & Test Commands

```bash
# One-time setup
dotnet workload install wasm-tools

# Build
dotnet build MqttDashboard.slnx

# Run all tests
dotnet test MqttDashboard.slnx

# Run a single test project
dotnet test tests/MqttDashboard.Client.Tests/MqttDashboard.Client.Tests.csproj
dotnet test tests/MqttDashboard.Server.Tests/MqttDashboard.Server.Tests.csproj

# Run with Docker (builds from source)
docker compose up --build
```

> **Note:** `dotnet build` may fail with file-lock errors while Visual Studio has the solution open. Build the `MqttDashboard.Client` project directly as a workaround, or build from within VS.

---

## Architecture Overview

```
Browser (InteractiveAuto — WASM preferred, server fallback)
  └─ Blazor Client (MqttDashboard.Client RCL)
       └─ SignalR WebSocket
            └─ ASP.NET Core Server (MqttDashboard.Server)
                 └─ MQTT (MQTTnet 5) → Broker
```

**Projects:**
- **`MqttDashboard.Client`** — Razor class library: all pages, widgets, models, client services. Shared between both hosts.
- **`MqttDashboard.Server`** — Server-side services: MQTT client, SignalR hub, REST API controllers, dashboard file storage.
- **`MqttDashboard.WebApp`** — Primary Blazor host (`InteractiveAuto` render mode, WASM + server-side).
- **`MqttDashboard.WebApp.Client`** — WASM-side entry point for the WebApp host.
- **`MqttDashboard.WebAppServerOnly`** — Alternate Blazor Server-only host (no WASM download; suited to Raspberry Pi).

**Data flow (MQTT → widget):**
1. `MqttClientService` receives a message from the broker.
2. `MqttDataHub` (SignalR) broadcasts it to connected clients.
3. Client `SignalRService` updates `MqttDataCache` (concurrent dict + watcher callbacks).
4. `BaseNodeWithDataWidget.SetupDataWatchers()` subscribed via `DataCache.Watch()` — fires `OnData1ReceivedCore()` + `TriggerLinkAnimation()` + `StateHasChanged()`.

**Diagram engine:** `rrSoft.Blazor.Diagrams` (fork of Blazor.Diagrams). Nodes are `MudNodeModel` subclasses; ports are `MudPortModel`; links are `LinkModel`. The central orchestrator is `ApplicationState.cs` (~800 lines).

---

## Adding a New Node/Widget Type

Follow this checklist — every existing widget (Gauge, Log, Switch, Battery, TreeView, Image, Grid) uses the same pattern.

### 1. Create the model — `src/MqttDashboard.Client/Models/`

```csharp
public class MyNodeModel : MudNodeModel
{
    public MyNodeModel(Point? position = null) : base(position)
    {
        NodeType = "My";          // unique discriminator string
    }
    public string MyProp { get; set; } = "default";
}
```

### 2. Add persistence fields — `Models/DiagramState.cs` (`NodeState` class)

```csharp
public string? MyProp { get; set; }
```

### 3. Create the widget — `src/MqttDashboard.Client/Widgets/MyNodeWidget.razor`

```razor
@inherits BaseNodeWithDataWidget<MyNodeModel>

<div @ondblclick="OnDoubleClick">
    @Node.DataValue
    @foreach (var port in Node.Ports)
    {
        <PortRenderer @key="port" Port="port" Style="@PortStyle(port as MudPortModel)" />
    }
</div>

@code {
    protected override void OnData1Updated() => StateHasChanged();
}
```

Use `BaseNodeWidget<T>` instead of `BaseNodeWithDataWidget<T>` only if the node never subscribes to MQTT topics.

### 4. Register in `ApplicationState.cs`

In **`CreateDiagramFromState()`** — add a `case` in the `NodeType` switch and call `diagram.RegisterComponent<MyNodeModel, MyNodeWidget>()`.

In **`GetDiagramState()`** — add an `else if (node is MyNodeModel m)` block to populate `NodeState.MyProp`.

### 5. Wire up the UI

- **`NodePropertyEditor.razor`** — add an `@if (Node is MyNodeModel)` section for type-specific properties.
- **`NodePropertyEditor.razor.cs`** — add any helper methods.
- **`NodeTypePickerDialog.razor`** — add an entry to the type picker.
- **`AppMenu.razor`** — add an "Add → My Node" menu item calling `AddNode("My")`.
- **`Display.razor.cs` `AddNode()`** — add a `case "My"` that calls `AppState.AddNodeToActivePage(new MyNodeModel(...))`.

---

## Key Conventions

### Widget base classes
- `BaseNodeWithDataWidget<T>` — for nodes that watch MQTT topics. Manages `DataCache.Watch()` subscriptions, link animation, `DataValue`/`DataValue2`.
- `BaseNodeWidget<T>` — raw base, no MQTT wiring. Use when the widget manages its own subscriptions (e.g. `GridNodeWidget` uses per-cell watchers).
- Never mutate `_entries` or other render-thread collections in place from MQTT callbacks — build a new collection and assign the reference atomically.

### State persistence
- `DiagramState` / `PageState` / `NodeState` (in `Models/DiagramState.cs`) are the serialisable models written to `data/diagram.json`.
- `ApplicationState.GetDiagramState()` serialises; `CreateDiagramFromState()` deserialises.
- `NodeState.NodeType` is the discriminator for round-tripping polymorphic models.
- Legacy single-page files have null `Pages`; the multi-page code handles this transparently.

### Link animations
- Controlled by `MudNodeModel.LinkAnimation` ("None" | "Forward" | "Reverse").
- `TriggerLinkAnimation()` in `BaseNodeWithDataWidget` sets `link.Animations[0].To` based on the sign of `DataValue`. Call it whenever data arrives or when seeding from cache.
- `ApplicationState.CheckForLinkAnimation()` adds the `AnimateModel` to a link at creation time.

### Undo/redo
- Call `AppState.PushUndoSnapshot()` before any destructive edit. Max depth 20.
- Located in `ApplicationState.cs`.

### JavaScript interop
- Only `localStorage` is used (via `LocalStorageService`). Everything else is pure Blazor/SignalR.

### CSS
- MudBlazor theme handles most styling.
- Global overrides: `src/MqttDashboard.WebApp/MqttDashboard.WebAppServerOnly/wwwroot/app.css`.
- Scoped widget styles: `Widgets/*.razor.css` files (CSS isolation).
- Client-specific styles: `src/MqttDashboard.Client/wwwroot/app.css` (menu/submenu rules).

### Configuration
- Environment variables use `__` as separator (e.g. `MqttSettings__Broker`).
- User-editable runtime settings persist to `data/appsettings.user.json` (not the main `appsettings.json`).

### Render mode awareness
- `AppState.IsInteractive` gates diagram rendering. The canvas (`DiagramCanvas`) is only rendered once `IsInteractive` is true to avoid SSR/WASM handoff flicker.
- `@rendermode InteractiveAuto` is the default for pages.
