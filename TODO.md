# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## 🟡 Bugs/improvements

- [ ] There should be a system wide property that selects whether to open a specific file when a new client connects or if it remembers the last file and re-uses that
- [ ] Node properties dialog
	- [ ] Can this dialog be moveable and apply changes dynamically (whilst still being able to cancel everything since opening)
	- [ ] Data binding should not just be 2 items but a list that can be added to. So "+" in the properties dialog to configure another, "x" to remove a current one and handle list of items with an index in changed event for example


## 🟡 Features

### FEAT-A: MQTT topic wildcards per node
- [ ] Allow node `DataTopic` to use `#` / `+` wildcards (e.g. `home/sensors/#`)
- [ ] In node text, use named substitution syntax like `{power}` where the key is the trailing topic segment
- [ ] `MqttTopicSubscriptionManager` already handles wildcard routing server-side; extend client binding

### FEAT-B: MQTT data processing / calculated values
- [ ] Support simple expressions/transforms on incoming values before display (unit conversion, arithmetic, string concat)
- [ ] Option: "virtual topics" defined at dashboard level, computed from raw MQTT values, reusable across nodes
- [ ] Option to write calculated values back to the MQTT broker

### FEAT-C: Additional node types _(Gauge, Switch, Battery done — see CHANGELOG)_
- [ ] **Grid** — table with rows/columns mapped to MQTT values
- [ ] **Log** — scrolling history list of a topic's messages (configurable max history)
- [ ] **Chart** — in-memory time-series sparkline graph
- [ ] **Image** — display an image from a URL
- [ ] **Markdown / HTML** — formatted static content, optionally with data substitution
- [ ] **IFrame** — embed another web page
- [ ] **Tree view** — hierarchical display of MQTT topics and values from a root subscription
- [ ] **Text node** - different node shapes (circle, diamond, etc.)

### FEAT-D: Multiple dashboard pages
- [ ] `DiagramState` becomes a list of pages, each with its own canvas
- [ ] Page tabs at the top with overflow handling (scrolling/dropdown)
- [ ] Swipe left/right gesture on mobile
- [ ] Page management: add, rename, delete, reorder
- [ ] Current "Data" view moves to an optional tab (tree view of all live values)

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
 