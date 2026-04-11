using System.Windows;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class HomeRunWireServiceTests
{
    private readonly HomeRunWireService _sut = new();

    // ── CreateHomeRun ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateHomeRun_ValidTwoPoints_ReturnsWireWithVertices()
    {
        var pts = new[] { new Point(0, 0), new Point(100, 0) };

        var wire = _sut.CreateHomeRun("cid", "pid", pts);

        Assert.Equal("cid", wire.CircuitId);
        Assert.Equal("pid", wire.PanelId);
        Assert.Equal(2, wire.Vertices.Count);
        Assert.Equal(WiringStyle.Chamfer, wire.WiringStyle);
    }

    [Fact]
    public void CreateHomeRun_ThreeVertices_PolylineStored()
    {
        var pts = new[] { new Point(0, 0), new Point(50, 0), new Point(50, 80) };

        var wire = _sut.CreateHomeRun("c1", "p1", pts, WiringStyle.Arc);

        Assert.Equal(3, wire.Vertices.Count);
        Assert.Equal(WiringStyle.Arc, wire.WiringStyle);
    }

    [Fact]
    public void CreateHomeRun_OnePointOnly_ThrowsArgumentException()
    {
        var pts = new[] { new Point(0, 0) };

        Assert.Throws<ArgumentException>(() => _sut.CreateHomeRun("c", "p", pts));
    }

    [Fact]
    public void CreateHomeRun_CoincidentPoints_ThrowsArgumentException()
    {
        var pts = new[] { new Point(5, 5), new Point(5, 5) };

        Assert.Throws<ArgumentException>(() => _sut.CreateHomeRun("c", "p", pts));
    }

    [Fact]
    public void CreateHomeRun_NullVertices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.CreateHomeRun("c", "p", null!));
    }

    // ── ValidateVertices ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateVertices_TwoDistinctPoints_ReturnsTrue()
    {
        Assert.True(_sut.ValidateVertices(
            new[] { new Point(0, 0), new Point(1, 0) }));
    }

    [Fact]
    public void ValidateVertices_OnePoint_ReturnsFalse()
    {
        Assert.False(_sut.ValidateVertices(new[] { new Point(0, 0) }));
    }

    [Fact]
    public void ValidateVertices_EmptyList_ReturnsFalse()
    {
        Assert.False(_sut.ValidateVertices(Array.Empty<Point>()));
    }

    [Fact]
    public void ValidateVertices_AdjacentCoincidentPoints_ReturnsFalse()
    {
        var pts = new[] { new Point(0, 0), new Point(10, 10), new Point(10, 10) };
        Assert.False(_sut.ValidateVertices(pts));
    }

    // ── AppendVertex ──────────────────────────────────────────────────────────

    [Fact]
    public void AppendVertex_AddsToEnd()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(50, 0) });

        _sut.AppendVertex(wire, new Point(50, 80));

        Assert.Equal(3, wire.Vertices.Count);
        Assert.Equal(new Point(50, 80), wire.Vertices[^1]);
    }

    [Fact]
    public void AppendVertex_CoincidentWithLast_ThrowsArgumentException()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(50, 0) });

        Assert.Throws<ArgumentException>(() => _sut.AppendVertex(wire, new Point(50, 0)));
    }

    // ── InsertVertex ──────────────────────────────────────────────────────────

    [Fact]
    public void InsertVertex_MiddleIndex_InsertsBetweenExisting()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(100, 0) });

        _sut.InsertVertex(wire, 1, new Point(50, 0));

        Assert.Equal(3, wire.Vertices.Count);
        Assert.Equal(new Point(50, 0), wire.Vertices[1]);
    }

    [Fact]
    public void InsertVertex_CoincidentWithPreceding_ThrowsArgumentException()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(100, 0) });

        Assert.Throws<ArgumentException>(() => _sut.InsertVertex(wire, 1, new Point(0, 0)));
    }

    [Fact]
    public void InsertVertex_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(100, 0) });

        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.InsertVertex(wire, 5, new Point(50, 0)));
    }

    // ── RemoveVertex ──────────────────────────────────────────────────────────

    [Fact]
    public void RemoveVertex_MiddleOfThree_LeavesTwo()
    {
        var wire = _sut.CreateHomeRun("c", "p",
            new[] { new Point(0, 0), new Point(50, 0), new Point(100, 0) });

        _sut.RemoveVertex(wire, 1);

        Assert.Equal(2, wire.Vertices.Count);
        Assert.Equal(new Point(0, 0), wire.Vertices[0]);
        Assert.Equal(new Point(100, 0), wire.Vertices[1]);
    }

    [Fact]
    public void RemoveVertex_OnlyTwoVertices_ThrowsInvalidOperationException()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(100, 0) });

        Assert.Throws<InvalidOperationException>(() => _sut.RemoveVertex(wire, 0));
    }

    // ── GetTickMarkPoints ─────────────────────────────────────────────────────

    [Fact]
    public void GetTickMarkPoints_HotConductors2_ReturnsTwoTicks()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(200, 0) });
        wire.HotConductors = 2;

        var ticks = _sut.GetTickMarkPoints(wire);

        Assert.Equal(2, ticks.Count);
    }

    [Fact]
    public void GetTickMarkPoints_HotConductors3_ReturnsThreeTicks()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(200, 0) });
        wire.HotConductors = 3;

        var ticks = _sut.GetTickMarkPoints(wire);

        Assert.Equal(3, ticks.Count);
    }

    [Fact]
    public void GetTickMarkPoints_HorizontalWire_TicksAreVertical()
    {
        // Wire runs along X axis → ticks should be vertical (same X, different Y)
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(200, 0) });
        wire.HotConductors = 1;
        double halfLen = HomeRunWireService.DefaultTickHalfLength;

        var ticks = _sut.GetTickMarkPoints(wire, tickHalfLength: halfLen);

        var (s, e) = ticks[0];
        Assert.Equal(s.X, e.X, precision: 6);        // same X → vertical line
        Assert.Equal(halfLen * 2, e.Y - s.Y, precision: 6); // total length = 2 × halfLen
    }

    [Fact]
    public void GetTickMarkPoints_TickCentresSpacedCorrectly()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(200, 0) });
        wire.HotConductors = 3;
        double spacing = HomeRunWireService.DefaultTickSpacing;

        var ticks = _sut.GetTickMarkPoints(wire, tickSpacing: spacing);

        // Centre of each tick is midpoint of start/end
        double cx0 = (ticks[0].Start.X + ticks[0].End.X) / 2.0;
        double cx1 = (ticks[1].Start.X + ticks[1].End.X) / 2.0;
        double cx2 = (ticks[2].Start.X + ticks[2].End.X) / 2.0;

        Assert.Equal(spacing, cx0, precision: 6);
        Assert.Equal(spacing * 2, cx1, precision: 6);
        Assert.Equal(spacing * 3, cx2, precision: 6);
    }

    [Fact]
    public void GetTickMarkPoints_VerticalWire_TicksAreHorizontal()
    {
        // Wire runs along Y axis → ticks should be horizontal (same Y, different X)
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(0, 200) });
        wire.HotConductors = 1;
        double halfLen = HomeRunWireService.DefaultTickHalfLength;

        var ticks = _sut.GetTickMarkPoints(wire, tickHalfLength: halfLen);

        var (s, e) = ticks[0];
        Assert.Equal(s.Y, e.Y, precision: 6);                 // same Y → horizontal line
        Assert.Equal(halfLen * 2, Math.Abs(e.X - s.X), precision: 6);
    }

    [Fact]
    public void GetTickMarkPoints_FewerThanTwoVertices_ReturnsEmpty()
    {
        var wire = new HomeRunWire { HotConductors = 2 };
        wire.Vertices.Add(new Point(0, 0));

        var ticks = _sut.GetTickMarkPoints(wire);

        Assert.Empty(ticks);
    }

    // ── UpdateVertices ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateVertices_ValidReplacement_ReplacesVertices()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(100, 0) });
        var newPts = new[] { new Point(10, 5), new Point(10, 90), new Point(60, 90) };

        _sut.UpdateVertices(wire, newPts);

        Assert.Equal(3, wire.Vertices.Count);
        Assert.Equal(new Point(10, 5), wire.Vertices[0]);
    }

    [Fact]
    public void UpdateVertices_InvalidSinglePoint_ThrowsArgumentException()
    {
        var wire = _sut.CreateHomeRun("c", "p", new[] { new Point(0, 0), new Point(100, 0) });

        Assert.Throws<ArgumentException>(() => _sut.UpdateVertices(wire, new[] { new Point(50, 0) }));
    }
}
