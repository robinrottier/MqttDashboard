# Developer Changelog

Detailed record of each Copilot-assisted work session — what was investigated, changed, and why.
For reviewing work item by item and moving anything back to [TODO.md](TODO.md) if needed.

The standard [CHANGELOG.md](CHANGELOG.md) contains release-level summaries following Keep a Changelog.

---

## 2026-03-22 — Undo state fixes, log columns, color transition per-node, UndoAll

**Commit:** _(to be filled after commit)_
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

## 2026-03-22 — Gauge enhancements, log topic column, color transition topic index
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
