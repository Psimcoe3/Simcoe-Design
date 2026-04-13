using ElectricalComponentSandbox.Services;
using Xunit;
using static ElectricalComponentSandbox.Services.ReceptacleSpacingService;

namespace ElectricalComponentSandbox.Tests.Services;

public class ReceptacleSpacingServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static WallSegment Wall(string id, double length, bool isDoor = false, bool isCountertop = false) =>
        new() { Id = id, StartX = 0, StartY = 0, EndX = length, EndY = 0, IsDoorway = isDoor, IsCountertop = isCountertop };

    private static ReceptaclePosition Recep(string id, string wallId, double x = 3, bool gfci = false) =>
        new() { Id = id, WallSegmentId = wallId, X = x, Y = 0, IsGFCI = gfci };

    // ── General Room — NEC 210.52(A) ─────────────────────────────────────────

    [Fact]
    public void General_WallWithReceptacle_Compliant()
    {
        var walls = new[] { Wall("W1", 10) };
        var receps = new[] { Recep("R1", "W1") };

        var result = CheckCompliance(RoomType.General, walls, receps);

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void General_WallOver2Ft_NoReceptacle_Violation()
    {
        var walls = new[] { Wall("W1", 8) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.General, walls, receps);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.52(A)(1)");
    }

    [Fact]
    public void General_WallUnder2Ft_NoReceptacleRequired()
    {
        var walls = new[] { Wall("W1", 1.5) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.General, walls, receps);

        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void General_Doorway_Ignored()
    {
        var walls = new[] { Wall("DOOR", 3, isDoor: true) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.General, walls, receps);

        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void General_LongWall_NeedsMultipleReceptacles()
    {
        var walls = new[] { Wall("W1", 14) }; // >12 ft needs 2 receptacles
        var receps = new[] { Recep("R1", "W1") };

        var result = CheckCompliance(RoomType.General, walls, receps);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Description.Contains("6 ft rule"));
    }

    [Fact]
    public void General_SuggestsReceptacleAtMidpoint()
    {
        var walls = new[] { Wall("W1", 8) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.General, walls, receps);
        var v = result.Violations[0];

        Assert.Equal(4.0, v.SuggestedX); // midpoint of 0-8
    }

    // ── Kitchen — NEC 210.52(C) ──────────────────────────────────────────────

    [Fact]
    public void Kitchen_CountertopWithReceptacle_Compliant()
    {
        var walls = new[] { Wall("CT1", 3, isCountertop: true) };
        var receps = new[] { Recep("R1", "CT1") };

        var result = CheckCompliance(RoomType.Kitchen, walls, receps);

        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void Kitchen_CountertopNoReceptacle_Violation()
    {
        var walls = new[] { Wall("CT1", 3, isCountertop: true) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.Kitchen, walls, receps);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.52(C)(1)");
    }

    [Fact]
    public void Kitchen_LongCountertop_Needs4FtSpacing()
    {
        var walls = new[] { Wall("CT1", 9, isCountertop: true) }; // 9ft → needs ceil(9/4)=3
        var receps = new[] { Recep("R1", "CT1"), Recep("R2", "CT1") }; // only 2

        var result = CheckCompliance(RoomType.Kitchen, walls, receps);

        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.52(C)(1)" && v.Description.Contains("4 ft rule"));
    }

    [Fact]
    public void Kitchen_MinimumRequired_AtLeast2()
    {
        var walls = new[] { Wall("W1", 3) };
        int min = CalculateMinimumReceptacles(RoomType.Kitchen, walls);

        Assert.True(min >= 2); // 2 small-appliance circuits minimum
    }

    // ── Bathroom — NEC 210.52(D) ─────────────────────────────────────────────

    [Fact]
    public void Bathroom_WithGFCI_Compliant()
    {
        var walls = new[] { Wall("W1", 4) };
        var receps = new[] { Recep("R1", "W1", gfci: true) };

        var result = CheckCompliance(RoomType.Bathroom, walls, receps);

        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void Bathroom_NoReceptacle_Violation()
    {
        var walls = new[] { Wall("W1", 4) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.Bathroom, walls, receps);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.52(D)");
    }

    [Fact]
    public void Bathroom_NonGFCI_Violation()
    {
        var walls = new[] { Wall("W1", 4) };
        var receps = new[] { Recep("R1", "W1", gfci: false) };

        var result = CheckCompliance(RoomType.Bathroom, walls, receps);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.11(C)(3)");
    }

    // ── Garage — NEC 210.52(G) ───────────────────────────────────────────────

    [Fact]
    public void Garage_WithReceptacle_Compliant()
    {
        var walls = new[] { Wall("W1", 20) };
        var receps = new[] { Recep("R1", "W1") };

        var result = CheckCompliance(RoomType.Garage, walls, receps);

        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void Garage_NoReceptacle_Violation()
    {
        var walls = new[] { Wall("W1", 20) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.Garage, walls, receps);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.52(G)(1)");
    }

    // ── Outdoor — NEC 210.52(E) ──────────────────────────────────────────────

    [Fact]
    public void Outdoor_WithReceptacle_Compliant()
    {
        var walls = Array.Empty<WallSegment>();
        var receps = new[] { Recep("R1", "") };

        var result = CheckCompliance(RoomType.Outdoor, walls, receps);

        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void Outdoor_NoReceptacle_Violation()
    {
        var result = CheckCompliance(RoomType.Outdoor, Array.Empty<WallSegment>(), Array.Empty<ReceptaclePosition>());

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.52(E)");
    }

    // ── Laundry — NEC 210.52(F) ──────────────────────────────────────────────

    [Fact]
    public void Laundry_WithReceptacle_Compliant()
    {
        var receps = new[] { Recep("R1", "W1") };
        var result = CheckCompliance(RoomType.Laundry, Array.Empty<WallSegment>(), receps);

        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void Laundry_NoReceptacle_Violation()
    {
        var result = CheckCompliance(RoomType.Laundry, Array.Empty<WallSegment>(), Array.Empty<ReceptaclePosition>());

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule == "NEC 210.52(F)");
    }

    // ── CalculateMinimumReceptacles ──────────────────────────────────────────

    [Theory]
    [InlineData(12, 1)]     // 12 ft → ceil(12/12)=1
    [InlineData(24, 2)]     // 24 ft → 2
    [InlineData(36, 3)]     // 36 ft → 3
    [InlineData(1, 1)]      // always at least 1
    public void CalculateMinimum_GeneralRoom(double wallLength, int expected)
    {
        var walls = new[] { Wall("W1", wallLength) };
        int min = CalculateMinimumReceptacles(RoomType.General, walls);

        Assert.Equal(expected, min);
    }

    [Fact]
    public void CalculateMinimum_ExcludesDoorways()
    {
        var walls = new[] { Wall("W1", 12), Wall("D1", 3, isDoor: true) };
        int min = CalculateMinimumReceptacles(RoomType.General, walls);

        Assert.Equal(1, min); // only 12 ft usable, not 15
    }

    // ── Hallway ──────────────────────────────────────────────────────────────

    [Fact]
    public void Hallway_TreatedSameAsGeneral()
    {
        var walls = new[] { Wall("H1", 10) };
        var receps = Array.Empty<ReceptaclePosition>();

        var result = CheckCompliance(RoomType.Hallway, walls, receps);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.Rule.Contains("210.52"));
    }
}
