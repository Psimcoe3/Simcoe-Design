using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MarkupInteractionServiceTests
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
    public void CanEditVertices_Polyline_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline
        };

        Assert.True(_sut.CanEditVertices(markup));
    }

    [Fact]
    public void HitTestVertexHandle_ReturnsMatchingIndex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.LeaderNote,
            Vertices = { new Point(10, 10), new Point(30, 40) }
        };

        var hitIndex = _sut.HitTestVertexHandle(new Point(31, 39), markup, tolerance: 2.0);

        Assert.Equal(1, hitIndex);
    }

    [Fact]
    public void MoveVertex_Polygon_UpdatesVertexAndBoundingRect()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 8) }
        };
        markup.UpdateBoundingRect();

        _sut.MoveVertex(markup, 2, new Point(12, 16));

        Assert.Equal(new Point(12, 16), markup.Vertices[2]);
        Assert.Equal(new Rect(0, 0, 12, 16), markup.BoundingRect);
    }

    [Fact]
    public void TryFindInsertionPoint_Polyline_ReturnsProjectedPointAndInsertIndex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(20, 0) }
        };

        var found = _sut.TryFindInsertionPoint(new Point(9, 2), markup, 3.0, out var insertIndex, out var projectedPoint);

        Assert.True(found);
        Assert.Equal(1, insertIndex);
        Assert.Equal(new Point(9, 0), projectedPoint);
    }

    [Fact]
    public void InsertVertex_Polygon_AddsVertexAndUpdatesBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10) }
        };
        markup.UpdateBoundingRect();

        var inserted = _sut.InsertVertex(markup, 1, new Point(5, -2));

        Assert.True(inserted);
        Assert.Equal(4, markup.Vertices.Count);
        Assert.Equal(new Point(5, -2), markup.Vertices[1]);
        Assert.Equal(new Rect(0, -2, 10, 12), markup.BoundingRect);
    }

    [Fact]
    public void DeleteVertex_Polyline_RespectsMinimumVertexCount()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
        };
        markup.UpdateBoundingRect();

        var deleted = _sut.DeleteVertex(markup, 1);
        var secondDelete = _sut.DeleteVertex(markup, 1);

        Assert.True(deleted);
        Assert.False(secondDelete);
        Assert.Equal(2, markup.Vertices.Count);
    }

    [Fact]
    public void CanEditRadius_Circle_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 8,
            Vertices = { new Point(10, 20) }
        };

        Assert.True(_sut.CanEditRadius(markup));
    }

    [Fact]
    public void CanEditArcAngles_Arc_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 8,
            Vertices = { new Point(10, 20) }
        };

        Assert.True(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void GetRadiusHandlePoint_Arc_UsesMidSweepAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 180,
            Vertices = { new Point(0, 0) }
        };

        var handle = _sut.GetRadiusHandlePoint(markup);

        Assert.Equal(0, handle.X, 6);
        Assert.Equal(10, handle.Y, 6);
    }

    [Fact]
    public void GetArcAngleHandlePoint_End_UsesStartPlusSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.End);

        Assert.Equal(-7.071067811865475, handle.X, 6);
        Assert.Equal(7.0710678118654755, handle.Y, 6);
    }

    [Fact]
    public void HitTestArcAngleHandle_ReturnsStartHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(5, 5) }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(15, 5), markup, tolerance: 1.5);

        Assert.Equal(MarkupArcAngleHandle.Start, hit);
    }

    [Fact]
    public void SetRadius_Circle_UpdatesRadiusAndBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 5,
            Vertices = { new Point(20, 20) }
        };
        markup.UpdateBoundingRect();

        _sut.SetRadius(markup, 12);

        Assert.Equal(12, markup.Radius);
        Assert.Equal(new Rect(8, 8, 24, 24), markup.BoundingRect);
    }

    [Fact]
    public void SetArcAngle_StartHandle_KeepsEndFixed()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.Start, 30);

        Assert.Equal(30, markup.ArcStartDeg, 6);
        Assert.Equal(60, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcAngle_EndHandle_KeepsStartFixedForNegativeSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = -90,
            Vertices = { new Point(0, 0) }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.End, 315);

        Assert.Equal(45, markup.ArcStartDeg, 6);
        Assert.Equal(-90, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcGeometry_WithSweep_UpdatesRadiusStartAndSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 15,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        var updated = _sut.SetArcGeometry(markup, 18, 30, sweepAngleDeg: 135);

        Assert.True(updated);
        Assert.Equal(18, markup.Radius, 6);
        Assert.Equal(30, markup.ArcStartDeg, 6);
        Assert.Equal(135, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcGeometry_WithEndAngle_PreservesNegativeOrientation()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = -90,
            Vertices = { new Point(0, 0) }
        };

        var updated = _sut.SetArcGeometry(markup, 12, 60, endAngleDeg: 300);

        Assert.True(updated);
        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(60, markup.ArcStartDeg, 6);
        Assert.Equal(-120, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void CaptureAndApply_Arc_PreservesAngles()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 30,
            ArcSweepDeg = 120,
            Vertices = { new Point(0, 0) }
        };

        var snapshot = _sut.Capture(markup);
        markup.ArcStartDeg = 0;
        markup.ArcSweepDeg = 45;

        _sut.Apply(markup, snapshot);

        Assert.Equal(30, markup.ArcStartDeg);
        Assert.Equal(120, markup.ArcSweepDeg);
    }

    [Fact]
    public void SnapAngleDegrees_RoundsToRequestedIncrement()
    {
        var snapped = _sut.SnapAngleDegrees(43, 15);

        Assert.Equal(45, snapped);
    }

    [Fact]
    public void SnapAngleDegrees_NormalizesNegativeAngles()
    {
        var snapped = _sut.SnapAngleDegrees(-14, 15);

        Assert.Equal(345, snapped);
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
