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
    public void GetSelectionSet_UngroupedMarkup_ReturnsOnlySelectedMarkup()
    {
        var selected = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(0, 0, 10, 10)
        };
        selected.Vertices.Add(selected.BoundingRect.TopLeft);
        selected.Vertices.Add(selected.BoundingRect.BottomRight);

        var other = CreateMarkup(new Rect(20, 0, 10, 10), "group-1");

        var selection = _sut.GetSelectionSet(selected, new[] { selected, other });

        Assert.Single(selection);
        Assert.Same(selected, selection[0]);
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
    public void GetAggregateBounds_EmptyMarkupSet_ReturnsEmpty()
    {
        var bounds = _sut.GetAggregateBounds(Array.Empty<MarkupRecord>());

        Assert.Equal(Rect.Empty, bounds);
    }

    [Fact]
    public void GetAggregateBounds_SkipsMarkupsWithoutUsableBounds()
    {
        var empty = new MarkupRecord
        {
            Type = MarkupType.Text,
            BoundingRect = Rect.Empty
        };
        var rectangle = CreateMarkup(new Rect(20, 5, 10, 15), "group-1");

        var bounds = _sut.GetAggregateBounds(new[] { empty, rectangle });

        Assert.Equal(new Rect(20, 5, 10, 15), bounds);
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

    [Theory]
    [InlineData(MarkupType.Rectangle, true)]
    [InlineData(MarkupType.Text, true)]
    [InlineData(MarkupType.Stamp, true)]
    [InlineData(MarkupType.Hyperlink, true)]
    [InlineData(MarkupType.Box, true)]
    [InlineData(MarkupType.Panel, true)]
    [InlineData(MarkupType.Polyline, true)]
    [InlineData(MarkupType.Polygon, true)]
    [InlineData(MarkupType.Circle, false)]
    [InlineData(MarkupType.Arc, false)]
    [InlineData(MarkupType.RevisionCloud, false)]
    [InlineData(MarkupType.LeaderNote, false)]
    [InlineData(MarkupType.Callout, false)]
    [InlineData(MarkupType.Dimension, false)]
    [InlineData(MarkupType.Measurement, false)]
    public void CanResize_SingleMarkupType_MatchesSupportedHandleResizeSet(MarkupType markupType, bool expected)
    {
        var markup = CreateMarkupOfType(markupType);

        var result = _sut.CanResize(new[] { markup });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanResize_EmptyMarkupSet_ReturnsFalse()
    {
        Assert.False(_sut.CanResize(Array.Empty<MarkupRecord>()));
    }

    [Fact]
    public void Apply_TextMarkup_RestoresGeometryAppearanceAndTimestamp()
    {
        var originalModifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "NOTE",
            BoundingRect = new Rect(10, 20, 40, 12),
            Radius = 2.5,
            ArcStartDeg = 12,
            ArcSweepDeg = -27,
            Metadata = new MarkupMetadata { ModifiedUtc = originalModifiedUtc }
        };
        markup.Vertices.Add(new Point(10, 32));
        markup.Appearance.StrokeColor = "#112233";
        markup.Appearance.StrokeWidth = 1.5;
        markup.Appearance.FillColor = "#445566";
        markup.Appearance.Opacity = 0.75;
        markup.Appearance.FontFamily = "Consolas";
        markup.Appearance.FontSize = 14;
        markup.Appearance.DashArray = "5,3";

        var snapshot = _sut.Capture(markup);

        markup.Vertices[0] = new Point(99, 88);
        markup.BoundingRect = new Rect(90, 80, 10, 5);
        markup.Radius = 7;
        markup.ArcStartDeg = 45;
        markup.ArcSweepDeg = 90;
        markup.Appearance.StrokeColor = "#ABCDEF";
        markup.Appearance.StrokeWidth = 9;
        markup.Appearance.FillColor = "#FEDCBA";
        markup.Appearance.Opacity = 0.2;
        markup.Appearance.FontFamily = "Courier New";
        markup.Appearance.FontSize = 30;
        markup.Appearance.DashArray = "1,1";
        markup.Metadata.ModifiedUtc = originalModifiedUtc.AddDays(1);

        _sut.Apply(markup, snapshot);

        Assert.Equal(new Point(10, 32), markup.Vertices[0]);
        Assert.Equal(new Rect(10, 20, 40, 12), markup.BoundingRect);
        Assert.Equal(2.5, markup.Radius, 6);
        Assert.Equal(12, markup.ArcStartDeg, 6);
        Assert.Equal(-27, markup.ArcSweepDeg, 6);
        Assert.Equal("#112233", markup.Appearance.StrokeColor);
        Assert.Equal(1.5, markup.Appearance.StrokeWidth, 6);
        Assert.Equal("#445566", markup.Appearance.FillColor);
        Assert.Equal(0.75, markup.Appearance.Opacity, 6);
        Assert.Equal("Consolas", markup.Appearance.FontFamily);
        Assert.Equal(14, markup.Appearance.FontSize, 6);
        Assert.Equal("5,3", markup.Appearance.DashArray);
        Assert.Equal(originalModifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void GetGroupId_WithoutAnnotationGroup_ReturnsNull()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(0, 0, 10, 10)
        };

        var groupId = _sut.GetGroupId(markup);

        Assert.Null(groupId);
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

    private static MarkupRecord CreateMarkupOfType(MarkupType markupType)
    {
        return markupType switch
        {
            MarkupType.Rectangle or MarkupType.Text or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel
                => new MarkupRecord
                {
                    Type = markupType,
                    BoundingRect = new Rect(0, 0, 10, 10),
                    Vertices = { new Point(0, 0), new Point(10, 10) }
                },
            MarkupType.Polyline => new MarkupRecord
            {
                Type = markupType,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10) }
            },
            MarkupType.Polygon => new MarkupRecord
            {
                Type = markupType,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 10) }
            },
            MarkupType.Circle or MarkupType.Arc => new MarkupRecord
            {
                Type = markupType,
                Radius = 5,
                Vertices = { new Point(5, 5) }
            },
            _ => new MarkupRecord
            {
                Type = markupType,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10) }
            }
        };
    }
}