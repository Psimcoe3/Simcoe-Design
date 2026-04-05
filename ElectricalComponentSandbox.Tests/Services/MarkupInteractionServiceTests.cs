using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class MarkupInteractionServiceTests
{
    private readonly MarkupInteractionService _sut = new();

    [Fact]
    public void GetSelectionSet_GroupedMarkup_ReturnsWholeGroup()
    {
        var groupId = "group-1";
        var first = CreateMarkup(new Rect(0, 0, 10, 10), groupId);
        var second = CreateMarkup(new Rect(20, 0, 10, 10), groupId);
        var third = CreateMarkup(new Rect(40, 0, 10, 10), "group-2");

        var selection = _sut.GetSelectionSet(first, new[] { first, second, third });

        Assert.Equal(2, selection.Count);
        Assert.Contains(first, selection);
        Assert.Contains(second, selection);
        Assert.DoesNotContain(third, selection);
    }

    [Fact]
    public void Translate_TextMarkup_ShiftsVertexAndBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "NOTE",
            BoundingRect = new Rect(10, 20, 40, 12)
        };
        markup.Vertices.Add(new Point(10, 32));

        _sut.Translate(markup, new Vector(15, -5));

        Assert.Equal(new Point(25, 27), markup.Vertices[0]);
        Assert.Equal(new Rect(25, 15, 40, 12), markup.BoundingRect);
    }

    [Fact]
    public void GetAggregateBounds_UnionsAllMarkupBounds()
    {
        var first = CreateMarkup(new Rect(0, 0, 10, 10), "group-1");
        var second = CreateMarkup(new Rect(20, 5, 10, 15), "group-1");

        var bounds = _sut.GetAggregateBounds(new[] { first, second });

        Assert.Equal(new Rect(0, 0, 30, 20), bounds);
    }

    [Fact]
    public void BuildResizedBounds_TopLeftHandle_KeepsOppositeCornerFixed()
    {
        var resized = _sut.BuildResizedBounds(
            new Rect(10, 20, 30, 40),
            new Point(4, 8),
            MarkupResizeHandle.TopLeft,
            minimumSize: 6);

        Assert.Equal(new Rect(4, 8, 36, 52), resized);
    }

    [Fact]
    public void HitTestResizeHandle_PrefersClosestEdgeHandleWithinTolerance()
    {
        var handle = _sut.HitTestResizeHandle(
            new Point(4, 0),
            new Rect(0, 0, 8, 10),
            tolerance: 5);

        Assert.Equal(MarkupResizeHandle.Top, handle);
    }

    [Fact]
    public void GetResizeHandlePoint_ReturnsExpectedPointsForAllHandles()
    {
        var bounds = new Rect(10, 20, 30, 40);

        Assert.Equal(new Point(10, 20), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.TopLeft));
        Assert.Equal(new Point(25, 20), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.Top));
        Assert.Equal(new Point(40, 20), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.TopRight));
        Assert.Equal(new Point(40, 40), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.Right));
        Assert.Equal(new Point(40, 60), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.BottomRight));
        Assert.Equal(new Point(25, 60), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.Bottom));
        Assert.Equal(new Point(10, 60), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.BottomLeft));
        Assert.Equal(new Point(10, 40), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.Left));
        Assert.Equal(new Point(25, 40), _sut.GetResizeHandlePoint(bounds, MarkupResizeHandle.None));
    }

    [Fact]
    public void BuildResizedBounds_TopHandle_ResizesHeightOnly()
    {
        var resized = _sut.BuildResizedBounds(
            new Rect(10, 20, 30, 40),
            new Point(25, 8),
            MarkupResizeHandle.Top,
            minimumSize: 6);

        Assert.Equal(new Rect(10, 8, 30, 52), resized);
    }

    [Fact]
    public void BuildResizedBounds_LeftHandle_ClampsWidthAndKeepsVerticalBounds()
    {
        var resized = _sut.BuildResizedBounds(
            new Rect(10, 20, 30, 40),
            new Point(38, 35),
            MarkupResizeHandle.Left,
            minimumSize: 6);

        Assert.Equal(new Rect(34, 20, 6, 40), resized);
    }

    [Fact]
    public void BuildResizedBounds_BottomHandle_ResizesHeightOnly()
    {
        var resized = _sut.BuildResizedBounds(
            new Rect(10, 20, 30, 40),
            new Point(25, 70),
            MarkupResizeHandle.Bottom,
            minimumSize: 6);

        Assert.Equal(new Rect(10, 20, 30, 50), resized);
    }

    [Fact]
    public void BuildResizedBounds_RightHandle_ClampsWidthAndKeepsVerticalBounds()
    {
        var resized = _sut.BuildResizedBounds(
            new Rect(10, 20, 30, 40),
            new Point(12, 35),
            MarkupResizeHandle.Right,
            minimumSize: 6);

        Assert.Equal(new Rect(10, 20, 6, 40), resized);
    }

    [Fact]
    public void BuildResizedBounds_BottomRightHandle_ClampsToMinimumSize()
    {
        var resized = _sut.BuildResizedBounds(
            new Rect(10, 20, 30, 40),
            new Point(12, 22),
            MarkupResizeHandle.BottomRight,
            minimumSize: 6);

        Assert.Equal(new Rect(10, 20, 6, 6), resized);
    }

    [Fact]
    public void Resize_GroupedTableMarkup_ScalesVerticesBoundsAndFont()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "HEADER",
            BoundingRect = new Rect(10, 10, 20, 8)
        };
        markup.Vertices.Add(new Point(12, 16));
        markup.Appearance.FontSize = 10;
        markup.Appearance.StrokeWidth = 1.2;

        var snapshot = _sut.Capture(markup);
        _sut.Resize(markup, snapshot, new Rect(0, 0, 100, 100), new Rect(0, 0, 200, 150));

        Assert.Equal(new Point(24, 24), markup.Vertices[0]);
        Assert.Equal(new Rect(20, 15, 40, 12), markup.BoundingRect);
        Assert.Equal(15, markup.Appearance.FontSize);
        Assert.Equal(1.8, markup.Appearance.StrokeWidth, 3);
    }

    [Fact]
    public void Resize_EmptyOriginalBounds_RestoresSnapshot()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "NOTE",
            BoundingRect = new Rect(10, 20, 40, 12)
        };
        markup.Vertices.Add(new Point(10, 32));
        markup.Appearance.FontSize = 10;
        markup.Appearance.StrokeWidth = 1.2;

        var snapshot = _sut.Capture(markup);

        markup.Vertices[0] = new Point(99, 99);
        markup.BoundingRect = new Rect(90, 90, 10, 10);
        markup.Appearance.FontSize = 24;
        markup.Appearance.StrokeWidth = 4.5;

        _sut.Resize(markup, snapshot, Rect.Empty, new Rect(0, 0, 200, 200));

        Assert.Equal(new Point(10, 32), markup.Vertices[0]);
        Assert.Equal(new Rect(10, 20, 40, 12), markup.BoundingRect);
        Assert.Equal(10, markup.Appearance.FontSize);
        Assert.Equal(1.2, markup.Appearance.StrokeWidth, 3);
    }

    [Fact]
    public void Resize_PolylineWithoutBoundingRect_RecomputesBoundsFromScaledVertices()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 10), new Point(20, 5) }
        };
        markup.Appearance.FontSize = 10;
        markup.Appearance.StrokeWidth = 1.2;

        var snapshot = _sut.Capture(markup);
        _sut.Resize(markup, snapshot, new Rect(0, 0, 20, 10), new Rect(10, 20, 40, 30));

        Assert.Equal(new Point(10, 20), markup.Vertices[0]);
        Assert.Equal(new Point(30, 50), markup.Vertices[1]);
        Assert.Equal(new Point(50, 35), markup.Vertices[2]);
        Assert.Equal(new Rect(10, 20, 40, 30), markup.BoundingRect);
        Assert.Equal(20, markup.Appearance.FontSize);
        Assert.Equal(2.4, markup.Appearance.StrokeWidth, 3);
    }

    [Fact]
    public void CanResize_GroupContainingRevisionCloud_ReturnsFalse()
    {
        var resizable = CreateMarkup(new Rect(0, 0, 10, 10), "group-1");
        var nonResizable = new MarkupRecord
        {
            Type = MarkupType.RevisionCloud,
            BoundingRect = new Rect(10, 10, 20, 20)
        };

        Assert.False(_sut.CanResize(new[] { resizable, nonResizable }));
    }

    [Fact]
    public void CanResize_GroupOfResizableMarkups_ReturnsTrue()
    {
        var rectangle = CreateMarkup(new Rect(0, 0, 10, 10), "group-1");
        var polygon = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = { new Point(20, 20), new Point(40, 20), new Point(30, 35) }
        };
        polygon.UpdateBoundingRect();

        Assert.True(_sut.CanResize(new[] { rectangle, polygon }));
    }

    private static MarkupRecord CreateMarkup(Rect rect, string groupId)
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = rect
        };
        markup.Vertices.Add(rect.TopLeft);
        markup.Vertices.Add(rect.BottomRight);
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = groupId;
        return markup;
    }
}