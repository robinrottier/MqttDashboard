# TO DO

## Enhancement Backlog — Next Build

### High Priority (correctness / stability)

**MQTT**
- [ ] MQTT reconnect on disconnect — `MqttClientService` logs disconnection but does not reconnect; add exponential backoff reconnect loop
- [ ] MQTT connection status visible in UI — not just in logs
- [ ] Unbounded message queue — `Messages` list in `ApplicationState` has no size cap; add configurable max (e.g. last 1000 messages)

**Async / error handling**
- [ ] Replace `async void` event handlers in `MqttInitializationService` (`HandleSubscriptionConfirmed`, `HandleUnsubscriptionConfirmed`) with `async Task` to prevent swallowed exceptions
- [ ] Remove `Console.WriteLine` debug output in `SignalRService` — replace with `ILogger`
- [ ] On SignalR reconnect, re-subscribe to MQTT topics that were active before disconnection

**Storage**
- [ ] Concurrent write safety — `DiagramStorageService` and `ApplicationStateController` write files without locking; add `SemaphoreSlim` per file to prevent corruption under concurrent saves

### Medium Priority (features / usability)

**Rename**
- rename "MqttDashboard" to "MqttDashboard" and propagate that change through-out all the code
	- projects names
		- "MqttDashboard" becomes "MqttDashBoard.Client"
		- "MqttDashboard.Server" becomes "MqttDashBoard.Server"
		- "MqttDashboard.WebApp" becomes just "MqttDashboard.WebAppAuto"
		- "MqttDashboard.WebAppServerOnly" becomes just "MqttDashboard.WebAppServerOnly"
		- "MqttDashboard.WebAppWasmOnly" becomes just "MqttDashboard.WebAppWasmOnly"
	- class names
	- namespaces
	- comments refering to MqttDashboard just change to MqttDashboard
- The display name for the app is then "Mqtt Dashboard"
- This is a big change and requires a checkpoint with GIT commit before and after, for all of it when complete before starting anything else

**Project structure**
= add a unit test framwork to at least get something started and create some basic tests for all the projects, certainly .client and .server
- is there stuff in .Client that is non-display logic and could be in a ".Core" project and that could have more unit tests etc
- can the 3 web host app projects be combined into modes of the "Auto" -- or at least can "WasmOnly" be merged into Auto so it operates identically just with out the initil server side render

**App**
- click on icon, top left corner should go to default view page
- the current Data and About menu items should be child items of a "View" menu with additional optin for "Home" (same as above)

**Diagram editing**
- [ ] Cut / Copy / Paste nodes — currently stubbed; implement using clipboard buffer in `ApplicationState`
- [ ] Save As — allow naming and saving to additional files (multi-file infrastructure already exists)
- Open - allow openning an existing file form the data directory
- New - clears current diagram and starts with an empty one
- WHen changing file c\or new file, check for changes and prompt to confirm to loose them
- [ ] Undo / Redo — add snapshot stack (configurable depth, e.g. 20 levels)
- DIagram name shuold be displayed in title bar, if option is seelcted to do so (and that option is saved with diagram and any other diagram otpions)
- OPtions/Show/Diagram Name menu item shoul dbe ticked if the option is seelcted
- In edit mode, diagram name is always displayed in title bar formated as "App name - Diagram Name"

**Deployment / ops**
- [ ] Health check endpoint — `/healthz` via `AddHealthChecks()` for Docker/load-balancer probes; include MQTT connection state
- [ ] Structured logging — add Serilog with JSON sink for log aggregators (Seq, Loki, etc.)
- [ ] `.env.example` at repo root — document all supported environment variables
- Current docker image builds frmo source code. Is this the best choice? Can the build generate some sort of package and this is installed by the docker image
- There needs to be a means to have "releases" and the docker image can auto=detect a new release and either update itself or have option to update on a menu somewhere

### Lower Priority

- About should include build config if DEBUG
- About should include a section on the hosting: server name where running (DEBUG only) and number of connected clients
- [ ] Authentication / authorisation — all endpoints and SignalR hub are open; add bearer/API-key if publicly accessible
- Come up with some solution to support a read only mode (i.e. just view already saved files) and an admin mode where by editing/saving  is allowed aswell 
the data and about views
- The "logon" needs to be persistent for different clients using cookie or something else. Nothing too heavy for now (no database of users or anything -- perhaps a simple admin password stored in config)
- [ ] Input validation on API models — `[Required]` / `[StringLength]` on `DiagramState` and `ApplicationStateData`
- [ ] Node data tooltip — show full MQTT topic path and raw value on hover in display mode
- [ ] Dark-mode diagram colours — node/link colours currently don't adapt to dark theme
- [ ] Export diagram as image (PNG/SVG)
- [ ] GitHub Actions CI — run `dotnet build` and Docker image build on every push
