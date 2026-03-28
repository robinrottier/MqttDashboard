# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- **Auto-save on exit edit mode** — new Options menu item "Auto-save on Exit" (visible while in edit mode). When enabled, exiting edit mode saves automatically without prompting. Preference is persisted to browser localStorage so it survives page reloads.
- **Edit mode and login/logout in Options menu** — always accessible from the hamburger menu regardless of screen width. "Edit Mode" toggles edit mode with a checkmark indicator; "Logout" / "Login as Admin" appear when auth is configured.
- **Theme preference persisted** — selected theme (Light/Dark/Auto) is now saved to localStorage and restored on page load.
- **Read-only deployment mode (`ReadOnly=true`)** — set as env var or config to disable all edit UI and block all write APIs. Ideal for public displays.
- **Dual-port read-only mode (`ReadOnlyPorts`)** — single process listens on two ports; specific ports are read-only while others remain editable. Shares MQTT connection and data cache between both ports. Example: `ReadOnlyPorts=8080` with `ASPNETCORE_URLS=http://+:8080;http://+:8081`.
- **`RenderMode=Server` for WebApp image** — run the standard Docker image in Blazor Server mode (no WASM download) by setting `RenderMode=Server`.
- **Deployment modes guide** — new `documents/deployment-modes.md` covering all access-control and render-mode options including future plans.

### Fixed
- **Node without a title no longer grows indefinitely** — set `ControlledSize = true` on `TextNodeModel` so Blazor.Diagrams' ResizeObserver is never activated for our nodes. We manage all node sizes explicitly via CSS and the resize handle; the observer was creating a sub-pixel feedback loop.
- **Grid no longer visible in view mode** — `GridSize` is now cleared to `null` on the diagram options when leaving edit mode.
- **Import dialog "Import" button now enables correctly** — replaced the conflicting `@bind-Value` + `Immediate` + `@oninput` triple on `MudTextField` with a clean `Value` / `ValueChanged` pattern that reliably triggers JSON parsing on every change.
- **Grid snap-to-centre setting is now correctly saved and restored** — was previously lost on reload because the negative-sign convention was decoded before `GridSnapToCenter` was set.
- **TreeView no longer collapses or loses focus on MQTT updates** — replaced MudTreeView/MudTreeViewItem with a lightweight custom div-based renderer; expansion state lives on the model, not inside MudBlazor component state. Added 80 ms debounce to coalesce rapid message bursts into a single render.
- **Import dialog no longer grows when status message appears** — reserved a fixed-height area for the parse-result alert so the dialog stays the same height whether an alert is visible or not.
- **Update-available banner removed from main layout** — was too intrusive; the About dialog already provides version info and the Restart button.

### Changed
- **Import / Export moved to File menu** — was in Edit menu; now in File menu (still gated on edit mode).
- **Grid size enforced to 5–100 px (step 5) in edit mode** — the old negative-value convention replaced by an explicit `gridSnapToCenter` boolean.
- **TreeView root topic now uses standard DataTopics** — the separate "Root Topic" property has been removed; set the topic via the standard MQTT Topics field (same as all other widgets). Existing saved dashboards migrate automatically.
- **TreeView visual improvements** — font reduced to 0.7 rem; value is now bold and right-aligned on each row; updated topics briefly highlight for 2 seconds.

### Added
- **Import / Export via JSON clipboard (FEAT-E)** — Export shows JSON for selected nodes or the current page; Import accepts that JSON (or pastes from clipboard) and adds to the current page or a new page.


- **Data topic management in Dashboard Properties** — MQTT topics are now managed via the Dashboard Properties dialog (topic list with add/remove controls). Dashboard is marked dirty when topics change, so topics are always saved with the dashboard.
- **"No data topics" banner on Display page** — when no data topics are configured, a warning banner at the top of the canvas guides users to Dashboard Properties. In edit mode shows a "Configure Topics" action button.
- **"Add Port → All" option** — new menu item adds all 4 ports (Top, Bottom, Left, Right) to the selected node at once.
- **Same Width / Same Height alignment** — two new buttons in the multi-select alignment toolbar resize all selected nodes to the widest/tallest node's dimensions.
- **Dashboard file metadata** — each saved dashboard now includes a `FileInfo` object with `WrittenAt` (ISO timestamp) and `Filename` at the end of the file.
- **Settings persistence to data directory (FEAT-M)** — `appsettings.user.json` (admin password hash, startup mode) is now written to and loaded from the volume-mounted data directory instead of the container root. Settings survive Docker container restarts and redeployments. Includes one-time migration from old location.
- **Restart from web UI (FEAT-N)** — Docker deployments now show a "Restart Now" button in the update notification banner. After running `docker compose pull`, clicking the button gracefully stops the app and Docker's restart policy brings it back on the new image.

### Fixed
- **No-data topics message** is now a slim banner at the top of the canvas instead of a centred overlay card.
- **`appsettings.user` no longer appears** in the Open Dashboard list.
- **Grid size defaults to 20 px** in edit mode when no grid has been explicitly configured.

### Changed
- **Dashboard Properties** dialog now includes Grid Size (px) and Snap-to-Centre controls (were accidentally dropped in the last refactor).
- **Options > Show > Dashboard Name** menu item removed — setting is now in the Dashboard Properties dialog.
- **Page > Home** menu removed — no longer relevant.
- **About dialog** now shows a "Restart App" button for admins on Docker deployments regardless of whether an update is available.

### Removed
- **"No topics" centered overlay** — replaced with a top banner (see Fixed above).
- **Options > Show > Dashboard Name** menu item — setting moved to Dashboard Properties dialog.
- **Page > Home** menu — no longer relevant.

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
