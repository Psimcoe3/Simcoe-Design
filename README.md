# Simcoe-Design

## Electrical Component Sandbox

A desktop application for designing and editing parametric electrical construction components, inspired by Revit Family Editor. This application provides an isolated workspace to create, edit, and manage electrical components like conduit, boxes, panels, and supports.

### Features

- **3D Viewport**: Interactive 3D visualization using HelixToolkit
- **Component Library**: Pre-defined electrical components (Conduit, Box, Panel, Support)
- **Parametric Design**: Adjustable parameters including size, material, elevation, and color
- **Transformation Tools**: Move, rotate, and scale components
- **Grid & Snap**: Grid display with snap-to-grid functionality
- **Property Panel**: Real-time editing of component properties
- **File Operations**: Save and load custom component files (.ecomp format)
- **JSON Export**: Export components to JSON format for integration with other tools
- **2D Drawing + Markup Canvas**: Hybrid WPF/Skia drawing surface with grip editing, measurement markup, and PDF/image underlays
- **Undo/Redo**: Command-driven editing across component and markup workflows
- **Interactive Conduit Bending**: Click and drag to create complex conduit paths with multiple bends (NEW!)
- **Local Reference Library**: Curated repo-relative access to electrical reference PDFs and folders from the top menu

### Technology Stack

- **Framework**: C# WPF (.NET 8.0)
- **3D Graphics**: HelixToolkit.Wpf 2.25.0
- **Serialization**: Newtonsoft.Json 13.0.3

### Building the Application

#### Prerequisites
- .NET 8.0 SDK or later
- Windows OS (WPF requires Windows)

#### Build Steps

```bash
# Clone the repository
git clone https://github.com/Psimcoe3/Simcoe-Design.git
cd Simcoe-Design

# Restore dependencies
dotnet restore ElectricalComponentSandbox/ElectricalComponentSandbox.csproj

# Build the project
dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj

# Run the application
dotnet run --project ElectricalComponentSandbox/ElectricalComponentSandbox.csproj
```

Alternatively, open `ElectricalComponentSandbox.slnx` in Visual Studio and build/run from there.

### Usage

#### Creating Components

1. Click one of the toolbar buttons (Conduit, Box, Panel, or Support) to create a new component
2. The component will appear in the 3D viewport
3. Select the component to edit its properties in the right panel

#### Editing Components

1. Select a component from the viewport
2. Modify properties in the Properties panel on the right
3. Click "Apply Changes" to update the component

#### Transforming Components

- **Position**: Adjust X, Y, Z coordinates in the Properties panel
- **Rotation**: Set rotation angles around X, Y, Z axes
- **Scale**: Components support scaling (implementation in progress)

#### Creating Conduit Bends (Interactive Path Editing)

1. Create or select a conduit component
2. Click the "Edit Conduit Path" button in the toolbar
3. The button will turn orange to indicate edit mode is active
4. Click anywhere on the conduit or in the 3D space to add bend points
5. Orange spherical handles will appear at each bend point
6. Drag these handles to adjust the position of bend points
7. The conduit will automatically render as connected segments with elbows at bends
8. To remove bend points:
   - Use Edit → Delete Last Bend Point to remove the most recent bend
   - Use the "Clear All Bend Points" button in the Properties panel to reset the conduit
9. Click "Exit Edit Mode" to finish editing
10. Enable "Snap to Grid" from the View menu for precise positioning

#### File Operations

- **New**: Create a new empty workspace (File → New or Ctrl+N)
- **Open**: Load a saved component file (File → Open or Ctrl+O)
- **Save**: Save the current component (File → Save or Ctrl+S)
- **Save As**: Save to a new file (File → Save As or Ctrl+Shift+S)
- **Export JSON**: Export component to JSON format (File → Export JSON or Ctrl+E)

#### Reference Library

- Use the **References** top menu to open the local reference library shipped in your workspace under `References/docs`
- The first pass surfaces the `2026_national_electrical_estimator_ebook.pdf` file directly and exposes the `Electrical Material` folder plus its current top-level files/folders
- The existing component-side **Reference URL** field still works and now also supports local repo-relative file or folder paths such as `References/docs/2026_national_electrical_estimator_ebook.pdf`
- The properties panel now also includes a curated reference picker plus a suggested-reference button so you can populate the Reference URL field from the local catalog before applying changes to the current selection
- The **References** menu now also lets you set or reset a custom reference root for the current session, and the app also honors the `ECS_REFERENCE_ROOT` environment variable for portable setups outside this repo
- Reference assets are workspace-local and are not copied into the app build or publish output in this pass

#### Sheets

- The left panel now includes a **Project Browser** tree so the project can hold multiple persisted sheets instead of a single implicit canvas
- Markups, PDF underlays, named views, and page setup are now sheet-scoped and switch with the active sheet, while electrical components remain project-global in this first document-model pass
- The sheet browser now supports add, rename, delete, and up/down reorder actions, and deleting a non-active sheet preserves the current active sheet
- The last active sheet is now persisted in `.ecproj` saves and restored on reopen
- Each sheet now expands to show its saved named views directly in the left rail, and selecting a named view from another sheet switches to that sheet and restores the saved camera/2D state
- The **Markups** tab can now review either the current sheet or all sheets across the project, includes a sheet column in the markup list, and automatically reveals cross-sheet markup selections in their owning sheet
- The **Markups** tab now also includes grouped issue buckets that summarize the current visible review set by sheet, status, or author and let you pivot the markup list to a single bucket without changing the broader review scope
- The **Markups** tab now also exposes a first-pass threaded review surface for the selected issue: you can add replies, see reply counts in the list, search reply text from the Markups search box, and export/import reply metadata through the current markup persistence and XFDF paths
- Approve, reject, resolve, and visible-set review actions now also write status-audit entries into that same thread, so status history and discussion stay in one place and undo restores both the status and its audit entry together
- Markup issues now support explicit assignees in the same review flow: you can assign the selected issue or bulk-assign the current visible review set, assignee text is searchable from the Markups tab, assignment state is shown in the list/details UI, and assignment changes are recorded as undoable audit entries in the shared thread and round-trip through the current JSON, XML, and XFDF persistence paths
- Bulk review actions now operate on the currently visible review set, so **Resolve Visible** and **Void Visible** follow the active scope and filters
- XFDF export now uses the current review scope and filters instead of exporting only the raw active-sheet markup collection
- Older `.ecproj` files still load: legacy single-sheet markup, underlay, and named-view state is migrated into the first sheet automatically

#### Grid and Snap

- Toggle grid visibility from View menu
- Enable/disable snap to grid from View menu
- Adjust grid size from the toolbar

### Component Types

#### Conduit
- Cylindrical electrical conduit
- Configurable diameter and length
- Material options (EMT, PVC, etc.)
- **Interactive Bending**: Create multi-segment conduits with bends
  - Click "Edit Conduit Path" button to enter edit mode
  - Click on the conduit to add bend points
  - Drag orange handles to adjust bend positions
  - Supports 90° and 45° bends
  - Automatic elbow rendering at bend points
  - Clear individual or all bend points via Edit menu or Properties panel

#### Box
- Electrical junction boxes
- Adjustable dimensions
- Knockout count configuration

#### Panel
- Distribution panels
- Circuit count settings
- Amperage ratings

#### Support
- Mounting brackets and supports
- Load capacity specifications
- Various support types

### File Formats

#### .ecomp Files
Custom binary format for saving component data with full type information.

#### JSON Export
Standard JSON format for component data, suitable for integration with other systems.

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+N | New file |
| Ctrl+O | Open file |
| Ctrl+S | Save file |
| Ctrl+Shift+S | Save As |
| Ctrl+E | Export to JSON |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Delete | Delete selected component |
| Ctrl+Shift+G | Edit selected markup geometry |
| F2 | Edit selected structured markup text |
| Ctrl+Shift+A | Edit selected markup appearance |
| Escape | Cancel active insert/edit mode |

### Running Tests

The project includes a comprehensive automated test suite covering models, services, view models, rendering, markup editing, and MainWindow interaction seams.

```bash
# Build and run tests (requires Windows)
dotnet test ElectricalComponentSandbox.Tests/ElectricalComponentSandbox.Tests.csproj
```

*Note: Tests require Windows OS to run due to WPF framework dependencies.*

Current validated baseline: **776/776 tests passing**.

### Future Enhancements

- Constraint system for parametric relationships
- Expanded 2D documentation and orthographic workflows
- Component validation rules
- Material library
- Advanced transformation tools
- Multi-component selection
- Component cloning
- Import from standard formats

### License

[Add license information here]

### Contributing

[Add contribution guidelines here]
