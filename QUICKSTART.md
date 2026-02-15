# Quick Start Guide

## Installation (Windows)

1. **Prerequisites**
   - Windows 10/11 (64-bit)
   - .NET 8.0 SDK: https://dotnet.microsoft.com/download

2. **Get the Code**
   ```bash
   git clone https://github.com/Psimcoe3/Simcoe-Design.git
   cd Simcoe-Design
   ```

3. **Build**
   ```bash
   # Option 1: Use build script
   build.bat
   
   # Option 2: Manual build
   dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Release
   ```

4. **Run**
   ```bash
   dotnet run --project ElectricalComponentSandbox/ElectricalComponentSandbox.csproj
   ```

## Quick Usage

### Create a Component
1. Click toolbar button: **[Conduit]**, **[Box]**, **[Panel]**, or **[Support]**
2. Component appears in 3D viewport

### Edit Properties
1. Click component in viewport to select
2. Edit values in Properties panel (right side)
3. Click **[Apply Changes]**

### Save Your Work
- **File → Save As** - Save component to .ecomp file
- **File → Export JSON** - Export to JSON format

### Navigate 3D View
- **Rotate**: Left-click + drag
- **Pan**: Right-click + drag
- **Zoom**: Mouse wheel

## Example Components

Try the sample files in `Examples/`:
- `sample-conduit.json` - 2-inch EMT conduit
- `sample-panel.json` - 200A distribution panel

Load via **File → Open**

## Need Help?

📖 Full documentation:
- **README.md** - Overview and setup
- **USERGUIDE.md** - Detailed instructions
- **ARCHITECTURE.md** - Technical details
- **UI-REFERENCE.md** - Interface guide

## Common Tasks

### Create a Custom Conduit
1. Click **[Conduit]**
2. In Properties panel:
   - Name: "3-inch EMT Main Feed"
   - Width: 3.0
   - Material: "EMT"
   - Color: "#A9A9A9"
3. Click **[Apply Changes]**
4. **File → Save As** → name it `main-feed.ecomp`

### Position a Component
1. Select component
2. In Properties:
   - Position X: 10.0
   - Position Y: 5.0
   - Position Z: 0.0
3. Click **[Apply Changes]**

### Change Grid Size
1. Toolbar: Find "Grid Size" field
2. Enter new value (e.g., 0.5 or 2.0)
3. Press Enter

## Tips

- Enable **View → Snap to Grid** for precise alignment
- Use ViewCube (top-right) to quickly change camera angle
- Grid units are arbitrary - use what makes sense for your scale
- Export to JSON for integration with other tools

## Troubleshooting

**Can't see component?**
- Check position is not (0,0,0) if camera is at origin
- Try resetting view with ViewCube
- Verify dimensions are reasonable

**Build fails?**
- Ensure .NET 8.0 SDK is installed
- Run `dotnet restore` first
- Check you're on Windows (WPF requirement)

**Application won't start?**
- This is a Windows-only application (WPF)
- Linux/Mac can build but not run

## Version Info

- **Version**: 1.0.0 (MVP)
- **Date**: February 2026
- **Framework**: .NET 8.0
- **Platform**: Windows

---

Ready to design electrical components! 🔌⚡
