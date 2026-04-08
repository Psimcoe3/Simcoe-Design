using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly record struct PlanWorldBounds(double MinX, double MaxX, double MinZ, double MaxZ);
    private readonly record struct UnderlayCanvasFrame(PdfUnderlay Underlay, double ScaledWidth, double ScaledHeight, Point[] Corners);
    private readonly record struct DimensionEntry(string Label, double FeetValue);
    private readonly record struct AxisDimensionSpan(char Axis, double ValueFeet, double MinCoordinate, double MaxCoordinate);
    private readonly record struct DimensionEdgePlacement(double EdgeX, double EdgeY, double EdgeZ, double OutwardX, double OutwardY, double OutwardZ);
    private readonly record struct AxisDimensionGuide(char Axis, double ValueFeet, Point3D EdgeStart, Point3D EdgeEnd, Vector3D Outward);
    private readonly record struct SemanticEdge(Point3D StartLocal, Point3D EndLocal);
    private readonly record struct SemanticFace(char Axis, double AxisValue, double MinA, double MaxA, double MinB, double MaxB);
    private sealed class CustomDimensionAnchor
    {
        public string? ComponentId { get; init; }
        public Point3D LocalPoint { get; init; }
        public Point3D WorldPoint { get; init; }
    }
    private sealed class CustomDimensionAnnotation
    {
        public CustomDimensionAnchor Start { get; init; } = new();
        public CustomDimensionAnchor End { get; init; } = new();
        public char Axis { get; init; }
    }
    private sealed class DimensionAxisOffsets
    {
        public double X;
        public double Y;
        public double Z;
    }
    private sealed class ComponentSemanticReferences
    {
        public Rect3D LocalBounds { get; init; }
        public Point3D CenterLocal { get; init; }
        public List<Point3D> PointLocals { get; } = new();
        public List<SemanticEdge> EdgeLocals { get; } = new();
        public List<SemanticFace> FaceLocals { get; } = new();
    }

    private sealed class InteropReviewGroupItem
    {
        public string SourceGroupKey { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string SourceSystem { get; init; } = string.Empty;
        public int Count { get; init; }
        public int ReviewCandidateCount { get; init; }
        public int ReviewedCount { get; init; }
        public int NeedsChangesCount { get; init; }
        public string SecondaryText { get; init; } = string.Empty;
        public string BreakdownText { get; init; } = string.Empty;
    }

    private enum MobilePane
    {
        Canvas,
        Library,
        Properties
    }

    private enum DimensionDisplayMode
    {
        FeetInches,
        DecimalFeet
    }

    private enum CustomDimensionSnapMode
    {
        Auto,
        Edge,
        Point,
        Face,
        Center,
        Intersection
    }

    private enum MobileTheme
    {
        IOS,
        AndroidMaterial
    }

    private abstract record SketchPrimitive(string Id);
    private sealed record SketchLinePrimitive(string Id, List<Point> Points) : SketchPrimitive(Id);
    private sealed record SketchRectanglePrimitive(string Id, Point Start, Point End) : SketchPrimitive(Id);
}
