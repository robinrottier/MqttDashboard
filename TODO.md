# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS
- [ ] ON startup, LInes only seem to start animating on a value update BUT value is already there as its shown in text?
- [ ] Node properties dialog
	- [ ] Can this dialog be moveable and have apply button to changes dynamically without closing
- [ ] Property transition
	- [ ] Gauge and battery, Color transition has property to select index of which topic to transition upon and which value to display
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
- [ ] mqtt publishing should have other parameters (e.g. message expiry)
- [ ] Widgets:
	- [ ] Log viewer columns: choices for date (and format), time (and format), topic path, topic name, value
- [ ] Deployment version / update checking
	- [ ] Latest version checks checks for tags ... but the actual docker image may not be available for some time later. Can it check actual images in ghcr?
	- [ ] We want to be able to select beta/non-latest pre-releases as an option i.e follow release only stream or latest beta stream
	- [ ] Can the image update itself somehow from within the docker container? even if it has to do a restart or exit and allow docker to restart it with a new version pulled.
	- [ ] How would we revert to a previous version if an update proved bad?
- [ ] User application settings should be in data directory so persist over deployments.:
	- [ ] Startup behaviour
	- [ ] Admin password hash
	- [ ] ...this is probably in the applicationstate file
- [ ] Gauge node
	- [ ] Guage properties dialog compaction... "Min", "Max", "Origin" and "Unit" can all be on one line
	- [ ] "The "Text" field above is displayed as a static label below the gauge arc. " message...actually text shoul dhave a display option either above or below the guage.
	- [ ] Gauge needs a way to specify which topic (0 based index) is used to set gauge value, default 0, first. All data nodes that h ave special display item will need this.
	- [ ] Default guage setup needs 2 transitions... below zero, red, above zero green
- [ ] The tooltip over dashboard title when editing, should include filename as well as display name, just so its clear whats happening.
- [ ] IMage:
	- [ ] also needs option to upload a bitmap and stored locally as content  or should it be byte values in dashboard file?)
	- [ ] option to go "behind" or "ontop" other nodes.. maybe z-order roperty for all nodes? HOw does this fit in with blazor.diagrams, maybe it has it already
- [ ] I set grid to 10, saved doc, reloaded it and went into edit mode ...grid was 20?
- [ ] Current grid seelction should be indicated by tick in menu...looked like0 was written to saved file
- [ ] Edit mode, multi seelction. the alignment toolbar is visiable but all greyed? this was on dev.
- [ ] Exceptions in code are not very visible...panel at bottom is pake yellow and not visible. Need better logging of that case too
- [ ] Try handle this error:
	[11:32:24 WRN] Microsoft.AspNetCore.Components.Server.Circuits.RemoteRenderer: Unhandled exception rendering component: Collection was modified; enumeration operation may not execute.
	System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
	   at System.Collections.Generic.List`1.Enumerator.MoveNext()
	   at MqttDashboard.Widgets.LogNodeWidget.<BuildRenderTree>b__0_0(RenderTreeBuilder __builder2) in C:\Users\robin\source\repos\MqttDashboard\src\MqttDashboard.Client\Widgets\LogNodeWidget.razor:line 37
	   at Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder.AddContent(Int32 sequence, RenderFragment fragment)
	   at MudBlazor.MudSimpleTable.BuildRenderTree(RenderTreeBuilder __builder)
	   at Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(RenderBatchBuilder batchBuilder, RenderFragment renderFragment, Exception& renderFragmentException)
	[11:32:24 ERR] Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost: Unhandled exception in circuit 'Wx6kAmLCLHnzEH6ZU5l6kJV1DASTHZST6ix2V-m0PQs'.
	System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
	   at System.Collections.Generic.List`1.Enumerator.MoveNext()
	   at MqttDashboard.Widgets.LogNodeWidget.<BuildRenderTree>b__0_0(RenderTreeBuilder __builder2) in C:\Users\robin\source\repos\MqttDashboard\src\MqttDashboard.Client\Widgets\LogNodeWidget.razor:line 37
	   at Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder.AddContent(Int32 sequence, RenderFragment fragment)
	   at MudBlazor.MudSimpleTable.BuildRenderTree(RenderTreeBuilder __builder)
	   at Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(RenderBatchBuilder batchBuilder, RenderFragment renderFragment, Exception& renderFragmentException)


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
 