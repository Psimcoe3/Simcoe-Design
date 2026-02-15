# Quick Start Guide: Interactive Conduit Bending

## Overview
This feature allows you to create complex conduit paths with multiple bends using a simple click-and-drag interface.

## Quick Steps

### 1. Enter Edit Mode
```
Toolbar → Click "Edit Conduit Path" button
  ↓
Button turns ORANGE to show edit mode is active
```

### 2. Add Bend Points
```
Click on conduit or in 3D space
  ↓
Orange sphere handle appears at that location
  ↓
Conduit automatically redraws as connected segments
```

### 3. Adjust Positions
```
Click and drag any orange handle
  ↓
Conduit updates in real-time
  ↓
Enable "Snap to Grid" for precise positioning
```

### 4. Exit Edit Mode
```
Click "Exit Edit Mode" button
  ↓
Orange handles disappear
  ↓
Conduit is ready for further editing or export
```

## Visual Reference

### Straight Conduit (Before)
```
Start ●━━━━━━━━━━━━━━━━━━━━━━━━━━● End
```

### L-Shaped Conduit (After Adding 1 Bend Point)
```
Start ●━━━━━━━━━━┓
                 ┃
                 ┃ ○ <- Orange Handle (Bend Point)
                 ┃
                 ┗━━━━━━━━━━● End
```

### U-Shaped Conduit (After Adding 3 Bend Points)
```
Start ●━━━━━━━━━━┓
                 ┃ ○ Bend 1
                 ┃
                 ┣━━━━━━━━━━○ Bend 2
                 ┃
                 ┃ ○ Bend 3
                 ┗━━━━━━━━━━● End
```

## Common Use Cases

### 1. Wall Penetration
Create a conduit that goes vertical, through wall, then horizontal:
- Add bend point at wall bottom
- Add bend point at wall top
- Result: Vertical → horizontal → vertical path

### 2. Obstacle Avoidance
Route conduit around obstacles:
- Add bend points before and after obstacle
- Drag handles to position around the obstacle
- Result: Smooth path avoiding the obstruction

### 3. Panel Connections
Connect equipment at different heights and locations:
- Start at one equipment position
- Add bend points for each direction change
- End at destination equipment
- Result: Professional-looking connection path

## Tips & Tricks

### Precision Work
1. Enable "Snap to Grid" (View menu)
2. Set appropriate grid size (toolbar)
3. Drag handles - they'll snap to grid intersections

### Quick Editing
- Edit → Delete Last Bend Point: Remove most recent bend
- Properties Panel → Clear All Bend Points: Start over
- Properties Panel: See current bend point count

### Best Practices
- Plan your path before adding many bend points
- Use grid snap for industry-standard conduit runs
- Keep bend count minimal for realistic installations
- Verify clearances and bend radius compliance

## Keyboard Shortcuts
Currently not implemented, but planned for future:
- Ctrl+Z: Undo last bend point
- Delete: Remove selected bend point
- Esc: Exit edit mode

## File Compatibility

### Saving
- All bend points saved automatically in .ecomp format
- JSON export includes BendPoints array
- Example files provided in Examples/ folder

### Loading
- Old conduit files (no bends) load as straight conduits
- New bent conduit files display with all bends preserved
- Fully backward and forward compatible

## Example Files

Try these pre-made examples:
1. `Examples/sample-conduit.json` - Simple straight conduit
2. `Examples/sample-bent-conduit.json` - L-shaped conduit
3. `Examples/sample-u-shaped-conduit.json` - U-shaped conduit

Load via: File → Open → Select example file

## Troubleshooting

### Q: Handles not appearing?
A: Make sure a conduit is selected and edit mode is active (orange button)

### Q: Can't drag handles smoothly?
A: Disable "Snap to Grid" for freeform positioning

### Q: Conduit looks disconnected?
A: Elbows are rendered automatically - if segments seem disconnected, try adjusting bend point positions

### Q: How to delete specific bend point?
A: Currently only "delete last" or "clear all" supported. Delete last repeatedly to remove specific points.

## Need Help?

See full documentation:
- README.md - Feature overview
- CONDUIT-BENDING-IMPLEMENTATION.md - Technical details
- ARCHITECTURE.md - System design

---

**Enjoy creating complex conduit paths! 🎉**
