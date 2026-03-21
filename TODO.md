# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS
- [ ] Node properties dialog
	- [ ] Can this dialog be moveable and have apply button to changes dynamically without closing
	- [ ] Edit node dialog...click outside it and it closes...should not be the case, it should be modal and instead have an apply button to make changes real time. Ideally it would be moveable too (so effect on diagram can be seen)
- [ ] Property transition
	- [ ] Gauge and battery, Color transition has property to select index of which topic to transition upon and which value to display
	- [ ] The color boxes in the transition/colour editor should have a chooser popup (via a button) to help with selecting the various types and well-known values
	- [ ] this would be same as "Background color" for main node property, so either 3 small buttons
	      or a single button goes to a dialog with 3 tabs, one for each of the colour modes
	- [ ] Color transition"when" needs "else" condition to specify default colour when no conditions met
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
- [ ] STartup sequence from a clean, new install...
	- [ ] "Admin password not configured. Set up now →" correclty displayed...follow link and shows first time setup (again..correct) BUT the "admin password not configured. setup now" still is shown...dont show it once login setup is shown
	- [ ] First time setup screen... "enter" should work to finsih the dialog and set password
	- [ ] Set password button should be greyed if password's not entered, confirmed, or not sufficient quality
	- [ ] About box
		- [ ] ... "deployment line e..g docker" should be in runtime/debug section
		- [ ] ... instead of close button, have "X" close in top right corner
		- [ ] "latest version" and "last checked" lines look more spaced than others...should be all in same compact table as previous lines
		- [ ] Make dialog wider, if screen res allows, to avoid some of the cramped layout and long lines wrapping
		- [ ] The "Up to date" badge shou ldbe against the version on 2nd line
		- [ ] "Last tchecked" shoul dbe a tooltip over the latest version line and not its own line, "Check now" shou ldbe button on the right of latest version line
	- [ ] Blank page at startup...went into edit mode and out again and it prompted for "save changes" ... but no changes made at all. As soon as entered edit mode icon is red?
	- [ ] ANd when selecting discard .. returned to view mode BUT grid stayed visible and page tabs stayed in edit mode with "X" and "+" and menus reflected edit mode even though slider indicated readonly mode
	- [ ] IN edit mode, click top left icon to return to home screen and no prompt to save changes==> shoudl follow same logic as eleswhere
	- [ ] Guage node
		- [ ] We dont need unit. Surely its just the text field format and if user wants a UNit they put it there e..g {0:0}W
		- [ ] color transition says "distance form arc origin" but it shoul dbe simply based on value
		- [ ] color transition shou dbe "first match" not last
		- [ ] Ive added a data topic, but it still says "No topic added"
		- [ ] GUage needs a way to specify which topic (0 based index) is used to set guage value, default 0, first.
		- [ ] "Transition properties" should be reusable component ..other things will have same issue, like battery
	- [ ] File save on a new, unnamed document and it said "File '' saved" ...shoul dhave said "Default" which seems ot be file name its using
	- [ ] The tooltip over dashboatd title when editing, should include filename aswell as display name, just so its clear whats happening.
	- [ ] Did a file save on a new doc. Grid was previously shown as 10px (I think), on reload an reeneter edit mode the grid now looks like 20. Its never  been set ...so somewhere default value is getting confued.
	- [ ] 


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
- [ ] Current "Data" could be replaced by an optional page (tree view and log of all live values)
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
 