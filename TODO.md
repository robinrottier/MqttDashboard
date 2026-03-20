# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS
- [ ] Node properties dialog
	- [ ] Can this dialog be moveable and have apply button to changes dynamically without closing
- [ ] Data item topics per node
	- [ ] Data binding should not just be 2 items but a variable list that can be added to.
	- [ ] So "+" in the properties dialog to configure another, "x" to remove a current one
	- [ ] Handle list of items with an index in changed event for example
	- [ ] "Link animation" has property for index of which data item to animate upon
- [ ] Property transition
	- [ ] Guage and battery, Color transition has property to select index of which topic to tranistion upon and which value to display
	- [ ] The color boxes in the transition/colour editor should have a chooser popup (via a button) to help with selecting the various types and well-known values
	- [ ] this would be same as "Background color" for main node property, so either 3 small buttons
	      or a single button goes to a dialog with 3 tabs, one for each of the colour modes
- [ ] In future, colour transitions may drive other properties (e.g. intensity, flashing, shading) — bear that in mind for the model
- [ ] Page tabs
	- [ ] Use MudTabs and related controls for displaying
	- [ ] Position option: support top/left/right/bottom
	- [ ] Drag to reorder pages
- [ ] mqtt publishing should have other parameters (e.g. message expiry)
- [ ] Widgets:
	- [ ] TReeview should be mudtreeview based
	- [ ] Log viewer should be Mud SImple Table (It is), columns choices for date (and format), time (and format), topic path, topic name, value
	- [ ] Is it 100% of whole node? doesnt look like it, maybe another continer in the heirarchy stops it going full width of whats visible
- [ ] Deployment version / update checking
	- [ ] Latest version checks checks for tags ... but the actual docker image may not be available for some time later. Can it check actual images in ghcr?
	- [ ] We want to be able to select beta/non-latest pre-releases as an option i.e follow release only stream or latest beta stream
	- [ ] Can the image update itself somehow from within the docker container? even if it has to do a restart or exit and allow docker to restart it with a new version pulled.
	- [ ] How would we revert to a previous version if an update proved bad?
- [ ] User application settings should be in data directory so persist over deployments.:
	- [ ] Startup behavoir
	- [ ] Admin password hash
	- [ ] ...this is probably in the applicatestate file?
- [ ] Remove the MRU files feature ...doesnt seem to be working well
- [ ] NOt in edit mode, clean start loaded a file... I simply went to OPen and it said "unsaved changes..." when clearly nothign was edited
- [ ] "OPen dashboard" dialog shoul dhave a small bin icon on each line to allow you to delete it, with a confirmation prompt

## 🟡 Features

### FEAT-A: MQTT topic wildcards per node
- [ ] Allow node `DataTopic` to use `#` / `+` wildcards (e.g. `home/sensors/#`)
- [ ] In node text, use named substitution syntax like `{power}` where the key is the trailing topic segment
- [ ] `MqttTopicSubscriptionManager` already handles wildcard routing server-side; extend client binding

### FEAT-B: MQTT data processing / calculated values
- [ ] Support simple expressions/transforms on incoming values before display (unit conversion, arithmetic, string concat)
- [ ] Option: "virtual topics" defined at dashboard level, computed from raw MQTT values, reusable across nodes
- [ ] Option to write calculated values back to the MQTT broker

### FEAT-C: Additional node types _(Gauge, Switch, Battery, Log, TreeView done — see CHANGELOG)_
- [ ] **Text node** - different node shapes (circle, diamond, etc.)
- [ ] **Grid** — table with rows/columns mapped to MQTT values
- [ ] **Chart** — in-memory time-series sparkline graph
- [ ] **Image** — display an image from a URL
- [ ] **Markdown / HTML** — formatted static content, optionally with data substitution
- [ ] **IFrame** — embed another web page

### FEAT-D: Multiple dashboard pages _(basic multi-page done — see CHANGELOG)_
- [ ] Page tab overflow handling (scrolling/dropdown when many pages)
- [ ] Swipe left/right gesture on mobile
- [ ] Page reordering (drag tabs)
- [ ] Current "Data" cold be replaced by an optional page (tree view and log of all live values)
	- [ ] user would create this is they wanted it manually or its some how a quick add page option when you create a new, empty dashboard
	- [ ] ...just need a way to specify dashboard set of requests ..so maybe thats in Dashboard properties dialog

### FEAT-E: Editing improvements
- [ ] Node-red style palette panel — drag node types from a sidebar onto the canvas
- [ ] Import/export selected nodes or a whole page as JSON (clipboard)
- [ ] Node alignment and distribution tools (align left/right/top/bottom, distribute evenly)

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

### FEAT-K: Dashboard versioning & Git integration
- [ ] Snapshot history for dashboards (previous versions, compare/diff)
- [ ] Optional Git commit/push of dashboard changes to a remote repo

### FEAT-L: Deployment enhancements
- [ ] Single Docker image supporting both server-only and WASM modes (different ports, shared data dir)
- [ ] Read-only runtime mode — view-only, no login/edit UI exposed
- [ ] Admin interface: runtime monitoring, logs, connected clients, dashboard file management

---

## 🟢 Chores / Cleanup

- [ ] **CHORE-1** — Remove unused overloads, components, classes, `using` directives
- [ ] **CHORE-2** — Add XML doc comments to key services and models (especially MQTT handling, SignalR dual-path)
- [ ] **CHORE-3** — Expand test coverage:
  - Unit: MQTT message handling, SignalR hub, `MqttTopicSubscriptionManager`
  - Integration/system: full flow with headless browser (Playwright?), both WASM and Server-only modes
 