# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- **Grid node** — new widget displaying a configurable table of MQTT values. Define column headers and rows; each cell is independently bound to an MQTT topic. Persisted as `GridColumnHeaders` / `GridRows` in the dashboard file.

### Fixed
- **LogNodeWidget "Collection was modified" exception** — `_entries` is now replaced atomically (new list assigned each update) instead of mutated in-place, so the Blazor render thread can never encounter a half-modified list.
- **Link animation not starting until first value update** — `SetupDataWatchers()` now calls `OnData1ReceivedCore()` and `TriggerLinkAnimation()` when seeding the initial value from the data cache, so animations are active as soon as the widget loads.
- **Grid size reverts to 20 on entering edit mode** — when switching into edit mode, the saved page's `GridSize` is now used to restore diagram options instead of the stale `ApplicationState.GridSize` default. Also aligned the `ApplicationState.GridSize` default from 20 → 10 to match `DiagramState`/`PageState` defaults.
- **Grid menu shows active selection** — the Options → Grid submenu now shows a tick (✓) next to the currently active grid size, matching the Theme submenu behaviour.

### Added
- **OS clipboard integration** — copy/paste of nodes now writes to and reads from the browser's native clipboard (via `navigator.clipboard`), enabling cross-window and cross-tab paste. Falls back gracefully to in-memory clipboard if the Clipboard API is unavailable or permission is denied.
- **Startup dashboard setting** — admin-configurable system-wide startup behaviour: *Last Used* (per-browser localStorage restore, previous behaviour), *Specific File* (always open a named dashboard for every new session), or *None* (blank canvas). Configure via **Startup Settings…** in the admin menu. Setting is stored in `appsettings.user.json`.
- `GET /api/settings/startup` and `POST /api/settings/startup` API endpoints.
- **Log node** — new widget showing a scrolling timestamped history of messages received on a topic. Configurable max entries, optional date and time columns.
- **TreeView node** — new widget displaying all live MQTT topics and values under a configurable root prefix in a collapsible tree using `MudTreeView`. Optional value column.
- **Multi-page dashboards** — a dashboard file now holds multiple named pages, each with its own independent canvas. Page tabs appear above the canvas (always visible in edit mode; hidden when only one page in view mode). Add/remove pages from the edit menu or page tab bar. All pages are saved and loaded together in a single `.json` file. Legacy single-page files load transparently as a one-page dashboard.
- **Colour transition direction** — each `GaugeColorThreshold` entry now has a `Direction` property (`>=` or `<=`) so thresholds can match values going upward *or* downward. "Last match wins" evaluation order.
- **Battery colour thresholds** — the Battery node now uses the same ordered `ColorThresholds` list as the Gauge instead of the fixed low/medium/high three-band system. Old `LowColor`/`MedColor`/`HighColor` properties are deprecated (still loaded for backward compatibility and auto-converted to thresholds).
- **`ColorTransitionEditor` component** — reusable MudBlazor component for editing an ordered list of value→colour thresholds with direction selectors; used by both Gauge and Battery property editors.
- **MQTT publish: Retain + QoS** — Switch node (and any future publish node) now has configurable *Retain* flag and *QoS level* (0 = At Most Once, 1 = At Least Once, 2 = Exactly Once). Options appear in the node properties editor under "Publish Options". Plumbed through `ISignalRService` → SignalR hub → `MqttClientService`.
- **Page tab rename** — double-click a page tab in edit mode to rename it inline.
- **Variable data topics per node** — each node now supports a configurable list of MQTT topics (instead of a fixed two-topic limit). The node properties editor shows a dynamic list with per-topic text fields, a clear (✕) adornment on each, and an **Add Topic** button. Old `DataTopic`/`DataTopic2` files are automatically migrated on load; saves write both the new list and the legacy fields for backward compatibility.
- **Dashboard delete from Open dialog** — the Open Dashboard dialog now has a trash icon on each row. Clicking it shows a confirmation prompt and, on confirm, permanently deletes the dashboard file via `DELETE /api/dashboard/{name}`.
- **Image node** — new widget that displays an image from a static URL or a live MQTT topic value (topic publishes the URL). Supports `object-fit` modes (contain / cover / fill / scale-down). Shows a placeholder icon when no URL is configured.
- **Node alignment tools** — in edit mode with 2+ nodes selected, a row of alignment buttons appears: align left, right, top, bottom, center horizontally, center vertically.

### Fixed
- **Auth on clean start** — cookie authentication services were only registered when `Auth:AdminPasswordHash` was already set at startup. On a first-ever run, setting the admin password via the Setup page then trying to log in threw `"No sign-in authentication handlers are registered"`. Auth services and middleware are now always registered unconditionally.
- **Docker image version shows `1.0.0`** — `.git` is now included in the Docker build context so MinVer can resolve the version from tags at compile time. The `BUILD_VERSION` build ARG workaround has been removed.
- **Dashboard save failure no longer silently exits edit mode** — if saving fails (network error, permission denied, etc.), the editor stays open rather than discarding all unsaved work. The error snackbar now includes the file name and a hint to check server logs.
- **Log node wildcard topics (`#`, `+`) now work** — `MqttDataCache.Watch()` previously only matched exact topics; it now supports MQTT wildcard patterns. Log nodes subscribed to `#` or `sensors/+/temp` receive all matching messages. Each log entry also shows the actual topic that fired when the subscription is a wildcard.
- **TreeView node no longer collapses on every update** — the widget previously rebuilt the entire tree on every state change, losing all user-expanded/collapsed state. It now uses per-topic data watchers so only the changed value is updated in-place. New topics cause a structure rebuild that preserves existing expansion state. Changed values are briefly highlighted.
- **SignalR NullReferenceException with `#` wildcard log** — `ConvertPayloadToString()` can return `null` for MQTT messages with empty payloads; the null value was forwarded to SignalR's typed `SendAsync` and caused a crash. The payload is now coerced to `""` before sending.
- **Spurious "unsaved changes" prompt on entering/exiting edit mode** — Blazor.Diagrams fires `Changed` events while locking/unlocking nodes and adding resize controls during mode transitions, which was incorrectly marking the dashboard as edited. Diagram-change tracking is now suppressed during mode switches and diagram loading.
- **Spurious "unsaved changes" prompt after opening a file** — `AddSubscriptionAsync`/`RemoveSubscriptionAsync` were calling `MarkEdited()` unconditionally; they now only do so in edit mode. The startup load path also now calls `MarkSaved()` after loading, preventing a false dirty flag on a clean session start.
- **False dirty flag after discarding changes** — choosing "Discard" when exiting edit mode now correctly clears the edited flag, so opening a new file afterwards no longer prompts for unsaved changes.
- **Discard reverts page additions/deletions** — choosing "Discard" when exiting edit mode now restores the full dashboard state (including page structure) to the snapshot taken when edit mode was entered. Previously, added or deleted pages persisted after discard.
- **Log table fills full node width** — added `min-width:0;` and `overflow-x:hidden` to the flex container so the `MudSimpleTable` correctly occupies 100 % of the widget's width.
- **Node properties dialog no longer dismisses on backdrop click** — clicking outside the node properties editor no longer closes it, preventing accidental loss of in-progress edits.
- **Discard fully exits edit mode** — previously, discarding changes reloaded the snapshot but left edit mode active (grid visible, page tab controls still shown, edit switch still indicating edit mode). Discard now properly calls `SetEditMode(false)` and unsubscribes all edit-mode event handlers.
- **Entering edit mode on blank page no longer shows red (dirty)** — `RefreshAll()` called after enabling edit mode was firing `Changed` events that marked the dashboard as edited. `MarkSaved()` is now called after `RefreshAll()` to clear any spurious dirty flag.
- **"Setup" banner hides when already on the Setup page** — the "Admin password not configured" alert in the header was shown unconditionally when the setup API reported no password set. It now hides itself when the current URL contains `/setup`.
- **Setup page: Enter key submits form** — pressing Enter in either password field on the first-time setup page now submits the form (same as clicking the button).
- **Setup page: Set Password button disabled when input is invalid** — the button is now greyed out when passwords are empty, too short (< 8 characters), or do not match each other.
- **Save on unnamed dashboard defaults to "Default"** — saving a new dashboard that was never given a name previously used an empty string as the filename, resulting in `"Saved ''"`. It now falls back to `"Default"` as the filename and updates the dashboard name accordingly.
- **Grid size default inconsistency (10 vs 20)** — `DiagramState.GridSize` and `PageState.GridSize` defaulted to `20` in the model but the code created new canvases with `10`. Both defaults are now `10`, so new files and newly-added pages are consistent.
- **Home icon now prompts for unsaved changes** — clicking the home/logo icon in the app bar while in edit mode with unsaved changes now shows the "Unsaved Changes — Leave without saving?" confirmation, the same as navigating away by any other means.
- **Gauge colour transitions now compare the raw value, not distance** — `GetArcColor()` was computing `Math.Abs(value − arcOrigin)` (distance from origin) and comparing that against thresholds. It now compares the actual data value directly. Rules also use **first-match** semantics (returns on the first matching threshold) instead of last-match.
- **Gauge tooltip shows "No data topic configured" even when topics are set** — the tooltip was checking the legacy `DataTopic` field instead of the new `DataTopics` list, so topics added via the multi-topic UI were invisible to it.

### Changed
- **TreeView widget uses `MudTreeView`** — replaced the hand-rolled recursive `RenderFragment` builder with MudBlazor `MudTreeView<TreeNode>` / `MudTreeViewItem<TreeNode>` components. Expansion state and highlight behaviour are preserved.
- **Edit mode indicator colour** — the edit-mode toggle switch now uses three colours: grey (view mode), orange/warning (editing, no unsaved changes), red/error (editing with unsaved changes). Previously it was blue/orange.
- **Title bar tooltip** — hovering over the dashboard name in the app bar now shows a tooltip with the file name, display name (if different), and current status (View mode / Editing / Editing — unsaved changes).
- **Page delete confirmation** — deleting a page now shows a confirmation dialog naming the page before removing it. Previously pages were deleted immediately on clicking the ✕ tab button.
- **Default dashboard file renamed** — the built-in default file is now `Default.json` (was `diagram.json`). Existing `diagram.json` files are automatically renamed on first startup.
- **Display name separated from file name** — `DiagramState.Name` (the human-readable dashboard title) is now stored separately from the file stem used to save/load. "Save As" changes the file name but leaves the display name unchanged. The title bar shows the display name if set, otherwise falls back to the file name.
- **Service renames** — `IDiagramService` → `IDashboardService`, `DiagramService` → `DashboardService`, `ServerDiagramService` → `ServerDashboardService`. Dialog components `DiagramPickerDialog` → `DashboardPickerDialog`, `DiagramPropertiesDialog` → `DashboardPropertiesDialog`. The `Diagram` name is now reserved exclusively for Blazor.Diagrams canvas components.
- **Switch widget uses `MudSwitch`** — replaced the custom chip+icon-button toggle with a proper `MudBlazor.MudSwitch<bool>` component in Full and Compact styles. IconOnly style retains the icon button.
- **Log widget uses `MudSimpleTable`** — replaced raw HTML divs with a `MudSimpleTable` for consistent MudBlazor styling.
- **Page tabs use MudBlazor buttons** — replaced the custom hand-rolled tab bar with `MudButton`/`MudButtonGroup` components for consistent styling and behaviour.
- **MRU (recent files) removed** — the recent files list in the app menu has been removed. The Open dialog is the sole entry point for switching dashboards. The `localStorage` key for recent files is no longer written.
- **`FormatText` in base class** — `FormatText()` and `FormattableValue` helper moved to `BaseNodeWithDataWidget`; format syntax (`{0:0}`, `{0:F2}`, etc.) now works identically in Text, Gauge, and Battery node text fields.
- **Link animation in base class** — `TriggerLinkAnimation()` moved to `BaseNodeWithDataWidget`; all node types now support the Link Animation property without per-widget code
- **Exit-edit prompt** — switching out of edit mode (or clicking the view button) when the dashboard has unsaved changes now shows a Save / Discard / Cancel dialog
- File/Save now saves to the currently-open filename (was always saving to `diagram.json`); snackbar confirms the filename saved
- **About dialog** — deployment type moved to the Runtime/Debug section; "Last Checked" is now a tooltip on the Latest Version row (not a separate row); "Check" button is inline with the version; "Up to date" chip is on the same row as the version; Close button replaced with the dialog's ✕ button; dialog widened to `MaxWidth.Medium`.
- **Gauge colour transition label updated** — node property editor label changed from "by distance from Arc Origin" to "by value", and "Last matching rule wins" to "First matching rule wins".

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
