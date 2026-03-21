using System.Windows;

namespace ElectricalComponentSandbox.Markup.Models;

/// <summary>
/// Enumerates the types of markup annotations
/// </summary>
public enum MarkupType
{
    // ── Basic drawing primitives ──────────────────────────────────────────────
    Polyline,
    Polygon,
    Rectangle,
    Circle,
    Arc,
    Text,
    // ── Electrical plan overlays ──────────────────────────────────────────────
    Box,
    Panel,
    ConduitRun,
    Dimension,
    // ── Extended annotation types (Bluebeam / Autodesk parity) ──────────────
    Callout,        // Note callout with leader line
    RevisionCloud,  // Revision cloud around changed area
    LeaderNote,     // Leader with text note
    Stamp,          // Approval / status stamp
    Hatch,          // Filled hatch region
    Measurement,    // Point-to-point measurement (read-only, not stored as dim)
    Hyperlink       // Clickable region linking to URL or page
}

/// <summary>
/// Lifecycle status for punch-list / review workflows (Bluebeam-style).
/// </summary>
public enum MarkupStatus
{
    /// <summary>New markup, not yet assigned</summary>
    Open,
    /// <summary>Assigned and work has begun</summary>
    InProgress,
    /// <summary>Work completed and verified</summary>
    Approved,
    /// <summary>Cancelled / no longer applicable</summary>
    Void
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
    /// <summary>Hatch pattern for Hatch/Polygon fill.  Empty = solid fill.</summary>
    public string HatchPattern { get; set; } = string.Empty;
    /// <summary>Dash array for line types (CSV of lengths, e.g., &quot;5,3&quot;).  Empty = solid.</summary>
    public string DashArray { get; set; } = string.Empty;
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
    /// For circle/arc: one point (center).
    /// For text/stamp/callout: one point (anchor / leader-tail).
    /// For dimension/leader: [start, end, text-anchor].
    /// </summary>
    public List<Point> Vertices { get; set; } = new();

    /// <summary>Bounding rectangle in Document coordinates</summary>
    public Rect BoundingRect { get; set; }

    /// <summary>Radius for circle / arc markups (Document units)</summary>
    public double Radius { get; set; }

    /// <summary>Start angle in degrees (for Arc markups)</summary>
    public double ArcStartDeg { get; set; }

    /// <summary>Sweep angle in degrees (for Arc markups, positive = counter-clockwise)</summary>
    public double ArcSweepDeg { get; set; } = 90.0;

    /// <summary>Rotation angle in degrees (for select/move/rotate)</summary>
    public double RotationDegrees { get; set; }

    /// <summary>Text content for Text / Callout / Stamp markups</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>
    /// Optional hyperlink URL.  Used directly by Hyperlink markups; also
    /// lets any annotation be clickable (e.g. a stamp that links to a spec sheet).
    /// </summary>
    public string? HyperlinkUrl { get; set; }

    // ── Punch-list / review workflow ──────────────────────────────────────────

    /// <summary>Review status for punch-list workflows</summary>
    public MarkupStatus Status { get; set; } = MarkupStatus.Open;

    /// <summary>
    /// Optional response / reply text (used in review workflows).
    /// e.g. "Fixed in Rev B"
    /// </summary>
    public string? StatusNote { get; set; }

    // ── Layer + style ─────────────────────────────────────────────────────────

    /// <summary>Layer this markup belongs to</summary>
    public string LayerId { get; set; } = "markup-default";

    public MarkupAppearance Appearance { get; set; } = new();
    public MarkupMetadata Metadata { get; set; } = new();

    /// <summary>For cutout calculations: IDs of inner polygon markups subtracted from this polygon</summary>
    public List<string> CutoutIds { get; set; } = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Recalculates the bounding rect from current vertices and radius</summary>
    public void UpdateBoundingRect()
    {
        if ((Type == MarkupType.Circle || Type == MarkupType.Arc) && Vertices.Count >= 1)
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
