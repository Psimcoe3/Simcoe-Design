# Electrical Component Sandbox - User Guide

## Getting Started

### Installation

1. Ensure you have .NET 8.0 SDK installed on your Windows machine
2. Clone the repository:
   ```bash
   git clone https://github.com/Psimcoe3/Simcoe-Design.git
   cd Simcoe-Design
   ```
3. Build the application:
   ```bash
   dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj
   ```
4. Run the application:
   ```bash
   dotnet run --project ElectricalComponentSandbox/ElectricalComponentSandbox.csproj
   ```

### First Launch

When you first launch the application, you'll see:
- **Left Panel**: Component Library with available electrical components
- **Center Panel**: 3D Viewport for visualizing and manipulating components
- **Right Panel**: Properties panel for editing component details

## Creating Components

### From Toolbar
Click any of the toolbar buttons to create a component:
- **Conduit**: Creates a cylindrical conduit segment
- **Box**: Creates an electrical junction box
- **Panel**: Creates an electrical distribution panel
- **Support**: Creates a support bracket

### From Library
Double-click any item in the Component Library (left panel) to add it to your workspace.

## Editing Components

### Selecting Components
- Click on a component in the 3D viewport to select it
- The Properties panel will update to show the component's details

### Modifying Properties

#### Basic Properties
- **Name**: Give your component a descriptive name
- **Type**: Shows the component type (read-only)

#### Transform Properties
- **Position (X, Y, Z)**: Set the 3D coordinates of the component
- **Rotation (X, Y, Z)**: Set rotation angles in degrees around each axis

#### Parameters
- **Width**: Component width
- **Height**: Component height
- **Depth**: Component depth
- **Material**: Material type (e.g., "Steel", "PVC", "Aluminum")
- **Elevation**: Elevation from reference level
- **Color**: Color in hex format (e.g., "#808080")

After modifying properties, click **Apply Changes** to update the component.

## Viewport Navigation

The 3D viewport supports standard navigation:
- **Rotate View**: Left-click and drag
- **Pan**: Right-click and drag (or middle-click and drag)
- **Zoom**: Mouse wheel
- **Reset View**: Use the ViewCube (top-right corner)

### Grid and Snap

- **Show Grid**: Toggle grid visibility from View menu
- **Snap to Grid**: Enable/disable snap-to-grid from View menu
- **Grid Size**: Adjust the grid spacing in the toolbar (default: 1.0 units)

## File Operations

### New File
File → New
- Creates a new empty workspace
- Warning: Unsaved changes will be lost

### Open File
File → Open
- Opens a previously saved component file (.ecomp)
- Replaces current workspace with loaded component

### Save File
File → Save
- Saves the currently selected component
- If no file path exists, prompts for Save As

### Save As
File → Save As
- Saves the currently selected component to a new file
- Prompts for file location and name

### Export JSON
File → Export JSON
- Exports the currently selected component to JSON format
- Useful for integration with other tools and systems

## Component Types

### Conduit
Electrical conduit for running wires and cables.

**Special Properties:**
- Diameter: Conduit inner diameter
- Length: Total conduit length
- Conduit Type: EMT, PVC, Rigid, etc.

**Typical Use Cases:**
- Wire runs between panels and boxes
- Cable protection in walls and ceilings

### Box
Junction boxes, outlet boxes, and electrical enclosures.

**Special Properties:**
- Knockout Count: Number of knockout holes
- Box Type: Junction Box, Outlet Box, Pull Box, etc.

**Typical Use Cases:**
- Wire junction points
- Device mounting
- Pull points for wire routing

### Panel
Electrical distribution panels and load centers.

**Special Properties:**
- Circuit Count: Number of circuits/breakers
- Amperage: Maximum amperage rating
- Panel Type: Distribution, Sub-Panel, Load Center, etc.

**Typical Use Cases:**
- Main electrical service panels
- Sub-panels for distributed loads
- Breaker panels

### Support
Mounting brackets, hangers, and support structures.

**Special Properties:**
- Load Capacity: Maximum load in pounds
- Support Type: Bracket, Hanger, Strap, etc.

**Typical Use Cases:**
- Conduit support
- Panel mounting
- Equipment support

## Tips and Best Practices

### Naming Convention
Use descriptive names that include:
- Component type
- Size/capacity
- Location or purpose
- Example: "2-inch EMT Conduit - Main Feed"

### Organization
- Group related components by elevation
- Use consistent material specifications
- Maintain standard color coding

### File Management
- Save frequently during design work
- Use meaningful file names
- Keep backup copies of important components

### Performance
- Complex components may affect viewport performance
- Close unused viewports when working with large models

## Keyboard Shortcuts

Currently the application uses menu and toolbar actions. Future versions will include:
- Ctrl+N: New file
- Ctrl+O: Open file
- Ctrl+S: Save file
- Delete: Delete selected component

## Troubleshooting

### Component Not Visible
- Check if the component is at the origin (0, 0, 0)
- Verify the component has non-zero dimensions
- Reset the viewport camera using the ViewCube

### Properties Not Updating
- Ensure you clicked "Apply Changes" after editing
- Check that a component is selected (highlighted in viewport)

### File Won't Save
- Ensure you have a component selected
- Check file path permissions
- Verify disk space is available

### Grid Not Visible
- Check View → Show Grid is enabled
- Adjust grid size if too small or large for current view scale

## Advanced Features

### Custom Materials
You can specify any material type in the Material field. Common options:
- Steel
- Aluminum
- PVC
- Copper
- Galvanized Steel
- Stainless Steel

### Color Coding
Use hex color codes to color-code your components:
- #FF0000 - Red (High voltage)
- #0000FF - Blue (Control circuits)
- #00FF00 - Green (Ground)
- #FFFF00 - Yellow (Caution)
- #808080 - Gray (Standard)

### Elevation Planning
Use the Elevation parameter to organize components by floor level:
- 0.0 - Grade level
- 10.0 - First floor (typical 10' ceiling)
- 20.0 - Second floor
- etc.

## Integration

### JSON Export Format
The JSON export includes all component properties and can be integrated with:
- BIM systems
- Cost estimation tools
- Material ordering systems
- Custom analysis tools

Example JSON structure:
```json
{
  "Name": "Component Name",
  "Type": "Conduit",
  "Position": { "X": 0, "Y": 0, "Z": 0 },
  "Parameters": {
    "Width": 2.0,
    "Material": "Steel",
    "Elevation": 10.0
  }
}
```

## Support

For issues, questions, or feature requests, please visit the GitHub repository:
https://github.com/Psimcoe3/Simcoe-Design

## Future Development

Planned features include:
- Constraint system for parametric relationships
- 2D drawing view
- Multi-component selection and editing
- Component libraries (import/export)
- Undo/Redo functionality
- Dimension annotations
- Material cost calculations
- Export to CAD formats
