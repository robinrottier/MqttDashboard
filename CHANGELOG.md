# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- **Multiple node types** — click "Add Node" to choose from a picker:
  - **Text node** — existing display node (icon + formatted text with MQTT value substitution)
  - **Gauge node** — SVG semicircular arc gauge with configurable min/max/unit; arc colour shifts green→yellow→red as value approaches max
  - **Switch node** — shows current ON/OFF state from a data topic; toggle button publishes a configurable payload back to MQTT
- **MQTT publish** — new `PublishMessageAsync` path through SignalR hub → `MqttClientService` → broker (used by Switch node)
- Node type picker dialog shown when adding a new node in edit mode
- **Gauge: MidPoint** — optional midpoint value; when set, arc is split into negative (red by default) and positive (green by default) regions with a tick mark at the midpoint; colours configurable via `NegativeColor` / `PositiveColor` properties
- **Gauge: Text label** — static text label rendered below the arc (previously unused)
- **Widget base classes** — `BaseNodeWidget<T>` and `BaseNodeWithDataWidget<T>` reduce duplication across all widget types; all widgets refactored to inherit from these bases

### Changed
- Node property editor now shows type-specific settings (Gauge: min/max/unit, midpoint, colours; Switch: publish topic, ON/OFF values)
- Gauge title is centred above the arc

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
