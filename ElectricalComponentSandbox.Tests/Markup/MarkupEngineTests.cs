using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;

namespace ElectricalComponentSandbox.Tests.Markup;

public class GeometryMathTests
{
    [Fact]
    public void Distance_ReturnsCorrectValue()
    {
        Assert.Equal(5.0, GeometryMath.Distance(new Point(0, 0), new Point(3, 4)), 6);
    }

    [Fact]
    public void Distance_SamePoint_ReturnsZero()
    {
        Assert.Equal(0.0, GeometryMath.Distance(new Point(5, 5), new Point(5, 5)));
    }

    [Fact]
    public void PolylineLength_TwoPoints()
    {
        var pts = new List<Point> { new(0, 0), new(3, 4) };
        Assert.Equal(5.0, GeometryMath.PolylineLength(pts), 6);
    }

    [Fact]
    public void PolylineLength_ThreePoints()
    {
        var pts = new List<Point> { new(0, 0), new(10, 0), new(10, 10) };
        Assert.Equal(20.0, GeometryMath.PolylineLength(pts), 6);
    }

    [Fact]
    public void PolylineLength_SinglePoint_ReturnsZero()
    {
        var pts = new List<Point> { new(5, 5) };
        Assert.Equal(0.0, GeometryMath.PolylineLength(pts));
    }

    [Fact]
    public void PolygonArea_UnitSquare()
    {
        var pts = new List<Point>
        {
            new(0, 0), new(1, 0), new(1, 1), new(0, 1)
        };
        Assert.Equal(1.0, GeometryMath.PolygonArea(pts), 6);
    }

    [Fact]
    public void PolygonArea_Triangle()
    {
        var pts = new List<Point>
        {
            new(0, 0), new(10, 0), new(0, 10)
        };
        Assert.Equal(50.0, GeometryMath.PolygonArea(pts), 6);
    }

    [Fact]
    public void PolygonArea_LessThan3Points_ReturnsZero()
    {
        var pts = new List<Point> { new(0, 0), new(1, 1) };
        Assert.Equal(0.0, GeometryMath.PolygonArea(pts));
    }

    [Fact]
    public void AreaWithCutouts_SubtractsInner()
    {
        var outer = new List<Point>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };
        var inner = new List<Point>
        {
            new(2, 2), new(4, 2), new(4, 4), new(2, 4)
        };
        double result = GeometryMath.AreaWithCutouts(outer, new[] { inner });
        Assert.Equal(96.0, result, 6); // 100 - 4
    }

    [Fact]
    public void AreaWithCutouts_MultipleCutouts()
    {
        var outer = new List<Point>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };
        var inner1 = new List<Point>
        {
            new(1, 1), new(3, 1), new(3, 3), new(1, 3)
        };
        var inner2 = new List<Point>
        {
            new(5, 5), new(7, 5), new(7, 7), new(5, 7)
        };
        double result = GeometryMath.AreaWithCutouts(outer, new[] { inner1, inner2 });
        Assert.Equal(92.0, result, 6); // 100 - 4 - 4
    }

    [Fact]
    public void Volume_AreaTimesDepth()
    {
        Assert.Equal(50.0, GeometryMath.Volume(10.0, 5.0));
    }

    [Fact]
    public void Volume_NegativeDepth_UsesAbsolute()
    {
        Assert.Equal(50.0, GeometryMath.Volume(10.0, -5.0));
    }

    [Fact]
    public void ApplySlopeFactor_NoSlope_ReturnsOriginal()
    {
        Assert.Equal(10.0, GeometryMath.ApplySlopeFactor(10.0, 0.0), 6);
    }

    [Fact]
    public void ApplySlopeFactor_WithSlope()
    {
        // slope = 1 → factor = sqrt(2) ≈ 1.414
        double result = GeometryMath.ApplySlopeFactor(10.0, 1.0);
        Assert.Equal(10.0 * Math.Sqrt(2), result, 6);
    }

    [Fact]
    public void PointInPolygon_Inside_ReturnsTrue()
    {
        var poly = new List<Point>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };
        Assert.True(GeometryMath.PointInPolygon(new Point(5, 5), poly));
    }

    [Fact]
    public void PointInPolygon_Outside_ReturnsFalse()
    {
        var poly = new List<Point>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };
        Assert.False(GeometryMath.PointInPolygon(new Point(15, 5), poly));
    }

    [Fact]
    public void PointToSegmentDistance_OnSegment_ReturnsZero()
    {
        double dist = GeometryMath.PointToSegmentDistance(
            new Point(5, 0), new Point(0, 0), new Point(10, 0));
        Assert.Equal(0.0, dist, 6);
    }

    [Fact]
    public void PointToSegmentDistance_Perpendicular()
    {
        double dist = GeometryMath.PointToSegmentDistance(
            new Point(5, 3), new Point(0, 0), new Point(10, 0));
        Assert.Equal(3.0, dist, 6);
    }

    [Fact]
    public void PointToSegmentDistance_BeyondEndpoint()
    {
        double dist = GeometryMath.PointToSegmentDistance(
            new Point(15, 0), new Point(0, 0), new Point(10, 0));
        Assert.Equal(5.0, dist, 6);
    }

    [Fact]
    public void ConstrainAngle45_Horizontal()
    {
        var anchor = new Point(0, 0);
        var free = new Point(10, 1); // nearly horizontal
        var constrained = GeometryMath.ConstrainAngle45(anchor, free);
        Assert.Equal(0.0, constrained.Y, 2); // should snap to Y=0
    }

    [Fact]
    public void ConstrainAngle45_Diagonal()
    {
        var anchor = new Point(0, 0);
        var free = new Point(10, 11); // near 45°
        var constrained = GeometryMath.ConstrainAngle45(anchor, free);
        Assert.Equal(constrained.X, constrained.Y, 2); // 45° means X=Y
    }

    [Fact]
    public void CircleArea_ReturnsCorrectValue()
    {
        Assert.Equal(Math.PI * 25, GeometryMath.CircleArea(5.0), 6);
    }

    [Fact]
    public void CircleCircumference_ReturnsCorrectValue()
    {
        Assert.Equal(2 * Math.PI * 5, GeometryMath.CircleCircumference(5.0), 6);
    }
}

public class CoordinateTransformServiceTests
{
    [Fact]
    public void ScreenToDocument_NoTransform_ReturnsSamePoint()
    {
        var svc = new CoordinateTransformService();
        var pt = svc.ScreenToDocument(new Point(100, 200));
        Assert.Equal(100, pt.X, 6);
        Assert.Equal(200, pt.Y, 6);
    }

    [Fact]
    public void ScreenToDocument_WithZoom()
    {
        var svc = new CoordinateTransformService { Zoom = 2.0 };
        var pt = svc.ScreenToDocument(new Point(200, 400));
        Assert.Equal(100, pt.X, 6);
        Assert.Equal(200, pt.Y, 6);
    }

    [Fact]
    public void ScreenToDocument_WithPanAndZoom()
    {
        var svc = new CoordinateTransformService
        {
            Zoom = 2.0,
            PanOffset = new Point(50, 100)
        };
        var pt = svc.ScreenToDocument(new Point(250, 500));
        Assert.Equal(100, pt.X, 6);
        Assert.Equal(200, pt.Y, 6);
    }

    [Fact]
    public void DocumentToScreen_RoundTrip()
    {
        var svc = new CoordinateTransformService
        {
            Zoom = 3.0,
            PanOffset = new Point(10, 20)
        };
        var original = new Point(42, 77);
        var screen = svc.DocumentToScreen(original);
        var back = svc.ScreenToDocument(screen);
        Assert.Equal(original.X, back.X, 6);
        Assert.Equal(original.Y, back.Y, 6);
    }

    [Fact]
    public void CalibrateUniform_SetsScale()
    {
        var svc = new CoordinateTransformService();
        var result = svc.CalibrateUniform(
            new Point(0, 0), new Point(100, 0), 10.0);

        Assert.True(result.IsValid);
        Assert.Equal(10.0, result.ScaleX, 6);
        Assert.Equal(10.0, result.ScaleY, 6);
        Assert.True(svc.IsCalibrated);
    }

    [Fact]
    public void CalibrateUniform_ZeroDistance_Invalid()
    {
        var svc = new CoordinateTransformService();
        var result = svc.CalibrateUniform(
            new Point(0, 0), new Point(100, 0), 0);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CalibrateUniform_SamePoints_Invalid()
    {
        var svc = new CoordinateTransformService();
        var result = svc.CalibrateUniform(
            new Point(50, 50), new Point(50, 50), 10);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CalibrateSeparateXY_DifferentScales()
    {
        var svc = new CoordinateTransformService();
        var result = svc.CalibrateSeparateXY(
            new Point(0, 0), new Point(200, 0), 10.0,
            new Point(0, 0), new Point(0, 100), 10.0);

        Assert.True(result.IsValid);
        Assert.Equal(20.0, svc.DocUnitsPerRealX, 6);
        Assert.Equal(10.0, svc.DocUnitsPerRealY, 6);
    }

    [Fact]
    public void DocumentToRealWorldDistance_WithCalibration()
    {
        var svc = new CoordinateTransformService();
        svc.CalibrateUniform(new Point(0, 0), new Point(100, 0), 10.0);
        // scale = 10 doc units per real unit
        double real = svc.DocumentToRealWorldDistance(50);
        Assert.Equal(5.0, real, 6);
    }

    [Fact]
    public void DocumentToRealWorldDistance_Vector_WithSeparateXY()
    {
        var svc = new CoordinateTransformService
        {
            DocUnitsPerRealX = 20.0,
            DocUnitsPerRealY = 10.0
        };
        // doc delta (40, 30) → real (2, 3) → dist = sqrt(4+9) = sqrt(13)
        double dist = svc.DocumentToRealWorldDistance(40, 30);
        Assert.Equal(Math.Sqrt(13), dist, 6);
    }

    [Fact]
    public void ScreenToRealWorld_CombinesTransforms()
    {
        var svc = new CoordinateTransformService
        {
            Zoom = 2.0,
            PanOffset = new Point(0, 0),
            DocUnitsPerRealX = 10.0,
            DocUnitsPerRealY = 10.0
        };
        // screen (200, 400) → doc (100, 200) → real (10, 20)
        var rw = svc.ScreenToRealWorld(new Point(200, 400));
        Assert.Equal(10, rw.X, 6);
        Assert.Equal(20, rw.Y, 6);
    }
}

public class HitTestServiceTests
{
    [Fact]
    public void HitTest_Polyline_OnLine_ReturnsTrue()
    {
        var svc = new HitTestService { Tolerance = 5.0 };
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };
        Assert.True(svc.HitTest(new Point(50, 2), markup));
    }

    [Fact]
    public void HitTest_Polyline_FarAway_ReturnsFalse()
    {
        var svc = new HitTestService { Tolerance = 5.0 };
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };
        Assert.False(svc.HitTest(new Point(50, 20), markup));
    }

    [Fact]
    public void HitTest_Polygon_Inside_ReturnsTrue()
    {
        var svc = new HitTestService { Tolerance = 5.0 };
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                new(0, 0), new(100, 0), new(100, 100), new(0, 100)
            }
        };
        Assert.True(svc.HitTest(new Point(50, 50), markup));
    }

    [Fact]
    public void HitTest_Polygon_Outside_ReturnsFalse()
    {
        var svc = new HitTestService { Tolerance = 5.0 };
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                new(0, 0), new(100, 0), new(100, 100), new(0, 100)
            }
        };
        Assert.False(svc.HitTest(new Point(200, 200), markup));
    }

    [Fact]
    public void HitTest_Circle_Inside_ReturnsTrue()
    {
        var svc = new HitTestService();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = new List<Point> { new(50, 50) },
            Radius = 25
        };
        Assert.True(svc.HitTest(new Point(50, 50), markup));
    }

    [Fact]
    public void HitTest_Circle_Outside_ReturnsFalse()
    {
        var svc = new HitTestService();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = new List<Point> { new(50, 50) },
            Radius = 25
        };
        Assert.False(svc.HitTest(new Point(100, 100), markup));
    }

    [Fact]
    public void HitTest_Rectangle_Inside_ReturnsTrue()
    {
        var svc = new HitTestService();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Vertices = new List<Point> { new(0, 0), new(100, 100) }
        };
        Assert.True(svc.HitTest(new Point(50, 50), markup));
    }

    [Fact]
    public void FindTopHit_ReturnsLastMatch()
    {
        var svc = new HitTestService { Tolerance = 5.0 };
        var m1 = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };
        var m2 = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };
        var result = svc.FindTopHit(new Point(50, 0), new[] { m1, m2 });
        Assert.Same(m2, result); // m2 is on top (last in list)
    }
}

public class MeasurementServiceTests
{
    private static MeasurementService CreateService(double scaleX = 1.0, double scaleY = 1.0)
    {
        var transform = new CoordinateTransformService
        {
            DocUnitsPerRealX = scaleX,
            DocUnitsPerRealY = scaleY,
            IsCalibrated = true
        };
        return new MeasurementService(transform);
    }

    [Fact]
    public void MeasureLength_Horizontal()
    {
        var svc = CreateService(10.0, 10.0);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };
        Assert.Equal(10.0, svc.MeasureLength(markup), 6);
    }

    [Fact]
    public void MeasureLength_MultiSegment()
    {
        var svc = CreateService(10.0, 10.0);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0), new(100, 100) }
        };
        Assert.Equal(20.0, svc.MeasureLength(markup), 6);
    }

    [Fact]
    public void MeasureArea_Square()
    {
        var svc = CreateService(10.0, 10.0);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                new(0, 0), new(100, 0), new(100, 100), new(0, 100)
            }
        };
        // 100/10 = 10 real units per side → area = 100 sq units
        Assert.Equal(100.0, svc.MeasureArea(markup), 6);
    }

    [Fact]
    public void MeasureVolume_AreaTimesDepth()
    {
        var svc = CreateService(10.0, 10.0);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                new(0, 0), new(100, 0), new(100, 100), new(0, 100)
            },
            Metadata = new MarkupMetadata { Depth = 5.0 }
        };
        Assert.Equal(500.0, svc.MeasureVolume(markup), 6);
    }

    [Fact]
    public void MeasureCircleArea()
    {
        var svc = CreateService(1.0, 1.0);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = new List<Point> { new(50, 50) },
            Radius = 10
        };
        Assert.Equal(Math.PI * 100, svc.MeasureCircleArea(markup), 6);
    }

    [Fact]
    public void MeasureAreaWithCutouts()
    {
        var svc = CreateService(1.0, 1.0);
        var outer = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                new(0, 0), new(10, 0), new(10, 10), new(0, 10)
            }
        };
        var inner = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                new(2, 2), new(4, 2), new(4, 4), new(2, 4)
            }
        };
        Assert.Equal(96.0, svc.MeasureAreaWithCutouts(outer, new[] { inner }), 6);
    }

    [Fact]
    public void MeasureLengthWithSlope()
    {
        var svc = CreateService(10.0, 10.0);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };
        double result = svc.MeasureLengthWithSlope(markup, 1.0);
        Assert.Equal(10.0 * Math.Sqrt(2), result, 6);
    }

    [Fact]
    public void GetMeasurementSummary_Polyline()
    {
        var svc = CreateService(10.0, 10.0);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };
        Assert.Contains("Length:", svc.GetMeasurementSummary(markup));
    }
}

public class MarkupToolStateMachineTests
{
    [Fact]
    public void InitialState_IsIdle_Select()
    {
        var sm = new MarkupToolStateMachine();
        Assert.Equal(MarkupToolType.Select, sm.ActiveTool);
        Assert.Equal(ToolState.Idle, sm.State);
    }

    [Fact]
    public void SetTool_ChangesActiveTool()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Polyline);
        Assert.Equal(MarkupToolType.Polyline, sm.ActiveTool);
    }

    [Fact]
    public void Polyline_Click_EntersDrawing()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Polyline);
        sm.OnClick(new Point(10, 10));
        Assert.Equal(ToolState.Drawing, sm.State);
        Assert.Single(sm.PendingVertices);
    }

    [Fact]
    public void Polyline_FinishShape_WithTwoPoints_ReturnsRecord()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Polyline);
        sm.OnClick(new Point(0, 0));
        sm.OnClick(new Point(100, 0));
        var record = sm.FinishShape();
        Assert.NotNull(record);
        Assert.Equal(MarkupType.Polyline, record.Type);
        Assert.Equal(2, record.Vertices.Count);
    }

    [Fact]
    public void Polyline_FinishShape_OnePoint_ReturnsNull()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Polyline);
        sm.OnClick(new Point(0, 0));
        Assert.Null(sm.FinishShape());
    }

    [Fact]
    public void Rectangle_TwoClicks_ReturnsRecord()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Rectangle);
        sm.OnClick(new Point(0, 0));
        var record = sm.OnClick(new Point(100, 100));
        Assert.NotNull(record);
        Assert.Equal(MarkupType.Rectangle, record.Type);
    }

    [Fact]
    public void Circle_TwoClicks_ReturnsRecordWithRadius()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Circle);
        sm.OnClick(new Point(50, 50));
        var record = sm.OnClick(new Point(50, 100));
        Assert.NotNull(record);
        Assert.Equal(MarkupType.Circle, record.Type);
        Assert.Equal(50.0, record.Radius, 6);
    }

    [Fact]
    public void Text_SingleClick_ReturnsRecord()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Text);
        var record = sm.OnClick(new Point(10, 20));
        Assert.NotNull(record);
        Assert.Equal(MarkupType.Text, record.Type);
        Assert.Equal("Text", record.TextContent);
    }

    [Fact]
    public void Dimension_TwoClicks_ReturnsRecord()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Dimension);
        sm.OnClick(new Point(0, 0));
        var record = sm.OnClick(new Point(100, 0));
        Assert.NotNull(record);
        Assert.Equal(MarkupType.Dimension, record.Type);
        Assert.Equal(2, record.Vertices.Count);
    }

    [Fact]
    public void Cancel_ResetsState()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Polyline);
        sm.OnClick(new Point(0, 0));
        sm.Cancel();
        Assert.Equal(ToolState.Idle, sm.State);
        Assert.Empty(sm.PendingVertices);
    }

    [Fact]
    public void Polygon_FinishShape_ThreePoints_ReturnsRecord()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.Polygon);
        sm.OnClick(new Point(0, 0));
        sm.OnClick(new Point(100, 0));
        sm.OnClick(new Point(50, 100));
        var record = sm.FinishShape();
        Assert.NotNull(record);
        Assert.Equal(MarkupType.Polygon, record.Type);
        Assert.Equal(3, record.Vertices.Count);
    }

    [Fact]
    public void ConduitRun_FinishShape_ReturnsConduitRunType()
    {
        var sm = new MarkupToolStateMachine();
        sm.SetTool(MarkupToolType.ConduitRun);
        sm.OnClick(new Point(0, 0));
        sm.OnClick(new Point(100, 0));
        var record = sm.FinishShape();
        Assert.NotNull(record);
        Assert.Equal(MarkupType.ConduitRun, record.Type);
    }
}

public class MarkupPersistenceServiceTests
{
    [Fact]
    public void Json_RoundTrip()
    {
        var svc = new MarkupPersistenceService();
        var markups = new List<MarkupRecord>
        {
            new()
            {
                Type = MarkupType.Polyline,
                Vertices = new List<Point> { new(0, 0), new(100, 0), new(100, 100) },
                Metadata = new MarkupMetadata { Label = "Run 1", Depth = 2.5 }
            },
            new()
            {
                Type = MarkupType.Circle,
                Vertices = new List<Point> { new(50, 50) },
                Radius = 25,
                Metadata = new MarkupMetadata { Label = "Junction" }
            }
        };

        var json = svc.SerializeToJson(markups);
        var loaded = svc.DeserializeFromJson(json);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(MarkupType.Polyline, loaded[0].Type);
        Assert.Equal(3, loaded[0].Vertices.Count);
        Assert.Equal("Run 1", loaded[0].Metadata.Label);
        Assert.Equal(2.5, loaded[0].Metadata.Depth);
        Assert.Equal(MarkupType.Circle, loaded[1].Type);
        Assert.Equal(25, loaded[1].Radius);
    }

    [Fact]
    public void Xml_RoundTrip()
    {
        var svc = new MarkupPersistenceService();
        var markups = new List<MarkupRecord>
        {
            new()
            {
                Type = MarkupType.Polygon,
                Vertices = new List<Point> { new(0, 0), new(10, 0), new(10, 10) },
                Appearance = new MarkupAppearance { StrokeColor = "#00FF00", StrokeWidth = 3.0 },
                Metadata = new MarkupMetadata { Label = "Area 1", Subject = "Floor", Author = "JD" }
            }
        };

        var xml = svc.SerializeToXml(markups);
        var loaded = svc.DeserializeFromXml(xml);

        Assert.Single(loaded);
        Assert.Equal(MarkupType.Polygon, loaded[0].Type);
        Assert.Equal(3, loaded[0].Vertices.Count);
        Assert.Equal("#00FF00", loaded[0].Appearance.StrokeColor);
        Assert.Equal(3.0, loaded[0].Appearance.StrokeWidth);
        Assert.Equal("Area 1", loaded[0].Metadata.Label);
        Assert.Equal("JD", loaded[0].Metadata.Author);
    }

    [Fact]
    public void Json_EmptyList_RoundTrip()
    {
        var svc = new MarkupPersistenceService();
        var json = svc.SerializeToJson(new List<MarkupRecord>());
        var loaded = svc.DeserializeFromJson(json);
        Assert.Empty(loaded);
    }
}

public class MarkupListServiceTests
{
    private static (MarkupListService Service, List<MarkupRecord> Markups) CreateTestData()
    {
        var transform = new CoordinateTransformService
        {
            DocUnitsPerRealX = 1.0,
            DocUnitsPerRealY = 1.0
        };
        var measurement = new MeasurementService(transform);
        var svc = new MarkupListService(measurement);
        var markups = new List<MarkupRecord>
        {
            new()
            {
                Type = MarkupType.Polyline,
                Vertices = new List<Point> { new(0, 0), new(100, 0) },
                LayerId = "layer-1",
                Metadata = new MarkupMetadata { Label = "Run A" }
            },
            new()
            {
                Type = MarkupType.Polygon,
                Vertices = new List<Point> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) },
                LayerId = "layer-2",
                Metadata = new MarkupMetadata { Label = "Area B" }
            },
            new()
            {
                Type = MarkupType.Polyline,
                Vertices = new List<Point> { new(0, 0), new(50, 0) },
                LayerId = "layer-1",
                Metadata = new MarkupMetadata { Label = "Run C" }
            }
        };
        return (svc, markups);
    }

    [Fact]
    public void FilterByType_ReturnsCorrectItems()
    {
        var (svc, markups) = CreateTestData();
        var result = svc.FilterByType(markups, MarkupType.Polyline).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterByLayer_ReturnsCorrectItems()
    {
        var (svc, markups) = CreateTestData();
        var result = svc.FilterByLayer(markups, "layer-2").ToList();
        Assert.Single(result);
    }

    [Fact]
    public void FilterByLabel_CaseInsensitive()
    {
        var (svc, markups) = CreateTestData();
        var result = svc.FilterByLabel(markups, "run").ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SortByLabel_Ascending()
    {
        var (svc, markups) = CreateTestData();
        var sorted = svc.SortByLabel(markups).ToList();
        Assert.Equal("Area B", sorted[0].Metadata.Label);
        Assert.Equal("Run A", sorted[1].Metadata.Label);
        Assert.Equal("Run C", sorted[2].Metadata.Label);
    }

    [Fact]
    public void ExportCsv_ContainsHeaders()
    {
        var (svc, markups) = CreateTestData();
        var csv = svc.ExportCsv(markups);
        Assert.Contains("Id,Type,Label", csv);
        Assert.Contains("Run A", csv);
    }

    [Fact]
    public void ExportGroupedBom_GroupsByTypeAndLabel()
    {
        var (svc, markups) = CreateTestData();
        var bom = svc.ExportGroupedBom(markups);
        Assert.Contains("Item,Type,Label,Quantity", bom);
    }
}

public class DetailLevelServiceTests
{
    [Fact]
    public void Coarse_Polyline_HasSymbolicLines()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0), new(100, 100) }
        };
        var rep = DetailLevelService.GetRepresentation(markup, DetailLevel.Coarse);
        Assert.Equal(2, rep.SymbolicLines.Count);
        Assert.False(rep.ShowFill);
    }

    [Fact]
    public void Fine_Polygon_HasFill()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point> { new(0, 0), new(100, 0), new(50, 100) }
        };
        var rep = DetailLevelService.GetRepresentation(markup, DetailLevel.Fine);
        Assert.True(rep.ShowFill);
        Assert.True(rep.ShowAnnotations);
    }
}

public class GridServiceTests
{
    [Fact]
    public void SnapToGrid_SnapsToNearestIntersection()
    {
        var svc = new GridService { GridSpacing = 10.0, IsEnabled = true };
        var snapped = svc.SnapToGrid(new Point(13, 27));
        Assert.Equal(10.0, snapped.X);
        Assert.Equal(30.0, snapped.Y);
    }

    [Fact]
    public void SnapToGrid_Disabled_ReturnsSamePoint()
    {
        var svc = new GridService { IsEnabled = false };
        var pt = new Point(13, 27);
        var snapped = svc.SnapToGrid(pt);
        Assert.Equal(pt.X, snapped.X);
        Assert.Equal(pt.Y, snapped.Y);
    }

    [Fact]
    public void GetGridLines_ReturnsLinesInViewport()
    {
        var svc = new GridService { GridSpacing = 10.0 };
        var (vertical, horizontal) = svc.GetGridLines(new Rect(0, 0, 50, 50));
        Assert.True(vertical.Count >= 5);
        Assert.True(horizontal.Count >= 5);
    }
}

public class MarkupUndoRedoServiceTests
{
    [Fact]
    public void Execute_AddsToUndoStack()
    {
        var svc = new MarkupUndoRedoService();
        var markups = new List<MarkupRecord>();
        var markup = new MarkupRecord { Type = MarkupType.Polyline };

        svc.Execute(new AddMarkupAction(markups, markup));

        Assert.Single(markups);
        Assert.True(svc.CanUndo);
    }

    [Fact]
    public void Undo_RemovesMarkup()
    {
        var svc = new MarkupUndoRedoService();
        var markups = new List<MarkupRecord>();
        var markup = new MarkupRecord { Type = MarkupType.Polyline };

        svc.Execute(new AddMarkupAction(markups, markup));
        svc.Undo();

        Assert.Empty(markups);
        Assert.True(svc.CanRedo);
    }

    [Fact]
    public void Redo_RestoresMarkup()
    {
        var svc = new MarkupUndoRedoService();
        var markups = new List<MarkupRecord>();
        var markup = new MarkupRecord { Type = MarkupType.Polyline };

        svc.Execute(new AddMarkupAction(markups, markup));
        svc.Undo();
        svc.Redo();

        Assert.Single(markups);
    }

    [Fact]
    public void MoveVertices_UndoRestoresOriginal()
    {
        var svc = new MarkupUndoRedoService();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = new List<Point> { new(0, 0), new(100, 0) }
        };

        var oldVerts = new List<Point>(markup.Vertices);
        var newVerts = new List<Point> { new(10, 10), new(110, 10) };

        svc.Execute(new MoveMarkupVerticesAction(markup, oldVerts, newVerts));
        Assert.Equal(10, markup.Vertices[0].X);

        svc.Undo();
        Assert.Equal(0, markup.Vertices[0].X);
    }
}

public class AngleConstraintHelperTests
{
    [Fact]
    public void Constrain45_NearHorizontal_SnapsHorizontal()
    {
        var result = AngleConstraintHelper.Constrain45(
            new Point(0, 0), new Point(100, 5));
        Assert.Equal(0.0, result.Y, 1);
    }

    [Fact]
    public void ConstrainOrtho_MoreHorizontal_KeepsXChangesY()
    {
        var result = AngleConstraintHelper.ConstrainOrtho(
            new Point(0, 0), new Point(100, 30));
        Assert.Equal(100, result.X);
        Assert.Equal(0, result.Y);
    }

    [Fact]
    public void ConstrainOrtho_MoreVertical_KeepsYChangesX()
    {
        var result = AngleConstraintHelper.ConstrainOrtho(
            new Point(0, 0), new Point(30, 100));
        Assert.Equal(0, result.X);
        Assert.Equal(100, result.Y);
    }

    [Fact]
    public void ApplyConstraint_NoShift_ReturnsOriginal()
    {
        var free = new Point(73, 42);
        var result = AngleConstraintHelper.ApplyConstraint(
            new Point(0, 0), free, shiftHeld: false);
        Assert.Equal(free.X, result.X);
        Assert.Equal(free.Y, result.Y);
    }

    [Fact]
    public void ApplyConstraint_WithShift_SnapsAngle()
    {
        var result = AngleConstraintHelper.ApplyConstraint(
            new Point(0, 0), new Point(100, 5), shiftHeld: true);
        Assert.Equal(0.0, result.Y, 1); // near horizontal → snaps to 0°
    }
}

public class RenderingArchitectureTests
{
    [Fact]
    public void DirtyRectTracker_MarkDirty_HasDirtyRegions()
    {
        var tracker = new DirtyRectTracker();
        Assert.False(tracker.HasDirtyRegions);

        tracker.MarkDirty(new Rect(10, 10, 50, 50));
        Assert.True(tracker.HasDirtyRegions);
    }

    [Fact]
    public void DirtyRectTracker_Flush_ClearsRegions()
    {
        var tracker = new DirtyRectTracker();
        tracker.MarkDirty(new Rect(10, 10, 50, 50));
        var rects = tracker.FlushDirtyRects();
        Assert.Single(rects);
        Assert.False(tracker.HasDirtyRegions);
    }

    [Fact]
    public void DirtyRectTracker_GetDirtyBounds_UnionOfAll()
    {
        var tracker = new DirtyRectTracker();
        tracker.MarkDirty(new Rect(0, 0, 10, 10));
        tracker.MarkDirty(new Rect(50, 50, 10, 10));
        var bounds = tracker.GetDirtyBounds();
        Assert.Equal(0, bounds.Left);
        Assert.Equal(0, bounds.Top);
        Assert.Equal(60, bounds.Right);
        Assert.Equal(60, bounds.Bottom);
    }

    [Fact]
    public void TileCacheService_Initialize_SetsGrid()
    {
        var svc = new TileCacheService { TileSize = 256 };
        svc.Initialize(1024, 768);
        var (cols, rows) = svc.GetGridSize();
        Assert.Equal(4, cols);
        Assert.Equal(3, rows);
    }

    [Fact]
    public void TileCacheService_MarkAndCheckValid()
    {
        var svc = new TileCacheService { TileSize = 256 };
        svc.Initialize(512, 512);
        Assert.False(svc.IsTileValid(0, 0));
        svc.MarkTileValid(0, 0);
        Assert.True(svc.IsTileValid(0, 0));
    }

    [Fact]
    public void TileCacheService_InvalidateAll()
    {
        var svc = new TileCacheService { TileSize = 256 };
        svc.Initialize(512, 512);
        svc.MarkTileValid(0, 0);
        svc.InvalidateAll();
        Assert.False(svc.IsTileValid(0, 0));
    }

    [Fact]
    public void TileCacheService_GetVisibleTiles_ReturnsSubset()
    {
        var svc = new TileCacheService { TileSize = 256 };
        svc.Initialize(1024, 1024);
        var tiles = svc.GetVisibleTiles(new Rect(0, 0, 300, 300)).ToList();
        Assert.Equal(4, tiles.Count); // 2x2 tiles visible
    }
}

public class Markup3DGeneratorTests
{
    [Fact]
    public void GenerateConduitMesh_TwoPointPolyline_CreatesMesh()
    {
        var transform = new CoordinateTransformService
        {
            DocUnitsPerRealX = 1.0,
            DocUnitsPerRealY = 1.0
        };
        var gen = new Markup3DGenerator(transform);
        var markup = new MarkupRecord
        {
            Type = MarkupType.ConduitRun,
            Vertices = new List<Point> { new(0, 0), new(10, 0) }
        };
        var mesh = gen.Generate3DMesh(markup);
        Assert.False(mesh.IsEmpty);
        Assert.True(mesh.Positions.Count > 0);
        Assert.True(mesh.TriangleIndices.Count > 0);
    }

    [Fact]
    public void GenerateExtrudedPolygon_Rectangle_CreatesMesh()
    {
        var transform = new CoordinateTransformService
        {
            DocUnitsPerRealX = 1.0,
            DocUnitsPerRealY = 1.0
        };
        var gen = new Markup3DGenerator(transform);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Vertices = new List<Point> { new(0, 0), new(10, 10) }
        };
        var mesh = gen.Generate3DMesh(markup);
        Assert.False(mesh.IsEmpty);
    }

    [Fact]
    public void GenerateCylinder_Circle_CreatesMesh()
    {
        var transform = new CoordinateTransformService
        {
            DocUnitsPerRealX = 1.0,
            DocUnitsPerRealY = 1.0
        };
        var gen = new Markup3DGenerator(transform);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = new List<Point> { new(5, 5) },
            Radius = 3
        };
        var mesh = gen.Generate3DMesh(markup);
        Assert.False(mesh.IsEmpty);
    }

    [Fact]
    public void GenerateText_ReturnsEmptyMesh()
    {
        var transform = new CoordinateTransformService();
        var gen = new Markup3DGenerator(transform);
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Vertices = new List<Point> { new(0, 0) }
        };
        var mesh = gen.Generate3DMesh(markup);
        Assert.True(mesh.IsEmpty);
    }
}

public class MarkupRecordTests
{
    [Fact]
    public void Constructor_GeneratesId()
    {
        var record = new MarkupRecord();
        Assert.False(string.IsNullOrEmpty(record.Id));
    }

    [Fact]
    public void UpdateBoundingRect_Polygon()
    {
        var record = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                new(10, 20), new(100, 20), new(100, 80), new(10, 80)
            }
        };
        record.UpdateBoundingRect();
        Assert.Equal(10, record.BoundingRect.Left);
        Assert.Equal(20, record.BoundingRect.Top);
        Assert.Equal(90, record.BoundingRect.Width);
        Assert.Equal(60, record.BoundingRect.Height);
    }

    [Fact]
    public void UpdateBoundingRect_Circle()
    {
        var record = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = new List<Point> { new(50, 50) },
            Radius = 25
        };
        record.UpdateBoundingRect();
        Assert.Equal(25, record.BoundingRect.Left);
        Assert.Equal(25, record.BoundingRect.Top);
        Assert.Equal(50, record.BoundingRect.Width);
        Assert.Equal(50, record.BoundingRect.Height);
    }

    [Fact]
    public void UpdateBoundingRect_Empty_ReturnsEmptyRect()
    {
        var record = new MarkupRecord { Type = MarkupType.Polyline };
        record.UpdateBoundingRect();
        Assert.Equal(Rect.Empty, record.BoundingRect);
    }
}

public class NullPdfGeometryProviderTests
{
    [Fact]
    public void IsLoaded_ReturnsFalse()
    {
        var provider = new NullPdfGeometryProvider();
        Assert.False(provider.IsLoaded);
    }

    [Fact]
    public void GetEndpoints_ReturnsEmpty()
    {
        var provider = new NullPdfGeometryProvider();
        Assert.Empty(provider.GetEndpoints());
    }

    [Fact]
    public void GetSegments_ReturnsEmpty()
    {
        var provider = new NullPdfGeometryProvider();
        Assert.Empty(provider.GetSegments());
    }

    [Fact]
    public void GetPageSize_ReturnsLetterSize()
    {
        var provider = new NullPdfGeometryProvider();
        var (w, h) = provider.GetPageSize();
        Assert.Equal(612, w);
        Assert.Equal(792, h);
    }
}
