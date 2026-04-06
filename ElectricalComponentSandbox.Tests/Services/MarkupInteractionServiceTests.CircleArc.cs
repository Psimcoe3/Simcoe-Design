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
    public void CanEditRadius_Arc_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
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
    public void CanEditRadius_CircleWithoutVertices_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 8
        };

        Assert.False(_sut.CanEditRadius(markup));
    }

    [Fact]
    public void CanEditRadius_ArcWithoutVertices_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 8
        };

        Assert.False(_sut.CanEditRadius(markup));
    }

    [Fact]
    public void CanEditArcAngles_ArcWithoutVertices_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 8
        };

        Assert.False(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditArcAngles_ArcWithMinimumRadius_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 0.1,
            Vertices = { new Point(10, 20) }
        };

        Assert.False(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditRadius_AngularMeasurement_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.True(_sut.CanEditRadius(markup));
    }

    [Fact]
    public void CanEditRadius_AngularDimension_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.True(_sut.CanEditRadius(markup));
    }

    [Fact]
    public void CanEditArcAngles_AngularMeasurement_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.True(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditArcAngles_AngularDimension_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.True(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditArcAngles_ArcLengthMeasurement_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7, 7) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        Assert.True(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditArcAngles_ArcLengthDimension_ReturnsTrue()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7, 7) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        Assert.True(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditRadius_AngularMeasurementWithoutEnoughVertices_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.False(_sut.CanEditRadius(markup));
    }

    [Fact]
    public void CanEditRadius_AngularDimensionWithoutEnoughVertices_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.False(_sut.CanEditRadius(markup));
    }

    [Fact]
    public void CanEditArcAngles_AngularMeasurementWithoutEnoughVertices_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.False(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditArcAngles_AngularDimensionWithoutEnoughVertices_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        Assert.False(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditArcAngles_ArcLengthMeasurementWithoutRadius_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 0,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7, 7) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        Assert.False(_sut.CanEditArcAngles(markup));
    }

    [Fact]
    public void CanEditArcAngles_ArcLengthDimensionWithoutRadius_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 0,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7, 7) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        Assert.False(_sut.CanEditArcAngles(markup));
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
    public void GetRadiusHandlePoint_Circle_UsesPositiveXAxis()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 10,
            Vertices = { new Point(2, 3) }
        };

        var handle = _sut.GetRadiusHandlePoint(markup);

        Assert.Equal(12, handle.X, 6);
        Assert.Equal(3, handle.Y, 6);
    }

    [Fact]
    public void GetRadiusHandlePoint_AngularMeasurement_UsesMidSweepBetweenRays()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handle = _sut.GetRadiusHandlePoint(markup);

        Assert.Equal(7.656854249492381, handle.X, 6);
        Assert.Equal(8.65685424949238, handle.Y, 6);
    }

    [Fact]
    public void GetRadiusHandlePoint_AngularDimension_UsesMidSweepBetweenRays()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handle = _sut.GetRadiusHandlePoint(markup);

        Assert.Equal(7.656854249492381, handle.X, 6);
        Assert.Equal(8.65685424949238, handle.Y, 6);
    }

    [Fact]
    public void GetRadiusHandlePoint_ArcLengthMeasurement_UsesArcCenterAndMidSweepAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handle = _sut.GetRadiusHandlePoint(markup);

        Assert.Equal(7.0710678118654755, handle.X, 6);
        Assert.Equal(7.0710678118654755, handle.Y, 6);
    }

    [Fact]
    public void GetRadiusHandlePoint_ArcLengthDimension_UsesArcCenterAndMidSweepAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handle = _sut.GetRadiusHandlePoint(markup);

        Assert.Equal(7.0710678118654755, handle.X, 6);
        Assert.Equal(7.0710678118654755, handle.Y, 6);
    }

    [Fact]
    public void GetRadiusHandlePoint_CannotEdit_ReturnsDefault()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handle = _sut.GetRadiusHandlePoint(markup);

        Assert.Equal(default, handle);
    }

    [Fact]
    public void GetRadiusPivotPoint_AngularMeasurement_ReturnsVertex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var pivot = _sut.GetRadiusPivotPoint(markup);

        Assert.Equal(new Point(2, 3), pivot);
    }

    [Fact]
    public void GetRadiusPivotPoint_AngularDimension_ReturnsVertex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var pivot = _sut.GetRadiusPivotPoint(markup);

        Assert.Equal(new Point(2, 3), pivot);
    }

    [Fact]
    public void GetRadiusPivotPoint_ArcLengthMeasurement_ReturnsArcCenter()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var pivot = _sut.GetRadiusPivotPoint(markup);

        Assert.Equal(new Point(0, 0), pivot);
    }

    [Fact]
    public void GetRadiusPivotPoint_ArcLengthDimension_ReturnsArcCenter()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var pivot = _sut.GetRadiusPivotPoint(markup);

        Assert.Equal(new Point(0, 0), pivot);
    }

    [Fact]
    public void GetRadiusPivotPoint_Circle_ReturnsCenter()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 10,
            Vertices = { new Point(2, 3) }
        };

        var pivot = _sut.GetRadiusPivotPoint(markup);

        Assert.Equal(new Point(2, 3), pivot);
    }

    [Fact]
    public void GetRadiusPivotPoint_CannotEdit_ReturnsDefault()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var pivot = _sut.GetRadiusPivotPoint(markup);

        Assert.Equal(default, pivot);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_AngularMeasurementEndHandle_ReturnsVertex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.End, out var pivot);

        Assert.True(found);
        Assert.Equal(new Point(2, 3), pivot);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_AngularDimensionEndHandle_ReturnsVertex()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.End, out var pivot);

        Assert.True(found);
        Assert.Equal(new Point(2, 3), pivot);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_ArcLengthMeasurementEndHandle_ReturnsArcCenter()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.End, out var pivot);

        Assert.True(found);
        Assert.Equal(new Point(0, 0), pivot);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_ArcLengthDimensionEndHandle_ReturnsArcCenter()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.End, out var pivot);

        Assert.True(found);
        Assert.Equal(new Point(0, 0), pivot);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_AngularMeasurementStartHandle_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.Start, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_AngularDimensionStartHandle_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.Start, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_ArcLengthMeasurementStartHandle_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.Start, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_ArcLengthDimensionStartHandle_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.Start, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_NoneHandle_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3) }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.None, out var pivot);

        Assert.False(found);
        Assert.Equal(default, pivot);
    }

    [Fact]
    public void GetArcAngleHandles_AngularDimension_ReturnsEndOnly()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handles = _sut.GetArcAngleHandles(markup);

        Assert.Single(handles);
        Assert.Equal(MarkupArcAngleHandle.End, handles[0]);
    }

    [Fact]
    public void GetArcAngleHandles_AngularMeasurement_ReturnsEndOnly()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handles = _sut.GetArcAngleHandles(markup);

        Assert.Single(handles);
        Assert.Equal(MarkupArcAngleHandle.End, handles[0]);
    }

    [Fact]
    public void GetArcAngleHandles_ArcLengthMeasurement_ReturnsEndOnly()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handles = _sut.GetArcAngleHandles(markup);

        Assert.Single(handles);
        Assert.Equal(MarkupArcAngleHandle.End, handles[0]);
    }

    [Fact]
    public void GetArcAngleHandles_ArcLengthDimension_ReturnsEndOnly()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handles = _sut.GetArcAngleHandles(markup);

        Assert.Single(handles);
        Assert.Equal(MarkupArcAngleHandle.End, handles[0]);
    }

    [Fact]
    public void GetArcAngleHandles_Arc_ReturnsStartAndEnd()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 15,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        var handles = _sut.GetArcAngleHandles(markup);

        Assert.Equal(2, handles.Count);
        Assert.Equal(MarkupArcAngleHandle.Start, handles[0]);
        Assert.Equal(MarkupArcAngleHandle.End, handles[1]);
    }

    [Fact]
    public void GetArcAngleHandles_CannotEdit_ReturnsEmpty()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handles = _sut.GetArcAngleHandles(markup);

        Assert.Empty(handles);
    }

    [Fact]
    public void GetArcAngleHandlePoint_AngularMeasurementEndHandle_ReturnsSecondRayEnd()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.End);

        Assert.Equal(new Point(2, 13), handle);
    }

    [Fact]
    public void GetArcAngleHandlePoint_AngularMeasurementStartHandle_ReturnsDefault()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.Start);

        Assert.Equal(default, handle);
    }

    [Fact]
    public void GetArcAngleHandlePoint_AngularDimensionEndHandle_ReturnsSecondRayEnd()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.End);

        Assert.Equal(new Point(2, 13), handle);
    }

    [Fact]
    public void GetArcAngleHandlePoint_AngularDimensionStartHandle_ReturnsDefault()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.Start);

        Assert.Equal(default, handle);
    }

    [Fact]
    public void GetArcAngleHandlePoint_ArcLengthMeasurementEndHandle_ReturnsArcEndPoint()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.End);

        Assert.Equal(new Point(0, 10), handle);
    }

    [Fact]
    public void GetArcAngleHandlePoint_ArcLengthMeasurementStartHandle_ReturnsDefault()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.Start);

        Assert.Equal(default, handle);
    }

    [Fact]
    public void GetArcAngleHandlePoint_ArcLengthDimensionEndHandle_ReturnsArcEndPoint()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.End);

        Assert.Equal(new Point(0, 10), handle);
    }

    [Fact]
    public void GetArcAngleHandlePoint_ArcLengthDimensionStartHandle_ReturnsDefault()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.Start);

        Assert.Equal(default, handle);
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
    public void GetArcAngleHandlePoint_Start_UsesArcStartAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.Start);

        Assert.Equal(7.0710678118654755, handle.X, 6);
        Assert.Equal(7.0710678118654755, handle.Y, 6);
    }

    [Fact]
    public void GetArcAngleHandlePoint_NoneHandle_ReturnsDefault()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        var handle = _sut.GetArcAngleHandlePoint(markup, MarkupArcAngleHandle.None);

        Assert.Equal(default, handle);
    }

    [Fact]
    public void TryGetArcAnglePivotPoint_ArcStartHandle_ReturnsCenter()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3) }
        };

        var found = _sut.TryGetArcAnglePivotPoint(markup, MarkupArcAngleHandle.Start, out var pivot);

        Assert.True(found);
        Assert.Equal(new Point(2, 3), pivot);
    }

    [Fact]
    public void HitTestArcAngleHandle_AngularMeasurementEnd_ReturnsEndHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(2.5, 12.5), markup, tolerance: 1.0);

        Assert.Equal(MarkupArcAngleHandle.End, hit);
    }

    [Fact]
    public void HitTestArcAngleHandle_AngularDimensionEnd_ReturnsEndHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(2.5, 12.5), markup, tolerance: 1.0);

        Assert.Equal(MarkupArcAngleHandle.End, hit);
    }

    [Fact]
    public void HitTestArcAngleHandle_ArcLengthDimensionEnd_ReturnsEndHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(0.5, 9.5), markup, tolerance: 1.0);

        Assert.Equal(MarkupArcAngleHandle.End, hit);
    }

    [Fact]
    public void HitTestArcAngleHandle_ArcLengthMeasurementEnd_ReturnsEndHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(0.5, 9.5), markup, tolerance: 1.0);

        Assert.Equal(MarkupArcAngleHandle.End, hit);
    }

    [Fact]
    public void HitTestArcAngleHandle_ArcEnd_ReturnsEndHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(5, 5) }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(5.5, 14.5), markup, tolerance: 1.0);

        Assert.Equal(MarkupArcAngleHandle.End, hit);
    }

    [Fact]
    public void HitTestArcAngleHandle_ArcWhenBothHandlesMatch_PrefersEnd()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(5, 5) }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(5, 5), markup, tolerance: 10.1);

        Assert.Equal(MarkupArcAngleHandle.End, hit);
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
    public void HitTestArcAngleHandle_AngularDimensionMiss_ReturnsNone()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(30, 30), markup, tolerance: 1.0);

        Assert.Equal(MarkupArcAngleHandle.None, hit);
    }

    [Fact]
    public void HitTestArcAngleHandle_CannotEdit_ReturnsNone()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestArcAngleHandle(new Point(2.5, 12.5), markup, tolerance: 1.0);

        Assert.Equal(MarkupArcAngleHandle.None, hit);
    }

    [Fact]
    public void HitTestRadiusHandle_Arc_ReturnsTrueNearHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 180,
            Vertices = { new Point(20, 20) }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(20.5, 29.5), markup, tolerance: 1.0);

        Assert.True(hit);
    }

    [Fact]
    public void HitTestRadiusHandle_ArcMiss_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 180,
            Vertices = { new Point(20, 20) }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(35, 35), markup, tolerance: 1.0);

        Assert.False(hit);
    }

    [Fact]
    public void HitTestRadiusHandle_AngularMeasurement_ReturnsTrueNearHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(7.9, 8.4), markup, tolerance: 0.5);

        Assert.True(hit);
    }

    [Fact]
    public void HitTestRadiusHandle_AngularDimension_ReturnsTrueNearHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(7.9, 8.4), markup, tolerance: 0.5);

        Assert.True(hit);
    }

    [Fact]
    public void HitTestRadiusHandle_ArcLengthMeasurement_ReturnsTrueNearHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(7.4, 7.0), markup, tolerance: 0.5);

        Assert.True(hit);
    }

    [Fact]
    public void HitTestRadiusHandle_ArcLengthDimension_ReturnsTrueNearHandle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(7.4, 7.0), markup, tolerance: 0.5);

        Assert.True(hit);
    }

    [Fact]
    public void HitTestRadiusHandle_AngularDimensionMiss_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3), new Point(2, 13) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(30, 30), markup, tolerance: 0.5);

        Assert.False(hit);
    }

    [Fact]
    public void HitTestRadiusHandle_CannotEdit_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(2, 3), new Point(12, 3) },
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(7.9, 8.4), markup, tolerance: 0.5);

        Assert.False(hit);
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
    public void SetRadius_Arc_ClampsMinimumAndPreservesAngles()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(20, 20) }
        };
        markup.UpdateBoundingRect();

        _sut.SetRadius(markup, 0);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Rect(19.9, 19.9, 0.2, 0.2), markup.BoundingRect);
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
    public void HitTestRadiusHandle_CircleMiss_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 12,
            Vertices = { new Point(20, 20) }
        };

        var hit = _sut.HitTestRadiusHandle(new Point(20, 35), markup, tolerance: 1.0);

        Assert.False(hit);
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
    public void SetArcAngle_NoneHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 15,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.None, 300);

        Assert.Equal(15, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_StartHandle_UsesPointAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.Start, new Point(8.660254037844387, 5), snapIncrementDeg: 15);

        Assert.Equal(30, markup.ArcStartDeg, 6);
        Assert.Equal(60, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_EndHandle_UsesPointAngleAndSnap()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(-9, 10), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(135, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_NoneHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 15,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.None, new Point(-9, 10), snapIncrementDeg: 15);

        Assert.Equal(15, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_PointAtPivot_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(0, 0), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
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
    public void SetArcGeometry_WithSweepAndEndAngle_PrefersExplicitSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        var updated = _sut.SetArcGeometry(markup, 12, 60, sweepAngleDeg: 100, endAngleDeg: 10);

        Assert.True(updated);
        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(60, markup.ArcStartDeg, 6);
        Assert.Equal(100, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcGeometry_WithZeroSweep_ClampsToPositiveMinimumAndRadius()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = 90,
            Vertices = { new Point(0, 0) }
        };

        var updated = _sut.SetArcGeometry(markup, 0, -30, sweepAngleDeg: 0);

        Assert.True(updated);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(330, markup.ArcStartDeg, 6);
        Assert.Equal(1, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcGeometry_WithTinyNegativeSweep_ClampsToNegativeMinimumAndRadius()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10,
            ArcStartDeg = 45,
            ArcSweepDeg = -90,
            Vertices = { new Point(0, 0) }
        };

        var updated = _sut.SetArcGeometry(markup, 0, 420, sweepAngleDeg: -0.2);

        Assert.True(updated);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(60, markup.ArcStartDeg, 6);
        Assert.Equal(-1, markup.ArcSweepDeg, 6);
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

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void SnapAngleDegrees_WithNonPositiveIncrement_ReturnsNormalizedAngle(double incrementDeg)
    {
        var snapped = _sut.SnapAngleDegrees(-14, incrementDeg);

        Assert.Equal(346, snapped);
    }
}
