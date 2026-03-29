using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class MarkupInteractionServiceTests
{
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
    public void HitTestRadiusHandle_Circle_ReturnsTrueNearHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 12,
            Vertices = { new Point(20, 20) }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(31.5, 20.5), markup, tolerance: 1.0);

        Assert.True(hit);
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
    public void Resize_Circle_ScalesCenterRadiusAndBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 5,
            Vertices = { new Point(10, 10) }
        };
        markup.UpdateBoundingRect();

        var snapshot = _sut.Capture(markup);
        _sut.Resize(markup, snapshot, new Rect(0, 0, 20, 20), new Rect(0, 0, 40, 40));

        Assert.Equal(new Point(20, 20), markup.Vertices[0]);
        Assert.Equal(10, markup.Radius, 6);
        Assert.Equal(new Rect(10, 10, 20, 20), markup.BoundingRect);
    }

    [Fact]
    public void Resize_Arc_ScalesRadiusAndPreservesAngles()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 8,
            ArcStartDeg = 45,
            ArcSweepDeg = -90,
            Vertices = { new Point(20, 20) }
        };
        markup.UpdateBoundingRect();

        var snapshot = _sut.Capture(markup);
        _sut.Resize(markup, snapshot, new Rect(0, 0, 40, 40), new Rect(0, 0, 80, 80));

        Assert.Equal(new Point(40, 40), markup.Vertices[0]);
        Assert.Equal(16, markup.Radius, 6);
        Assert.Equal(45, markup.ArcStartDeg, 6);
        Assert.Equal(-90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Rect(24, 24, 32, 32), markup.BoundingRect);
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
}
