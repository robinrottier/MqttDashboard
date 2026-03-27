# Developer Changelog

Detailed record of each Copilot-assisted work session — what was investigated, changed, and why.
For reviewing work item by item and moving anything back to [TODO.md](TODO.md) if needed.

---

## 2026-03-26 (batch 4) — Read-only mode + RenderMode=Server

### Commit: (see git log) · UTC 2026-03-26 · branch: develop

### Read-only runtime mode (`ReadOnly=true`)

**Problem:** No way to deploy a public view-only instance without exposing edit controls.

**Files changed:**
- `src/MqttDashboard.Server/Controllers/AuthController.cs` — `GET /api/auth/status` now includes a `readOnly` field; when `ReadOnly=true` the response always returns `{ isAdmin: false, authEnabled: false, readOnly: true }` regardless of other config
- `src/MqttDashboard.Server/Filters/RequireAdminFilter.cs` — returns HTTP 403 with error message when `ReadOnly: true`; checked before auth (no one can write in read-only mode)
- `src/MqttDashboard.Server/Controllers/SettingsController.cs` — `POST /api/settings/startup` returns 403 when read-only (has its own inline auth check, so had to add the read-only guard here too)
- `src/MqttDashboard.Server/Services/ServerAuthService.cs` — `GetStatusAsync()` returns `(bool isAdmin, bool authEnabled, bool readOnly)` 3-tuple; reads `ReadOnly` from `IConfiguration`
- `src/MqttDashboard.Client/Services/IAuthService.cs` — `GetStatusAsync()` return type changed to `(bool isAdmin, bool authEnabled, bool readOnly)`
- `src/MqttDashboard.Client/Services/AuthService.cs` — parses `ReadOnly` from JSON response; `AuthStatusResponse` record extended
- `src/MqttDashboard.Client/Services/ApplicationState.cs` — added `IsReadOnly` property; `SetAuthState()` gains `readOnly = false` parameter
- `src/MqttDashboard.Client/Services/MqttInitializationService.cs` — passes `readOnly` from auth status to `SetAuthState()`
- `src/MqttDashboard.Client/Layout/MainLayout.razor` — login/logout buttons and edit-mode toggle both wrapped in `!AppState.IsReadOnly`; setup alert also hidden in read-only mode
- `src/MqttDashboard.WebApp/MqttDashboard.WebApp/appsettings.json` — added `"ReadOnly": false`
- `src/MqttDashboard.WebApp/MqttDashboard.WebAppServerOnly/appsettings.json` — added `"ReadOnly": false`

**How it works:** Set `ReadOnly=true` as environment variable or in `appsettings.user.json`. The server blocks all write API calls (403) and reports read-only to the client. The client hides the edit toggle, login/logout buttons, and admin setup alert. No username/password is needed or shown — anyone accessing the URL gets the live view only.

### RenderMode=Server support

**Problem:** The single WebApp Docker image only supported `Auto` and `WebAssembly` render modes. There was no way to run Blazor Server-only (no WASM download) from the standard image.

**Files changed:**
- `src/MqttDashboard.WebApp/MqttDashboard.WebApp/Program.cs` — render mode switch changed from binary `WebAssembly`/`Auto` to a proper switch expression supporting `"Auto"` (default), `"WebAssembly"`, and `"Server"` → `BlazorRenderMode.InteractiveServer`
- `docker-compose.yml` — added commented env var examples for `RenderMode` and `ReadOnly`
- `docker-compose.production.yml` — same

**How it works:** Set `RenderMode=Server` to run the WebApp image in pure Blazor Server mode (no WASM bundle downloaded by clients). Useful for Raspberry Pi or other low-memory clients. The WASM bundle is still built into the image but never delivered.

### Minor cleanup

- `src/MqttDashboard.Client/Components/AboutDialog.razor` — removed unused `_restarting` field (it was always set but never read after the update banner removal; was causing a CS0414 warning)

---

## 2026-03-26 (batch 3) — TreeView overhaul + import dialog + padding

### Commit: (see git log) · UTC 2026-03-26 · branch: develop

---

### Fix: TreeView collapse/focus loss + replace MudTreeView with custom rendering (`TreeViewNodeWidget.razor`)

**Problem:** `MudTreeView`/`MudTreeViewItem` are stateful components. Every call to `StateHasChanged` caused MudBlazor to reconcile the component tree. For rapid MQTT bursts this resulted in constant re-rendering that visually collapsed the tree (MudBlazor's internal expanded state was reset) and lost keyboard focus.

**Fix:** Replaced `MudTreeView`/`MudTreeViewItem` entirely with a custom lightweight div-based recursive renderer (`RenderNode`). Expansion state is stored on the `TreeNode` model objects (`Expanded` bool), not inside any MudBlazor component — so re-renders never lose state.

Added an **80 ms debounce timer** (`_debounceTimer`): `OnTopicChanged` now starts/restarts this timer rather than calling `StateHasChanged` directly. Rapid bursts are coalesced into a single render.

Added a **highlight-clear timer** (`_highlightClearTimer`): 2.2 s after the last update fires, triggers one more `StateHasChanged` to clear the highlight colouring (previously highlights could linger until the next MQTT message arrived).

**Files:** `src/MqttDashboard.Client/Widgets/TreeViewNodeWidget.razor`, `.razor.css`

---

### Change: TreeView root topic merged into base DataTopics (`TreeViewNodeModel.cs`)

**Problem:** `TreeViewNodeModel` had a bespoke `RootTopic` property separate from the standard `DataTopics` list, duplicating the concept and appearing as an extra "Root Topic" field in the property editor (via `[NpText]`) while the standard topic list remained empty and unused.

**Fix:** Removed `RootTopic` from `TreeViewNodeModel`. The widget now reads `Node.DataTopics[0]` via a `RootTopicValue` computed property. Users set the root topic via the standard MQTT Topics section in the property editor (same as all other widgets).

**Backward compat:** `TreeViewNodeData.RootTopic` remains in the data model (read-only for load). `FromData()` migrates: if `data.RootTopic` is non-empty and `DataTopics` is empty, `RootTopic` is added as `DataTopics[0]`. No data loss from existing saved files.

**Files:** `src/MqttDashboard.Client/Models/TreeViewNodeModel.cs`, `TreeViewNodeWidget.razor`

---

### Enhancement: TreeView visual improvements (`TreeViewNodeWidget.razor.css`)

With the custom renderer, full layout control is available:
- Font reduced to **0.7 rem** (down from 0.75 rem)
- Each tree row is a flex row: label left (ellipsis on overflow), value **bold + primary colour** right-aligned
- **2 s highlight** on updated topics via `tv-highlighted` CSS class (background warning color, 0.3 s transition). Cleared automatically by `_highlightClearTimer`
- Expand/collapse icons (▾ / ▸) and leaf bullet (•) via unicode HTML entities; `tv-children` indented 12 px
- Hover uses `--mud-palette-action-hover` for consistency with MudBlazor theming

---

### Fix: Import dialog doesn't grow when status alert appears (`ImportNodesDialog.razor`)

**Problem:** When valid JSON was pasted, a `MudAlert` appeared below the textarea, expanding the dialog height and causing layout shifts/scrollbars.

**Fix:** Wrapped the conditional alert in `<div style="min-height:40px;">`. The reserved height matches a Dense MudAlert, so the dialog occupies the same vertical space whether or not an alert is visible.

**Files:** `src/MqttDashboard.Client/Components/ImportNodesDialog.razor`

---

### Fix: TreeView and Log widget internal padding (`*.razor`, `*.razor.css`)

- **Log:** header row padding reduced from `2px 4px 0` → `1px 3px 0`; added CSS rule `::deep .mud-table-root th, td { padding: 2px 4px }` to override MudBlazor's default (typically 12px) cell padding
- **TreeView:** custom CSS classes use minimal padding (1–2 px per row); no more `padding:2px 0` wrapper div

**Files:** `src/MqttDashboard.Client/Widgets/LogNodeWidget.razor`, `LogNodeWidget.razor.css`, `TreeViewNodeWidget.razor.css`

---

### Commit: (see git log) · UTC 2026-03-26 · branch: develop

---

### Bug fix: node height loop — proper root cause fix (`TextNodeModel.cs`)

**Root cause (confirmed):** `NodeRenderer` in rrSoft.Blazor.Diagrams 0.1.2 attaches a JS `ResizeObserver` on the `.diagram-node` wrapper element whenever `Node.ControlledSize` is `false` (the default). The observer calls `OnResize(getBoundingClientRect())` after every render. Because `getBoundingClientRect()` can include sub-pixel rounding and zoom-division noise, the reported size may differ slightly from the stored `Node.Size`, triggering a re-render → re-measure loop that manifests as slow indefinite height growth — particularly on nodes without a title (where there is no stable text anchor).

**Previous (incorrect) fix:** `StandardNodeLayout` kept the title `<div>` in the DOM with `display:none` to stabilise the DOM structure. This was addressing a symptom rather than the cause and did not stop the observer loop.

**Correct fix:** Convert `TextNodeModel` from primary-constructor syntax to a regular constructor and set `ControlledSize = true` in the body. All our nodes set explicit `Node.Size` in `OnInitialized` and the user can resize via the drag handle (which calls `NodeModel.SetSize()` directly, unaffected by `ControlledSize`). The `init` accessor on `ControlledSize` is accessible from derived-class constructors per the C# spec ("init context" extends to derived constructors).

**Reverted:** The `TitleDivFullStyle`/`display:none` workaround in `StandardNodeLayout` has been removed; the original clean `@if (… && HasTitleContent)` guards are restored.

**Future:** A TODO comment has been left in `rrSoft.Blazor.Diagrams/NodeModel.cs` suggesting `ControlledSize` be changed to `{ get; protected set; }` in a future library version for clarity.

**Files:** `src/MqttDashboard.Client/Models/TextNodeModel.cs`, `src/MqttDashboard.Client/Widgets/StandardNodeLayout.razor`

---

### Bug fix: grid shown in view mode (`Display.razor.cs`)

**Problem:** When switching from edit → view mode, `_diagram.Options.GridSize` was left at the edit-mode value, so the grid dotted background remained visible in view mode.

**Fix:** Added `_diagram.Options.GridSize = null;` to the `else` branch of `SwitchMode` (the path taken when `enterEditMode` is false).

**Files:** `src/MqttDashboard.Client/Pages/Display.razor.cs`

---

### Bug fix: Import dialog "Import" button never enabled (`ImportNodesDialog.razor`)

**Problem:** The `MudTextField` had three conflicting data-binding attributes: `@bind-Value="_json"`, `Immediate="true"`, and `@oninput="OnJsonChanged"`. MudBlazor's `Immediate` mode generates its own internal `oninput` handler; adding a second `@oninput` created a race between the two handlers. In practice `_json` was sometimes not updated before `TryParse()` ran, so `_parsed` stayed null and the Import button remained disabled.

**Fix:** Replaced the triple with `Value="@_json" ValueChanged="@OnValueChanged"` (no `@oninput`, no `Immediate`). `OnValueChanged(string v)` sets `_json = v` and calls `TryParse()` — single, deterministic update path. Also renamed `OnJsonChanged` → `OnValueChanged` to match the MudBlazor pattern.

**Files:** `src/MqttDashboard.Client/Components/ImportNodesDialog.razor`

---

### Change: Import / Export moved to File menu (`AppMenu.razor`)

**Change:** Export… and Import… items moved from the Edit submenu to the File submenu (below Dashboard Properties). Both remain gated on `IsEditMode`.

**Files:** `src/MqttDashboard.Client/Layout/AppMenu.razor`

---

### Commit: (see git log) · UTC 2026-03-26 · branch: develop

---

### Bug fix: node without title grows indefinitely (`StandardNodeLayout.razor`)

**Problem:** `StandardNodeLayout` guarded both title `<div>` elements with `@if (… && HasTitleContent)`. When `Title` and `Icon` are both empty, the div was removed from the DOM. Blazor.Diagrams measures node height after each render; the DOM change caused a size change which triggered another render, creating an infinite grow loop.

**Fix:** Changed `@if (ShowTitle && ShowTitleFirst && HasTitleContent)` to `@if (ShowTitle && ShowTitleFirst)` (and equivalent for the bottom position). Added a `TitleDivFullStyle` computed property that appends `;display:none` to `TitleDivStyle` when `!HasTitleContent`. The div is always in the DOM; the browser reserves no space for it when hidden — so the node renders at the correct size with no spurious remeasure events.

**Files:** `src/MqttDashboard.Client/Widgets/StandardNodeLayout.razor`

---

### Bug fix: grid snap-to-centre not saved/restored; negative-value convention removed

**Problem (1):** `CreateDiagramFromPageData` set `options.GridSize = int.Abs(page.GridSize)` (always positive) and then set `options.GridSnapToCenter = options.GridSize < 0` — which was always false. So snap-to-centre was never restored from file.

**Problem (2):** `GetPageData` serialised snap-to-centre as a negative `GridSize` (e.g. `-20`). Reading this back correctly would have required keeping the sign, but it was stripped by `int.Abs` first.

**Fix:**
- `DashboardPageModel`: added `GridSnapToCenter bool` field (default false). `GridSize` default raised from 10 → 20.
- `ApplicationState.GridSnapToCenter` property added.
- `SetGridSize(int)`: in edit mode, clamps to 5–100 and rounds to nearest 5. Also applies `GridSnapToCenter` to the diagram.
- New `SetGridSnapToCenter(bool)` method.
- `CreateDiagramFromPageData`: reads `page.GridSnapToCenter` directly; clamps `GridSize` to 5–100.
- `GetPageData`: writes positive `GridSize` and `GridSnapToCenter` separately.
- `Display.razor.cs` entering-edit-mode path: uses `page.GridSnapToCenter` instead of `savedGs < 0`.
- `Display.razor.cs` `BuildFullState`: copies `GridSnapToCenter` from `_pageStates`.
- `DashboardPropertiesDialog`: `OnInitialized` reads `AppState.GridSnapToCenter`; `ApplyAsync` calls `SetGridSnapToCenter` + `SetGridSize` separately (no negative-value encoding). Min raised from 0 → 5 in the numeric field. Caption updated.

**Files:** `src/MqttDashboard.Client/Models/DashboardModel.cs`, `src/MqttDashboard.Client/Services/ApplicationState.cs`, `src/MqttDashboard.Client/Pages/Display.razor.cs`, `src/MqttDashboard.Client/Components/DashboardPropertiesDialog.razor`

⚠️ Breaking file format change: old files with negative `gridSize` will load as positive (snap-to-centre will default off). Acceptable per dev notes.

---

### Feature FEAT-E: clipboard import/export (Node-Red style)

**Overview:** Users can now export nodes or a whole page as JSON text, and import that JSON back (onto the current page or a new page). The UX mirrors Node-RED's import/export flow.

#### New files
- `src/MqttDashboard.Client/Models/ImportResult.cs` — `ImportResult` record (`Nodes`, `Links`, `AddAsNewPage`).
- `src/MqttDashboard.Client/Components/ExportNodesDialog.razor` — shows JSON in a `Lines=18` read-only textarea. Mode selector: "Selected nodes (N)" (disabled if none selected) or "Current page". Copy button writes to OS clipboard via `mqttClipboard.writeText`.
- `src/MqttDashboard.Client/Components/ImportNodesDialog.razor` — textarea for pasting JSON; "Paste from clipboard" icon button; auto-detects format (`mqttdashboard:"nodes"` or `mqttdashboard:"page"`); shows detected node/link count; destination radio (current page / new page); Import button disabled until valid JSON detected.

#### JSON formats
- Nodes: `{"mqttdashboard":"nodes","data":[...NodeData...]}` (existing copy/paste format)
- Page: `{"mqttdashboard":"page","data":{...DashboardPageModel...}}` (new)

#### Modified files
- `src/MqttDashboard.Client/Services/ApplicationState.cs` — added `MenuExportNodes`, `MenuImportNodes` events; `TriggerExportNodes()`, `TriggerImportNodes()`.
- `src/MqttDashboard.Client/Layout/AppMenu.razor` — added "Export…" and "Import…" items after Cut/Copy/Paste in the Edit menu; added `MenuExportNodes()` and `MenuImportNodes()` handlers.
- `src/MqttDashboard.Client/Pages/Display.razor.cs`:
  - `_onMenuExportNodes` / `_onMenuImportNodes` stored action fields.
  - `SubscribeEditEvents` / `UnsubscribeEditEvents` updated.
  - `ExportNodesAsync()` — captures selected nodes + current page data, opens `ExportNodesDialog`.
  - `ImportNodesAsync()` — opens `ImportNodesDialog`; on result with `AddAsNewPage=true` creates a new `DashboardPageModel` and calls `SwitchToPageAsync`; on `AddAsNewPage=false` pastes nodes into the current diagram (same logic as `PasteNodesAsync`).

---

## 2026-03-25 — Bug fixes: menu cleanup, no-data banner, grid, restart

### Commit: (see git log)

### Bug fixes

#### No-data message → top banner (`Display.razor`)
- Changed the "no data topics" notification from a centred floating `MudPaper` card to a `MudAlert` banner anchored at the top of the canvas (`position:absolute;top:0;left:0;right:0`).
- Edit-mode shows an action button "Configure Topics" that opens Dashboard Properties. View-mode shows a plain info text.

#### Removed stale menu items (`AppMenu.razor`)
- **Options > Show > Dashboard Name** removed — "Show Dashboard Name" is now a checkbox in the Dashboard Properties dialog.
- **Page > Home** menu (and the entire `Page` submenu) removed — navigation to "/" was the only item and it isn't needed now. `IsCurrentPage()` helper also removed.

#### `appsettings.user` excluded from dashboard list (`DashboardStorageService.cs`)
- `ListDiagramNamesAsync()` now filters out `"appsettings.user"` in addition to empty names.
- `MigrateLegacyDashboardFiles()` excludes `"appsettings.user.json"` from being moved to the `dashboards/` subdirectory.

#### GridSize restored to Dashboard Properties dialog (`DashboardPropertiesDialog.razor`)
- Added a `MudNumericField` (0–100 px, step 5) for grid size and a "Snap to cell centre" checkbox.
- Reads and writes `AppState.GridSize` using the existing sign convention (negative = snap-to-centre).
- `ApplyAsync` calls `AppState.SetGridSize(newGridSize)` so the live diagram updates immediately.

#### New/empty diagrams default to grid-enabled (`ApplicationState.cs`)
- `CreateDiagramFromPageData`: when `page.GridSize == 0` (no saved grid), now defaults to 20 instead of `null`. This ensures edit-mode always gets a grid for new/unsaved diagrams.

#### Restart button in About dialog (`AboutDialog.razor`)
- Added a "Restart App" button that is always visible to admin users on Docker deployments (not only when an update is available).
- Calls `POST /api/update/restart` (existing endpoint). Connection loss after the call is expected and silently swallowed.

---



### Commit: 30b6e69 (completes b6005f1)

### Problem
`NodeState` was a ~50-field flat DTO covering all node types. `ApplicationState.GetDiagramState()` and `CreateDiagramFromState()` had ~100-line manual switch/case blocks duplicating every node property. Adding a new node type required editing 6+ files.

### What changed

#### New `DashboardModel.cs` (`src/MqttDashboard.Client/Models/`)
- Complete serializable POCO hierarchy: `DashboardModel` → `DashboardPageModel` → `List<NodeData>` (polymorphic, STJ `[JsonPolymorphic]`) → typed subclasses: `TextNodeData`, `GaugeNodeData`, `SwitchNodeData`, `BatteryNodeData`, `LogNodeData`, `TreeViewNodeData`.
- Nested value types: `NumericRangeData`, `ColorTransitionData`, `ColorThresholdData`, `SwitchSettingsData`, `LogColumnsData`, `NodePortData`, `LinkData`, `DashboardFileInfo`.
- No manual switch/case needed in serialization path — STJ handles polymorphism via `nodeType` discriminator.

#### Runtime model renames
- `MudNodeModel` → `TextNodeModel` (in `MudNodeModel.cs`). `MudNodeModel` kept as `[Obsolete]` alias.
- `MudPortModel` → `NodePortModel` (in `MudPortModel.cs`). `MudPortModel` kept as `[Obsolete]` alias.

#### Per-node serialization (`ToData()` / `FromData()`)
Each node type now owns its own serialization:
- `TextNodeModel`, `GaugeNodeModel`, `SwitchNodeModel`, `BatteryNodeModel`, `LogNodeModel`, `TreeViewNodeModel` — all implement `NodeData ToData()` and `static T FromData(XxxNodeData)`.
- `ColorTransitionHelper` added to `ColorTransition.cs` for round-tripping `ColorTransition` ↔ `ColorTransitionData`.

#### `ApplicationState.cs`
- `GetDiagramState()` and `CreateDiagramFromState()` deleted.
- Replaced by `GetPageData()` (returns `DashboardPageModel`) and `CreateDiagramFromPageData(DashboardPageModel, bool)`.
- `ApplyDashboardModel(DashboardModel)` — applies top-level Name/ShowDiagramName/MqttSubscriptions.
- Clipboard type: `List<NodeState>` → `List<NodeData>`. Undo stack: `Stack<DiagramState>` → `Stack<DashboardPageModel>`.

#### `Display.razor.cs`
- `_pageStates: List<DiagramState>` → `List<DashboardPageModel>`.
- `_editSnapshot: DiagramState?` → `DashboardModel?`.
- All page-switch, undo/redo, save, cut/copy/paste, add-node methods updated to use new types.
- `AddNode()` uses `TextNodeModel` instead of `MudNodeModel`.
- `UpdateSelectionState()` uses `NodePortModel` instead of `MudPortModel`.

#### Widget base classes and Razor components
- `BaseNodeWidget<TNode>` and `BaseNodeWithDataWidget<TNode>` — type constraint changed from `MudNodeModel` to `TextNodeModel`; `PortStyle` parameter from `MudPortModel` to `NodePortModel`.
- `NodePropertyEditor.razor.cs` — `[Parameter] Node` type changed to `TextNodeModel`.
- `StandardNodeLayout.razor`, `MudNodeWidget.razor`, `DataValueTooltipContent.razor`, `LogNodeWidget.razor`, `TreeViewNodeWidget.razor` — updated to `TextNodeModel`/`NodePortModel`.
- `ColorTransitionGroupEditor.razor`, `NumericRangeEditor.razor`, `NodePropertyRenderer.razor` — `[Parameter] Node` type changed to `TextNodeModel`.
- `NodePropertyAttributes.cs` — updated `NpCustomAttribute` doc comment.

#### Service/controller/test files
- `IDashboardService.cs`, `DashboardService.cs`, `ServerDashboardService.cs`, `DashboardStorageService.cs`, `DashboardController.cs` — all `DiagramState` → `DashboardModel` throughout.
- `DiagramStorageServiceTests.cs` — test updated to build `DashboardModel`+`DashboardPageModel`+`TextNodeData` instead of `DiagramState`+`NodeState`.

#### Deleted
- `src/MqttDashboard.Client/Models/DiagramState.cs` — replaced by `DashboardModel.cs`.

### Result
- Build: 0 errors, 0 warnings.
- Tests: 11/11 pass (5 client, 6 server).
- New JSON format is nested (not flat), with `nodeType` discriminator per node — no backward compat with old files (by design).

---



### FEAT-M: `appsettings.user.json` moved to data directory

**Problem:** Both `SettingsController` and `SetupController` wrote `appsettings.user.json` to `IWebHostEnvironment.ContentRootPath` — in Docker that's `/app/`, inside the container image, and is wiped on every container restart. Admin password and startup mode settings were therefore lost on redeploy.

**Fix — `Program.cs` (both hosts):**
- Replaced `builder.Configuration.AddJsonFile("appsettings.user.json", ...)` with a block that:
  1. Resolves the data directory early using the same priority logic as `DashboardStorageService` (env var `DIAGRAM_DATA_DIR` → config `DiagramStorage:DataDirectory` → `{ContentRoot}/Data`).
  2. Creates the data dir if it doesn't exist.
  3. One-time migration: if `{ContentRoot}/appsettings.user.json` exists and `{dataDir}/appsettings.user.json` does not, copies it across.
  4. Loads the settings file from data dir using a `PhysicalFileProvider` so the absolute path is unambiguous.
- ⚠️ Uses `new PhysicalFileProvider(dataDir)` + `AddJsonFile(provider, "appsettings.user.json", ...)` — NOT `AddJsonFile(absolutePath, ...)` because the default file provider base path is `AppContext.BaseDirectory`, not `ContentRootPath`.

**Fix — `SettingsController.cs` and `SetupController.cs`:**
- Removed `IWebHostEnvironment` injection from both.
- Injected `DashboardStorageService` instead.
- Changed `Path.Combine(_env.ContentRootPath, "appsettings.user.json")` → `Path.Combine(_storage.StoragePath, "appsettings.user.json")` in both the `Save()` method (SettingsController) and `SavePasswordHash()` (SetupController).

**Files changed:**
- `src/MqttDashboard.WebApp/MqttDashboard.WebApp/Program.cs`
- `src/MqttDashboard.WebApp/MqttDashboard.WebAppServerOnly/Program.cs`
- `src/MqttDashboard.Server/Controllers/SettingsController.cs`
- `src/MqttDashboard.Server/Controllers/SetupController.cs`

---

### FEAT-N: Restart from web UI (Docker)

**Problem:** In Docker, when a new image is available (pulled via `docker compose pull` or Watchtower), there was no way to restart the app from the browser — users had to SSH in and run `docker compose up -d`.

**Fix — `UpdateController.cs`:**
- Added `IHostApplicationLifetime` and `IConfiguration` injection.
- New `POST /api/update/restart` endpoint:
  - Checks admin auth if auth is configured.
  - Schedules `_lifetime.StopApplication()` after a 500ms delay (so the HTTP response is delivered first).
  - Returns `{ success: true, message: "..." }`.
  - Docker `restart: always` policy then brings the container back up with the (already-pulled) image.

**Fix — `MainLayout.razor`:**
- Added `_restarting` state field.
- Added `RestartAppAsync()` method: calls `POST /api/update/restart` via `Http.PostAsync`, swallows connection-drop exceptions (expected as app shuts down).
- Docker update banner: replaced plain text with a flex row showing the `docker compose pull` instruction + **"Restart Now"** button (disabled while restarting).
- Standalone update banner: replaced `MudLink` with a `MudButton` variant for visual consistency.

**Files changed:**
- `src/MqttDashboard.Server/Controllers/UpdateController.cs`
- `src/MqttDashboard.Client/Layout/MainLayout.razor`


The standard [CHANGELOG.md](CHANGELOG.md) contains release-level summaries following Keep a Changelog.

---

## 2026-03-24 — Bulk TODO bug fixes: UI cleanup, port menu, alignment, paste, undo, serialization

### Commit: 18e3ca7 (develop)

### Batches completed

#### Batch 1 — Small UI/UX fixes
- **`MainLayout.razor`**: App title fallback changed from `"Mqtt Dashboard"` → `"MQTT Dashboard"`.
- **`ApplicationState.cs`**: `ShowDiagramName` default `true`; `GridSize` default `20`; `CreateDiagramFromState` syncs both `_diagram.Options.GridSize` and `AppState.GridSize` from loaded state (previously GridSize property stayed at default, causing snapping to be wrong on first load).
- **`Display.razor.cs`** `SaveAsDiagram()`: removed `&& !string.Equals(name, AppState.DiagramName, ...)` overwrite guard — Save As now always prompts when file exists.
- **`DashboardPropertiesDialog.razor`**: removed "Title Bar" section heading; removed Grid section (MudText + MudSelect); replaced `ColorPicker` with `ColorInputRow` for canvas background.
- **`NodePropertyEditor.razor`**: dialog title now `"Edit {NodeType} Node Properties"`; removed subtitle + node type display lines; Title + TitlePosition on one compact MudGrid row; Background Image + Fit on one compact row, moved to top section; IconColor uses `ColorInputRow` (was MudSelect of enum names); removed "MQTT Data Binding" section header; removed `MudDivider` after link animation.
- **`StandardNodeLayout.razor`**: `<MudIcon>` now uses `Style="@IconStyle"` (CSS `color:` property) instead of `Color="@IconColor"` (MudBlazor enum). Removed old `IconColor` switch property; added `IconStyle` string property. ⚠️ Old save files with MudBlazor enum names (e.g. `"Primary"`) won't render icon color correctly — backward compat not a concern per user.
- **`NumericRangeEditor.razor`**: "Arc midpoint / zero-point" helper text changed to "Origin / zero-point".

#### Batch 2 — Grid startup + Options menu
- **`AppMenu.razor`**: removed entire `<MudMenu Label="Grid">` submenu from Options menu.
- **`ApplicationState.cs`** `CreateDiagramFromState`: sets `GridSize = X` (public property) in addition to `options.GridSize = X` so snapping is immediately correct on load.

#### Batch 3 — Port menu per-item greying
- **`ApplicationState.cs`**: added `SelectedNodePorts` (`HashSet<PortAlignment>?`); extended `UpdateSelectionState()` to accept `selectedPorts` parameter; added `MenuAddAllPorts` event and `TriggerAddAllPorts()`.
- **`Display.razor.cs`**: `UpdateSelectionState()` passes port HashSet from selected node; added `AddAllPortsToSelectedNode()` method; subscribed/unsubscribed `MenuAddAllPorts`.
- **`AppMenu.razor`**: Add Port items disabled when port exists; Delete Port items disabled when absent; added "All" item to Add Port submenu; added `MenuAddAllPorts()` method.

#### Batch 4 — Paste keeps selection + dirty flag fix
- **`Display.razor.cs`** paste loop: changed `_diagram.SelectModel(node, true)` → `SelectModel(node, false)` — `true` means "unselect others", so only the last pasted node was ever selected. `false` appends to selection.
- **`Display.razor.cs`** `OnSelectionChanged`: added `_ = InvokeAsync(() => _pendingDirtyMark = false)` deferred clear alongside the immediate clear. This handles the case where Blazor.Diagrams fires `SelectionChanged` BEFORE `node.Changed` (in that ordering, the immediate clear has no effect since the flag isn't set yet; the deferred clear runs after `node.Changed` has set the flag).

#### Batch 5 — Same Width / Same Height alignment
- **`Display.razor`**: added two new `<MudIconButton>` items ("Make Same Width" + "Make Same Height") after the existing bottom-align button, separated by a `MudDivider`.
- **`Display.razor.cs`**: added `SameWidth()` and `SameHeight()` methods — push undo snapshot, find max width/height among selected nodes, resize all to match, refresh.

#### Batch 6 — Serialization cleanup
- **`DiagramState.cs`**:
  - Added `DiagramFileInfo` class (`WrittenAt` ISO timestamp, `Filename` string).
  - `DiagramState`: added `[JsonPropertyOrder(n)]` to all properties (order: Name, ShowDiagramName, GridSize, BackgroundColor, Pages, MqttSubscriptions, Nodes, Links, FileInfo=99).
  - `NodeState`: moved `NodeType` to top of class with `[JsonPropertyOrder(0)]`; changed coordinate precision from 5dp to 2dp; removed `DataTopic` and `DataTopic2` scalar properties; made `Metadata` and `Ports` nullable (omitted when null by `WhenWritingNull` serializer option).
- **`ApplicationState.cs`** `GetDiagramState()`: removed `DataTopic`/`DataTopic2` from written output; Metadata written only when non-empty; Ports written only when non-empty; fallback loading of old `DataTopic`/`DataTopic2` scalar fields removed.
- **`Display.razor.cs`** `BuildFullState()`: populates `FileInfo` with `DateTimeOffset.UtcNow.ToString("o")` and `DiagramName`; sets it on both multi-page and single-page paths.
- **`DashboardStorageService.cs`**: both `SaveDiagramAsync` and `SaveDiagramByNameAsync` now use `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` — null optional properties are omitted from the JSON file entirely.

### Known caveats
- ⚠️ Icon colors saved with old MudBlazor enum names (e.g. `"Primary"`) will not render correctly — old files are not supported per user decision.
- ⚠️ Node IDs are still GUIDs in the file; sequential ID mapping was deferred (requires port/link ID remapping).
- ⚠️ Empty `Metadata: {}` and `Ports: []` are now fully omitted; old files with those empty fields load cleanly.

---

## 2026-03-24 — Refactor: Remove Data page; move topic management to Dashboard Properties

### Summary
Removed the `/data` page entirely and moved MQTT topic management into the Dashboard Properties dialog. A "no topics" overlay on the Display page prompts users to configure topics when none are set.

### `src/MqttDashboard.Client/Components/DashboardPropertiesDialog.razor`
- Added a **Data Topics** section: lists current topics with remove (×) buttons and an add-topic input field (Enter or + button to add).
- `Apply()` made async (`ApplyAsync`): diffs previous vs new topic set, calls `SignalRService.SubscribeToTopicAsync` / `UnsubscribeFromTopicAsync` for each change, then calls `AppState.SetSubscribedTopics()` + `AppState.MarkEdited()`.
- Opening this dialog from edit mode is the **only** way to manage topics going forward.

### `src/MqttDashboard.Client/Pages/Display.razor`
- Added a centered overlay card shown when `AppState.SubscribedTopics.Count == 0`.
  - **Edit mode**: shows "No data topics configured" + "Configure Topics →" button that opens Dashboard Properties.
  - **View mode**: shows an info message to switch to edit mode.

### `src/MqttDashboard.Client/Pages/Display.razor.cs`
- Added `OpenDashboardProperties()` helper (delegates to `ShowDiagramPropertiesAsync()`).

### `src/MqttDashboard.Client/Pages/MqttData.razor` — **DELETED**
- The entire Data page (topic management, data cache explorer, message log) has been removed.
- `MqttDataCache` and related services are **not** removed — they're still used by widgets.

### `src/MqttDashboard.Client/Layout/AppMenu.razor`
- Removed the "Data" menu item that navigated to `/data`.

### `src/MqttDashboard.Client/Layout/NavMenu.razor`
- Removed the `/data` nav link.

⚠️ Users with dashboards that had no topics will see the overlay on first load and need to open Dashboard Properties to add topics. Backward compat is not a concern per user instruction.



### Investigation
User reported topics added via the MqttData page were not remembered after a server restart.

Traced the full flow:
- Startup: `MqttInitializationService` loads `Default.json` → `AppState.SetSubscribedTopics(dashboard.MqttSubscriptions)` → restores SignalR subscriptions.
- Add topic: `MqttData.razor` → `SignalRService.SubscribeToTopicAsync` → server confirms → `HandleSubscriptionConfirmed` → `AppState.AddSubscriptionAsync` → `if (IsEditMode) MarkEdited()`.
- Save: triggered by user saving from edit mode → `GetDiagramState()` serializes `SubscribedTopics` into the dashboard JSON.

**Root cause:** The MqttData page displayed add/remove topic controls in view mode. Since `MarkEdited()` is gated on `IsEditMode`, topics added outside edit mode never marked the dashboard dirty. The save prompt/button never appeared, so topics were lost on restart.

### Fix — `src/MqttDashboard.Client/Pages/MqttData.razor`
- Wrapped the per-topic unsubscribe (×) button in `@if (AppState.IsEditMode)`.
- Wrapped the entire "add new subscription" row (text field + Add button) in `@if (AppState.IsEditMode)`.

Topics are now read-only in view mode — consistent with the rest of the dashboard. To add/remove topics, enter edit mode, make changes, and save.

The existing `if (IsEditMode) MarkEdited()` guard in `ApplicationState.cs` is correct and unchanged.

⚠️ If existing topics were stored only in the legacy `applicationstate.json` (pre-refactor format), they will need to be re-added once in edit mode and saved.

## 2026-03-24 — ColorInputRow refactor + color transition ElseColor + battery fix

**Branch:** develop

### New: `ColorInputRow.razor` — reusable color input component

**`src/MqttDashboard.Client/Components/ColorInputRow.razor`** (new file)
- Parameters: `Value/ValueChanged` (string?), `Label`, `Placeholder`, `ShowClear`
- Renders: color swatch preview + editable `MudTextField` + three icon buttons (Theme/Named/Custom) + optional clear
- Internally opens `ColorPickerDialog` using injected `IDialogService`
- Replaces duplicated color-picker markup in both `NodePropertyEditor` and `ColorTransitionEditor`

### Refactored: `NodePropertyEditor.razor` — Background Color uses `ColorInputRow`

**`src/MqttDashboard.Client/Components/NodePropertyEditor.razor`**
- Background Color section (was ~35 lines with inline swatch + read-only text + 3 buttons + conditional clear) replaced with a single `<ColorInputRow ... ShowClear="true">` tag
- Now editable text field (was read-only) — user can type a color directly or use picker buttons

**`src/MqttDashboard.Client/Components/NodePropertyEditor.razor.cs`**
- Removed `OpenColorPicker(ColorPickerMode mode)` and `ClearColor()` methods (now handled inside `ColorInputRow`)

### Refactored: `ColorTransitionEditor.razor` — threshold rows use `ColorInputRow`

**`src/MqttDashboard.Client/Components/ColorTransitionEditor.razor`**
- Per-row color (was: inline swatch + editable text + click-to-expand quick-color panel with 15 swatches) replaced with `<ColorInputRow>` — gives full Theme/Named/Custom dialog access on each row
- Removed `_editingThreshold` state, `_commonColors` static array, and quick-color panel markup

### Added: `ElseColor` fallback for color transitions

**`src/MqttDashboard.Client/Models/ColorTransition.cs`**
- Added `ElseColor` property (`string?`, default null) — applied when no threshold rule matches

**`src/MqttDashboard.Client/Models/DiagramState.cs`**
- `ColorTransitionState` — added `ElseColor` property for JSON persistence

**`src/MqttDashboard.Client/Services/ApplicationState.cs`**
- `DeserializeColorTransition` maps `state.ElseColor`
- `SerializeColorTransition` saves `ElseColor`; null check extended to include `ElseColor`

**`src/MqttDashboard.Client/Widgets/GaugeNodeWidget.razor`**
- `GetArcColor()` now returns `Node.GaugeColor.ElseColor` when thresholds are configured but none match (instead of always falling through to the percent-based default)

**`src/MqttDashboard.Client/Widgets/BatteryNodeWidget.razor`**
- `GetFillColor()` now returns first-matching rule (was accidentally returning last-matching — logic bug fixed)
- Returns `Node.BatteryColor.ElseColor` when thresholds are configured but none match (was `var(--mud-palette-primary)`)

**`src/MqttDashboard.Client/Components/ColorTransitionGroupEditor.razor`**
- Added "Else (no rule matched)" section below the transition list using `ColorInputRow` with `ShowClear="true"`

### Fixed: MUD0002 analyzer warning in `LogNodeWidget.razor`

**`src/MqttDashboard.Client/Widgets/LogNodeWidget.razor`**
- Pause/Play button: `Title="..."` attribute replaced with `<MudTooltip>` wrapper (MudBlazor MUD0002 — `Title` not valid on `MudIconButton`)

---



**Commit:** 14f0abc  **Branch:** develop

### Root cause analysis

`InvalidCharacterError: String contains an invalid character` was thrown by the browser DOM when rendering SVG `MarkupString` content (gauge/battery widgets). The core issue:

1. `HtmlEncode` (used in both widgets) does **not** strip null bytes (`\0`) — it only encodes `<>&"'`. SVG is parsed as XML which strictly rejects null bytes.
2. The server-side `SanitizePayload` (from previous session) only applies to data arriving after the server rebuild. MQTT values already in the server's in-memory cache — replayed to clients on reconnect/load — bypassed server sanitization entirely.
3. On hover, MudTooltip triggers a full component re-render including the SVG `MarkupString`. If `DataValue` contained a null byte, the SVG injection crashed the circuit.

### Fix: `MqttDataCache.UpdateValue` — client-side gateway sanitization

**`src/MqttDashboard.Client/Services/MqttDataCache.cs`**
- Added `using MqttDashboard.Helpers`
- `UpdateValue()` now calls `XmlStringHelper.StripInvalidXmlChars(s)` when the incoming value is a string
- This is the single entry point for ALL MQTT data on the client side — covers both live data and server-replayed cached values

### Fix: new `XmlStringHelper` utility

**`src/MqttDashboard.Client/Helpers/XmlStringHelper.cs`** (new file)
- `StripInvalidXmlChars(string?)` — strips chars illegal in XML 1.0 (null bytes, lone surrogates, C0/C1 control chars except tab/LF/CR)
- `XmlSafeEncode(string?)` — strips invalid chars then HTML-encodes; use for any MarkupString SVG injection

### Fix: `GaugeNodeWidget` and `BatteryNodeWidget` SVG encoding

**`src/MqttDashboard.Client/Widgets/GaugeNodeWidget.razor`**
- `@using MqttDashboard.Helpers` added
- `RenderSvgLabels()` now calls `XmlStringHelper.XmlSafeEncode()` for the gauge value text and unit text (was `System.Net.WebUtility.HtmlEncode`)

**`src/MqttDashboard.Client/Widgets/BatteryNodeWidget.razor`**
- `@using MqttDashboard.Helpers` added
- SVG `<text>` content now uses `XmlStringHelper.XmlSafeEncode(FormatPercent())` (was `HtmlEncode`)

⚠️ The `DataValueTooltipContent.razor` tooltip already had its own `SanitizeForDisplay()` from a previous session — that path was protected. The crash path was the SVG MarkupString re-render on tooltip hover, not the tooltip content itself.

---



**Branch:** develop

### Widget refactor: MudNodeWidget uses StandardNodeLayout

**`StandardNodeLayout.razor`** updated to support icon rendering alongside the title:
- Added `HasTitleContent` computed property — title area renders when either `Node.Icon` or `Node.Title` is non-empty (was only checking title)
- Added `IconColor` computed property (same enum mapping as MudNodeWidget previously had inline)
- Updated `TitleDivStyle`: Left/Right positions use column-flex with centred icon above text; Above/Below use row-flex with icon+text side by side
- Removed duplicated icon-color logic from `MudNodeWidget`

**`MudNodeWidget.razor`** fully replaced with `StandardNodeLayout` wrapper:
- Removed ~90 lines of custom MudCard/MudCardHeader/MudCardContent/tooltip/port rendering — all now inherited from `StandardNodeLayout`
- Text content passed as `ExtraContent` RenderFragment
- Now benefits from: proper `DataValueTooltipContent` (multi-topic aware, sanitised), background image support, correct port rendering, double-click via `AppState.TriggerEditProperties()`
- Fixed latent bug: old widget checked `Node.DataTopic` (legacy singular field) as the loop condition inside a loop over `Node.DataTopics`

### Property editor refactor: reflection-driven, no more @if blocks

**`NodePropertyEditor.razor`** — removed 5 `@if (Node is XxxModel)` blocks (~135 lines):
- Replaced with a 4-line `@foreach (var category in GetNodeSpecificCategories())` loop that renders `<NodePropertyRenderer Node="Node" Category="@category" />`
- Each node-type-specific category gets a `<MudDivider>` + caption heading + the renderer
- `NodePropertyRenderer.razor` (already existed) reads `[NpXxx]` attributes via reflection and renders the appropriate MudBlazor control (MudTextField, MudNumericField, MudCheckBox, MudSelect, DynamicComponent for custom group editors)

**`NodePropertyEditor.razor.cs`** — added `GetNodeSpecificCategories()`:
- Reflects over the current node's type, collects distinct `Category` values from all `[NpXxx]`-annotated properties, returns them in declaration order
- Added `using System.Reflection`

**Effect of this change:**
- Adding a new node type no longer requires editing `NodePropertyEditor.razor` — just annotate the model properties with `[NpCustom]`/`[NpText]`/`[NpNumeric]`/`[NpCheckbox]`/`[NpSelect]` attributes and they appear automatically
- ⚠️ Minor visual change: node-specific fields are now stacked vertically rather than in compact MudGrid rows (e.g. Gauge: Min/Max/Origin/Unit now stack instead of appearing in one row). Functionally identical.

### Removed Grid/Image editor extractor todos
- `refactor-editor-grid` and `refactor-editor-image` marked done (those node types no longer exist)

---

## 2026-03-23 — Fix InvalidCharacterError from invalid chars in MQTT payloads

**Branch:** develop

### Root cause
MQTT brokers can send payloads containing null bytes (`\0`, U+0000) or other characters that are illegal in XML 1.0 / HTML DOM text nodes (lone surrogates U+D800–U+DFFF, C0/C1 control chars). When Blazor Server applied a render batch containing these characters in a text node, the browser's DOM API threw `DOMException: InvalidCharacterError`, which Blazor reported as an `InvalidOperationException` and killed the SignalR circuit.

The symptom was intermittent crashes that correlated with MQTT data arriving; the user observed it as a crash when opening the Battery node property editor (the timing coincided with a data update).

### Fix
Three-layer defence:

1. **`MqttClientService.SanitizePayload()`** (`src/MqttDashboard.Server/Services/MqttClientService.cs`)
   - New private static helper strips characters outside the valid XML 1.0 character set: keeps `\t`, `\n`, `\r`, U+0020–U+D7FF, U+E000–U+FFFD; discards everything else.
   - Called at the single point where MQTT payloads are decoded: `var value = SanitizePayload(ConvertPayloadToString(...));`

2. **`BatteryNodeWidget.razor`** — SVG `<text>` rendered via `MarkupString`
   - `FormatPercent()` output is now wrapped with `System.Net.WebUtility.HtmlEncode()` before being interpolated into the raw HTML string.
   - `HtmlEncode` also encodes `<`, `>`, `&` so the SVG is always valid markup.

3. **`DataValueTooltipContent.razor`** — displays raw MQTT value in tooltip
   - Added `SanitizeForDisplay(string?)` local method (same stripping logic) applied to `val?.ToString()` before it's rendered in a text node.

### Cleanup
- `MudNodeModel.cs` — removed orphaned XML doc comment block that was left dangling after the `BackgroundImageFromData` property was deleted in the previous session.

---

## 2026-03-23 — Remove Grid/Image node types; add background image to base node

**Commit:** `2074325`
**Branch:** develop

### Removed
- `GridNodeModel.cs` + `GridNodeWidget.razor` — Grid node removed entirely. Was an outlier: its per-cell MQTT topic model didn't fit the base `StandardNodeLayout` pattern, and the wildcard-topic routing design (path/+/+ → row/column) needed for a useful Grid is a larger feature best deferred.
- `ImageNodeModel.cs` + `ImageNodeWidget.razor` — Image node removed as a separate type.

### Changed
- `MudNodeModel` — added three new base properties available on **all** node types:
  - `BackgroundImageUrl` (string?) — static CSS background image URL
  - `BackgroundObjectFit` (string, default "cover") — background-size: "cover", "contain", or "fill" (→ `100% 100%`)
  - `BackgroundImageFromData` (bool) — when true, uses the node's first MQTT data value as the background image URL (dynamic image from broker)
- `StandardNodeLayout.razor` — `ContainerStyle` now computes `background-image` + `background-size` + `background-position` from the new base properties.
- `NodePropertyEditor.razor` — replaced Image-specific and Grid-specific sections with a universal "Background Image" section (URL, Image Fit dropdown, "Use data value as URL" checkbox) shown for every node type.
- `ApplicationState.cs` — removed Image/Grid component registrations, removed type-specific deserialise/serialise blocks for Image and Grid, added base background image round-trip for all node types. Legacy `Image` NodeType entries in saved files load cleanly as plain Text nodes with the `BackgroundImageUrl` set from the old `StaticImageUrl` field.
- `DiagramState.cs` / `NodeState` — added `BackgroundImageUrl`, `BackgroundObjectFit`, `BackgroundImageFromData` as base fields; removed `GridColumnHeaders`/`GridRows`/`GridRowState`; kept `StaticImageUrl`/`ObjectFit` as nullable read-only legacy fields for old-file compat.
- `Display.razor.cs` — removed Image/Grid from `AddNode()` and paste/copy snapshots; added background image to base paste restore.
- `NodeTypePickerDialog.razor` — removed Image and Grid entries.
- `NodePropertyEditor.razor.cs` — removed `AddGridColumn`, `RemoveGridColumn`, `EnsureGridTopicSlots` helpers.

### Caveats
⚠️ Old saved files with `"NodeType": "Grid"` nodes will load as plain text nodes and their row/column data will be lost. This is intentional — backward compat for format is deprioritised per project notes.
⚠️ Old `"NodeType": "Image"` nodes load as plain text nodes with `BackgroundImageUrl` set from the old `StaticImageUrl` field, so images are preserved.

---

: shared layout, attributes, property groups

**Commit:** `dde31e9`
**Branch:** develop

### New files

| File | Purpose |
|---|---|
| `Models/NodePropertyAttributes.cs` | `[NpText]`, `[NpNumeric]`, `[NpCheckbox]`, `[NpSelect]`, `[NpCustom]` attributes for model properties. `NpNumericAttribute.Min/Max` use `double.NaN` as "no limit" sentinel (attribute parameters can't be nullable types). |
| `Models/NumericRangeSettings.cs` | Shared POCO: `Min`, `Max`, `Origin?`, `DataTopicIndex`. Used by both `GaugeNodeModel` and `BatteryNodeModel`. |
| `Widgets/DataValueTooltipContent.razor` | Shared tooltip content component accepting `MudNodeModel`. Shows all topics with values + timestamps; single "No data topic configured" fallback. Replaces 5 near-identical inline tooltip blocks. |
| `Widgets/StandardNodeLayout.razor` | Shared outer shell for visual nodes (Gauge, Battery, Switch, Image). Injects `AppState`; handles tooltip, container div + CSS class + background colour, title positioning (Above/Below/Left/Right), double-click → edit, port rendering. Accepts `ExtraContent` RenderFragment + optional `ShowTitle` bool. Correctly suppresses both title positions when `ShowTitle=false` (fixes a bug in the old `ImageNodeWidget` where the title would still appear below even when `ShowTitle=false` with `TitlePos=Above`). |
| `Components/NumericRangeEditor.razor` | MudGrid editor for `NumericRangeSettings`: Min, Max, Origin (nullable), DataTopicIndex. Accepts `[Parameter] object? Value` (cast to `NumericRangeSettings` internally). |
| `Components/ColorTransitionGroupEditor.razor` | Wraps existing `ColorTransitionEditor`. Accepts `[Parameter] object? Value` (cast to `ColorTransition`). Shows `ColorTopicIndex` numeric field + delegates threshold list to `ColorTransitionEditor`. |
| `Components/NodePropertyRenderer.razor` | Reflection-driven control renderer. Loops over `[NpXxx]` attributes on the node type filtered by `Category`; renders matching MudBlazor controls. Uses `RenderTreeBuilder` delegate pattern for generic `MudNumericField<T>` and `MudSelect<T>`. `NpCustom` → `DynamicComponent` with `Node` + `Value` params. |

### Modified files

**Models:**
- `GaugeNodeModel.cs` — `MinValue/MaxValue/ArcOrigin/DataTopicIndex` replaced by `NumericRangeSettings Range`. Read-only convenience accessors (`MinValue => Range.Min` etc.) kept for backward compat in widget render code. Added `[NpCustom]`, `[NpText]`, `[NpSelect]` attributes.
- `BatteryNodeModel.cs` — same pattern as Gauge with `NumericRangeSettings Range`. Added `[NpCustom]`, `[NpCheckbox]` attributes.
- `SwitchNodeModel.cs` — added `[NpText]`/`[NpSelect]`/`[NpCheckbox]` to all properties.
- `ImageNodeModel.cs` — added `[NpText]`/`[NpSelect]`/`[NpCheckbox]` to all properties.
- `LogNodeModel.cs` — added `[NpNumeric]`/`[NpCheckbox]` to all properties.
- `TreeViewNodeModel.cs` — added `[NpText]`/`[NpCheckbox]` to all properties.

**Widgets:**
- `BaseNodeWithDataWidget.cs` — added `protected` title positioning methods: `TitlePos`, `ShowTitleFirst()`, `OuterFlexStyle()`, `TitleDivStyle()`. These are now in one place; previously copied identically into 4 widget files.
- `GaugeNodeWidget.razor` — fully refactored to use `<StandardNodeLayout>`. Removed title methods, tooltip, container div, port loop (~35 lines of boilerplate). Only SVG arc + text remain as `<ExtraContent>`.
- `BatteryNodeWidget.razor` — same refactor as Gauge.
- `SwitchNodeWidget.razor` — same refactor (removed `@using Blazor.Diagrams.Components.Renderers`).
- `ImageNodeWidget.razor` — same refactor; passes `ShowTitle="@Node.ShowTitle"` to `StandardNodeLayout`.

**Services/Pages:**
- `ApplicationState.cs` — Gauge/Battery deserialization uses `Range = new NumericRangeSettings { Min=..., Max=..., Origin=..., DataTopicIndex=... }`. Serialization uses `g.Range.Min` etc.
- `Display.razor.cs` — paste-cloning code updated to use `Range = new NumericRangeSettings { ... }` instead of assigning flat read-only accessors.
- `NodePropertyEditor.razor` — Gauge/Battery property sections updated to use `gaugeNode.Range.Min` etc. (direct two-way binding to the POCO properties). Node property renderer (`NodePropertyRenderer`) infrastructure created but NodePropertyEditor still uses hand-crafted sections for all node types — the full migration from `@if (Node is XxxModel)` to `NodePropertyRenderer` is deferred; the infrastructure is now in place.

### Caveats / remaining work
⚠️ `NodePropertyRenderer` is created and compiles, but `NodePropertyEditor.razor` still uses manual type-dispatch for all node types. The renderer infrastructure can be adopted incrementally — annotate a model property with `[NpXxx]`, add a Category, and `NodePropertyRenderer` will render it automatically.
⚠️ `NpCustom` attributes on model properties reference `typeof(NumericRangeEditor)` which is in `MqttDashboard.Components` — a slight model→UI namespace dependency. Acceptable for now; could be removed by using string-based component lookup in future.

---

## 2026-03-23 — Bug fixes: node resize loop, port visibility, alignment toolbar, save/save-as

**Commit:** `492a2cc`
**Timestamp:** 2026-03-23 ~18:15 UTC
**Branch:** FEAT-C

### bug-node-grow — Node grows indefinitely when Title is cleared
**Files:** `src/MqttDashboard.Client/Widgets/MudNodeWidget.razor`

`<MudCardHeader>` was conditionally removed from the DOM when both `Node.Title` and `Node.Icon` were empty. Blazor.Diagrams re-measures node content height after each render; losing the header element caused a size change, which triggered another render, which re-measured again → infinite loop. Fix: always render the `MudCardHeader` but apply `style="display:none"` when both fields are empty. The DOM structure stays stable; Blazor.Diagrams sees no size change.

---

### bug-port-invisible — Ports invisible on all non-Text nodes; blank border visible
**Files:** `src/MqttDashboard.Client/Widgets/BaseNodeWidget.cs`, `GaugeNodeWidget.razor`, `SwitchNodeWidget.razor`, `BatteryNodeWidget.razor`, `GridNodeWidget.razor`, `ImageNodeWidget.razor`, `LogNodeWidget.razor`, `TreeViewNodeWidget.razor`

Two root causes:
1. `ContainerStyle()` in `BaseNodeWidget` added `overflow:hidden` whenever `Node.Size` was set. Ports (rendered inside that container) were clipped at the node boundary. Removed `overflow:hidden` from the style string.
2. All affected widgets applied `pa-1` (4 px MudBlazor padding) on the outer container div, creating a visible blank gap between the node's outer border and its content. Removed `pa-1` from the outer div in all 7 widgets. Inner content retains its own spacing as needed.

`MudNodeWidget` was unaffected because it renders ports outside the `<MudCard>` element.

---

### bug-align-toolbar — Alignment toolbar buttons unclickable
**Files:** `src/MqttDashboard.Client/Pages/Display.razor`, `Display.razor.cs`

The alignment toolbar overlay (`position:absolute;z-index:10`) was inside a `<MudPaper>` that lacked `position:relative`. The `<DiagramCanvas>` SVG was rendered on top and intercepting pointer events. Two fixes:
1. Added `position:relative` to `CanvasStyle` so the absolute-positioned toolbar is scoped to the canvas container.
2. Raised toolbar `z-index` from `10` → `1000` to ensure it sits above all diagram canvas elements.

---

### bug-new-save-state — Save enabled after New; Save As overwrites silently
**Files:** `src/MqttDashboard.Client/Layout/AppMenu.razor`, `src/MqttDashboard.Client/Pages/Display.razor.cs`

Two problems:
1. After File → New, `DiagramName` is empty but the Save menu item was enabled. `SaveDashboard()` had a silent fallback: `var name = string.IsNullOrEmpty(...) ? "Default" : ...`. Fixed: Save menu item now has `Disabled="@string.IsNullOrEmpty(AppState.DiagramName)"`. The silent fallback removed; `SaveDashboard()` returns early with a warning snackbar if no filename is set.
2. Save As did not check for an existing file before overwriting. Fixed: after the user enters a name, `ListDashboardsAsync()` is called; if a match exists (case-insensitive) and it differs from the current filename, a MudBlazor "Overwrite?" confirm dialog is shown before proceeding.

Note: `DiagramName` (filename on disk, no extension) and `DiagramDisplayName` (human label in JSON, shown in title bar) are distinct — Save/Save As operate on the filename only.

---



**Commit:** `dbb63cb`
**Timestamp:** 2026-03-22 ~18:15 UTC
**Branch:** FEAT-C

### Items completed

#### Fix: Color transition topic index is per-node, not per-threshold
- `Models/GaugeNodeModel.cs` — removed `TopicIndex` from `GaugeColorThreshold`; `ColorTopicIndex` already on `GaugeNodeModel` (added earlier this session)
- `Widgets/GaugeNodeWidget.razor` — `GetArcColor()` uses `Node.ColorTopicIndex` for all threshold comparisons
- `Components/ColorTransitionEditor.razor` — reverted: no per-threshold topic field; clean 3-column layout (When / Value / Color)
- `Components/NodePropertyEditor.razor` — Gauge section: `ColorTopicIndex` spinner in same row as `DataTopicIndex`; removed `ShowTopicIndex` param from `ColorTransitionEditor` call
- `Models/DiagramState.cs` — `GaugeColorTopicIndex` on `NodeState`; `TopicIndex` removed from `GaugeColorThresholdState`
- `Services/ApplicationState.cs` — serialize/deserialize updated; `GaugeColorTopicIndex` null-when-0 for clean JSON

#### Fix: Log column options — full independent booleans, no wildcard logic
- `Models/LogNodeModel.cs` — replaced `ShowTopic` with six booleans: `ShowDate`, `ShowTime`, `ShowTopicFull`, `ShowTopicPath`, `ShowTopicName`, `ShowValue`
- `Widgets/LogNodeWidget.razor` — removed `IsWildcard`; all 6 columns driven by model booleans; added `TopicPath(topic)` and `TopicName(topic)` helper methods; `colCount` computed inline for empty-row colspan
- `Components/NodePropertyEditor.razor` — Log section: replaced single ShowTopic checkbox with 6-checkbox responsive grid (3 per row)
- `Models/DiagramState.cs` — replaced `ShowTopic` with `ShowTopicFull`, `ShowTopicPath`, `ShowTopicName`, `ShowValue` fields
- `Services/ApplicationState.cs` — serialize/deserialize updated; `ShowValue` written as null when true (clean JSON default)

#### Fix: Undo stack cleared on entering Edit Mode
- `Pages/Display.razor.cs` — in `SwitchMode(enterEditMode:true)`, added `AppState.ClearUndoRedo()` immediately after capturing `_editSnapshot`. Entering Edit Mode is now always a clean undo state.

#### Fix: Reload from disc exits Edit Mode
- `Pages/Display.razor.cs` — rewrote `ReloadDiagram()`: always calls `AppState.SetEditMode(false)` + `AppState.MarkSaved()` + `AppState.ClearUndoRedo()` before loading; always loads with `readOnly: true`; removed the re-subscription block that was restoring Edit Mode after reload.

#### Added: Undo All menu item
- `Services/ApplicationState.cs` — added `event Action? MenuUndoAll` and `TriggerUndoAll()` method
- `Layout/AppMenu.razor` — added `<MudMenuItem Label="Undo All" ...>` after Redo; added `private void UndoAll() => AppState.TriggerUndoAll();`
- `Pages/Display.razor.cs` — added `_onMenuUndoAll` private field; wired in `SubscribeEditEvents` / unwired in `UnsubscribeEditEvents` / nulled in null-out block; added `UndoAllAction()` async method that applies `_editSnapshot`, clears undo/redo, marks saved, shows snackbar — uses `_suppressDirty` guard to prevent false dirty mark during replay

### Notes
- All builds succeeded with 0 errors (pre-existing MUD0002 warning on LogNodeWidget `Title` attribute is unchanged)
- `UndoAllAction` applies the pre-edit snapshot (`_editSnapshot`) not the last undo state, so it always fully reverts to the clean state — regardless of how many changes were made

---

## 2026-03-23 — Fix: Undo All reverts to empty page

**Commit:** _(this batch)_
**Timestamp:** 2026-03-23 ~00:15 UTC
**Branch:** FEAT-C

### Bug fixed

#### Fix: Undo All reverts to empty page
**Root cause:** `UndoAllAction` called `ApplyDiagramState(_editSnapshot)`. `ApplyDiagramState` calls `CreateDiagramFromState(state, ...)` which expects a flat single-page `DiagramState` (with `Nodes` / `Links` at top level). But `_editSnapshot` from `BuildFullState()` with multiple pages is a *wrapper* `DiagramState` with a `Pages` list and empty top-level `Nodes` / `Links`. `CreateDiagramFromState` saw an empty node list and produced an empty diagram.

**Fix:** `Pages/Display.razor.cs` — `UndoAllAction` now uses `LoadFullState(_editSnapshot, readOnly: false)` which correctly handles both single-page and multi-page snapshots. After `LoadFullState`, edit-mode event handlers are re-attached (`SelectionChanged`, `Changed`, `SubscribeEditEvents`, `UpdateSelectionState`). The old `ApplyDiagramState` call for `UndoAll` is removed.

Same issue exists for regular Undo/Redo if they ever snapshot a multi-page state — noted for future hardening (regular Undo/Redo only snapshot the active page via `GetDiagramState()`, so they are safe for now).

### Notes
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — Link animation startup fix (SSR/F5 flash + initial-value timing)

**Commit:** _(this batch)_
**Timestamp:** 2026-03-22 ~19:55 UTC
**Branch:** FEAT-C

### Items completed

#### Fix: Link animations flash on F5 refresh / not shown until first live data arrives
**Root cause (two issues):**
1. `SetupDataWatchers()` was called on every `OnParametersSet`, which fires for every re-render of the node widget. Each call disposed and recreated all watchers and re-seeded from cache, calling `TriggerLinkAnimation()` on each re-run. During `RefreshAll()`, every node got its watchers torn down and rebuilt, causing an animation reset flash.
2. On initial load, `SetupDataWatchers()` seeds from cache and calls `TriggerLinkAnimation()` + `l.Refresh()` before the diagram SVG is rendered (the DiagramCanvas is guarded by `!IsInteractive`). So the animation update was lost. Animations only showed when first live data arrived.

**Fix:**
- `Widgets/BaseNodeWithDataWidget.cs` — Added `_watcherTopicsKey` (string?). `SetupDataWatchers()` now returns early if `Node.DataTopics` key matches `_watcherTopicsKey`, preventing redundant teardown/rebuild on repeated `OnParametersSet` calls. Key is cleared on `Dispose()` to ensure proper re-init.
- Added `OnAfterRenderAsync(bool firstRender)` override: calls `TriggerLinkAnimation()` when `firstRender = true`. Node widgets only mount (and fire `firstRender`) after `IsInteractive = true` because the DiagramCanvas is inside an `@if (AppState.IsInteractive)` guard in Display.razor. This ensures animation fires when the SVG is actually in the DOM.
- Promoted `TriggerLinkAnimation()` from `private` to `protected` (needed by `OnAfterRenderAsync`; also available to subclasses).

### Notes
- Fixes both "lines only shown on first data update" (timing) and "F5 flash" (redundant re-initialization).
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — Dirty flag on selection fix, log width, link delete dirty tracking

**Commit:** _(this batch)_
**Timestamp:** 2026-03-22 ~19:40 UTC
**Branch:** FEAT-C

### Items completed

#### Fix: Dirty flag fires on node selection
**Root cause:** `OnNodeChanged(node)` called `AppState.MarkEdited()` directly (no deferral), so every node.Changed event — including selection — instantly marked the diagram dirty. The `_pendingDirtyMark` pattern existed only in `OnDiagramChanged`, which fires separately.

**Fix:**
- `Pages/Display.razor.cs` — `OnNodeChanged`: removed direct `MarkEdited()` call; now uses the same `_pendingDirtyMark = true` + `InvokeAsync(...)` deferred pattern. `OnSelectionChanged` clears the flag before the callback runs for selection events, so selection doesn't mark dirty. Real moves/resizes still trigger dirty + undo push.
- `OnDiagramChanged`: removed all dirty logic; now only calls `InvokeAsync(StateHasChanged)` (diagram-level `Changed` was redundant for dirty tracking now that per-node events handle it).

#### Fix: Link removal doesn't mark diagram dirty
- `Pages/Display.razor.cs` — added `OnLinkRemoved` handler: calls `AppState.MarkEdited() + PushUndoSnapshot()`.
- `SubscribeEditEvents`: added `_diagram.Links.Removed += OnLinkRemoved`.
- `UnsubscribeEditEvents`: added unsubscription.
- `OnLinkAdded`: added `MarkEdited() + PushUndoSnapshot()` (link additions also now explicitly mark dirty).

#### Fix: Log view width expands with long content
- `Widgets/BaseNodeWidget.cs` — `ContainerStyle()`: added `overflow:hidden` to the size string. All node widgets now clip any overflowing content to their declared size.

### Notes
- `align-toolbar-grey` and `error-ui-css` were already correctly implemented — marked done.
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — ColorTransition class refactor (Gauge + Battery)

**Commit:** _(this batch)_
**Timestamp:** 2026-03-22
**Branch:** FEAT-C

### Items completed

#### Refactor: Introduce `ColorTransition` class to wrap color threshold state
- `Models/ColorTransition.cs` — NEW FILE. Contains `ColorTransition` (wraps `ColorTopicIndex` + `List<GaugeColorThreshold>`) and `GaugeColorThreshold` (moved from `GaugeNodeModel`).
- `Models/GaugeNodeModel.cs` — `ColorThresholds` and `ColorTopicIndex` removed; replaced by single `GaugeColor` property of type `ColorTransition`. `GaugeColorThreshold` class removed from this file.
- `Models/BatteryNodeModel.cs` — `ColorThresholds` and `ColorTopicIndex` removed; replaced by single `BatteryColor` property of type `ColorTransition`. Obsolete `LowColor`, `MedColor`, `HighColor`, `MidPoint`, `NegativeColor`, `PositiveColor` fields removed entirely.
- `Widgets/GaugeNodeWidget.razor` — `GetArcColor()` now uses `Node.GaugeColor.ColorThresholds` and `Node.GaugeColor.ColorTopicIndex`.
- `Widgets/BatteryNodeWidget.razor` — `ColorValue` helper uses `Node.BatteryColor.ColorTopicIndex`; `GetFillColor()` uses `Node.BatteryColor.ColorThresholds`. Obsolete color fallback code removed.
- `Components/NodePropertyEditor.razor` — all Gauge and Battery color bindings updated to new nested paths (`gaugeNode.GaugeColor.*`, `batteryNode.BatteryColor.*`).
- `Models/DiagramState.cs` — stripped all legacy flat fields (`ColorThresholds`, `ColorTopicIndex`, `GaugeColorTopicIndex`, `LowColor`, etc.); added `ColorTransitionState` DTO; `NodeState.GaugeColor` and `NodeState.BatteryColor` are `ColorTransitionState?`.
- `Services/ApplicationState.cs` — added `DeserializeColorTransition()` / `SerializeColorTransition()` private helpers + `DeserializeColorTransitionStatic()` / `SerializeColorTransitionStatic()` public static wrappers; Gauge and Battery deserialize/serialize blocks updated to use helpers.
- `Pages/Display.razor.cs` — copy/paste node serialization updated for Gauge and Battery (was using old flat `ColorThresholds`; now calls `SerializeColorTransitionStatic` / `DeserializeColorTransitionStatic`).

### Notes
- No backward compat with old JSON files — nodes will load with empty color transitions if saved with old format.
- `Display.razor.cs` also had copy-node code referencing old fields — fixed in same batch.
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — DataTopic refactor, Battery topic index parity

**Commit:** `ac7b2f9`
**Timestamp:** 2026-03-22 ~18:50 UTC
**Branch:** FEAT-C

### Items completed

#### Refactor: DataTopic/DataTopic2 → computed from DataTopics list
- `Models/MudNodeModel.cs` — removed settable `DataTopic`/`DataTopic2` properties; replaced with computed read-only accessors (`=> DataTopics[0]`/`[1]`). Added `DataValues` (object?[]) and `DataUpdatedTimes` (DateTime?[]) arrays; added computed compat `DataValue`/`DataValue2`/`DataLastUpdated`/`DataLastUpdated2` getters. `DataTopics` list is now the single source of truth.
- `Widgets/BaseNodeWithDataWidget.cs` — `SetupDataWatchers()` now sizes `DataValues`/`DataUpdatedTimes` arrays to match topic count and writes to `Node.DataValues[idx]`/`Node.DataUpdatedTimes[idx]`. Removed old scalar writes. The fallback to `DataTopic`/`DataTopic2` is gone (DataTopics list must be populated by deserialization).
- `Services/ApplicationState.cs`:
  - Removed `node.DataTopic = nodeState.DataTopic` and `node.DataTopic2 = nodeState.DataTopic2` (computed, can't be set)
  - `CreateQuickAddNode`: changed `DataTopic = topicPath` → `DataTopics = new List<string> { topicPath }` in object initializer
  - Serialization: simplified `DataTopic`/`DataTopic2` write to use computed props (cleaner, same output)
- `Pages/Display.razor.cs` — paste/copy node path also used `node.DataTopic = ...`; fixed to use `node.DataTopics.Add(...)`.

#### Fix: Battery gets same DataTopicIndex + ColorTopicIndex as Gauge
- `Models/BatteryNodeModel.cs` — added `DataTopicIndex` (int, default 0) and `ColorTopicIndex` (int, default 0) properties
- `Widgets/BatteryNodeWidget.razor`:
  - Added `ActiveValue` computed property (mirrors Gauge): `Node.DataTopicIndex == 1 ? Node.DataValue2 : Node.DataValue`
  - Added `ColorValue` computed property: `Node.ColorTopicIndex == 1 ? Node.DataValue2 : Node.DataValue`
  - `UpdatePercent()` now uses `ActiveValue` instead of `Node.DataValue`
  - Added `protected override void OnData2Updated() => UpdatePercent()` (so DataTopicIndex=1 also updates fill)
  - `GetFillColor()` now uses `ColorValue` for threshold comparisons instead of `_percent` when a ColorValue is available
  - `FormatPercent()` uses `ActiveValue`
- `Models/DiagramState.cs` — added generic `DataTopicIndex`/`ColorTopicIndex` fields; kept `GaugeDataTopicIndex`/`GaugeColorTopicIndex` as backward-compat read-only fallback fields
- `Services/ApplicationState.cs` — Gauge deserialise: fallback chain `DataTopicIndex ?? GaugeDataTopicIndex ?? 0`; Gauge serialise: writes to `DataTopicIndex`/`ColorTopicIndex` (not Gauge-specific names); Battery deserialise+serialise: reads/writes `DataTopicIndex`/`ColorTopicIndex`
- `Components/NodePropertyEditor.razor` — Battery section: added 2-column row with "Value Topic (0-based)" and "Color Topic (0-based)" spinners, identical layout to Gauge

### Notes
- `DataValue`/`DataValue2`/`DataLastUpdated`/`DataLastUpdated2` are still usable everywhere as computed shims; no widget code required changing
- Old dashboard files with scalar `DataTopic`/`DataTopic2` fields (and no `DataTopics` array) are migrated transparently on load
- `GaugeDataTopicIndex`/`GaugeColorTopicIndex` in JSON are still read (fallback); new saves write generic `DataTopicIndex`/`ColorTopicIndex` — so old Gauge configs load correctly after upgrade
- All 11 tests pass; 0 build errors

---


**Commit:** `5378e80` · 2026-03-22 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: Gauge properties dialog compaction**  
- `NodePropertyEditor.razor` — all four fields (Min, Max, Origin, Unit) now live in a single `MudGrid` row with `xs="3"` each. Previously Origin was on its own line and the old informational `<MudText>` about text position is removed (replaced by the TextPosition selector).

**Added: Gauge text position (above / below arc)**  
- `GaugeNodeModel.cs` — added `TextPosition` property (string, default `"Below"`).  
- `GaugeNodeWidget.razor` — when `TextPosition == "Above"`, the static Text `<div>` is rendered before the SVG; otherwise it renders after (existing "below" behavior).  
- `NodePropertyEditor.razor` — `MudSelect` for TextPosition (Below arc / Above arc).  
- `DiagramState.cs` / `ApplicationState.cs` — persisted as `NodeState.TextPosition`; only written when non-default (saves `null` = "Below" for clean JSON).

**Added: Gauge value topic index selector**  
- `GaugeNodeModel.cs` — added `DataTopicIndex` property (int, default `0`).  
- `GaugeNodeWidget.razor` — added `private object? ActiveValue` helper that returns `Node.DataValue2` when `DataTopicIndex == 1`, else `Node.DataValue`. `UpdatePercent()`, `GetArcColor()`, and `FormatValue()` all use `ActiveValue`. Also added `OnData2Updated()` override so the widget repaints when either topic updates (correct value is read via `ActiveValue`).  
- `NodePropertyEditor.razor` — `MudNumericField` labelled "Value Topic (0-based)" with helper text.  
- `DiagramState.cs` / `ApplicationState.cs` — persisted as `NodeState.GaugeDataTopicIndex`; written as `null` when 0 (default).

**Added: Per-threshold topic index in color transitions**  
- `GaugeColorThreshold.cs` (in `GaugeNodeModel.cs`) — added `TopicIndex` property (int, default `0`).  
- `GaugeNodeWidget.razor` `GetArcColor()` — each threshold now uses `t.TopicIndex == 1 ? Node.DataValue2 : Node.DataValue` for its comparison. Different thresholds can watch different topics on the same node.  
- `ColorTransitionEditor.razor` — added `ShowTopicIndex` bool parameter (default `false`). When true, a small "Topic #" `MudNumericField` (min=0) is prepended to each threshold row; grid widths adjust (`When` drops from `xs="3"` to `xs="2"`, Color from `xs="4"` to `xs="3"`). Gauge passes `ShowTopicIndex="true"`; Battery does not.  
- `GaugeColorThresholdState` / `ApplicationState.cs` — `TopicIndex` serialised and round-tripped.

**Added: Log "always show topic column"**  
- `LogNodeModel.cs` — added `ShowTopic` bool (default `false`).  
- `LogNodeWidget.razor` — topic column and its `colspan` now show when `IsWildcard || Node.ShowTopic`. Header and data cell both updated.  
- `NodePropertyEditor.razor` — added `MudCheckBox` "Always Show Topic Column" to the Log settings section.  
- `DiagramState.cs` / `ApplicationState.cs` — persisted as `NodeState.ShowTopic`; written as `null` when false.

---

## 2026-03-22 — Bug fixes batch 2: dirty flag, thresholds, log pause, width, reconnect
**Commit:** `52f7f93` · 2026-03-22 17:11 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: Dirty flag on node selection**  
- `Display.razor.cs` `OnDiagramChanged` — Blazor.Diagrams fires `Changed` then `SelectionChanged` synchronously when a node is clicked. Previously, `Changed` immediately called `MarkEdited()`, marking the dashboard dirty even though nothing had been edited.  
- Fix: `OnDiagramChanged` now sets `_pendingDirtyMark = true` and defers via `InvokeAsync`. `OnSelectionChanged` clears the flag before the async work runs. If the change was a real edit (node moved, etc.), `SelectionChanged` does not fire, so the flag stays set and `MarkEdited()` is called.  
- ⚠️ Remaining: after undoing all changes, the dirty flag stays red ("undo stack back to saved state" detection not yet implemented — noted in TODO).

**Fixed: Default color thresholds for Battery and Gauge**  
- `BatteryNodeModel.cs` — constructor now initialises `ColorThresholds` with red ≤25%, orange ≤50%, green ≥50%.  
- `GaugeNodeModel.cs` — constructor now initialises `ColorThresholds` with red ≤0, green ≥0 (sensible for voltage/temperature readings centered on zero).  
- Old saved files that have no thresholds are unaffected (the JSON deserialiser will overwrite the constructor defaults with the empty list from the file). This only applies to newly-created nodes.

**Fixed: Log and TreeView not filling widget width**  
- Created `LogNodeWidget.razor.css` with `::deep .mud-simple-table`, `::deep .mud-table-container`, `::deep .mud-table-root` all set to `width:100%; overflow-x:hidden`.  
- Created `TreeViewNodeWidget.razor.css` with `::deep .mud-treeview { width:100%; min-width:0 }`.  
- `TreeViewNodeWidget.razor` inner flex container: added `min-width:0` to prevent flex overflow.

**Added: Log node pause/resume button**  
- `LogNodeWidget.razor` — added `_paused` bool. New header row (always shown, title fades with opacity:0 when empty so layout is stable) contains title text + small Pause/Play `MudIconButton`.  
- `OnData1ReceivedCore` returns early when `_paused = true`. Entries shown are frozen until resumed.

**Added: Reconnect value replay**  
- `MqttClientService.cs` — added `ConcurrentDictionary<string, string> _lastKnownValues`; populated with every received message.  
- `MqttTopicSubscriptionManager.cs` — `TopicMatchesFilter(filter, topic)` made public (wraps private `TopicMatches`).  
- `MqttDataHub.cs` — added `GetCurrentValuesForTopics(List<string> requestedFilters)` hub method: iterates `LastKnownValues`, returns all topics matching any of the requested filters.  
- `ISignalRService.cs` — added `GetCurrentValuesForTopicsAsync(List<string> topics)`.  
- `SignalRService.cs` — implemented via `InvokeAsync<Dictionary<string,string>>("GetCurrentValuesForTopics", ...)`.  
- `ServerSignalRService.cs` — implemented directly against `MqttClientService.LastKnownValues`.  
- `MqttInitializationService.cs` — `RestoreSubscriptionsAsync()` now calls `GetCurrentValuesForTopicsAsync` after re-subscribing and seeds `AppState.DataCache` with the results. Widgets show current data immediately on page refresh or reconnect.

---

## 2026-03-22 — Bug fixes batch 1: regression, error UI, alignment toolbar
**Commit:** `32fb995` · 2026-03-22 15:37 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: `BaseNodeWithDataWidget` initial-seed regression**  
- Previous batch's startup-animation fix called `OnData1ReceivedCore()` during cache seeding, which caused `LogNodeWidget` to append a duplicate entry every time `OnParametersSet` fired (e.g., on WASM interactive handoff).  
- Fix: `SetupDataWatchers()` changed back to calling `OnData1Updated()` + `TriggerLinkAnimation()` for the initial seed. `OnData1ReceivedCore` is only called from live MQTT messages.

**Fixed: `#blazor-error-ui` panel invisible**  
- `app.css` had `.blazor-error-boundary` styles but no `#blazor-error-ui` rule at all. The panel was always visible (unstyled, pale yellow from browser default).  
- Fix: added `display:none` default + dark-red/white styling matching the `.blazor-error-boundary` style. Panel is now hidden until Blazor raises an unhandled exception.

**Fixed: Alignment toolbar buttons greyed out**  
- `Display.razor` — six alignment `MudIconButton` elements were using default `Color.Default` (grey). Added `Color="Color.Primary"` to each so they appear clearly active.

**Also:** `TODO.md` cleaned up (fixed items marked, verbose error log stack trace removed). `CHANGELOG.md` updated. `.github/copilot-instructions.md` created.

---

## 2026-03-22 — Grid node feature + bug fixes
**Commit:** `71f4f4d` · 2026-03-22 11:58 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: LogNodeWidget "Collection was modified" race condition**  
- `_entries` was a `readonly List<LogEntry>` mutated in-place from the MQTT callback thread while Blazor's render thread was iterating it.  
- Fix: `_entries` changed to a replaceable field. `OnData1ReceivedCore` builds a new list and assigns it atomically. CLR guarantees reference assignment is atomic, so the render thread always reads a complete list.

**Fixed: Link animation not starting until first value update**  
- `BaseNodeWithDataWidget.SetupDataWatchers()` seeded initial value via `OnData1Updated()` only. `OnData1ReceivedCore()` and `TriggerLinkAnimation()` were not called for the cache seed.  
- Fix: both are now called for the initial seed (later partially reverted — see regression fix above).

**Fixed: Grid size reverts to 20 on entering edit mode**  
- When entering edit mode, `_diagram.Options.GridSize` was null (read-only diagrams don't set it). The code fell back to `AppState.GridSize` which defaulted to 20.  
- Fix: fall back to `_pageStates[_activePageIndex].GridSize` instead. Also aligned `ApplicationState.GridSize` default from 20 → 10.

**Fixed: Grid menu tick marks missing**  
- `AppMenu.razor` grid submenu items were not checking `AppState.GridSize`; no visual indicator of active selection.  
- Fix: added `@(AppState.GridSize == X ? "✓ " : "  ")` prefix, matching the Theme submenu pattern.

**Added: Grid node widget**  
- New `GridNodeModel.cs` (NodeType="Grid") with `GridRowDefinition` (label + list of per-cell topic strings).  
- New `GridNodeWidget.razor` — inherits `BaseNodeWidget<GridNodeModel>`, manages its own per-cell `DataCache.Watch()` subscriptions keyed by `"r{rowIdx}:c{colIdx}"`.  
- Full persistence: `NodeState.GridColumnHeaders` + `NodeState.GridRows` (as `GridRowState` list), serialised/deserialised in `ApplicationState`.  
- Property editor: column headers, row labels, per-cell topic inputs.  
- Registered in `NodeTypePickerDialog`, `AppMenu`, `Display.razor.cs AddNode()`.

---

## 2026-03-21 — Image node, alignment tools, bug fixes
**Commit:** `477b77a` · 2026-03-21 12:21 UTC

### What was done
- **Image node** — new widget for static URL or MQTT-driven image URL. `object-fit` configurable (contain / cover / fill / scale-down). Placeholder icon when no URL set.
- **Node alignment tools** — multi-select toolbar (align left/right/top/bottom, center H/V) appears over the canvas when 2+ nodes selected in edit mode.
- Various bug fixes (see CHANGELOG for full list — auth on clean start, Docker version, save failure handling).

---

## 2026-03-20 — Multi-topic, MudTreeView, dashboard delete, MRU removal
**Commit:** `7155fc6` · 2026-03-20 14:07 UTC

### What was done
- **Variable data topics per node** — replaced fixed `DataTopic`/`DataTopic2` with a dynamic list `DataTopics`. Old files auto-migrated; saves write both formats for backward compat.
- **TreeView rewritten with MudTreeView** — replaced hand-rolled `RenderFragment` builder. Per-topic watchers avoid full-tree rebuild on each update. Expansion state preserved.
- **Dashboard delete** — Open dialog now has trash icon per row with confirmation.
- **MRU list removed** — recent files list removed; Open dialog is the sole entry point.
- **Spurious dirty on subscription add/remove** — `AddSubscriptionAsync`/`RemoveSubscriptionAsync` now only call `MarkEdited()` in edit mode.

---

## 2026-03-20 — Spurious dirty, SignalR null, edit indicator, discard fixes
**Commit:** `109223f` · 2026-03-20 10:30 UTC

### What was done
- Spurious dirty flag on mode switch suppressed during diagram lock/unlock operations.
- Dirty flag after discard fixed — discard now calls `MarkSaved()`.
- Discard now restores full page structure (not just nodes/links).
- Discard now properly exits edit mode (`SetEditMode(false)` called).
- `MarkSaved()` called after `RefreshAll()` on entering edit mode (blank page was showing red on enter).
- SignalR NullReferenceException with `#` wildcard fixed — payload coerced to `""`.
- Log table width fix — `min-width:0` and `overflow-x:hidden` on flex container.
- Edit mode indicator: grey (view), orange (editing clean), red (editing with unsaved changes).
- Page delete now shows confirmation dialog.
- Node properties dialog: backdrop click no longer dismisses it.

---

## 2026-03-20 — Save failure, wildcard Watch, MQTT retain/QoS
**Commit:** `ffe6771` · 2026-03-20 00:28 UTC

### What was done
- **Save failure stays in edit mode** — dashboard no longer closes edit mode if save fails; error snackbar includes filename and hint.
- **Log wildcard topics** — `MqttDataCache.Watch()` now supports `#`/`+` patterns. Log entries show the actual matched topic when using a wildcard subscription.
- **MQTT publish Retain + QoS** — Switch node publishes with configurable Retain flag and QoS level (0/1/2).
- Various MudBlazor UI polish.

---

## 2026-03-19 — Log/TreeView nodes, multi-page, colour transitions
**Commit:** `4b50748` · 2026-03-19 21:51 UTC

### What was done
- **Log node** — scrolling timestamped MQTT message history. Configurable max entries, optional date/time columns.
- **TreeView node** — collapsible topic tree under a configurable root prefix. Optional value column.
- **Multi-page dashboards** — `DiagramState.Pages` list; page tabs above canvas; add/remove pages; legacy single-page files load transparently.
- **Colour transition direction** — `GaugeColorThreshold.Direction` property (`>=`/`<=`).
- **Battery colour thresholds** — migrated from fixed three-band system to ordered `ColorThresholds` list matching Gauge.
- **`ColorTransitionEditor` component** — reusable threshold editor used by both Gauge and Battery.

---

## 2026-03-19 — Bug fixes, service renames, clipboard, startup setting
**Commit:** `c460e75` · 2026-03-19 19:31 UTC

### What was done
- **OS clipboard integration** — copy/paste writes to/reads from `navigator.clipboard`.
- **Startup dashboard setting** — admin-configurable: Last Used / Specific File / None. Stored in `appsettings.user.json`.
- **Service renames** — `IDiagramService` → `IDashboardService`, related dialog renames.
- **Gauge colour fix** — `GetArcColor()` was comparing `Math.Abs(value − origin)` (distance), now compares raw value. First-match semantics.
- Various bug fixes (see CHANGELOG).

---

## 2026-03-19 — Switch, Gauge, Battery nodes
**Commit:** `0543bcc` · 2026-03-19 17:17 UTC

### What was done
- **Switch node** — `MudSwitch<bool>` component, Full/Compact/IconOnly styles, MQTT publish on toggle.
- **Gauge node** — SVG arc gauge with colour thresholds, configurable min/max/unit/arc origin.
- **Battery node** — battery percentage display with colour thresholds.
- Initial `ColorTransitionEditor` scaffolding.
- `FormatText()` / `FormattableValue` moved to `BaseNodeWithDataWidget` base class.
- `TriggerLinkAnimation()` moved to `BaseNodeWithDataWidget`.

---

_Entries above this line represent the Copilot-assisted development history for this project._
_For release-level summaries see [CHANGELOG.md](CHANGELOG.md)._
