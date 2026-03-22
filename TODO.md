# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS
- [ ] Lines animated on first SSR render then cleared — lines in blazor.diagrams SVG layer (SSR→WASM handoff timing); also lines only drawn on first data update rather than on first value
- [ ] Dirty flag still fires on selection (enter edit mode, select node → dirty; investigate `_pendingDirtyMark` pattern in `OnDiagramChanged`)
- [ ] Log view changes width depending on message length — should fill widget width, not content width
- [ ] F5 full-page refresh: many link animations fire then clear before settling — possibly Blazor Diagrams SVG layer not pre-rendering

## 🟡 Enhancements

- [ ] Server-side "lazy cache": if client request is dropped, server should keep data live for a configurable delay (e.g. 30s) before removing references
- [ ] Property transition
	- [x] `ColorTopicIndex` and `DataTopicIndex` per node — **done for both Gauge and Battery**
	- [x] `ColorTransition` class wraps `ColorTopicIndex` + `ColorThresholds` — **done; `GaugeColor` on GaugeNodeModel, `BatteryColor` on BatteryNodeModel**
	- [ ] The color boxes in the transition/colour editor should have a chooser popup (via a button) to help with selecting the various types and well-known values
	- [ ] this would be same as "Background color" for main node property, so either 3 small buttons
	      or a single button goes to a dialog with 3 tabs, one for each of the colour modes
	- [ ] Color transition "when" needs "else" condition to specify default colour when no conditions met
	- [ ] Also a means to drag reordering around the conditions to specify which is first match
- [ ] In future, colour transitions may drive other properties (e.g. intensity, flashing, shading) — bear that in mind for the model
- [ ] Data item topics per node
	- [ ] "Link animation" has property for index of which data item to animate upon
- [ ] Page tabs
	- [ ] Use MudTabs and related controls for displaying. MudTabs has a different model...every page is rendered inside tab component BUT maybe there's a way to use index of selected tab to render it outside MudTabs component?
	- [ ] Position option for tabs: top/left/right/bottom in dashboard properties
	- [ ] Drag to reorder pages when in edit mode. MudTabs would support this but need setting noticed and saved.
- [ ] Node properties dialog
	- [ ] Can this dialog be moveable and have apply button to changes dynamically without closing
- [ ] IMage:
	- [ ] also needs option to upload a bitmap and stored locally as content  or should it be byte values in dashboard file?)
	- [ ] option to go "behind" or "ontop" other nodes.. maybe z-order roperty for all nodes? HOw does this fit in with blazor.diagrams, maybe it has it already
- [ ] Log viewer columns: choices for date (and format), time (and format), topic path, topic name, topic full path&name, value — **Full 6-column boolean options done**; date/time format options still open
- [ ] Log view needs a "pause" button to stop updates. — **Done** (previous session)
- [ ] mqtt publishing should have other parameters (e.g. message expiry)
- [ ] Confirm- mqtt publishing is reusable compoennts (especially configuration of it in node properties)


## 🟡 Features

### FEAT-A: MQTT topic wildcards per node
- [ ] Allow node `DataTopic` to use `#` / `+` wildcards (e.g. `home/sensors/#`)
- [ ] In node text, use named substitution syntax like `{power}` where the key is the trailing topic segment
- [ ] `MqttTopicSubscriptionManager` already handles wildcard routing server-side; extend client binding

### FEAT-B: MQTT data processing / calculated values
- [ ] Support simple expressions/transforms on incoming values before display (unit conversion, arithmetic, string concat)
- [ ] Option: "virtual topics" defined at dashboard level, computed from raw MQTT values, reusable across nodes
- [ ] Option to write calculated values back to the MQTT broker

### FEAT-C: Additional node types _(Gauge, Switch, Battery, Log, TreeView, Image done — see CHANGELOG)_
- [ ] **Text node** - different node shapes (circle, diamond, etc.)
- [ ] **Grid** — table with rows/columns mapped to MQTT values
	- [ ] Data topic like path/blah/+/+ ...row is from first "+" and column from second +
	- [ ] So could also have "path/blah/+/value/+"
	- [ ] and "path/blah/+/+/value" would mean row from first match againt + , column name in 2nd + match BUT actual value taken form value field there
	- [ ] This could be a whole load of test cases
- [ ] **Chart** — in-memory time-series sparkline graph
- [ ] **Markdown / HTML** — formatted static content, optionally with data substitution
- [ ] **IFrame** — embed another web page

### FEAT-D: Multiple dashboard pages _(basic multi-page done — see CHANGELOG)_
- [ ] Page tab overflow handling (scrolling/dropdown when many pages)
- [ ] Swipe left/right gesture on mobile
- [ ] Page reordering (drag tabs)
- [ ] Current "Data" could be replaced by an optional page (tree view and log of all live values)
	- [ ] user would create this is they wanted it manually or its some how a quick add page option when you create a new, empty dashboard
	- [ ] ...just need a way to specify dashboard set of requests ..so maybe thats in Dashboard properties dialog

### FEAT-E: Editing improvements
- [ ] Node-red style palette panel — drag node types from a sidebar onto the canvas
- [ ] Import/export selected nodes or a whole page as JSON (clipboard)
- [x] Node alignment and distribution tools (align left/right/top/bottom, distribute evenly) — **alignment done**

### FEAT-F: Link improvements
- [ ] Links as proper model objects with a properties editor: color, thickness, dash style
- [ ] Arrow heads to show flow direction
- [ ] Data-driven link styling — color/intensity driven by a topic value
- [ ] Draggable Bezier control points
- [ ] Fork/junction points between links

### FEAT-G: Grouping / layout containers
- [ ] "Group" box — labeled background rectangle that visually wraps related nodes
- [ ] Moving a group moves all contained nodes

### FEAT-H: Alternate data sources / plugin architecture
- [ ] Plugin architecture for data sources beyond MQTT
- [ ] Built-in integrations: REST APIs, WebSockets, Home Assistant local API, Emoncms feeds and time-series
- [ ] Admin configures available plugins; nodes select source and configure connection
- [ ] Active data sources stored in the dashboard file

### FEAT-I: Responsive / mobile layout
- [ ] Responsive layout adapts to screen size
- [ ] Per-page optional alternative layout for mobile
- [ ] Touch-friendly editing interactions

### FEAT-J: User management & auth
- [ ] Multi-user with roles: read-only / read-write / admin
- [ ] Builds on existing `ServerAuthService` / admin password hash mechanism
- [ ] User registration mechanism (username, email, password) with email verification.

### FEAT-K: Dashboard versioning & Git integration
- [ ] Snapshot history for dashboards (previous versions, compare/diff)
- [ ] Optional Git commit/push of dashboard changes to a remote repo

### FEAT-L: Deployment enhancements
- [ ] Single Docker image supporting both server-only and WASM modes (different ports, shared data dir)
- [ ] Read-only runtime mode — view-only, no login/edit UI exposed
- [ ] Admin interface: runtime monitoring, logs, connected clients, dashboard file management

### FEAT-M: Settings persistence
- [ ] User set application settings should be in data directory so persist over deployments.:
	- [ ] Startup behaviour
	- [ ] Admin password hash
	- [ ] ...this is probably in the applicationstate file

### FEAT-N: Self-updating deployment
- [ ] Deployment version / update checking
	- [ ] Latest version checks checks for tags ... but the actual docker image may not be available for some time later. Can it check actual images in ghcr?
	- [ ] We want to be able to select beta/non-latest pre-releases as an option i.e follow release only stream or latest beta stream
	- [ ] Can the image update itself somehow from within the docker container? even if it has to do a restart or exit and allow docker to restart it with a new version pulled.
	- [ ] How would we revert to a previous version if an update proved bad?


---

## 🟢 Chores / Cleanup

- [ ] **CHORE-1** — Remove unused overloads, components, classes, `using` directives
- [ ] **CHORE-2** — Add XML doc comments to key services and models (especially MQTT handling, SignalR dual-path)
- [ ] **CHORE-3** — Expand test coverage:
  - Unit: MQTT message handling, SignalR hub, `MqttTopicSubscriptionManager`
  - Integration/system: full flow with headless browser (Playwright?), both WASM and Server-only modes
 