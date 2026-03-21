using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Services.Dimensioning;

/// <summary>
/// Creates parametric <see cref="MarkupRecord"/> instances for standard 2-D dimension types
/// (Linear, Aligned, Angular, Radial, Diameter, Arc-Length).
///
/// All points are in Document-space (PDF points / drawing units).
/// The returned records carry enough information for both rendering and editing:
///  - Vertices[0..1] = the two measured points (extension line origins)
///  - Vertices[2]    = the dimension line offset position (text anchor hint)
///  - TextContent    = formatted measurement string
/// </summary>
public class Dimension2DService
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Number format for all measurements (e.g. "F2", "F3")</summary>
    public string NumberFormat { get; set; } = "F2";

    /// <summary>Unit suffix appended to every label (e.g. "\"", " mm", " in")</summary>
    public string UnitSuffix { get; set; } = "\"";

    /// <summary>Scale factor applied to raw document-unit distances before formatting</summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>Arrow head size in document units</summary>
    public double ArrowSize { get; set; } = 4.0;

    /// <summary>Text height in document units</summary>
    public double TextHeight { get; set; } = 8.0;

    /// <summary>Default extension line offset from the measured point</summary>
    public double ExtLineOffset { get; set; } = 2.0;

    /// <summary>Default appearance applied to created dimension records</summary>
    public MarkupAppearance DefaultAppearance { get; set; } = new()
    {
        StrokeColor = "#000000",
        StrokeWidth = 1.0,
        FillColor = "#00000000",
        FontFamily = "Arial",
        FontSize = 8.0,
        Opacity = 1.0
    };

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Horizontal or vertical linear dimension between two points.
    /// The dimension line is placed at <paramref name="dimLineOffset"/> perpendicular to the measured axis.
    /// </summary>
    /// <param name="p1">First extension line origin</param>
    /// <param name="p2">Second extension line origin</param>
    /// <param name="dimLineOffset">
    /// Signed offset from the measured axis to the dimension line.
    /// Positive = away from origin side.
    /// </param>
    /// <param name="isHorizontal">True = measure horizontal (X) distance; False = vertical (Y).</param>
    public MarkupRecord CreateLinear(Point p1, Point p2, double dimLineOffset, bool isHorizontal)
    {
        double rawDist = isHorizontal
            ? Math.Abs(p2.X - p1.X)
            : Math.Abs(p2.Y - p1.Y);

        string label = FormatDistance(rawDist);

        Point dimMid = isHorizontal
            ? new Point((p1.X + p2.X) / 2.0, Math.Max(p1.Y, p2.Y) + dimLineOffset)
            : new Point(Math.Max(p1.X, p2.X) + dimLineOffset, (p1.Y + p2.Y) / 2.0);

        var rec = CreateBase(MarkupType.Dimension, p1, p2, dimMid, label);
        rec.Metadata.Subject = isHorizontal ? "Linear-H" : "Linear-V";
        return rec;
    }

    /// <summary>
    /// Aligned dimension measured along the true line between two arbitrary points.
    /// </summary>
    /// <param name="p1">First point</param>
    /// <param name="p2">Second point</param>
    /// <param name="offsetPerp">
    /// Perpendicular offset distance from the P1–P2 line to place the dimension line.
    /// </param>
    public MarkupRecord CreateAligned(Point p1, Point p2, double offsetPerp)
    {
        double rawDist = Distance(p1, p2);
        string label = FormatDistance(rawDist);

        // Midpoint of the dimension line (offset perpendicular to P1-P2)
        double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
        double len = Math.Max(rawDist, 1e-6);
        var mid = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
        var dimMid = new Point(mid.X + offsetPerp * (-dy / len),
                               mid.Y + offsetPerp * (dx / len));

        var rec = CreateBase(MarkupType.Dimension, p1, p2, dimMid, label);
        rec.Metadata.Subject = "Aligned";
        return rec;
    }

    /// <summary>
    /// Angular dimension between two rays sharing a common vertex.
    /// </summary>
    /// <param name="vertex">Angle vertex</param>
    /// <param name="ray1End">Point on first ray</param>
    /// <param name="ray2End">Point on second ray</param>
    /// <param name="arcRadius">Radius of the dimension arc in document units</param>
    public MarkupRecord CreateAngular(Point vertex, Point ray1End, Point ray2End, double arcRadius)
    {
        double angle1 = Math.Atan2(ray1End.Y - vertex.Y, ray1End.X - vertex.X);
        double angle2 = Math.Atan2(ray2End.Y - vertex.Y, ray2End.X - vertex.X);

        double sweepRad = angle2 - angle1;
        // Normalize to [-PI, PI]
        while (sweepRad >  Math.PI) sweepRad -= 2 * Math.PI;
        while (sweepRad < -Math.PI) sweepRad += 2 * Math.PI;

        double angleDeg = Math.Abs(sweepRad) * (180.0 / Math.PI);
        string label = $"{angleDeg.ToString(NumberFormat)}°";

        // Text anchor at arc mid-angle
        double midAngle = angle1 + sweepRad / 2.0;
        var textAnchor = new Point(
            vertex.X + (arcRadius + TextHeight) * Math.Cos(midAngle),
            vertex.Y + (arcRadius + TextHeight) * Math.Sin(midAngle));

        var rec = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = [vertex, ray1End, ray2End, textAnchor],
            Radius = arcRadius,
            ArcStartDeg = angle1 * (180.0 / Math.PI),
            ArcSweepDeg = sweepRad * (180.0 / Math.PI),
            TextContent = label,
            Appearance = CloneAppearance(),
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        rec.UpdateBoundingRect();
        return rec;
    }

    /// <summary>
    /// Radial dimension from the center of a circle to a point on its circumference.
    /// </summary>
    /// <param name="center">Circle center</param>
    /// <param name="radiusPoint">Point on circle edge (determines direction of callout)</param>
    public MarkupRecord CreateRadial(Point center, Point radiusPoint)
    {
        double rawRadius = Distance(center, radiusPoint);
        string label = "R" + FormatDistance(rawRadius);

        // Leader extends beyond the circle edge
        double angle = Math.Atan2(radiusPoint.Y - center.Y, radiusPoint.X - center.X);
        var textAnchor = new Point(
            radiusPoint.X + TextHeight * Math.Cos(angle),
            radiusPoint.Y + TextHeight * Math.Sin(angle));

        var rec = CreateBase(MarkupType.Dimension, center, radiusPoint, textAnchor, label);
        rec.Radius = rawRadius;
        rec.Metadata.Subject = "Radial";
        return rec;
    }

    /// <summary>
    /// Diameter dimension (full chord across circle center).
    /// </summary>
    /// <param name="center">Circle center</param>
    /// <param name="radiusPoint">One point on the circumference</param>
    public MarkupRecord CreateDiameter(Point center, Point radiusPoint)
    {
        double rawRadius = Distance(center, radiusPoint);
        double rawDiam = rawRadius * 2.0;
        string label = "Ø" + FormatDistance(rawDiam);

        double angle = Math.Atan2(radiusPoint.Y - center.Y, radiusPoint.X - center.X);
        var farPoint = new Point(
            center.X - rawRadius * Math.Cos(angle),
            center.Y - rawRadius * Math.Sin(angle));

        var textAnchor = new Point(
            radiusPoint.X + TextHeight * Math.Cos(angle),
            radiusPoint.Y + TextHeight * Math.Sin(angle));

        var rec = CreateBase(MarkupType.Dimension, farPoint, radiusPoint, textAnchor, label);
        rec.Radius = rawRadius;
        rec.Metadata.Subject = "Diameter";
        return rec;
    }

    /// <summary>
    /// Arc-length dimension for an arc defined by center, radius, start angle, and sweep.
    /// </summary>
    /// <param name="center">Arc center</param>
    /// <param name="radius">Arc radius in document units</param>
    /// <param name="startDeg">Start angle in degrees</param>
    /// <param name="sweepDeg">Sweep angle in degrees (positive = CCW)</param>
    public MarkupRecord CreateArcLength(Point center, double radius, double startDeg, double sweepDeg)
    {
        double arcLen = Math.Abs(sweepDeg * Math.PI / 180.0) * radius;
        string label = FormatDistance(arcLen);

        double startRad = startDeg * Math.PI / 180.0;
        double endRad   = (startDeg + sweepDeg) * Math.PI / 180.0;

        var p1 = new Point(center.X + radius * Math.Cos(startRad),
                           center.Y + radius * Math.Sin(startRad));
        var p2 = new Point(center.X + radius * Math.Cos(endRad),
                           center.Y + radius * Math.Sin(endRad));

        double midRad = startRad + (sweepDeg / 2.0) * Math.PI / 180.0;
        var textAnchor = new Point(
            center.X + (radius + TextHeight) * Math.Cos(midRad),
            center.Y + (radius + TextHeight) * Math.Sin(midRad));

        var rec = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = [p1, p2, textAnchor],
            Radius = radius,
            ArcStartDeg = startDeg,
            ArcSweepDeg = sweepDeg,
            TextContent = label,
            Appearance = CloneAppearance(),
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };
        rec.UpdateBoundingRect();
        return rec;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private MarkupRecord CreateBase(MarkupType type, Point p1, Point p2, Point textAnchor, string label)
    {
        var rec = new MarkupRecord
        {
            Type = type,
            Vertices = [p1, p2, textAnchor],
            TextContent = label,
            Appearance = CloneAppearance(),
            Metadata = new MarkupMetadata()
        };
        rec.UpdateBoundingRect();
        return rec;
    }

    private string FormatDistance(double rawDist)
    {
        double scaled = rawDist * ScaleFactor;
        return scaled.ToString(NumberFormat) + UnitSuffix;
    }

    private MarkupAppearance CloneAppearance() => new()
    {
        StrokeColor = DefaultAppearance.StrokeColor,
        StrokeWidth = DefaultAppearance.StrokeWidth,
        FillColor   = DefaultAppearance.FillColor,
        FontFamily  = DefaultAppearance.FontFamily,
        FontSize    = DefaultAppearance.FontSize,
        Opacity     = DefaultAppearance.Opacity
    };

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
