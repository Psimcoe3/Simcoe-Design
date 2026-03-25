using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class ArrayServiceTests
{
    private readonly ArrayService _service = new();

    // ── Rectangular Array ───────────────────────────────────────────────────

    [Fact]
    public void RectangularArray_2x3_Returns5Placements()
    {
        var source = new Point3D(0, 0, 0);
        var rotation = new Vector3D(0, 0, 0);
        var p = new RectangularArrayParams { Rows = 2, Columns = 3 };

        var result = _service.ComputeRectangularArray(source, rotation, p);

        // 2 rows * 3 columns = 6 total, minus 1 source = 5
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void RectangularArray_1x1_ReturnsEmpty()
    {
        var source = new Point3D(10, 5, 3);
        var rotation = new Vector3D(0, 45, 0);
        var p = new RectangularArrayParams { Rows = 1, Columns = 1 };

        var result = _service.ComputeRectangularArray(source, rotation, p);

        Assert.Empty(result);
    }

    [Fact]
    public void RectangularArray_WithLevels_2x2x2_Returns7Placements()
    {
        var source = new Point3D(0, 0, 0);
        var rotation = new Vector3D(0, 0, 0);
        var p = new RectangularArrayParams
        {
            Rows = 2,
            Columns = 2,
            Levels = 2,
            LevelSpacing = 10.0
        };

        var result = _service.ComputeRectangularArray(source, rotation, p);

        // 2 * 2 * 2 = 8 total, minus 1 source = 7
        Assert.Equal(7, result.Count);
    }

    [Fact]
    public void RectangularArray_SpacingApplied_CorrectPositions()
    {
        var source = new Point3D(100, 0, 200);
        var rotation = new Vector3D(0, 90, 0);
        var p = new RectangularArrayParams
        {
            Rows = 2,
            Columns = 2,
            ColumnSpacing = 10.0,
            RowSpacing = 20.0
        };

        var result = _service.ComputeRectangularArray(source, rotation, p);

        Assert.Equal(3, result.Count);

        // (row=0, col=1): X += 10, Z unchanged
        var col1 = result.First(r =>
            Math.Abs(r.Position.X - 110) < 0.001 &&
            Math.Abs(r.Position.Z - 200) < 0.001);
        Assert.NotNull(col1);
        Assert.Equal(new Vector3D(0, 90, 0), col1.Rotation);

        // (row=1, col=0): X unchanged, Z += 20
        var row1 = result.First(r =>
            Math.Abs(r.Position.X - 100) < 0.001 &&
            Math.Abs(r.Position.Z - 220) < 0.001);
        Assert.NotNull(row1);

        // (row=1, col=1): X += 10, Z += 20
        var corner = result.First(r =>
            Math.Abs(r.Position.X - 110) < 0.001 &&
            Math.Abs(r.Position.Z - 220) < 0.001);
        Assert.NotNull(corner);
    }

    // ── Polar Array ─────────────────────────────────────────────────────────

    [Fact]
    public void PolarArray_4Items_360Degrees_Returns3Placements()
    {
        var source = new Point3D(10, 0, 0);
        var rotation = new Vector3D(0, 0, 0);
        var p = new PolarArrayParams
        {
            Center = new Point3D(0, 0, 0),
            Count = 4,
            TotalAngleDegrees = 360.0
        };

        var result = _service.ComputePolarArray(source, rotation, p);

        // 4 items total, minus 1 source = 3
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void PolarArray_RotateItems_AddsYRotation()
    {
        var source = new Point3D(10, 0, 0);
        var rotation = new Vector3D(0, 0, 0);
        var p = new PolarArrayParams
        {
            Center = new Point3D(0, 0, 0),
            Count = 4,
            TotalAngleDegrees = 360.0,
            RotateItems = true
        };

        var result = _service.ComputePolarArray(source, rotation, p);

        // Angle increment = 360 / 4 = 90 degrees
        // Item 1 should have Y rotation = 90
        Assert.Equal(90.0, result[0].Rotation.Y, 5);
        // Item 2 should have Y rotation = 180
        Assert.Equal(180.0, result[1].Rotation.Y, 5);
        // Item 3 should have Y rotation = 270
        Assert.Equal(270.0, result[2].Rotation.Y, 5);
    }

    [Fact]
    public void PolarArray_PartialAngle_180Degrees_CorrectPositions()
    {
        // Source is at (10, 0, 0), center at origin, 180-degree arc with 3 items
        var source = new Point3D(10, 0, 0);
        var rotation = new Vector3D(0, 0, 0);
        var p = new PolarArrayParams
        {
            Center = new Point3D(0, 0, 0),
            Count = 3,
            TotalAngleDegrees = 180.0,
            RotateItems = false
        };

        var result = _service.ComputePolarArray(source, rotation, p);

        // 3 items total, minus source = 2
        Assert.Equal(2, result.Count);

        // Angle increment = 180 / 3 = 60 degrees
        // Item 1: 60 degrees around Y axis on XZ plane
        // X = 10 * cos(60) = 5,  Z = 10 * sin(60) = 8.660...
        Assert.Equal(5.0, result[0].Position.X, 3);
        Assert.Equal(8.660, result[0].Position.Z, 3);

        // Item 2: 120 degrees
        // X = 10 * cos(120) = -5,  Z = 10 * sin(120) = 8.660...
        Assert.Equal(-5.0, result[1].Position.X, 3);
        Assert.Equal(8.660, result[1].Position.Z, 3);

        // RotateItems is false, so rotation stays at source rotation
        Assert.Equal(new Vector3D(0, 0, 0), result[0].Rotation);
        Assert.Equal(new Vector3D(0, 0, 0), result[1].Rotation);
    }

    [Fact]
    public void PolarArray_1Count_ReturnsEmpty()
    {
        var source = new Point3D(5, 0, 5);
        var rotation = new Vector3D(0, 45, 0);
        var p = new PolarArrayParams
        {
            Center = new Point3D(0, 0, 0),
            Count = 1,
            TotalAngleDegrees = 360.0
        };

        var result = _service.ComputePolarArray(source, rotation, p);

        Assert.Empty(result);
    }
}
