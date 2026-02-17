# Electrical Component Sandbox - Technical Architecture

## Overview

The Electrical Component Sandbox is a WPF desktop application built using the MVVM (Model-View-ViewModel) pattern. It provides a 3D workspace for designing parametric electrical components.

## Technology Stack

### Core Framework
- **.NET 8.0**: Modern .NET platform with C# 12
- **WPF (Windows Presentation Foundation)**: UI framework for Windows desktop applications
- **XAML**: Declarative markup for UI definition

### Third-Party Libraries
- **HelixToolkit.Wpf 2.25.0**: 3D graphics rendering and viewport controls
- **Newtonsoft.Json 13.0.3**: JSON serialization and deserialization

## Architecture

### Project Structure

```
ElectricalComponentSandbox/
├── Converters/                # WPF value converters
│   ├── NotNullToVisibilityConverter.cs
│   └── NullToVisibilityConverter.cs
├── Examples/                  # Example components
│   └── ComponentExamples.cs      # Sample component factory
├── Models/                    # Data models
│   ├── ElectricalComponent.cs    # Base component class + ComponentType enum
│   ├── BoxComponent.cs           # Box-specific model
│   ├── CableTrayComponent.cs     # Cable tray model
│   ├── ConduitComponent.cs       # Conduit-specific model
│   ├── HangerComponent.cs        # Hanger/threaded rod model
│   ├── Layer.cs                  # Drawing layer model
│   ├── PanelComponent.cs         # Panel-specific model
│   ├── PdfUnderlay.cs            # PDF underlay reference model
│   ├── ProjectModel.cs           # Top-level project model for save/load
│   └── SupportComponent.cs       # Support-specific model
├── Services/                  # Business logic services
│   ├── BomExportService.cs       # Bill of Materials CSV export
│   ├── ComponentFileService.cs   # Component file I/O (.ecomp format)
│   ├── PdfCalibrationService.cs  # PDF scale calibration
│   ├── ProjectFileService.cs     # Project file I/O (.ecproj format)
│   ├── SnapService.cs            # Endpoint/midpoint/intersection snapping
│   ├── UndoRedoService.cs        # Undo/redo with command pattern
│   └── UnitConversionService.cs  # Imperial/metric unit conversion
├── ViewModels/                # View models (MVVM pattern)
│   └── MainViewModel.cs          # Main window view model
├── App.xaml / App.xaml.cs     # Application entry point
├── MainWindow.xaml / .xaml.cs # Main window UI and code-behind
└── AssemblyInfo.cs            # Assembly metadata
```

### Design Patterns

#### MVVM (Model-View-ViewModel)
- **Models**: Pure data classes representing electrical components
- **Views**: XAML UI definitions with minimal code-behind
- **ViewModels**: Business logic, data binding, and UI state management

#### Repository Pattern
- `ComponentFileService`: Encapsulates component file I/O operations
- `ProjectFileService`: Handles full project save/load
- `BomExportService`: Bill of Materials CSV generation
- Separates data access logic from business logic

#### Factory Pattern
- `ComponentExamples`: Creates preconfigured sample components
- `MainViewModel.AddComponent()`: Factory method for creating components

## Component Model

### Class Hierarchy

```
ElectricalComponent (abstract)
├── BoxComponent
├── CableTrayComponent
├── ConduitComponent
├── HangerComponent
├── PanelComponent
└── SupportComponent
```

### Base Component Properties

```csharp
public abstract class ElectricalComponent
{
    string Id                    // Unique identifier (GUID)
    string Name                  // Display name
    ComponentType Type           // Component type enum
    Point3D Position            // 3D position
    Vector3D Rotation           // Rotation angles (X, Y, Z)
    Vector3D Scale              // Scale factors (X, Y, Z)
    ComponentParameters         // Dimensional and material parameters
    List<string> Constraints    // Design constraints (future use)
}
```

### Component Parameters

```csharp
public class ComponentParameters
{
    double Width                // Width dimension
    double Height               // Height dimension
    double Depth                // Depth dimension
    string Material             // Material type
    double Elevation            // Elevation from reference
    string Color                // Color in hex format
}
```

## Data Persistence

### File Formats

#### .ecomp Format
- Binary JSON with type information
- Uses `TypeNameHandling.Auto` for polymorphic deserialization
- Preserves exact component type and all properties
- Not human-readable but complete

#### JSON Export Format
- Standard JSON without type metadata
- Human-readable and editable
- Suitable for integration with external systems
- May lose some type-specific information

### Serialization Strategy

```csharp
// Save with type info (internal format)
TypeNameHandling = TypeNameHandling.Auto

// Export without type info (standard JSON)
TypeNameHandling = TypeNameHandling.None
```

## 3D Rendering

### HelixToolkit Integration

The application uses HelixToolkit.Wpf for 3D visualization:

- **HelixViewport3D**: Main 3D viewport control
- **MeshBuilder**: Programmatic mesh generation
- **DefaultLights**: Standard 3D lighting setup
- **GridLinesVisual3D**: Reference grid overlay

### Geometry Generation

Each component type generates appropriate 3D geometry:

```csharp
Conduit  → Cylinder (MeshBuilder.AddCylinder)
Box      → Rectangular box (MeshBuilder.AddBox)
Panel    → Rectangular box (MeshBuilder.AddBox)
Support  → Rectangular box (MeshBuilder.AddBox)
```

### Transformations

Components support standard 3D transformations:
1. Translation (Position)
2. Rotation (around X, Y, Z axes)
3. Scaling (uniform or non-uniform)

Applied via `Transform3DGroup` in order.

## UI Components

### Main Window Layout

```
┌─────────────────────────────────────────────┐
│  Menu Bar (File, Edit, View)               │
├─────────────────────────────────────────────┤
│  Toolbar (Component buttons, Grid size)    │
├───────┬──────────────────────────┬──────────┤
│ Lib   │   3D Viewport            │ Props    │
│ rary  │   (HelixViewport3D)      │ Panel    │
│       │                          │          │
│ List  │   Grid, Components       │ Edit     │
│       │   Camera controls        │ Fields   │
│       │                          │          │
└───────┴──────────────────────────┴──────────┘
```

### Data Binding

- **Component Library**: Bound to `LibraryComponents` collection
- **3D Viewport**: Updated via code-behind when `Components` changes
- **Properties Panel**: Two-way binding to `SelectedComponent` properties
- **Grid Settings**: Bound to `ShowGrid`, `SnapToGrid`, `GridSize`

## Key Features

### Grid and Snap

```csharp
if (SnapToGrid)
{
    position.X = Math.Round(position.X / GridSize) * GridSize;
    position.Y = Math.Round(position.Y / GridSize) * GridSize;
    position.Z = Math.Round(position.Z / GridSize) * GridSize;
}
```

### Property Editing

1. User selects component in viewport
2. Properties panel populates with component data
3. User edits values in text boxes
4. User clicks "Apply Changes"
5. ViewModel updates component
6. Viewport refreshes to show changes

## Error Handling

### File Operations
- Try-catch blocks wrap all I/O operations
- Exceptions wrapped in `InvalidOperationException` with context
- User-friendly error messages via MessageBox

### Property Validation
- Try-catch around property parsing (string → double)
- Invalid values show error dialog
- Component state remains unchanged on error

## Performance Considerations

### 3D Rendering
- HelixToolkit handles viewport rendering efficiently
- MeshBuilder creates optimized geometry
- Viewport only refreshes when components change

### Data Binding
- INotifyPropertyChanged pattern for reactive updates
- ObservableCollection for automatic UI updates
- Minimal re-rendering with targeted property change notifications

## Extensibility

### Adding New Component Types

1. Create new class inheriting from `ElectricalComponent`
2. Add to `ComponentType` enum
3. Add factory method in `MainViewModel.AddComponent()`
4. Add geometry generation in `CreateComponentGeometry()`
5. Add to library initialization

### Custom Properties

1. Add properties to component class
2. Update Properties panel XAML
3. Add data binding in code-behind
4. Update `ApplyProperties_Click()` handler

### New File Formats

1. Add methods to `ComponentFileService`
2. Create appropriate serializer settings
3. Add menu items and handlers in `MainWindow`

## Security

### No Known Vulnerabilities
- CodeQL analysis: 0 alerts
- No dependency vulnerabilities (GitHub Advisory Database checked)

### Safe Practices
- Async file I/O prevents UI blocking
- Exception handling prevents crashes
- No eval or dynamic code execution
- No external network connections

## Future Enhancements

### Planned Features
- Constraint system for parametric design
- Undo/Redo with command pattern
- Multi-component selection
- 2D orthographic views
- Component validation rules
- Plugin system for custom components

### Potential Improvements
- Unit tests for models and services
- Integration tests for file operations
- Performance profiling for large component counts
- Accessibility improvements (keyboard navigation)
- Localization support

## Build and Deployment

### Build Process
```bash
dotnet restore
dotnet build
```

### Publishing
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### System Requirements
- Windows 10/11 (x64)
- .NET 8.0 Runtime (or self-contained deployment)
- DirectX 9 or higher for 3D rendering
- 4GB RAM minimum, 8GB recommended

## References

- [WPF Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [HelixToolkit Documentation](https://github.com/helix-toolkit/helix-toolkit)
- [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
