# TO DO

## Enhancement Backlog — Next Build

### Medium Priority (features / usability)

- [ ] In read only mode:
	- [ ] the "File" menu shou dbe replaced by "Open..." as this should be only option available, so user can open other dashboard files
	- [ ] the "Edit" menu should be hidden...all options are grayed anyway
	- [ ] Previously open dashboard shuold be rememberd per user in a cookie? or some other way
- [ ] IN edit mode, I moved a node and "eidt slider" did not change colour--it needs a refresh to see it
- [ ] I moved a node and "Undo" did not become un-greyed so I could not undo it
- [ ] Also want an option for "Undo All" which reverts to file from disk (maybe other ways to acheive that...perhaps "File/Reload". Make a suggestion.
- [ ] Diagram name should be an editable property. In fact maybe there will be a set of properties we'll need to edit about a diagram and saved to its file:
	- [ ] Name
	- [ ] Option to display in title bar: App name and/or DIagram file display name
	- [ ] Grid size (when shown in edit mode)
	- [ ] Background colour of whole canvas (and implement it). Colour choice should be 3 part options we have for node background color. Make this widget a reusable component.
		- css well known color name
		- RGB color values in hex
		- THeme colour choice
- [ ] Blazor diagrams version is showing up as "0.1.0" ...is this a bug in blazor diagrams packaging? as it should be 0.1.2

### Lower Priority
- [ ] Did this get done? Authentication / authorisation — all endpoints and SignalR hub are open; add bearer/API-key if publicly accessible
- [ ] Did this get done? Input validation on API models — `[Required]` / `[StringLength]` on `DiagramState` and `ApplicationStateData`
- [ ] Node data tooltip — show full MQTT topic path and raw value on hover in display mode
- [ ] Dark-mode diagram colours — node/link colours currently don't adapt to dark theme

**Deployment / ops**
- [ ] Current docker image builds from source code. Is this the best choice? Can the build generate some sort of package and this is installed by the docker image
- [ ] There needs to be a means to have "releases" and the docker image can auto=detect a new release and either update itself or have option to update on a menu somewhere
- [ ] I want some sort of package or distributable installer I can install on a Raspberry Pi (supports .net, ARM64 chip) and WIndows aswell as the docker deployment
- [ ] Likewise this needs means to detect new version and update itself on confirmation

