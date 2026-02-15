# Electrical Component Sandbox - UI Reference

## Application Overview

The Electrical Component Sandbox provides a modern, professional desktop interface for designing electrical components. The interface is divided into three main panels with a menu bar and toolbar at the top.

## Main Window Layout

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  File   Edit   View                                                          │
├──────────────────────────────────────────────────────────────────────────────┤
│  [Conduit]  [Box]  [Panel]  [Support]  │  Grid Size: [1.0  ]                │
├──────────┬─────────────────────────────────────────────┬─────────────────────┤
│          │                                             │                     │
│ LIBRARY  │           3D VIEWPORT                       │    PROPERTIES       │
│          │                                             │                     │
│ ┌──────┐ │  ╔═══════════════════════════════════╗     │ Select a component  │
│ │Conduit│ │  ║                                   ║     │ to view properties  │
│ │Box    │ │  ║         [ViewCube]                ║     │                     │
│ │Panel  │ │  ║                                   ║     │ ─────────────────   │
│ │Support│ │  ║                                   ║     │                     │
│ └──────┘ │  ║         Grid with                 ║     │ When a component    │
│          │  ║         3D components              ║     │ is selected:        │
│          │  ║                                   ║     │                     │
│          │  ║         Coordinate System         ║     │ Basic Properties    │
│          │  ║         in corner                 ║     │ Name: [         ]   │
│          │  ║                                   ║     │ Type: [         ]   │
│          │  ╚═══════════════════════════════════╝     │                     │
│          │                                             │ Transform           │
│          │  Grid extends in X and Z directions        │ Position X: [    ]  │
│          │  Y-axis is vertical (up)                   │ Position Y: [    ]  │
│          │                                             │ Position Z: [    ]  │
│          │                                             │ Rotation X: [    ]  │
│          │                                             │ Rotation Y: [    ]  │
│          │                                             │ Rotation Z: [    ]  │
│          │                                             │                     │
│          │                                             │ Parameters          │
│          │                                             │ Width:  [        ]  │
│          │                                             │ Height: [        ]  │
│          │                                             │ Depth:  [        ]  │
│          │                                             │ Material: [      ]  │
│          │                                             │ Elevation: [     ]  │
│          │                                             │ Color: [         ]  │
│          │                                             │                     │
│          │                                             │ [Apply Changes]     │
│          │                                             │                     │
└──────────┴─────────────────────────────────────────────┴─────────────────────┘
```

## Menu Bar

### File Menu
- **New**: Clear workspace and start fresh
- **Open...**: Load a saved component file (.ecomp)
- **Save**: Save the current component
- **Save As...**: Save to a new file location
- ──────────
- **Export JSON...**: Export component as JSON
- ──────────
- **Exit**: Close the application

### Edit Menu
- **Delete Selected**: Remove the currently selected component

### View Menu
- ☑ **Show Grid**: Toggle grid visibility
- ☑ **Snap to Grid**: Enable/disable snap-to-grid

## Toolbar

From left to right:
1. **[Conduit]** button - Create a new conduit component
2. **[Box]** button - Create a new electrical box
3. **[Panel]** button - Create a new electrical panel
4. **[Support]** button - Create a new support bracket
5. **│** Separator
6. **Grid Size:** label
7. **[1.0]** text input - Adjustable grid spacing

## Left Panel - Component Library

**Header**: "Component Library" (bold, light gray background)

**Content**: List of available component types:
- Conduit
- Electrical Box
- Electrical Panel
- Support Bracket

**Interaction**: Double-click any item to add it to the workspace

**Size**: 200 pixels wide

## Center Panel - 3D Viewport

**Header**: "3D Viewport" (bold, light gray background)

**Features**:
- White background for clear visualization
- Grid overlay (when enabled):
  - Light gray grid lines
  - 100 x 100 unit grid
  - Minor lines every 1 unit (configurable)
  - Major lines every 10 units
- Coordinate system indicator (bottom-left corner):
  - Red X-axis arrow
  - Green Y-axis arrow (up)
  - Blue Z-axis arrow
- ViewCube (top-right corner):
  - Interactive camera orientation control
  - Click faces/edges to snap to standard views
  - Front, Back, Left, Right, Top, Bottom views

**Component Visualization**:
- Conduit: Gray cylinder
- Box: Medium gray rectangular box
- Panel: Dark gray larger rectangular box
- Support: Light gray small rectangular box

**Camera Controls**:
- Left-click drag: Rotate view around components
- Right-click drag: Pan camera
- Mouse wheel: Zoom in/out
- Middle-click drag: Pan camera (alternative)

**Lighting**: Default 3D lighting from multiple angles for clear visualization

## Right Panel - Properties

**Header**: "Properties" (bold, light gray background)

**Size**: 300 pixels wide

**Default State** (no component selected):
- Displays: "Select a component to view properties" (italic, gray text)

**Active State** (component selected):

### Basic Properties
- **Name**: Editable text field for component name
- **Type**: Read-only text showing component type (Conduit, Box, Panel, or Support)

### Transform
Group of transformation properties:

**Position (X, Y, Z)**:
- Three numeric text fields
- X: Horizontal position (left-right)
- Y: Vertical position (up-down)
- Z: Depth position (forward-back)
- Units in grid spacing

**Rotation (X, Y, Z)**:
- Three numeric text fields
- Rotation angles in degrees
- X: Pitch (rotate around X-axis)
- Y: Yaw (rotate around Y-axis)
- Z: Roll (rotate around Z-axis)

### Parameters
Component dimensional and material properties:

- **Width**: Numeric field (units)
- **Height**: Numeric field (units)
- **Depth**: Numeric field (units)
- **Material**: Text field (e.g., "Steel", "PVC", "Aluminum")
- **Elevation**: Numeric field (height from reference level)
- **Color**: Text field (hex color code, e.g., "#808080")

### Actions
- **[Apply Changes]** button: Save property edits and update the 3D view

## Visual Design

### Color Scheme
- **Window Background**: White (#FFFFFF)
- **Panel Headers**: Light Gray (#D3D3D3)
- **Panel Borders**: Medium Gray (#808080)
- **Grid Lines**: Light Gray (#DCDCDC)
- **3D Background**: White (#FFFFFF)
- **Default Component Color**: Gray (#808080)

### Typography
- **Headers**: Bold, 14pt
- **Labels**: Regular, 11pt
- **Input Fields**: Regular, 11pt
- **Help Text**: Italic, 11pt, Gray

### Spacing
- **Margins**: 5 pixels around most elements
- **Padding**: 10 pixels for headers, 5 pixels for inputs
- **Panel Borders**: 1 pixel solid gray

## Component Appearance in 3D Viewport

### Conduit
- **Shape**: Cylinder
- **Default Orientation**: Along Z-axis
- **Default Color**: Dark Gray (#A9A9A9)
- **Default Dimensions**: 
  - Diameter: 0.5 units
  - Length: 10 units

### Box
- **Shape**: Rectangular box
- **Default Color**: Medium Gray (#808080)
- **Default Dimensions**:
  - Width: 4 units
  - Height: 4 units
  - Depth: 2 units

### Panel
- **Shape**: Large rectangular box
- **Default Color**: Charcoal (#696969)
- **Default Dimensions**:
  - Width: 20 units
  - Height: 30 units
  - Depth: 6 units

### Support
- **Shape**: Small rectangular box
- **Default Color**: Silver (#C0C0C0)
- **Default Dimensions**:
  - Width: 3 units
  - Height: 3 units
  - Depth: 1.5 units

## Interaction Feedback

### Selection
- Selected component may show highlight or outline (implementation-specific)
- Properties panel updates immediately when selection changes
- Library highlights double-clicked item briefly

### Editing
- Invalid property values show error dialog
- Successful property update shows success message
- File operations show progress and completion messages

### Dialog Boxes
All dialog boxes follow standard Windows conventions:
- **Information**: Blue (i) icon
- **Warning**: Yellow (!) icon
- **Error**: Red (X) icon
- **Question**: Blue (?) icon

## Responsive Behavior

The interface is designed for:
- **Minimum Resolution**: 1024 x 768
- **Recommended**: 1920 x 1080 or higher
- **Default Window Size**: 1400 x 800 pixels
- **Default State**: Maximized

Panels maintain fixed widths (Library: 200px, Properties: 300px) while the 3D viewport flexibly fills the remaining space.

## Accessibility

- All controls accessible via keyboard
- Tab order follows left-to-right, top-to-bottom
- Menu items have standard keyboard shortcuts (future enhancement)
- High contrast support through Windows settings

## Future UI Enhancements

Planned improvements include:
- Status bar showing current selection and coordinate information
- Multiple component selection with Ctrl+Click
- Context menus for quick actions
- Drag-and-drop from library to viewport
- Minimap for large component collections
- Layer/visibility controls
- Measurement tools and dimension display
