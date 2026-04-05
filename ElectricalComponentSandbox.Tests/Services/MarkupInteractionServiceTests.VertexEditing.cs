using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class MarkupInteractionServiceTests
{
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
    public void HitTestVertexHandle_Measurement_ReturnsMatchingIndex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 10), new Point(20, 20), new Point(30, 30) }
        };

        var hitIndex = _sut.HitTestVertexHandle(new Point(29.5, 30.5), markup, tolerance: 1.0);

        Assert.Equal(2, hitIndex);
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
    public void TryFindInsertionPoint_PolygonClosingEdge_ReturnsClosingInsertIndex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10) }
        };

        var found = _sut.TryFindInsertionPoint(new Point(-1, 4), markup, 2.0, out var insertIndex, out var projectedPoint);

        Assert.True(found);
        Assert.Equal(4, insertIndex);
        Assert.Equal(new Point(0, 4), projectedPoint);
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
    public void SetPolylineGeometry_Polyline_ReplacesVerticesAndUpdatesBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetPolylineGeometry(markup, new List<Point>
        {
            new(5, 5),
            new(15, 5),
            new(20, 10)
        });

        Assert.True(result);
        Assert.Equal(3, markup.Vertices.Count);
        Assert.Equal(new Point(5, 5), markup.Vertices[0]);
        Assert.Equal(new Point(15, 5), markup.Vertices[1]);
        Assert.Equal(new Point(20, 10), markup.Vertices[2]);
        Assert.Equal(new Rect(5, 5, 15, 5), markup.BoundingRect);
    }

    [Fact]
    public void SetPolylineGeometry_PolygonWithTooFewVertices_ReturnsFalseAndPreservesExistingGeometry()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 8) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetPolylineGeometry(markup, new List<Point>
        {
            new(1, 1),
            new(9, 1)
        });

        Assert.False(result);
        Assert.Equal(3, markup.Vertices.Count);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(new Point(5, 8), markup.Vertices[2]);
        Assert.Equal(new Rect(0, 0, 10, 8), markup.BoundingRect);
    }
}
