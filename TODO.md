# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS

## 🟡 Minor Enhancements

- [ ] Property transition
	- [ ] Also a means to drag reordering around the conditions to specify which is first match
- [ ] Serialization:
	- [ ] logged-on user not yet written to `FileInfo` (always admin for now — fine to leave)
	- [ ] should include version of this app doing the write, and server written from
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
- [ ] IMport and Export dont seem to be able to see Windows clipboard ... is there some permissions to enable it? This was on firefox
- [ ] Serialization: node ID GUIDs in file — map to sequential 1-based IDs for file (need port+link ID remapping too). Needs a json serilaizer class for Dashboard to manage the mapping.


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
- [ ] **Guage**
	- [ ] needs alternatives such as full circle, 90 or 270 .... aybe thats all the 
		  option is, how much of a circle is drawn and properties to control orientation
	- [ ] options to draw "needle" also from some center point to the guage ...	  
- [ ] **Markdown / HTML** — formatted static content, optionally with data substitution
- [ ] **IFrame** — embed another web page
- [ ] **Chart** — in-memory time-series sparkline graph

### FEAT-D: Multiple dashboard pages _(basic multi-page done — see CHANGELOG)_
- [ ] Page tab overflow handling (scrolling/dropdown when many pages)
- [ ] Swipe left/right gesture on mobile
- [ ] Page reordering (drag tabs)

### FEAT-E: Editing improvements
- [ ] Node-red style palette panel — drag node types from a sidebar onto the canvas
- [ ] Keyboard funcionality esp.:
	- [ ] arrows to move selcted nodes

### FEAT-F: Link improvements
- [ ] Links as proper model objects with a properties editor: color, thickness, dash style
- [ ] Arrow heads to show flow direction
- [ ] Data-driven link styling — color/intensity driven by a topic value
- [ ] Draggable Bezier control points
- [ ] Fork/junction points between links

### FEAT-G: Grouping / layout containers
- [ ] "Group" box — labeled background rectangle that visually wraps related nodes
- [ ] Moving a group moves all contained nodes
- [ ] Split panel type controls to divide up work area into resizable sections

### FEAT-H: Data layer refactor - alternate data sources / plugin architecture
= [ ] There's alot of overlap between data access on client to server, and server to mqtt so:
	- [ ] Refactor client & server data access to use a common interface and shared code where possible, with separate implementations for client-server (SignalR) and server-MQTT.
	- [ ] MQTT data cache needs seperating out to reusable package with a pub/sub interface and backend to matchin interface either over signal r, or mqtt directly.
	- [ ] Support a "lazy cache" layer that can be used for both client-server and server-mqtt data access, with a pub/sub interface for updates. This would allow for more efficient data handling and reduce redundant requests.
	- [ ] so if client request is dropped, server should keep data live for a configurable delay (e.g. 30s) before removing references
	- [ ] client api to this incude direct and async memory access also
- [ ] Its all in a seperate proehct MqttDashboard.Data implmenting the basic pub/sub api
	- [ ] Client API is requesting a topic to an inmeoery cache, and subscribing to updates.
		- [ ] Thats handles wildards and all the topic parsing and matching
		- [ ] Its likely to look very much mqtt fontend api but with a more flexible backend and caching layer
		- [ ] Each in memory cache has a server (maybe more than one) that implements the particular backend talking to the server API of our common data thing
		- [ ] Somehow, servers say what topics they have data for, by default "#" meaning everything from the root
		- [ ] If there was more than more server attached to some cache then they would have to differentitate themsevles perhaps just by a root topic path		
	- [ ] One Server implemtation is subscribing to MQTT topics and updating the cache
	- [ ] Another server imlemention is over signalR to the server project implementation of this in meory cache
	- [ ] Another "server" implementation could be a mock data generator for testing,
	- [ ] or a REST API backend for other data sources (not sure about updates...maybe for things that dont update much)
	- [ ] This is going to basically impleemt the current front end data aceess with little logical change, just functions names etc.
	- [ ] Likewise the backend. The middle tier will take some more work to refactor out the common code and interfaces, but the actual MQTT handling code should be mostly reusable as is, just moved into the new structure.
	- [ ] As a seperate module it wil be easy to create test project for it without exgternal dependencies.
	- [ ] Perhaps the actual MQTT stuff is seperate again into MqttDashBoard.Data.Mqtt -- the commin client and serve .Data library, although looking very mqtt like wont actual have any mqtt in it
- [ ] Active data sources stored in the dashboard file? Data requests do not know the source of data just the "topic" key to access it...so potentially same dashboard talks to diffeent backends.

**Phase 2**
- [ ] Extend Plugin architecture for data sources beyond MQTT
	- [ ] Built-in integrations: REST APIs, WebSockets, Home Assistant local API, Emoncms feeds and time-series
- [ ] Admin configures available plugins; nodes select source and configure connection
- [ ] 

### FEAT-I: Responsive / mobile layout
- [ ] Responsive layout adapts to screen size
- [ ] Per-page optional alternative layout for mobile
- [ ] Touch-friendly editing interactions

### FEAT-J: User management & auth
- [ ] Multi-user with roles: read-only / read-write / admin
- [ ] Builds on existing `ServerAuthService` / admin password hash mechanism
- [ ] User registration mechanism (username, email, password) with email verification--not sure how as this deployment wont have a mail?
- [ ] "Admin" is a status now and not an actual user name. Mutliple users could log on and be "admin"
	- [ ] Admin user can create other users, assign roles, and delete users
	- [ ] First time setup requires creating an admin user, which is then used to log in and manage the system
- [ ] COnfirmation of each new user registration from admin user
	- [ ] - new users stays "pendign" until admin confirms request
	- [ ] when admin logs on they see pending user registrations and can confirm or reject them
- [ ] User management UI in admin interface
- [ ] Usr "database" is nothign complicated -- fine as an encryted file store and password encrypted in that
- [ ] JWT-based auth for API endpoints, with token issued on login and stored in browser local storage
	- [ ] I dont know what that means -- does it apply to this type of setup? API calls are all internal frmo client to server


### FEAT-K: Dashboard versioning & Git integration
- [ ] Optional Git commit/push of dashboard to a remote repo
	- [ ] Modeled on node-red project feature.
- [ ] Or history/backup for dashboards (previous versions, compare/diff) for non-git users

### FEAT-L: Deployment enhancements
- [ ] Admin interface: runtime monitoring, logs, connected clients, dashboard file management
- [ ] We've lost the "restart" button ...might want to do a restart for other reasosns
- [ ] More automation to speed relase process
	- [ ] Local script to run tests, do a final commit, and push, create PR, let the various actions run, merge the PR to main and kick patch-release. Then kick deployment to test server

### FEAT-M: Settings persistence _(done — settings now in data directory)_

### FEAT-N: Self-updating deployment
- [ ] The Latest version check checks for tags ... but the actual Docker image may not be available for some time after a tag is pushed. Can it check actual images in ghcr?
- [ ] Option to follow release-only stream or latest beta stream of pre-releases
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
 
