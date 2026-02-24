# TO DO

## App
- ✅ add common data directory for file saving and loading, between WebAppServer and WebAppWasm projects
  - `DiagramStorage:DataDirectory` in `appsettings.json` or `DIAGRAM_DATA_DIR` environment variable; falls back to `ContentRoot/Data`
- ✅ that data directory should come from config file and environment variable
- ✅ default file is diagram.json but other files will be available

## Layout
- ✅ add a conventional looking top-level menu for most new functionality
- ✅ button in top right using 3 bars icon
- ✅ structured as follows:
...
	File
		New ✅
		Open ✅
		Save ✅
		Save As (stub — single-file model)
		Reload ✅
	Edit
		Add Node ✅
		Delete Node ✅
		seperator ✅
		Cut selected nodes (stub)
		Copy selected nodes (stub)
		Paste nodes (stub)
		Seperator ✅
		Add port ✅
			Top ✅
			Left ✅
			Bottom ✅
			Right ✅
		Delete port ✅
			Top ✅
			Left ✅
			Bottom ✅
			Right ✅
		Properties ✅
	Options
		Theme ✅
			Light ✅
			Dark ✅
			Auto ✅
		Show ✅
			Diagram name in title bar ✅
		Grid (applies when in edit only) ✅
			None ✅
			Small (5px) ✅
			Medium (10px) ✅
			Large (20px) ✅
	Data ✅
	About ✅
...
- ✅ top panel in current layout should be:
	Icon (flowchart icon — AccountTree) ✅
	Title as {AppName} and optional {Diagram name} ✅
	-space to justify to right- ✅
	Edit mode switch (navigates between / and /edit) ✅
	Menu button ✅
- ✅ remove the current side panel and toolbar in editor mode


## Nodes
- ✅ should have optional 2nd data element (DataTopic2 / DataValue2)
- ✅ should have format options for each item of data e.g. number dec places, units string appended
- ✅ format should be some sort of string interpolation with c# string format notation (string.Format, e.g. `{0:F2} °C`)
- ✅ font size property (FontSize in px, editable in property editor)
- ✅ optional value coloring (NodeColorRule list with operator/threshold/color per data topic)

## Edit page
- ✅ double click on a node and it should go to edit properties

