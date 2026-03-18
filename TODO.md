# TODO

## 🟡 Features

### MQTT topic wildcards in a node
- [ ] Add support for MQTT topic wildcards (e.g. `home/sensors/#`) to allow subscribing to multiple topics with a single node.
      -- Then in node text use a syntax like {power} to identify each individual value to display ike string interpolation

### MQTT data & subscriptions
- [ ] Support for filtering nodes so can remove values not relevant to the dashboard
- [ ] Support some logic to process incoming MQTT messages before displaying.
      e.g.:
      to convert raw values to more user-friendly formats,
      or to combine multiple messages into a single display value
      e.g. concatenating strings
      or arthimetic operations on numeric values
      Perhaps by creating psuedo data values written back to the data cache that can then be used in the node text with the same syntax as for MQTT values, e.g. {power} or {batteryLevel} etc
- [ ] Option to write these calculated data values back to mqtt source

### Alternate data sources
- [ ] Support for other data sources beyond MQTT, such as REST APIs, WebSockets, or local data files. This could be implemented by allowing nodes to have different "data source types" with corresponding properties for configuration.
- [ ] maybe there are a list of "plugins" configured on the server side that can be used as data sources, and each plugin has its own set of properties for configuration. Then in the node properties, you select the data source type and configure the corresponding properties to connect to that data source and retrieve values to display on the node.
- [ ] ie. so an admin user can edit the list of plugins, their properties, for that particular deployment/install
- [ ] perhaps a list of selecgted/active data sources is then stored in the dashboard itself and available to its nodes
- [ ] IN the home assitant integration can we get all sensor values from the local HA?? to appear as an alternate data source
- [ ] IN a emoncms integration can we get all feed values from the local emoncms?? to appear as an alternate data source
- [ ] And then time series bistory values also from emoncms http api eg. "kwh today" type values for a power feed.

### Multiple node types
Add different node types that could be added to the dashboard
- [ ] Current node is a simple "TextNode" with a single text property.
- [ ] Differnet shaped ndoes e.g. circle, oval, diamond, etc
    
We could add other types like:
- [ ] "GaugeNode" with a numeric value and a visual gauge,
- [ ] "Switch" with an on/off state and a toggle control.
- [ ] "Grid" for a table like display, rows and columns defined by properties, values from MQTT messages mapped to cells.
- [ ] "Log" for a scrolling list of message updates, with property for history to store, max to display, ability to filter etc
- [ ] "Tree view" for hierarchical data display of MQTT topics and values from some root subscription
- [ ] "Battery" for a battery level display with percentage and visual icon
- [ ] "Temperature" for a temperature display with numeric value and color coding
- [ ] "Chart" for a time series graph of values from MQTT messages over time but what and how to keep values?

### Mutiple pages
- [ ] Add support for multiple tabs (or pages) in one dashboard file, each with its own canvas and set of nodes/links
- [ ] Pages are selected by swiping left/right or by clicking on page tabs at the top, with logic to handle too many page names
      for window width to display them all (scrolling, dropdown, etc)
- [ ] The current data page becomes just another tab with a tree view of all current values, with filtering and sorting capabilities
- [ ] The "Page" menu is then for managing the pages themselves, with options to add, rename, delete, and reorder pages aswell as which page to display.
- [ ] Current "Data" page is not via a different url then UNLESS its there in admin mode only to show something useful?

### Grouping and layout
- [ ] Add grouping for nodes, to allow visually organizing related nodes together with a labeled box or background

### Non-mqtt data node types
- Other node types:
    - [ ] "Image" for displaying an image from a URL or file
    - [ ] "Markdown" for displaying formatted text with markdown syntax --could this include data substitution syntax?
    - [ ] "HTML" for displaying custom HTML content --could this include data substitution syntax?
    - [ ] "WebView" or "IFrame" for embedding another web page within a dashboard

### Links
- [ ] The link between nodes should become more like a node object with a properties page too-- to select color, flow, corresponding data item etc
- [ ] Links need option to have arrows to show flow direction
- [ ] And more syle options for the lines including animation:
- [ ] thickness, color, dashed/solid, animated flow with speed control incdicated by moving dot or arrow
- [ ] color and intensity could also be data driven to show strength of connection or value of a particular item, with a default mapping but also options to customize the mapping of data values to visual properties of the link
- [ ] links coul dbe straight or curvey or have draggable control points to adjust the shape of the link (blazor.diagrams supports this, so just need a gui to do it)
- [ ] Links need ability to be "forked" or joined along the way to other links ... so a link starts or ends at a junction point with other links. I think blazor.diagrams supports this, so just need a gui to do it

### Editing
- [ ] For adding nodes of differeny types, Use node-red as example for editing experience, with drag and drop from a palette of node types, and a properties panel for each node and link
- [ ] Add import/export to json text, allowing to share node(s) or page or whole dashboard via text copy/paste
- [ ] Again, use node-red as example for editing experience to import/export via text and clipboard
- [ ] Node aligmnet and distribution tools to help organize the layout of the diagram, including auto-arrange and manual alignment guides

## Testing
- [ ] Add unit tests for key components and logic, such as MQTT message handling, Signal
- [ ] Is there a way of having full system tests that run the server and a headless browser client to test the full flow of MQTT messages through to the UI updates?
- [ ] And include in that (system test) all the startup permumations for serverobnkly or wasm modes

## User management and authentication
- [ ] Add user management and authentication to allow multiple users with different access levels
- [ ] e.g readonly, read-write (i.e. can create, edit dashboards), admin (can manage users and settings)

## Dashboard versioning and history
- [ ] Add versioning and history for dashboards, allowing users to see previous versions, compare
- [ ] Add support for GIT integration to allow users to commit and push dashboard changes to a remote repository for backup and collaboration

## Deployment
- [ ] Can same docker file support both server-only and wasm versions at same time on different ports but sharing same data directory
- [ ] Also, deployment need an option to be "read only" mode where the dashboard can be viewed but not edited, for sharing with users who should not have edit access. This could be a separate image or a runtime option.
- [ ] Then the server only deployment could be for readonly (with no option to login or edit at all) and the wasm version could be for display and edit mode
- [ ] Server side runtime management. Needs ways to monitor the running application, view logs, manage dashboard files, etc.
- [ ] This could be a separate admin interface or integrated into the main UI with appropriate access controls for admin mode only
- [ ] Also, number of connected cilents and their status (editing/viewing) could be displayed in the UI for awareness

## Responsive layout
- [ ] The UI should be responsive and work well on different screen sizes, including mobile devices. This may require adjustments to the layout and controls for smaller screens.
- [ ] Perhaps each page shoul dhave mutiple layouts for different screen sizes, with the same nodes and links but arranged differently for optimal display on desktop vs mobile
- [ ] For mobile, consider a different interaction model for editing, such as a properties panel

## 🔴 Bugs
- mqtt daashboard package version is not being display in about
- title says "MqttBashboard.client" but should just be "Mqtt Dashboard"
- "diagrams" should be refered to as "Dashboard" in the UI and documentation, to avoid confusion with the Blazor.Diagrams library
- applicationstate.json is showing up in the list of files to open. move all dashboards to a subdirectory called "dashboards"
- in fact the application state file is not required ... it only stores the data subscriptions to be made and this should be in the dashboard file itself. Then we can remove the application state file entirely and simplify the code and data management
- about box in admin mode shou dhave detailed depoyment info like COMPUTERNAME where running, not just hostname


## 🟢 Chores / Cleanup
- [ ] Remove unused overloads, components, classes, usings, etc
- [ ] Clean up the code and add comments to explain the logic and flow of the application, especially for the more complex parts like the MQTT handling and SignalR communication
- [ ] Add more unit tests for the various components and logic in the application, to ensure that it is working correctly and to catch any potential bugs or issues
- [ ] 