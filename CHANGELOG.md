# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- **OS clipboard integration** — copy/paste of nodes now writes to and reads from the browser's native clipboard (via `navigator.clipboard`), enabling cross-window and cross-tab paste. Falls back gracefully to in-memory clipboard if the Clipboard API is unavailable or permission is denied.
- **Startup dashboard setting** — admin-configurable system-wide startup behaviour: *Last Used* (per-browser localStorage restore, previous behaviour), *Specific File* (always open a named dashboard for every new session), or *None* (blank canvas). Configure via **Startup Settings…** in the admin menu. Setting is stored in `appsettings.user.json`.
- `GET /api/settings/startup` and `POST /api/settings/startup` API endpoints.
- **Log node** — new widget showing a scrolling timestamped history of messages received on a topic. Configurable max entries, optional date and time columns.
- **TreeView node** — new widget displaying all live MQTT topics and values under a configurable root prefix in a collapsible tree. Optional value column.
- **Multi-page dashboards** — a dashboard file now holds multiple named pages, each with its own independent canvas. Page tabs appear above the canvas (always visible in edit mode; hidden when only one page in view mode). Add/remove pages from the edit menu or page tab bar. All pages are saved and loaded together in a single `.json` file. Legacy single-page files load transparently as a one-page dashboard.
- **Colour transition direction** — each `GaugeColorThreshold` entry now has a `Direction` property (`>=` or `<=`) so thresholds can match values going upward *or* downward. "Last match wins" evaluation order.
- **Battery colour thresholds** — the Battery node now uses the same ordered `ColorThresholds` list as the Gauge instead of the fixed low/medium/high three-band system. Old `LowColor`/`MedColor`/`HighColor` properties are deprecated (still loaded for backward compatibility and auto-converted to thresholds).
- **`ColorTransitionEditor` component** — reusable MudBlazor component for editing an ordered list of value→colour thresholds with direction selectors; used by both Gauge and Battery property editors.

### Fixed
- **Auth on clean start** — cookie authentication services were only registered when `Auth:AdminPasswordHash` was already set at startup. On a first-ever run, setting the admin password via the Setup page then trying to log in threw `"No sign-in authentication handlers are registered"`. Auth services and middleware are now always registered unconditionally.
- **Docker image version shows `1.0.0`** — `.git` is now included in the Docker build context so MinVer can resolve the version from tags at compile time. The `BUILD_VERSION` build ARG workaround has been removed.

### Changed
- **Default dashboard file renamed** — the built-in default file is now `Default.json` (was `diagram.json`). Existing `diagram.json` files are automatically renamed on first startup.
- **Display name separated from file name** — `DiagramState.Name` (the human-readable dashboard title) is now stored separately from the file stem used to save/load. "Save As" changes the file name but leaves the display name unchanged. The title bar shows the display name if set, otherwise falls back to the file name.
- **Service renames** — `IDiagramService` → `IDashboardService`, `DiagramService` → `DashboardService`, `ServerDiagramService` → `ServerDashboardService`. Dialog components `DiagramPickerDialog` → `DashboardPickerDialog`, `DiagramPropertiesDialog` → `DashboardPropertiesDialog`. The `Diagram` name is now reserved exclusively for Blazor.Diagrams canvas components.


  - **Text node** — existing display node (icon + formatted text with MQTT value substitution)
  - **Gauge node** — SVG semicircular arc gauge with configurable min/max/unit; arc colour shifts green→yellow→red as value approaches max
  - **Switch node** — shows current ON/OFF state from a data topic; toggle button publishes a configurable payload back to MQTT
  - **Battery node** — SVG battery icon with colour-coded fill level (red/amber/green); configurable min/max, low/med/high colours, optional percentage display
- **MQTT publish** — new `PublishMessageAsync` path through SignalR hub → `MqttClientService` → broker (used by Switch node)
- Node type picker dialog shown when adding a new node in edit mode
- **Gauge: ArcOrigin / colour thresholds** — arc can be drawn from a configurable value (e.g. 0 in a ±1000 range) rather than always from the minimum; ordered list of colour threshold breakpoints (value → colour, direction-aware)
- **Gauge/Switch: title position** — `TitlePosition` property (Above / Below / Left / Right) on all nodes; title drawn horizontally in all positions
- **Switch: style modes** — `SwitchStyle` property: Full (icon + text), Compact (icon + small text), Icon-only; `OnText` / `OffText` properties for configurable labels
- **Switch: read-only mode** — `IsReadOnly` property; when set, toggle is disabled and no MQTT publish occurs
- **Gauge: Text label** — static or formatted text rendered below the arc (supports `{0:F1}` / `{0:0}` MQTT value substitution)
- **Widget base classes** — `BaseNodeWidget<T>` and `BaseNodeWithDataWidget<T>` reduce duplication; all widgets inherit from these bases
- **FormatText in base class** — `FormatText()` and `FormattableValue` helper moved to `BaseNodeWithDataWidget`; format syntax (`{0:0}`, `{0:F2}`, etc.) now works identically in Text, Gauge, and Battery node text fields
- **Link animation in base class** — `TriggerLinkAnimation()` moved to `BaseNodeWithDataWidget`; all node types now support the Link Animation property without per-widget code
- **Exit-edit prompt** — switching out of edit mode (or clicking the view button) when the dashboard has unsaved changes now shows a Save / Discard / Cancel dialog
- File/Save now saves to the currently-open filename (was always saving to `diagram.json`); snackbar confirms the filename saved

### Fixed
- MQTT reconnect storm — `MQTTnet` v5 fires `DisconnectedAsync` even on failed `ConnectAsync` attempts; added `_isReconnecting` interlocked flag to prevent cascading parallel reconnect loops
- Blazor Server JSInterop errors after circuit disconnect — `InvokeAsync(StateHasChanged)` now guarded by `_disposed` flag and wrapped in try/catch; prevents `InvalidOperationException` spam in logs
- Battery / Gauge `Text` property did not accept `{0:0}` numeric format syntax — now handled via shared `FormatText()` base class method
- Switch compact layout stability — min-width on text span prevents reflow when toggling ON/OFF
- Switch icon size — Full style uses Large icon, Compact uses Medium

### Changed
- Node property editor shows type-specific settings for all node types (Gauge: arc origin, colour thresholds; Switch: style, read-only, on/off text; Battery: min/max, colours, show-percent)
- About dialog: title is "About Mqtt Dashboard"; application section header removed; layout condensed; "Up to date" chip inline with version; "Check" button inline with last-checked date; Deployment row reordered above Latest version

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
