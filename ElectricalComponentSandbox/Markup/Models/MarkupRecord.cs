using System.Windows;

namespace ElectricalComponentSandbox.Markup.Models;

/// <summary>
/// Enumerates the types of markup annotations
/// </summary>
public enum MarkupType
{
    Polyline,
    Polygon,
    Rectangle,
    Circle,
    Text,
    Box,
    Panel,
    ConduitRun,
    Dimension
}

/// <summary>
/// Visual appearance settings for a markup
/// </summary>
public class MarkupAppearance
{
    public string StrokeColor { get; set; } = "#FF0000";
    public double StrokeWidth { get; set; } = 2.0;
    public string FillColor { get; set; } = "#40FF0000";
    public double Opacity { get; set; } = 1.0;
    public string FontFamily { get; set; } = "Arial";
    public double FontSize { get; set; } = 12.0;
}

/// <summary>
/// Metadata attached to a markup (label, depth, custom fields)
/// </summary>
public class MarkupMetadata
{
    public string Label { get; set; } = string.Empty;
    public double Depth { get; set; } = 0.0;
    public string Subject { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> CustomFields { get; set; } = new();
}

/// <summary>
/// A parametric markup record stored on the annotation layer.
/// Uses Document-space (PDF points) coordinates.
/// </summary>
public class MarkupRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MarkupType Type { get; set; }

    /// <summary>
    /// Ordered vertices in Document coordinates (PDF points).
    /// For polyline/polygon: the vertex list.
    /// For rectangle: two corners (top-left, bottom-right).
    /// For circle: one point (center).
    /// For text: one point (anchor).
    /// </summary>
    public List<Point> Vertices { get; set; } = new();

    /// <summary>
    /// Bounding rectangle in Document coordinates
    /// </summary>
    public Rect BoundingRect { get; set; }

    /// <summary>
    /// Radius for circle markups (in Document units)
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// Rotation angle in degrees (for select/move/rotate)
    /// </summary>
    public double RotationDegrees { get; set; }

    /// <summary>
    /// Text content for Text markups
    /// </summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>
    /// Layer this markup belongs to
    /// </summary>
    public string LayerId { get; set; } = "markup-default";

    public MarkupAppearance Appearance { get; set; } = new();
    public MarkupMetadata Metadata { get; set; } = new();

    /// <summary>
    /// For cutout calculations: IDs of inner polygon markups subtracted from this polygon
    /// </summary>
    public List<string> CutoutIds { get; set; } = new();

    /// <summary>
    /// Recalculates the bounding rect from current vertices and radius
    /// </summary>
    public void UpdateBoundingRect()
    {
        if (Type == MarkupType.Circle && Vertices.Count >= 1)
        {
            var c = Vertices[0];
            BoundingRect = new Rect(c.X - Radius, c.Y - Radius, Radius * 2, Radius * 2);
            return;
        }

        if (Vertices.Count == 0)
        {
            BoundingRect = Rect.Empty;
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var v in Vertices)
        {
            if (v.X < minX) minX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.X > maxX) maxX = v.X;
            if (v.Y > maxY) maxY = v.Y;
        }

        BoundingRect = new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
