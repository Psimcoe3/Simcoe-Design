using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class MarkupInteractionServiceTests
{
    [Fact]
    public void SetBoundsGeometry_UpdatesRectangleBoundsAndVertices()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(10, 20, 30, 40)
        };
        markup.Vertices.Add(new Point(10, 20));
        markup.Vertices.Add(new Point(40, 60));

        var result = _sut.SetBoundsGeometry(markup, 55, 25);

        Assert.True(result);
        Assert.Equal(new Rect(10, 20, 55, 25), markup.BoundingRect);
        Assert.Equal(new Point(10, 20), markup.Vertices[0]);
        Assert.Equal(new Point(65, 45), markup.Vertices[1]);
    }

    [Fact]
    public void SetBoundsGeometry_UpdatesStampBoundsAndVertices()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Stamp,
            BoundingRect = new Rect(100, 200, 120, 30)
        };
        markup.Vertices.Add(new Point(160, 215));

        var result = _sut.SetBoundsGeometry(markup, 150, 40);

        Assert.True(result);
        Assert.Equal(new Rect(100, 200, 150, 40), markup.BoundingRect);
        Assert.Equal(new Point(100, 200), markup.Vertices[0]);
        Assert.Equal(new Point(250, 240), markup.Vertices[1]);
    }

    [Fact]
    public void SetBoundsGeometry_UsesVertexBoundsWhenBoundingRectIsEmpty()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Panel,
            BoundingRect = Rect.Empty,
            Vertices = { new Point(100, 200), new Point(220, 230) }
        };

        var result = _sut.SetBoundsGeometry(markup, 150, 40);

        Assert.True(result);
        Assert.Equal(new Rect(100, 200, 150, 40), markup.BoundingRect);
        Assert.Equal(new Point(100, 200), markup.Vertices[0]);
        Assert.Equal(new Point(250, 240), markup.Vertices[1]);
    }

    [Fact]
    public void SetBoundsGeometry_NonPositiveDimensions_ClampToMinimumSize()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(10, 20, 30, 40)
        };
        markup.Vertices.Add(new Point(10, 20));
        markup.Vertices.Add(new Point(40, 60));

        var result = _sut.SetBoundsGeometry(markup, 0, 0);

        Assert.True(result);
        Assert.Equal(10, markup.BoundingRect.X, 6);
        Assert.Equal(20, markup.BoundingRect.Y, 6);
        Assert.Equal(0.1, markup.BoundingRect.Width, 6);
        Assert.Equal(0.1, markup.BoundingRect.Height, 6);
        Assert.Equal(new Point(10, 20), markup.Vertices[0]);
        Assert.Equal(10.1, markup.Vertices[1].X, 6);
        Assert.Equal(20.1, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetBoundsGeometry_WithoutStoredOrVertexBounds_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Hyperlink,
            BoundingRect = Rect.Empty
        };

        var result = _sut.SetBoundsGeometry(markup, 150, 40);

        Assert.False(result);
        Assert.Equal(Rect.Empty, markup.BoundingRect);
        Assert.Empty(markup.Vertices);
    }

    [Fact]
    public void SetLineGeometry_UpdatesDimensionEndPointAndBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 10), new Point(30, 10) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 25, 90);

        Assert.True(result);
        Assert.Equal(new Point(10, 10), markup.Vertices[0]);
        Assert.Equal(10, markup.Vertices[1].X, 6);
        Assert.Equal(35, markup.Vertices[1].Y, 6);
        Assert.Equal(10, markup.BoundingRect.X, 6);
        Assert.Equal(10, markup.BoundingRect.Y, 6);
        Assert.Equal(0, markup.BoundingRect.Width, 6);
        Assert.Equal(25, markup.BoundingRect.Height, 6);
    }

    [Fact]
    public void SetLineGeometry_ThreePointDimension_RepositionsAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 45);

        Assert.True(result);
        Assert.Equal(3, markup.Vertices.Count);
        Assert.Equal(16.970562748477143, markup.Vertices[1].X, 6);
        Assert.Equal(16.97056274847714, markup.Vertices[1].Y, 6);
        Assert.Equal(3.3941125496954285, markup.Vertices[2].X, 6);
        Assert.Equal(13.57645019878171, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetLineGeometry_UpdatesMeasurementEndPointAndBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 10), new Point(30, 10) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 25, 90);

        Assert.True(result);
        Assert.Equal(new Point(10, 10), markup.Vertices[0]);
        Assert.Equal(10, markup.Vertices[1].X, 6);
        Assert.Equal(35, markup.Vertices[1].Y, 6);
        Assert.Equal(10, markup.BoundingRect.X, 6);
        Assert.Equal(10, markup.BoundingRect.Y, 6);
        Assert.Equal(0, markup.BoundingRect.Width, 6);
        Assert.Equal(25, markup.BoundingRect.Height, 6);
    }

    [Fact]
    public void SetLineGeometry_NonPositiveLength_ClampsToMinimumLengthUsingOriginalAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 10), new Point(30, 10) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 0, 90);

        Assert.True(result);
        Assert.Equal(new Point(10, 10), markup.Vertices[0]);
        Assert.Equal(10.1, markup.Vertices[1].X, 6);
        Assert.Equal(10, markup.Vertices[1].Y, 6);
        Assert.Equal(10, markup.BoundingRect.X, 6);
        Assert.Equal(10, markup.BoundingRect.Y, 6);
        Assert.Equal(0.1, markup.BoundingRect.Width, 6);
        Assert.Equal(0, markup.BoundingRect.Height, 6);
    }

    [Fact]
    public void SetLineGeometry_ThreePointMeasurement_RepositionsAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 45);

        Assert.True(result);
        Assert.Equal(3, markup.Vertices.Count);
        Assert.Equal(16.970562748477143, markup.Vertices[1].X, 6);
        Assert.Equal(16.97056274847714, markup.Vertices[1].Y, 6);
        Assert.Equal(3.3941125496954285, markup.Vertices[2].X, 6);
        Assert.Equal(13.57645019878171, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetLineGeometry_RadialDimension_RepositionsRadiusPointAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(13, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 20, 90);

        Assert.True(result);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(0, markup.Vertices[1].X, 6);
        Assert.Equal(20, markup.Vertices[1].Y, 6);
        Assert.Equal(-6, markup.Vertices[2].X, 6);
        Assert.Equal(26, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetLineGeometry_RadialMeasurement_RepositionsRadiusPointAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(13, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 20, 90);

        Assert.True(result);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(0, markup.Vertices[1].X, 6);
        Assert.Equal(20, markup.Vertices[1].Y, 6);
        Assert.Equal(-6, markup.Vertices[2].X, 6);
        Assert.Equal(26, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetLineGeometry_DiameterDimension_RepositionsSpanAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(-10, 0), new Point(10, 0), new Point(13, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 90);

        Assert.True(result);
        Assert.Equal(new Point(-10, 0), markup.Vertices[0]);
        Assert.Equal(-10, markup.Vertices[1].X, 6);
        Assert.Equal(24, markup.Vertices[1].Y, 6);
        Assert.Equal(-13.6, markup.Vertices[2].X, 6);
        Assert.Equal(27.6, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetLineGeometry_DiameterMeasurement_RepositionsSpanAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(-10, 0), new Point(10, 0), new Point(13, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 90);

        Assert.True(result);
        Assert.Equal(new Point(-10, 0), markup.Vertices[0]);
        Assert.Equal(-10, markup.Vertices[1].X, 6);
        Assert.Equal(24, markup.Vertices[1].Y, 6);
        Assert.Equal(-13.6, markup.Vertices[2].X, 6);
        Assert.Equal(27.6, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetLineGeometryByEndpoints_UpdatesDimensionStartEndAndBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 10), new Point(30, 10) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometryByEndpoints(markup, new Point(5, 5), new Point(5, 30));

        Assert.True(result);
        Assert.Equal(new Point(5, 5), markup.Vertices[0]);
        Assert.Equal(new Point(5, 30), markup.Vertices[1]);
        Assert.Equal(5, markup.BoundingRect.X, 6);
        Assert.Equal(5, markup.BoundingRect.Y, 6);
        Assert.Equal(0, markup.BoundingRect.Width, 6);
        Assert.Equal(25, markup.BoundingRect.Height, 6);
    }

    [Fact]
    public void SetLineGeometryByEndpoints_ThreePointMeasurement_RotatesAndScalesAnchorAroundMidpoint()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometryByEndpoints(markup, new Point(10, 10), new Point(10, 30));

        Assert.True(result);
        Assert.Equal(new Point(10, 10), markup.Vertices[0]);
        Assert.Equal(new Point(10, 30), markup.Vertices[1]);
        Assert.Equal(4, markup.Vertices[2].X, 6);
        Assert.Equal(20, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetLineGeometryByEndpoints_ZeroLengthTarget_ClampsToMinimumLengthUsingOriginalAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometryByEndpoints(markup, new Point(20, 20), new Point(20, 20));

        Assert.True(result);
        Assert.Equal(new Point(20, 20), markup.Vertices[0]);
        Assert.Equal(20.1, markup.Vertices[1].X, 6);
        Assert.Equal(20, markup.Vertices[1].Y, 6);
        Assert.Equal(20.05, markup.Vertices[2].X, 6);
        Assert.Equal(20.03, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetAngularGeometry_AngularDimension_RepositionsSecondRayAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 12), new Point(10, 10) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetAngularGeometry(markup, 60, 14);

        Assert.True(result);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(6, markup.Vertices[2].X, 6);
        Assert.Equal(10.392304845413264, markup.Vertices[2].Y, 6);
        Assert.Equal(17.443601136622522, markup.Vertices[3].X, 6);
        Assert.Equal(10.071067811865476, markup.Vertices[3].Y, 6);
        Assert.Equal(14, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(60, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetAngularGeometry_AngularMeasurement_RepositionsSecondRayAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 12), new Point(10, 10) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetAngularGeometry(markup, 60, 14);

        Assert.True(result);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(6, markup.Vertices[2].X, 6);
        Assert.Equal(10.392304845413264, markup.Vertices[2].Y, 6);
        Assert.Equal(17.443601136622522, markup.Vertices[3].X, 6);
        Assert.Equal(10.071067811865476, markup.Vertices[3].Y, 6);
        Assert.Equal(14, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(60, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetAngularGeometry_AngularMeasurement_NonPositiveInputs_ClampMinimumsAndPreserveNegativeSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, -12), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetAngularGeometry(markup, 0, 0);

        Assert.True(result);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-1, markup.ArcSweepDeg, 6);
        Assert.Equal(11.9981723418767, markup.Vertices[2].X, 6);
        Assert.Equal(-0.209428877247402, markup.Vertices[2].Y, 6);
        Assert.Equal(2.09992003843476, markup.Vertices[3].X, 6);
        Assert.Equal(-0.0183257245465853, markup.Vertices[3].Y, 6);
    }

    [Fact]
    public void SetAngularGeometry_AngularDimension_NonPositiveInputs_ClampMinimumsAndPreserveNegativeSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, -12), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetAngularGeometry(markup, 0, 0);

        Assert.True(result);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-1, markup.ArcSweepDeg, 6);
        Assert.Equal(11.9981723418767, markup.Vertices[2].X, 6);
        Assert.Equal(-0.209428877247402, markup.Vertices[2].Y, 6);
        Assert.Equal(2.09992003843476, markup.Vertices[3].X, 6);
        Assert.Equal(-0.0183257245465853, markup.Vertices[3].Y, 6);
    }

    [Fact]
    public void SetAngularGeometry_AngularDimensionWithoutEnoughVertices_ReturnsFalse()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetAngularGeometry(markup, 60, 14);

        Assert.False(result);
        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(new Rect(0, 0, 10, 0), markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetAngularGeometry_AngularMeasurementWithoutEnoughVertices_ReturnsFalse()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetAngularGeometry(markup, 60, 14);

        Assert.False(result);
        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(new Rect(0, 0, 10, 0), markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcAngle_AngularDimension_EndHandleUpdatesSweepAndSecondRay()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.End, 45);

        Assert.Equal(45, markup.ArcSweepDeg, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].X, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetArcAngle_AngularMeasurement_EndHandleUpdatesSweepAndSecondRay()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.End, 45);

        Assert.Equal(45, markup.ArcSweepDeg, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].X, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetArcAngle_AngularDimension_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.Start, 45);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[2]);
    }

    [Fact]
    public void SetArcAngle_AngularMeasurement_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.Start, 45);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[2]);
    }

    [Fact]
    public void SetArcAngle_AngularDimensionWithoutEnoughVertices_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.End, 45);

        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcAngle_AngularMeasurementWithoutEnoughVertices_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.End, 45);

        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularDimension_EndHandle_UsesVertexPivotAndSnap()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(9, 10), snapIncrementDeg: 15);

        Assert.Equal(45, markup.ArcSweepDeg, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].X, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularMeasurement_EndHandle_UsesVertexPivotAndSnap()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(9, 10), snapIncrementDeg: 15);

        Assert.Equal(45, markup.ArcSweepDeg, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].X, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[2].Y, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularDimension_PointAtVertexPivot_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(0, 0), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[2]);
        Assert.Equal(new Point(8, 8), markup.Vertices[3]);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularMeasurement_PointAtVertexPivot_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(0, 0), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[2]);
        Assert.Equal(new Point(8, 8), markup.Vertices[3]);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularDimension_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.Start, new Point(10, 10), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[2]);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularMeasurement_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.Start, new Point(10, 10), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[2]);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularDimensionWithoutEnoughVertices_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(9, 10), snapIncrementDeg: 15);

        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcAngleFromPoint_AngularMeasurementWithoutEnoughVertices_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(9, 10), snapIncrementDeg: 15);

        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetRadius_AngularDimension_UpdatesRadiusAndPreservesAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(9, 9) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetRadius(markup, 12);

        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(11.828427124746192, markup.Vertices[3].X, 6);
        Assert.Equal(11.82842712474619, markup.Vertices[3].Y, 6);
    }

    [Fact]
    public void SetRadius_AngularMeasurement_UpdatesRadiusAndPreservesAngle()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(9, 9) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetRadius(markup, 12);

        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(11.828427124746192, markup.Vertices[3].X, 6);
        Assert.Equal(11.82842712474619, markup.Vertices[3].Y, 6);
    }

    [Fact]
    public void SetRadius_AngularDimension_NonPositiveRadius_ClampsMinimumAndPreservesNegativeSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, -12), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetRadius(markup, 0);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-90, markup.ArcSweepDeg, 6);
        Assert.Equal(0, markup.Vertices[2].X, 6);
        Assert.Equal(-12, markup.Vertices[2].Y, 6);
        Assert.Equal(1.48492424049175, markup.Vertices[3].X, 6);
        Assert.Equal(-1.48492424049175, markup.Vertices[3].Y, 6);
    }

    [Fact]
    public void SetRadius_AngularMeasurement_NonPositiveRadius_ClampsMinimumAndPreservesNegativeSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, -12), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };

        _sut.SetRadius(markup, 0);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-90, markup.ArcSweepDeg, 6);
        Assert.Equal(0, markup.Vertices[2].X, 6);
        Assert.Equal(-12, markup.Vertices[2].Y, 6);
        Assert.Equal(1.48492424049175, markup.Vertices[3].X, 6);
        Assert.Equal(-1.48492424049175, markup.Vertices[3].Y, 6);
    }

    [Fact]
    public void SetRadius_AngularMeasurementWithoutEnoughVertices_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetRadius(markup, 14);

        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetRadius_AngularDimensionWithoutEnoughVertices_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetRadius(markup, 14);

        Assert.Equal(8, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 0), markup.Vertices[0]);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcLengthGeometry_ArcLengthDimension_RepositionsEndPointAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetArcLengthGeometry(markup, 6.283185307179586, 12);

        Assert.True(result);
        Assert.Equal(new Point(12, 0), markup.Vertices[0]);
        Assert.Equal(10.392304845413264, markup.Vertices[1].X, 6);
        Assert.Equal(6, markup.Vertices[1].Y, 6);
        Assert.Equal(11.59110991546882, markup.Vertices[2].X, 6);
        Assert.Equal(3.105828541230249, markup.Vertices[2].Y, 6);
        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(30, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcLengthGeometry_ArcLengthMeasurement_RepositionsEndPointAndAnchor()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetArcLengthGeometry(markup, 6.283185307179586, 12);

        Assert.True(result);
        Assert.Equal(new Point(12, 0), markup.Vertices[0]);
        Assert.Equal(10.392304845413264, markup.Vertices[1].X, 6);
        Assert.Equal(6, markup.Vertices[1].Y, 6);
        Assert.Equal(11.59110991546882, markup.Vertices[2].X, 6);
        Assert.Equal(3.105828541230249, markup.Vertices[2].Y, 6);
        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(30, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcLengthGeometry_ArcLengthMeasurement_NonPositiveInputs_ClampMinimumsAndPreserveNegativeSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, -10), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetArcLengthGeometry(markup, 0, 0);

        Assert.True(result);
        Assert.Equal(0.1, markup.Vertices[0].X, 6);
        Assert.Equal(0, markup.Vertices[0].Y, 6);
        Assert.Equal(0.0999847695156391, markup.Vertices[1].X, 6);
        Assert.Equal(-0.00174524064372835, markup.Vertices[1].Y, 6);
        Assert.Equal(0.0999961923064171, markup.Vertices[2].X, 6);
        Assert.Equal(-0.000872653549837394, markup.Vertices[2].Y, 6);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-1, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcLengthGeometry_ArcLengthDimension_NonPositiveInputs_ClampMinimumsAndPreserveNegativeSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, -10), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetArcLengthGeometry(markup, 0, 0);

        Assert.True(result);
        Assert.Equal(0.1, markup.Vertices[0].X, 6);
        Assert.Equal(0, markup.Vertices[0].Y, 6);
        Assert.Equal(0.0999847695156391, markup.Vertices[1].X, 6);
        Assert.Equal(-0.00174524064372835, markup.Vertices[1].Y, 6);
        Assert.Equal(0.0999961923064171, markup.Vertices[2].X, 6);
        Assert.Equal(-0.000872653549837394, markup.Vertices[2].Y, 6);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-1, markup.ArcSweepDeg, 6);
    }

    [Fact]
    public void SetArcLengthGeometry_ArcLengthDimensionAtMinimumRadius_ReturnsFalse()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 0.1,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetArcLengthGeometry(markup, 6.283185307179586, 12);

        Assert.False(result);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(10, 0), markup.Vertices[0]);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcLengthGeometry_ArcLengthMeasurementAtMinimumRadius_ReturnsFalse()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 0.1,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();

        var result = _sut.SetArcLengthGeometry(markup, 6.283185307179586, 12);

        Assert.False(result);
        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(10, 0), markup.Vertices[0]);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcAngle_ArcLengthDimension_EndHandleUpdatesSweepAndEndPoint()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.End, 180);

        Assert.Equal(180, markup.ArcSweepDeg, 6);
        Assert.Equal(-10, markup.Vertices[1].X, 6);
        Assert.Equal(0, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetArcAngle_ArcLengthDimension_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.Start, 180);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
    }

    [Fact]
    public void SetArcAngle_ArcLengthMeasurement_EndHandleUpdatesSweepAndEndPoint()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.End, 180);

        Assert.Equal(180, markup.ArcSweepDeg, 6);
        Assert.Equal(-10, markup.Vertices[1].X, 6);
        Assert.Equal(0, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetArcAngle_ArcLengthMeasurement_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngle(markup, MarkupArcAngleHandle.Start, 180);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthDimension_EndHandle_UsesArcCenterAndSnap()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(-9, 10), snapIncrementDeg: 15);

        Assert.Equal(135, markup.ArcSweepDeg, 6);
        Assert.Equal(-7.071067811865475, markup.Vertices[1].X, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthMeasurement_EndHandle_UsesArcCenterAndSnap()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(-9, 10), snapIncrementDeg: 15);

        Assert.Equal(135, markup.ArcSweepDeg, 6);
        Assert.Equal(-7.071067811865475, markup.Vertices[1].X, 6);
        Assert.Equal(7.0710678118654755, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthDimension_PointAtArcCenter_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(0, 0), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthMeasurement_PointAtArcCenter_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(0, 0), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthDimension_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.Start, new Point(-10, 0), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthMeasurement_StartHandle_DoesNothing()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.Start, new Point(-10, 0), snapIncrementDeg: 15);

        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthDimensionAtMinimumRadius_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 0.1,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(-9, 10), snapIncrementDeg: 15);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(10, 0), markup.Vertices[0]);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetArcAngleFromPoint_ArcLengthMeasurementAtMinimumRadius_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 0.1,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetArcAngleFromPoint(markup, MarkupArcAngleHandle.End, new Point(-9, 10), snapIncrementDeg: 15);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(10, 0), markup.Vertices[0]);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetRadius_ArcLengthDimension_PreservesArcLengthAndUpdatesSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetRadius(markup, 12);

        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(75, markup.ArcSweepDeg, 6);
        Assert.Equal(3.105828541230249, markup.Vertices[1].X, 6);
        Assert.Equal(11.59110991546882, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetRadius_ArcLengthMeasurement_PreservesArcLengthAndUpdatesSweep()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetRadius(markup, 12);

        Assert.Equal(12, markup.Radius, 6);
        Assert.Equal(75, markup.ArcSweepDeg, 6);
        Assert.Equal(3.105828541230249, markup.Vertices[1].X, 6);
        Assert.Equal(11.59110991546882, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetRadius_ArcLengthDimension_NonPositiveRadius_ClampsMinimumAndPreservesArcLength()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, -10), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetRadius(markup, 0);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-359, markup.ArcSweepDeg, 6);
        Assert.Equal(0.1, markup.Vertices[0].X, 6);
        Assert.Equal(0, markup.Vertices[0].Y, 6);
        Assert.Equal(0.0999847695156391, markup.Vertices[1].X, 6);
        Assert.Equal(0.00174524064372845, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetRadius_ArcLengthMeasurement_NonPositiveRadius_ClampsMinimumAndPreservesArcLength()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, -10), new Point(7.0710678118654755, -7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = -90,
            Metadata = new MarkupMetadata { Subject = "ArcLength" }
        };

        _sut.SetRadius(markup, 0);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(-359, markup.ArcSweepDeg, 6);
        Assert.Equal(0.1, markup.Vertices[0].X, 6);
        Assert.Equal(0, markup.Vertices[0].Y, 6);
        Assert.Equal(0.0999847695156391, markup.Vertices[1].X, 6);
        Assert.Equal(0.00174524064372845, markup.Vertices[1].Y, 6);
    }

    [Fact]
    public void SetRadius_ArcLengthMeasurementAtMinimumRadius_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 0.1,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetRadius(markup, 12);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(10, 0), markup.Vertices[0]);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetRadius_ArcLengthDimensionAtMinimumRadius_DoesNothing()
    {
        var modifiedUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 0.1,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "ArcLength", ModifiedUtc = modifiedUtc }
        };
        markup.UpdateBoundingRect();
        var originalBounds = markup.BoundingRect;

        _sut.SetRadius(markup, 12);

        Assert.Equal(0.1, markup.Radius, 6);
        Assert.Equal(0, markup.ArcStartDeg, 6);
        Assert.Equal(90, markup.ArcSweepDeg, 6);
        Assert.Equal(new Point(10, 0), markup.Vertices[0]);
        Assert.Equal(new Point(0, 10), markup.Vertices[1]);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), markup.Vertices[2]);
        Assert.Equal(originalBounds, markup.BoundingRect);
        Assert.Equal(modifiedUtc, markup.Metadata.ModifiedUtc);
    }

    [Fact]
    public void SetLineGeometry_ArcLengthDimension_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.Metadata.Subject = "ArcLength";
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 45);

        Assert.False(result);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
    }

    [Fact]
    public void SetLineGeometry_ArcLengthMeasurement_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.Metadata.Subject = "ArcLength";
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 45);

        Assert.False(result);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
    }

    [Fact]
    public void SetLineGeometry_AngularMeasurement_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.Metadata.Subject = "Angular";
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 45);

        Assert.False(result);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
    }

    [Fact]
    public void SetLineGeometry_AngularDimension_ReturnsFalse()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
        };
        markup.Metadata.Subject = "Angular";
        markup.UpdateBoundingRect();

        var result = _sut.SetLineGeometry(markup, 24, 45);

        Assert.False(result);
        Assert.Equal(new Point(10, 0), markup.Vertices[1]);
    }
}
