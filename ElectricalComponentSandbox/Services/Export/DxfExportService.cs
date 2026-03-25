using System.IO;
using System.Text;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services.Export;

/// <summary>
/// Exports project components, layers, and circuits to a valid AutoCAD DXF R2010 text file.
/// </summary>
public class DxfExportService
{
    private int _handleCounter;

    private string NextHandle() => (++_handleCounter).ToString("X");

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Exports components and layers to a DXF file.
    /// Components are placed as INSERT references to block definitions.
    /// Conduit paths are exported as LWPOLYLINE entities.
    /// </summary>
    public void Export(
        IReadOnlyList<ElectricalComponent> components,
        IReadOnlyList<Layer> layers,
        string outputPath)
    {
        _handleCounter = 0;
        var sb = new StringBuilder();

        WriteHeader(sb);
        WriteTables(sb, layers);
        WriteBlocks(sb, components);
        WriteEntities(sb, components);
        WriteEof(sb);

        File.WriteAllText(outputPath, sb.ToString(), Encoding.ASCII);
    }

    // ── HEADER section ────────────────────────────────────────────────────────

    private void WriteHeader(StringBuilder sb)
    {
        WriteSectionStart(sb, "HEADER");

        WriteGroupCode(sb, 9, "$ACADVER");
        WriteGroupCode(sb, 1, "AC1024");

        WriteGroupCode(sb, 9, "$INSUNITS");
        WriteGroupCode(sb, 70, "1");

        WriteSectionEnd(sb);
    }

    // ── TABLES section ────────────────────────────────────────────────────────

    private void WriteTables(StringBuilder sb, IReadOnlyList<Layer> layers)
    {
        WriteSectionStart(sb, "TABLES");

        // LAYER table
        WriteGroupCode(sb, 0, "TABLE");
        WriteGroupCode(sb, 2, "LAYER");
        WriteGroupCode(sb, 5, NextHandle());
        WriteGroupCode(sb, 70, layers.Count.ToString());

        foreach (var layer in layers)
        {
            WriteGroupCode(sb, 0, "LAYER");
            WriteGroupCode(sb, 5, NextHandle());
            WriteGroupCode(sb, 2, layer.Name);

            int flags = 0;
            if (layer.IsFrozen) flags |= 1;
            if (layer.IsLocked) flags |= 4;
            WriteGroupCode(sb, 70, flags.ToString());

            WriteGroupCode(sb, 62, MapColorToAci(layer.Color).ToString());
            WriteGroupCode(sb, 6, MapLineType(layer.LineType));
        }

        WriteGroupCode(sb, 0, "ENDTAB");

        // LTYPE table (minimal - define the standard linetypes)
        WriteGroupCode(sb, 0, "TABLE");
        WriteGroupCode(sb, 2, "LTYPE");
        WriteGroupCode(sb, 5, NextHandle());
        WriteGroupCode(sb, 70, "1");

        WriteGroupCode(sb, 0, "LTYPE");
        WriteGroupCode(sb, 5, NextHandle());
        WriteGroupCode(sb, 2, "CONTINUOUS");
        WriteGroupCode(sb, 70, "0");
        WriteGroupCode(sb, 3, "Solid line");
        WriteGroupCode(sb, 72, "65");
        WriteGroupCode(sb, 73, "0");
        WriteGroupCode(sb, 40, "0.0");

        WriteGroupCode(sb, 0, "ENDTAB");

        WriteSectionEnd(sb);
    }

    // ── BLOCKS section ────────────────────────────────────────────────────────

    private void WriteBlocks(StringBuilder sb, IReadOnlyList<ElectricalComponent> components)
    {
        WriteSectionStart(sb, "BLOCKS");

        var blockTypes = new[]
        {
            ComponentType.Conduit,
            ComponentType.Box,
            ComponentType.Panel,
            ComponentType.Support,
            ComponentType.CableTray,
            ComponentType.Hanger
        };

        foreach (var type in blockTypes)
        {
            var representative = FindRepresentative(components, type);
            WriteBlockDefinition(sb, type, representative);
        }

        WriteSectionEnd(sb);
    }

    private ElectricalComponent? FindRepresentative(
        IReadOnlyList<ElectricalComponent> components, ComponentType type)
    {
        foreach (var c in components)
        {
            if (c.Type == type) return c;
        }
        return null;
    }

    private void WriteBlockDefinition(StringBuilder sb, ComponentType type,
        ElectricalComponent? representative)
    {
        string blockName = GetBlockName(type);

        WriteGroupCode(sb, 0, "BLOCK");
        WriteGroupCode(sb, 5, NextHandle());
        WriteGroupCode(sb, 8, "0");
        WriteGroupCode(sb, 2, blockName);
        WriteGroupCode(sb, 70, "0");
        WriteGroupCode(sb, 10, "0.0");
        WriteGroupCode(sb, 20, "0.0");
        WriteGroupCode(sb, 30, "0.0");

        if (type == ComponentType.Conduit)
        {
            double length = 10.0;
            if (representative is ConduitComponent conduit)
                length = conduit.Length;

            // Conduit block: a LINE along X
            WriteGroupCode(sb, 0, "LINE");
            WriteGroupCode(sb, 5, NextHandle());
            WriteGroupCode(sb, 8, "0");
            WriteGroupCode(sb, 10, "0.0");
            WriteGroupCode(sb, 20, "0.0");
            WriteGroupCode(sb, 30, "0.0");
            WriteGroupCode(sb, 11, FormatDouble(length));
            WriteGroupCode(sb, 21, "0.0");
            WriteGroupCode(sb, 31, "0.0");
        }
        else
        {
            // Rectangle using LWPOLYLINE (Width x Depth)
            double w = representative?.Parameters.Width ?? 1.0;
            double d = representative?.Parameters.Depth ?? 1.0;
            double hw = w / 2.0;
            double hd = d / 2.0;

            WriteGroupCode(sb, 0, "LWPOLYLINE");
            WriteGroupCode(sb, 5, NextHandle());
            WriteGroupCode(sb, 8, "0");
            WriteGroupCode(sb, 90, "4");
            WriteGroupCode(sb, 70, "1"); // closed polyline

            WriteGroupCode(sb, 10, FormatDouble(-hw));
            WriteGroupCode(sb, 20, FormatDouble(-hd));
            WriteGroupCode(sb, 10, FormatDouble(hw));
            WriteGroupCode(sb, 20, FormatDouble(-hd));
            WriteGroupCode(sb, 10, FormatDouble(hw));
            WriteGroupCode(sb, 20, FormatDouble(hd));
            WriteGroupCode(sb, 10, FormatDouble(-hw));
            WriteGroupCode(sb, 20, FormatDouble(hd));
        }

        WriteGroupCode(sb, 0, "ENDBLK");
        WriteGroupCode(sb, 5, NextHandle());
        WriteGroupCode(sb, 8, "0");
    }

    // ── ENTITIES section ──────────────────────────────────────────────────────

    private void WriteEntities(StringBuilder sb, IReadOnlyList<ElectricalComponent> components)
    {
        WriteSectionStart(sb, "ENTITIES");

        foreach (var comp in components)
        {
            if (comp is ConduitComponent conduit)
            {
                WriteConduitEntity(sb, conduit);
            }
            else
            {
                WriteInsertEntity(sb, comp);
            }
        }

        WriteSectionEnd(sb);
    }

    private void WriteInsertEntity(StringBuilder sb, ElectricalComponent comp)
    {
        string blockName = GetBlockName(comp.Type);

        WriteGroupCode(sb, 0, "INSERT");
        WriteGroupCode(sb, 5, NextHandle());
        WriteGroupCode(sb, 8, comp.LayerId);
        WriteGroupCode(sb, 2, blockName);

        // Position: X, Z for plan view (Y up in 3D becomes Z in DXF 2D)
        WriteGroupCode(sb, 10, FormatDouble(comp.Position.X));
        WriteGroupCode(sb, 20, FormatDouble(comp.Position.Z));
        WriteGroupCode(sb, 30, "0.0");

        // Scale
        WriteGroupCode(sb, 41, FormatDouble(comp.Scale.X));
        WriteGroupCode(sb, 42, FormatDouble(comp.Scale.Y));

        // Rotation (Y component as angle in degrees)
        WriteGroupCode(sb, 50, FormatDouble(comp.Rotation.Y));
    }

    private void WriteConduitEntity(StringBuilder sb, ConduitComponent conduit)
    {
        if (conduit.BendPoints.Count > 0)
        {
            // LWPOLYLINE with path points (X, Z for plan view)
            var pathPoints = conduit.GetPathPoints();

            WriteGroupCode(sb, 0, "LWPOLYLINE");
            WriteGroupCode(sb, 5, NextHandle());
            WriteGroupCode(sb, 8, conduit.LayerId);
            WriteGroupCode(sb, 90, pathPoints.Count.ToString());
            WriteGroupCode(sb, 70, "0"); // open polyline

            foreach (var pt in pathPoints)
            {
                // Offset by component position; X/Z for plan view
                double x = conduit.Position.X + pt.X;
                double y = conduit.Position.Z + pt.Z;
                WriteGroupCode(sb, 10, FormatDouble(x));
                WriteGroupCode(sb, 20, FormatDouble(y));
            }
        }
        else
        {
            // Simple LINE from position to position + length along X
            double x1 = conduit.Position.X;
            double y1 = conduit.Position.Z;
            double x2 = conduit.Position.X + conduit.Length;
            double y2 = conduit.Position.Z;

            WriteGroupCode(sb, 0, "LINE");
            WriteGroupCode(sb, 5, NextHandle());
            WriteGroupCode(sb, 8, conduit.LayerId);
            WriteGroupCode(sb, 10, FormatDouble(x1));
            WriteGroupCode(sb, 20, FormatDouble(y1));
            WriteGroupCode(sb, 30, "0.0");
            WriteGroupCode(sb, 11, FormatDouble(x2));
            WriteGroupCode(sb, 21, FormatDouble(y2));
            WriteGroupCode(sb, 31, "0.0");
        }
    }

    // ── DXF structure helpers ─────────────────────────────────────────────────

    private static void WriteSectionStart(StringBuilder sb, string sectionName)
    {
        WriteGroupCode(sb, 0, "SECTION");
        WriteGroupCode(sb, 2, sectionName);
    }

    private static void WriteSectionEnd(StringBuilder sb)
    {
        WriteGroupCode(sb, 0, "ENDSEC");
    }

    private static void WriteEof(StringBuilder sb)
    {
        WriteGroupCode(sb, 0, "EOF");
    }

    private static void WriteGroupCode(StringBuilder sb, int code, string value)
    {
        sb.AppendLine(code.ToString().PadLeft(3));
        sb.AppendLine(value);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static string GetBlockName(ComponentType type) => type switch
    {
        ComponentType.Conduit => "CONDUIT_BLOCK",
        ComponentType.Box => "BOX_BLOCK",
        ComponentType.Panel => "PANEL_BLOCK",
        ComponentType.Support => "SUPPORT_BLOCK",
        ComponentType.CableTray => "CABLETRAY_BLOCK",
        ComponentType.Hanger => "HANGER_BLOCK",
        _ => "UNKNOWN_BLOCK"
    };

    /// <summary>
    /// Maps a hex color string to the closest AutoCAD Color Index (ACI) value.
    /// </summary>
    public static int MapColorToAci(string? hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
            return 7;

        var upper = hexColor.Trim().ToUpperInvariant();

        return upper switch
        {
            "#FF0000" => 1,  // red
            "#FFFF00" => 2,  // yellow
            "#00FF00" => 3,  // green
            "#00FFFF" => 4,  // cyan
            "#0000FF" => 5,  // blue
            "#FF00FF" => 6,  // magenta
            "#FFFFFF" => 7,  // white
            "#808080" => 8,  // dark gray
            _ => 7           // default to white
        };
    }

    private static string MapLineType(LineType lineType) => lineType switch
    {
        LineType.Continuous => "CONTINUOUS",
        LineType.Dashed => "DASHED",
        LineType.Dotted => "DOT",
        LineType.Phantom => "PHANTOM",
        LineType.Hidden => "HIDDEN",
        LineType.Center => "CENTER",
        LineType.DashDot => "DASHDOT",
        LineType.DashDotDot => "DIVIDE",
        _ => "CONTINUOUS"
    };

    private static string FormatDouble(double value)
    {
        return value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }
}
