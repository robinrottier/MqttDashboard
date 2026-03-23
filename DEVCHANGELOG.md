# Developer Changelog

Detailed record of each Copilot-assisted work session — what was investigated, changed, and why.
For reviewing work item by item and moving anything back to [TODO.md](TODO.md) if needed.

The standard [CHANGELOG.md](CHANGELOG.md) contains release-level summaries following Keep a Changelog.

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
