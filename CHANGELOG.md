# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- **Data topic management in Dashboard Properties** — MQTT topics are now managed via the Dashboard Properties dialog (topic list with add/remove controls). Dashboard is marked dirty when topics change, so topics are always saved with the dashboard.
- **"No topics" overlay on Display page** — when no data topics are configured, a centered prompt guides users to Dashboard Properties to add topics (edit mode only; view mode shows an info message).
- **"Add Port → All" option** — new menu item adds all 4 ports (Top, Bottom, Left, Right) to the selected node at once.
- **Same Width / Same Height alignment** — two new buttons in the multi-select alignment toolbar resize all selected nodes to the widest/tallest node's dimensions.
- **Dashboard file metadata** — each saved dashboard now includes a `FileInfo` object with `WrittenAt` (ISO timestamp) and `Filename` at the end of the file.
- **Settings persistence to data directory (FEAT-M)** — `appsettings.user.json` (admin password hash, startup mode) is now written to and loaded from the volume-mounted data directory instead of the container root. Settings survive Docker container restarts and redeployments. Includes one-time migration from old location.
- **Restart from web UI (FEAT-N)** — Docker deployments now show a "Restart Now" button in the update notification banner. After running `docker compose pull`, clicking the button gracefully stops the app and Docker's restart policy brings it back on the new image.

### Changed
- **Node property editor dialog title** now reads "Edit {Type} Node Properties" (e.g. "Edit Gauge Node Properties").
- **Dashboard file format redesigned** — replaced flat `DiagramState`/`NodeState` with a clean nested `DashboardModel` hierarchy. Each node type owns its own serialization (`ToData()`/`FromData()`). Old files are not compatible (new format is explicitly breaking). `MudNodeModel` renamed to `TextNodeModel`; `MudPortModel` renamed to `NodePortModel`.
- **Title and Title Position** are on one compact row in the node property editor.
- **Background Image + Image Fit** moved to the top common section of node properties and shown on one compact row.
- **Icon Color** now uses the same composite ColorPicker widget (CSS/Hex/Theme) as other color properties.
- **Canvas Background** in Dashboard Properties uses the composite ColorPicker widget on a single line.
- **Grid** section removed from Dashboard Properties dialog and from the Options menu; edit mode always shows a 20px grid.
- **New dashboard defaults**: `ShowDiagramName = true`, `GridSize = 20`.
- **App title** changed to "MQTT Dashboard" (was "Mqtt Dashboard").
- **Origin/zero-point** label replaces "Arc midpoint" in the numeric range editor.
- **Serialization improvements**: `NodeType` is always the first field in each node JSON object; top-level fields ordered logically (Name → ShowDiagramName → GridSize → BackgroundColor → Pages → MqttSubscriptions → Nodes → Links → FileInfo); coordinates rounded to 2 decimal places; legacy `DataTopic` / `DataTopic2` scalar fields removed (only `DataTopics` list is written); null and empty node properties omitted from output.

### Fixed
- **Grid snapping on load** — diagram grid size is now synced to `AppState.GridSize` when a dashboard loads, so snapping works immediately without toggling the grid menu.
- **Save As: overwrite prompt** — Save As now always prompts for overwrite confirmation when the chosen filename already exists (previously skipped when saving to the same name).
- **Port menu disabled states** — Add Port sub-items are greyed when the port already exists; Delete Port sub-items are greyed when the port is absent.
- **Pasted nodes stay selected** — after a paste, all pasted nodes remain selected so they can be moved as a group immediately.
- **Selecting a node no longer sets the dirty flag** — deferred clearing of the pending-dirty flag now handles the case where Blazor.Diagrams fires `SelectionChanged` before `node.Changed`.

### Removed
- **Data page removed** — the separate `/data` page (topic management, data cache explorer, message log) has been removed. Topic management has moved to Dashboard Properties.
- **"Node Properties" subtitle** and redundant node-type display line removed from the node property editor.
- **"MQTT Data Binding" section header** and divider after link animation removed from the node property editor.
- **"Title Bar" section header** removed from Dashboard Properties dialog.

- **Icon rendering in all node types** — StandardNodeLayout now renders Node.Icon alongside Node.Title for all visual node types (Gauge, Battery, Switch, Text). Previously only Text nodes rendered icons.
- **Node-type-specific properties auto-rendered** — NodePropertyEditor no longer has any `@if (Node is XxxModel)` blocks; properties appear from `[NpXxx]` model attributes automatically. Adding a new node type requires only annotating its model properties.
- **Battery: value topic index + color topic index** — Battery nodes now have the same DataTopicIndex and ColorTopicIndex controls as Gauge nodes.
- **Undo All** — new Edit menu item that reverts all unsaved changes in a single step.
- **Gauge: text position** — the static Text label can now be displayed above or below the gauge arc.
- **Gauge: value topic index** — specify which data topic (0-based) drives the gauge arc and displayed value.
- **Gauge / Battery: color transition topic index** — single Color Topic setting controls which data topic drives all color transition rules.
- **Log: independent column toggles** — six checkboxes (Date / Time / Full Topic / Topic Path / Topic Name / Value) independently control which log columns are visible.
- **Log node pause button** — Pause/Play icon button in the log widget header allows freezing the log.
- **Reconnect value replay** — after a SignalR reconnect, the server pushes last-known values to the client immediately.
- **Log node** — scrolling timestamped history of messages received on a topic.
- **TreeView node** — displays all live MQTT topics and values under a configurable root prefix.
- **Multi-page dashboards** — a dashboard file holds multiple named pages. Page tabs appear above the canvas.
- **Colour transition direction** — each threshold entry has a `Direction` property (>= or <=).
- **Battery colour thresholds** — Battery uses the same ordered ColorThresholds list as Gauge.
- **ColorTransitionEditor component** — reusable component for editing ordered value→colour thresholds.
- **MQTT publish: Retain + QoS** — Switch node has configurable Retain flag and QoS level (0/1/2).
- **Page tab rename** — double-click a page tab in edit mode to rename it inline.
- **Variable data topics per node** — configurable list of MQTT topics per node. Old DataTopic/DataTopic2 files auto-migrated.
- **Dashboard delete from Open dialog** — trash icon with confirmation prompt.
- **Node alignment tools** — in edit mode with 2+ nodes selected, alignment buttons appear.
- **OS clipboard integration** — copy/paste of nodes uses the browser's native clipboard.
- **Startup dashboard setting** — admin-configurable: Last Used, Specific File, or None.
- **Color transition "Else" fallback** — each Gauge/Battery color transition now has an optional "Else Color" that applies when no threshold rule matches. Previously a hardcoded percent-based default was used.
- **`ColorInputRow` component** — reusable color input row (swatch preview + editable text + Theme/Named/Custom picker buttons + optional clear) used in NodePropertyEditor background color and ColorTransitionEditor threshold rows.

### Changed
- **`MudNodeWidget` (Text node)** now uses `StandardNodeLayout` like all other visual nodes — gains multi-topic-aware tooltip, background image support, and consistent port rendering.
- **`GaugeNodeModel` and `BatteryNodeModel`** — flat MinValue/MaxValue/ArcOrigin/DataTopicIndex replaced by `NumericRangeSettings Range` group. Old dashboard files load correctly.
- **`FormatText` in base class** — format syntax (`{0:0}`, `{0:F2}`, etc.) works identically in Text, Gauge, and Battery.
- **Link animation in base class** — `TriggerLinkAnimation()` moved to `BaseNodeWithDataWidget`; all node types support Link Animation without per-widget code.
- **TreeView widget uses `MudTreeView`** — replaced hand-rolled recursive RenderFragment.
- **Edit mode indicator colour** — grey (view), orange (editing), red (editing with unsaved changes).
- **Page delete confirmation** — confirmation dialog before removing a page.
- **Default dashboard file renamed** — built-in default is now `Default.json` (was `diagram.json`). Existing files auto-renamed on first startup.
- **Display name separated from file name** — human-readable title stored separately from the file stem.
- **Service renames** — IDiagramService → IDashboardService etc.
- **Switch widget uses `MudSwitch`** — replaced custom chip+icon-button.
- **Log widget uses `MudSimpleTable`**.
- **Exit-edit prompt** — shows Save / Discard / Cancel when exiting edit mode with unsaved changes.
- File/Save now saves to the currently-open filename.

### Removed
- **`BackgroundImageFromData` property** — removed from all active code paths (kept as legacy null field in `NodeState` for file compatibility).
- **Duplicate icon/tooltip/port code** in `MudNodeWidget` — replaced by `StandardNodeLayout` (~90 lines removed).
- **Hardcoded node-type `@if` blocks** in `NodePropertyEditor` (~135 lines across 5 blocks) — replaced by 4-line `NodePropertyRenderer` loop.
- **Image node** — removed as a separate node type. Use any node with the background image URL property. Old `Image` nodes load correctly with the image URL preserved.
- **Grid node** — removed. Wildcard topic → row/column binding is deferred to a future release.
- **MRU (recent files)** — removed; the Open dialog is the sole entry point.

### Fixed
- **InvalidCharacterError on hover/render** — SVG `MarkupString` injection in Gauge and Battery widgets used `HtmlEncode` which does not strip null bytes. SVG is XML-strict and rejects null bytes even when HTML-encoded. Fixed by: (1) sanitizing all incoming MQTT string values at `MqttDataCache.UpdateValue` (client-side gateway, covers live and server-replayed cached values), and (2) switching Gauge/Battery SVG text injection to `XmlStringHelper.XmlSafeEncode` (strips invalid XML chars then HTML-encodes). Previous fix only sanitized at the server source; values already in the server's in-memory cache bypassed that.
- **InvalidCharacterError crash** — MQTT payloads containing null bytes or other HTML-invalid characters killed the Blazor circuit. Fixed by sanitizing at source in `MqttClientService`, HTML-encoding `BatteryNodeWidget` MarkupString content, and sanitizing `DataValueTooltipContent` output.
- **Image title not hidden correctly** — `StandardNodeLayout` now checks `ShowTitle` for both title positions.
- **Node infinite resize loop** — clearing the Title field caused the node to grow indefinitely. Fixed by always rendering the header hidden with `display:none` when empty.
- **Ports invisible on all non-Text nodes** — `overflow:hidden` was clipping ports; `pa-1` padding created a blank border ring. Both removed.
- **Alignment toolbar buttons unclickable** — canvas was intercepting clicks. Fixed with `position:relative` and `z-index:1000` on the toolbar overlay.
- **File → New incorrectly enables Save** — Save is now disabled when no filename is set.
- **Save As overwrites without confirmation** — Save As now checks the file list and shows an Overwrite? dialog.
- **Link animations not shown on startup** — `TriggerLinkAnimation` now called in `OnAfterRenderAsync(firstRender:true)` after the SVG is in the DOM.
- **MQTT reconnect storm** — interlocked flag prevents cascading parallel reconnect loops.
- **Battery / Gauge `Text` format syntax** — `{0:0}` format now handled via shared `FormatText()` base class method.
- **Log node wildcard topics** — `MqttDataCache.Watch()` now supports MQTT wildcard patterns (`#`, `+`).
- **TreeView node no longer collapses on every update** — per-topic watchers update only the changed value in-place.
- **Auth on clean start** — auth services now always registered unconditionally.
- **Gauge colour transitions compare raw value** — was comparing `Math.Abs(value − arcOrigin)`; now compares the actual data value.
- Various dirty-flag, discard, and edit-mode prompt fixes (see DEVCHANGELOG for details).

---
## [0.1.2] - 2026-03-18

### Fixed
- Browser tab title was "MqttBashboard.client" — now reads "Mqtt Dashboard"
- About box was not showing the application version — now reads from `AssemblyInformationalVersionAttribute` (MinVer), git SHA suffix trimmed
- All user-facing references to "diagram/diagrams" renamed to "dashboard/dashboards" throughout the UI (Save As dialog, menus, snackbars, property editor, etc.)
- Dashboard files moved from the root data directory into a `dashboards/` subdirectory; existing files are auto-migrated on first startup
- MQTT subscriptions were stored separately in `applicationstate.json` — now embedded in the dashboard `.json` file itself; `applicationstate.json` and all related server/client services removed
- About box in admin mode now shows additional server deployment info: machine name, OS, .NET version, data directory, runtime identifier
- About box update checker and "Check for updates" button now only visible to admin users
- MRU (recent files) list was showing files that had since been deleted — list is now filtered against the server file list on startup and invalid entries are removed when opened
- Opening a dashboard file now correctly restores and activates its MQTT subscriptions via SignalR
- `Show dashboard name in title bar` setting was not persisted in the dashboard file — now saved and restored
- Authentication state was initialised after dashboard load; if load failed, auth was left disabled (login button hidden). Auth state is now initialised first.

### Removed
- `applicationstate.json` file format and all associated code (`ApplicationStateData`, `IApplicationStateService`, `ApplicationStateService`, `ServerApplicationStateService`, `ApplicationStateController`)

---

## [0.1.1] - initial tagged release

_No changelog entry — this predates the changelog._
