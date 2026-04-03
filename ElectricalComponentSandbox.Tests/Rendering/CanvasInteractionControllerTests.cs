using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Rendering;

public class CanvasInteractionControllerTests
{
    [Fact]
    public void OnMouseMove_SnapsToEndpoint_AndRaisesCursorMoved()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService { SnapRadius = 10 };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());
        var cursorMovedCount = 0;

        controller.CursorMoved += () => cursorMovedCount++;

        controller.OnMouseMove(
            new Point(52, 52),
            new[] { new Point(50, 50) },
            Array.Empty<(Point A, Point B)>());

        Assert.Equal(1, cursorMovedCount);
        Assert.NotNull(controller.LastSnap);
        Assert.True(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.Endpoint, controller.LastSnap.Type);
        Assert.Equal(new Point(50, 50), controller.CursorDocPoint);
    }

    [Fact]
    public void OnMouseMove_SnapsToCircleCenter_AndRaisesCursorMoved()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService { SnapRadius = 10 };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());
        var cursorMovedCount = 0;

        controller.CursorMoved += () => cursorMovedCount++;

        controller.OnMouseMove(
            new Point(42, 39),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[] { new SnapCircle(new Point(40, 40), 10.0) });

        Assert.Equal(1, cursorMovedCount);
        Assert.NotNull(controller.LastSnap);
        Assert.True(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.Center, controller.LastSnap.Type);
        Assert.Equal(new Point(40, 40), controller.CursorDocPoint);
    }

    [Fact]
    public void OnMouseMove_NearVisibleArcCenter_SnapsToCenter()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService { SnapRadius = 10 };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());

        controller.OnMouseMove(
            new Point(42, 39),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) });

        Assert.NotNull(controller.LastSnap);
        Assert.True(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.Center, controller.LastSnap.Type);
        Assert.Equal(new Point(40, 40), controller.CursorDocPoint);
    }

    [Fact]
    public void OnMouseMove_NearVisibleArcQuadrant_SnapsToQuadrant()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService { SnapRadius = 10, SnapToCenter = false };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());

        controller.OnMouseMove(
            new Point(41, 49),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[] { new SnapCircle(new Point(40, 40), 10.0, 45.0, 180.0) });

        Assert.NotNull(controller.LastSnap);
        Assert.True(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.Quadrant, controller.LastSnap.Type);
        Assert.Equal(new Point(40, 50), controller.CursorDocPoint);
    }

    [Fact]
    public void OnMouseMove_WithArcQuadrantOutsideSweep_DoesNotSnap()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService { SnapRadius = 10, SnapToCenter = false };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());

        controller.OnMouseMove(
            new Point(29, 39),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) });

        Assert.NotNull(controller.LastSnap);
        Assert.False(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.None, controller.LastSnap.Type);
        Assert.Equal(new Point(29, 39), controller.CursorDocPoint);
    }

    [Fact]
    public void OnMouseMove_NearVisibleArcCurve_SnapsToNearest()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService
        {
            SnapRadius = 10,
            SnapToEndpoints = false,
            SnapToMidpoints = false,
            SnapToCenter = false,
            SnapToQuadrant = false,
            SnapToNearest = true
        };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());

        controller.OnMouseMove(
            new Point(48, 48),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) });

        Assert.NotNull(controller.LastSnap);
        Assert.True(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.Nearest, controller.LastSnap.Type);
        Assert.Equal(47.1, controller.CursorDocPoint.X, 1);
        Assert.Equal(47.1, controller.CursorDocPoint.Y, 1);
    }

    [Fact]
    public void OnMouseMove_NearVisibleArcMidpoint_SnapsToMidpoint()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService { SnapRadius = 10 };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());

        controller.OnMouseMove(
            new Point(48, 48),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) });

        Assert.NotNull(controller.LastSnap);
        Assert.True(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.Midpoint, controller.LastSnap.Type);
        Assert.Equal(47.1, controller.CursorDocPoint.X, 1);
        Assert.Equal(47.1, controller.CursorDocPoint.Y, 1);
    }

    [Fact]
    public void OnMouseMove_WithVisibleArcArcIntersection_SnapsToIntersection()
    {
        var drawCtx = new DrawingContext2D();
        var snapService = new SnapService { SnapRadius = 10 };
        var controller = new CanvasInteractionController(drawCtx, snapService, new ShadowGeometryTree());

        controller.OnMouseMove(
            new Point(45, 49),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[]
            {
                new SnapCircle(new Point(40, 40), 10.0, 0.0, 180.0),
                new SnapCircle(new Point(50, 40), 10.0, 0.0, 180.0)
            });

        Assert.NotNull(controller.LastSnap);
        Assert.True(controller.LastSnap!.Snapped);
        Assert.Equal(SnapService.SnapType.Intersection, controller.LastSnap.Type);
        Assert.Equal(45.0, controller.CursorDocPoint.X, 1);
        Assert.Equal(48.7, controller.CursorDocPoint.Y, 1);
    }

    [Fact]
    public void OnMouseWheel_ZoomsAboutCursor_AndRaisesCursorMoved()
    {
        var drawCtx = new DrawingContext2D
        {
            Zoom = 1.0,
            PanX = 0,
            PanY = 0
        };
        drawCtx.SyncCoordTransform();

        var controller = new CanvasInteractionController(drawCtx, new SnapService(), new ShadowGeometryTree());
        var cursorMovedCount = 0;

        controller.CursorMoved += () => cursorMovedCount++;

        controller.OnMouseWheel(new Point(100, 100), 120);

        Assert.Equal(1, cursorMovedCount);
        Assert.Equal(1.1, drawCtx.Zoom, 3);
        Assert.Equal(-10.0, drawCtx.PanX, 3);
        Assert.Equal(-10.0, drawCtx.PanY, 3);
    }

    [Fact]
    public void CrossingSelection_CompletesWithIntersectingIds_AndCrossingFlag()
    {
        var shadowTree = new ShadowGeometryTree();
        var component = new ConduitComponent
        {
            Id = "conduit-1",
            Position = new Point3D(5, 0, 5),
            Parameters = new ComponentParameters
            {
                Width = 2,
                Depth = 2
            }
        };
        shadowTree.AddOrUpdate(component);

        var controller = new CanvasInteractionController(new DrawingContext2D(), new SnapService(), shadowTree);
        IReadOnlyList<string>? selectionIds = null;
        bool? isCrossing = null;

        controller.SelectionRectCompleted += (ids, crossing) =>
        {
            selectionIds = ids;
            isCrossing = crossing;
        };

        controller.OnMouseDown(new Point(10, 10), MouseButton.Left, ModifierKeys.None);
        controller.OnMouseMove(new Point(0, 0), Array.Empty<Point>(), Array.Empty<(Point A, Point B)>());
        controller.OnMouseUp(new Point(0, 0), MouseButton.Left);

        Assert.NotNull(selectionIds);
        Assert.True(isCrossing);
        Assert.Contains("conduit-1", selectionIds!);
    }

    [Fact]
    public void KeyToggles_KeepOrthoAndPolarMutuallyExclusive()
    {
        var controller = new CanvasInteractionController(new DrawingContext2D(), new SnapService(), new ShadowGeometryTree());

        controller.OnKeyDown(Key.F8, ModifierKeys.None);

        Assert.True(controller.IsOrthoActive);
        Assert.False(controller.IsPolarActive);

        controller.OnKeyDown(Key.F10, ModifierKeys.None);

        Assert.False(controller.IsOrthoActive);
        Assert.True(controller.IsPolarActive);
    }

    [Fact]
    public void F3_TogglesMasterSnapEnable()
    {
        var snapService = new SnapService { IsEnabled = true };
        var controller = new CanvasInteractionController(new DrawingContext2D(), snapService, new ShadowGeometryTree());

        controller.OnKeyDown(Key.F3, ModifierKeys.None);

        Assert.False(snapService.IsEnabled);
        Assert.Null(controller.LastSnap);
    }

    [Fact]
    public void DrawOverlays_DrawsSnapGlyphForCurrentSnap()
    {
        var controller = new CanvasInteractionController(new DrawingContext2D(), new SnapService { SnapRadius = 10 }, new ShadowGeometryTree());
        var renderer = new RecordingRenderer();

        controller.OnMouseMove(new Point(52, 52), new[] { new Point(50, 50) }, Array.Empty<(Point A, Point B)>());
        controller.DrawOverlays(renderer);

        Assert.Equal(new Point(50, 50), renderer.LastSnapGlyphPoint);
        Assert.Equal(SnapGlyphType.Endpoint, renderer.LastSnapGlyphType);
    }

    [Theory]
    [InlineData(SnapService.SnapType.Intersection, SnapGlyphType.Intersection)]
    [InlineData(SnapService.SnapType.Center, SnapGlyphType.Center)]
    [InlineData(SnapService.SnapType.Perpendicular, SnapGlyphType.Perpendicular)]
    [InlineData(SnapService.SnapType.Quadrant, SnapGlyphType.Quadrant)]
    [InlineData(SnapService.SnapType.Tangent, SnapGlyphType.Tangent)]
    public void MapSnapGlyphTypeForTesting_UsesExpectedGlyph(SnapService.SnapType snapType, SnapGlyphType glyphType)
    {
        var mappedGlyphType = CanvasInteractionController.MapSnapGlyphTypeForTesting(snapType);

        Assert.Equal(glyphType, mappedGlyphType);
    }

    [Fact]
    public void DrawOverlays_WithIntersectionSnap_DrawsIntersectionGlyph()
    {
        var controller = new CanvasInteractionController(new DrawingContext2D(), new SnapService { SnapRadius = 10 }, new ShadowGeometryTree());
        var renderer = new RecordingRenderer();

        controller.OnMouseMove(
            new Point(45, 49),
            Array.Empty<Point>(),
            Array.Empty<(Point A, Point B)>(),
            new[]
            {
                new SnapCircle(new Point(40, 40), 10.0, 0.0, 180.0),
                new SnapCircle(new Point(50, 40), 10.0, 0.0, 180.0)
            });
        controller.DrawOverlays(renderer);

        Assert.Equal(45.0, renderer.LastSnapGlyphPoint.X, 1);
        Assert.Equal(48.7, renderer.LastSnapGlyphPoint.Y, 1);
        Assert.Equal(SnapGlyphType.Intersection, renderer.LastSnapGlyphType);
    }

    [Fact]
    public void DrawOverlays_DrawsSelectionRectWhileRubberBanding()
    {
        var shadowTree = new ShadowGeometryTree();
        shadowTree.AddOrUpdateNode("component-1", ShadowGeometryTree.ShadowNodeKind.Component, new Rect(4, 4, 4, 4));
        var controller = new CanvasInteractionController(new DrawingContext2D(), new SnapService(), shadowTree);
        var renderer = new RecordingRenderer();

        controller.OnMouseDown(new Point(10, 10), MouseButton.Left, ModifierKeys.None);
        controller.OnMouseMove(new Point(0, 0), Array.Empty<Point>(), Array.Empty<(Point A, Point B)>());
        controller.DrawOverlays(renderer);

        Assert.True(renderer.SelectionRectDrawn);
        Assert.True(renderer.SelectionRectCrossing);
        Assert.Equal(new Rect(new Point(10, 10), new Point(0, 0)), renderer.LastSelectionRect);
    }

    [Fact]
    public void GripPointNode_IsHitTestedBeforeMarkup()
    {
        var shadowTree = new ShadowGeometryTree();
        shadowTree.AddOrUpdateNode("markup-1", ShadowGeometryTree.ShadowNodeKind.Markup,
            new Rect(0, 0, 100, 100));
        shadowTree.AddGripPoint("markup-1", 2, new Point(50, 50));

        var hit = shadowTree.HitTest(new Point(50, 50), 5);

        Assert.NotNull(hit);
        Assert.Equal(ShadowGeometryTree.ShadowNodeKind.GripPoint, hit!.Kind);
        Assert.Equal("markup-1", hit.Id);
        Assert.Equal(2, hit.GripIndex);
    }

    [Fact]
    public void GripPointHit_StartsGripDrag_AndFiresGripDragCompleted()
    {
        var shadowTree = new ShadowGeometryTree();
        shadowTree.AddGripPoint("markup-1", 3, new Point(10, 10));

        var controller = new CanvasInteractionController(new DrawingContext2D(), new SnapService(), shadowTree);
        string? firedId = null;
        int firedIndex = -1;
        Vector firedDelta = default;

        controller.GripDragCompleted += (id, idx, delta) =>
        {
            firedId = id;
            firedIndex = idx;
            firedDelta = delta;
        };

        controller.OnMouseDown(new Point(10, 10), MouseButton.Left, ModifierKeys.None);
        controller.OnMouseUp(new Point(15, 20), MouseButton.Left);

        Assert.Equal("markup-1", firedId);
        Assert.Equal(3, firedIndex);
        Assert.Equal(5.0, firedDelta.X, 1);
        Assert.Equal(10.0, firedDelta.Y, 1);
    }

    [Fact]
    public void MultipleGripPoints_CoexistInShadowTree()
    {
        var shadowTree = new ShadowGeometryTree();
        shadowTree.AddGripPoint("markup-1", 0, new Point(0, 0));
        shadowTree.AddGripPoint("markup-1", 1, new Point(100, 0));
        shadowTree.AddGripPoint("markup-1", 2, new Point(100, 100));

        var hit0 = shadowTree.HitTest(new Point(0, 0), 5);
        var hit1 = shadowTree.HitTest(new Point(100, 0), 5);
        var hit2 = shadowTree.HitTest(new Point(100, 100), 5);

        Assert.NotNull(hit0);
        Assert.Equal(0, hit0!.GripIndex);
        Assert.NotNull(hit1);
        Assert.Equal(1, hit1!.GripIndex);
        Assert.NotNull(hit2);
        Assert.Equal(2, hit2!.GripIndex);
    }

    private sealed class RecordingRenderer : ICanvas2DRenderer
    {
        public Point LastSnapGlyphPoint { get; private set; }
        public SnapGlyphType? LastSnapGlyphType { get; private set; }
        public bool SelectionRectDrawn { get; private set; }
        public bool SelectionRectCrossing { get; private set; }
        public Rect LastSelectionRect { get; private set; }

        public Rect ViewportDocRect => Rect.Empty;
        public double Zoom => 1.0;

        public void Clear(string backgroundColor = "#FF1E1E1E") { }
        public void RequestRedraw() { }
        public void InvalidateRegion(Rect docRect) { }
        public void PushTransform(double translateX, double translateY, double rotateDeg = 0, double scale = 1.0) { }
        public void PopTransform() { }
        public void DrawLine(Point start, Point end, RenderStyle style) { }
        public void DrawPolyline(IReadOnlyList<Point> points, RenderStyle style) { }
        public void DrawPolygon(IReadOnlyList<Point> points, RenderStyle style) { }
        public void DrawRect(Rect rect, RenderStyle style) { }
        public void DrawEllipse(Point center, double radiusX, double radiusY, RenderStyle style) { }
        public void DrawArc(Point center, double radiusX, double radiusY, double startAngleDeg, double sweepAngleDeg, RenderStyle style) { }
        public void DrawBezier(IReadOnlyList<Point> controlPoints, RenderStyle style) { }
        public void DrawHatch(IReadOnlyList<Point> boundary, HatchPattern pattern, RenderStyle style) { }
        public void DrawText(Point anchor, string text, RenderStyle style, TextAlign align = TextAlign.Left) { }
        public void DrawTextBox(Point anchor, string text, RenderStyle textStyle, string boxFill = "#CCFFFFFF", double padding = 3.0) { }
        public void DrawDimension(Point p1, Point p2, double offset, string valueText, DimensionStyle dimStyle) { }
        public void DrawSnapGlyph(Point docPos, SnapGlyphType glyphType)
        {
            LastSnapGlyphPoint = docPos;
            LastSnapGlyphType = glyphType;
        }

        public void DrawTrackingLine(Point docOrigin, double angleDeg) { }

        public void DrawSelectionRect(Rect docRect, bool crossing)
        {
            SelectionRectDrawn = true;
            SelectionRectCrossing = crossing;
            LastSelectionRect = docRect;
        }

        public void DrawGrip(Point docPos, bool hot = false) { }
        public void DrawRevisionCloud(IReadOnlyList<Point> points, RenderStyle style, double arcRadius = 0.5) { }
        public void DrawLeader(IReadOnlyList<Point> points, string? calloutText, RenderStyle style) { }
    }
}