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

Alternatively, open `ElectricalComponentSandbox.sln` in Visual Studio and build/run from there.

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

#### File Operations

- **New**: Create a new empty workspace (File → New)
- **Open**: Load a saved component file (File → Open)
- **Save**: Save the current component (File → Save)
- **Save As**: Save to a new file (File → Save As)
- **Export JSON**: Export component to JSON format (File → Export JSON)

#### Grid and Snap

- Toggle grid visibility from View menu
- Enable/disable snap to grid from View menu
- Adjust grid size from the toolbar

### Component Types

#### Conduit
- Cylindrical electrical conduit
- Configurable diameter and length
- Material options (EMT, PVC, etc.)

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
