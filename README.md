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
- **Interactive Conduit Bending**: Click and drag to create complex conduit paths with multiple bends (NEW!)

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
| Delete | Delete selected component |
| Escape | Exit conduit edit mode |

### Running Tests

The project includes a comprehensive unit test suite covering models, services, and viewmodels.

```bash
# Build and run tests (requires Windows)
dotnet test ElectricalComponentSandbox.Tests/ElectricalComponentSandbox.Tests.csproj
```

*Note: Tests require Windows OS to run due to WPF framework dependencies.*

### Future Enhancements

- Constraint system for parametric relationships
- Advanced 2D drawing mode
- Component validation rules
- Material library
- Advanced transformation tools
- Undo/Redo functionality
- Multi-component selection
- Component cloning
- Import from standard formats

### License

[Add license information here]

### Contributing

[Add contribution guidelines here]
