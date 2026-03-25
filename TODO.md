# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS

- [ ] Serialization: node ID GUIDs in file — map to sequential 1-based IDs for file (need port+link ID remapping too; deferred due to complexity)
- [ ] Serialization: logged-on user not yet written to `FileInfo` (always admin for now — fine to leave)
- [ ] THe no data msg: "No data topics configured. Switch to edit mode to add MQTT data topics." shold be more of abanner accross the top not a dialgo in the middle
- [ ] I added a topic and switched to page 2 ... it had same msg. Data topics are per dashboard, not per page.


## 🟡 Minor Enhancements

- [ ] Server-side "lazy cache": if client request is dropped, server should keep data live for a configurable delay (e.g. 30s) before removing references
- [ ] Property transition
	- [ ] Also a means to drag reordering around the conditions to specify which is first match
- [ ] Data item topics per node
	- [ ] "Link animation" needs a property for index of which data item to animate upon
- [ ] Page tabs
	- [ ] Use MudTabs and related controls for displaying. MudTabs has a different model...every page is rendered inside tab component BUT maybe there's a way to use index of selected tab to render it outside MudTabs component?
	- [ ] Position option for tabs: top/left/right/bottom in dashboard properties
	- [ ] Drag to reorder pages when in edit mode. MudTabs would support this but need setting noticed and saved.
- [ ] Node properties dialog
	- [ ] Can this dialog be moveable and have apply button to changes dynamically without closing
- [ ] Log viewer columns: choices for date (and format), time (and format), topic path, topic name, topic full path&name, value — **Full 6-column boolean options done**; date/time format options still open
- [ ] mqtt publishing should have other parameters (e.g. message expiry)
- [ ] Confirm- mqtt publishing is a reusable compoennt (especially configuration of it in node properties)


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
- [ ] **Text node** - different node shapes (circle, diamond, etc.) Perhaps the "shapre" applies to all derived
      nodes too e.g. a guage inside a triangle or circle. Or maybe shape is just a property of the base node.
- [ ] **Chart** — in-memory time-series sparkline graph
- [ ] **Markdown / HTML** — formatted static content, optionally with data substitution
- [ ] **IFrame** — embed another web page

### FEAT-D: Multiple dashboard pages _(basic multi-page done — see CHANGELOG)_
- [ ] Page tab overflow handling (scrolling/dropdown when many pages)
- [ ] Swipe left/right gesture on mobile
- [ ] Page reordering (drag tabs)

### FEAT-E: Editing improvements
- [ ] Node-red style palette panel — drag node types from a sidebar onto the canvas
- [ ] Import/export selected nodes or a whole page as JSON (clipboard)

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
- [ ] Single Docker image supporting both server-only SSR and WASM modes (different ports, shared data dir)
- [ ] Read-only runtime mode — view-only, no login/edit UI exposed
- [ ] Admin interface: runtime monitoring, logs, connected clients, dashboard file management

### FEAT-M: Settings persistence _(done — settings now in data directory)_

### FEAT-N: Self-updating deployment
- [ ] Latest version check checks for tags ... but the actual Docker image may not be available for some time after a tag is pushed. Can it check actual images in ghcr?
- [ ] Option to follow release-only stream or latest beta stream
- [ ] How would we revert to a previous version if an update proved bad?


---

## 🟢 Chores / Cleanup

- [ ] **CHORE-1** — Remove unused overloads, components, classes, `using` directives
- [ ] **CHORE-2** — Add XML doc comments to key services and models (especially MQTT handling, SignalR dual-path)
- [ ] **CHORE-3** — Expand test coverage:
  - Unit: MQTT message handling, SignalR hub, `MqttTopicSubscriptionManager`
  - Integration/system:
	- full flow with headless browser (Playwright?),
	- a console based "client" that connects to the server and validates:
		- the SignalR messages received for a given dashboard file and set of MQTT messages published?
		- This would be a more lightweight test than spinning up a full browser instance, and could run as part of the regular test suite.
		- Or testing functions via some sort of "command interface" simulating menu selections etc
	- both WASM and Server-only modes
 
