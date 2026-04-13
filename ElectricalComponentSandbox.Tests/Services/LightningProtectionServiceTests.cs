using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class LightningProtectionServiceTests
{
    private static LightningProtectionService.BuildingParameters DefaultBuilding() => new()
    {
        LengthMeters = 40,
        WidthMeters = 20,
        HeightMeters = 15,
        Occupancy = LightningProtectionService.BuildingOccupancy.Commercial,
        FlashDensity = 4.0,
        IsIsolated = false,
        HasExplosiveContents = false,
        HasElectronicSystems = true,
    };

    // ── Collection Area ──────────────────────────────────────────────────────

    [Fact]
    public void CollectionArea_Positive()
    {
        double area = LightningProtectionService.CalculateCollectionArea(40, 20, 15);
        Assert.True(area > 0);
    }

    [Fact]
    public void CollectionArea_TallerBuilding_LargerArea()
    {
        double short_ = LightningProtectionService.CalculateCollectionArea(40, 20, 5);
        double tall = LightningProtectionService.CalculateCollectionArea(40, 20, 20);
        Assert.True(tall > short_);
    }

    // ── Strike Frequency ─────────────────────────────────────────────────────

    [Fact]
    public void StrikeFrequency_Positive()
    {
        double nd = LightningProtectionService.CalculateStrikeFrequency(10000, 4.0, false);
        Assert.True(nd > 0);
    }

    [Fact]
    public void StrikeFrequency_Isolated_Higher()
    {
        double normal = LightningProtectionService.CalculateStrikeFrequency(10000, 4.0, false);
        double isolated = LightningProtectionService.CalculateStrikeFrequency(10000, 4.0, true);
        Assert.True(isolated > normal);
    }

    // ── Tolerable Risk ───────────────────────────────────────────────────────

    [Fact]
    public void TolerableRisk_HighRisk_Lowest()
    {
        double highRisk = LightningProtectionService.GetTolerableRisk(
            LightningProtectionService.BuildingOccupancy.HighRisk);
        double commercial = LightningProtectionService.GetTolerableRisk(
            LightningProtectionService.BuildingOccupancy.Commercial);
        Assert.True(highRisk < commercial);
    }

    // ── Risk Assessment ──────────────────────────────────────────────────────

    [Fact]
    public void AssessRisk_SmallBuilding_LowDensity_NotRequired()
    {
        var bldg = new LightningProtectionService.BuildingParameters
        {
            LengthMeters = 10, WidthMeters = 8, HeightMeters = 4,
            Occupancy = LightningProtectionService.BuildingOccupancy.Residential,
            FlashDensity = 1.0, IsIsolated = false,
        };
        var result = LightningProtectionService.AssessRisk(bldg);
        Assert.False(result.ProtectionRequired);
    }

    [Fact]
    public void AssessRisk_LargeBuilding_HighDensity_Required()
    {
        var bldg = new LightningProtectionService.BuildingParameters
        {
            LengthMeters = 100, WidthMeters = 50, HeightMeters = 30,
            Occupancy = LightningProtectionService.BuildingOccupancy.Assembly,
            FlashDensity = 10.0, IsIsolated = true,
        };
        var result = LightningProtectionService.AssessRisk(bldg);
        Assert.True(result.ProtectionRequired);
    }

    [Fact]
    public void AssessRisk_Explosive_AlwaysRequired()
    {
        var bldg = new LightningProtectionService.BuildingParameters
        {
            LengthMeters = 10, WidthMeters = 8, HeightMeters = 4,
            Occupancy = LightningProtectionService.BuildingOccupancy.Industrial,
            FlashDensity = 0.5, IsIsolated = false,
            HasExplosiveContents = true,
        };
        var result = LightningProtectionService.AssessRisk(bldg);
        Assert.True(result.ProtectionRequired);
        Assert.Equal(LightningProtectionService.LightningProtectionLevel.I, result.RecommendedLevel);
    }

    [Fact]
    public void AssessRisk_ReturnsJustification()
    {
        var result = LightningProtectionService.AssessRisk(DefaultBuilding());
        Assert.False(string.IsNullOrEmpty(result.Justification));
    }

    // ── Rolling Sphere ───────────────────────────────────────────────────────

    [Fact]
    public void RollingSphere_LevelI_SmallestRadius()
    {
        var lvl1 = LightningProtectionService.GetRollingSphereParameters(
            LightningProtectionService.LightningProtectionLevel.I, 120);
        var lvl4 = LightningProtectionService.GetRollingSphereParameters(
            LightningProtectionService.LightningProtectionLevel.IV, 120);
        Assert.True(lvl1.SphereRadiusMeters < lvl4.SphereRadiusMeters);
    }

    [Fact]
    public void RollingSphere_MinDownConductors_AtLeastTwo()
    {
        var result = LightningProtectionService.GetRollingSphereParameters(
            LightningProtectionService.LightningProtectionLevel.III, 10);
        Assert.True(result.MinDownConductors >= 2);
    }

    [Fact]
    public void RollingSphere_LargePerimeter_MoreDownConductors()
    {
        var small = LightningProtectionService.GetRollingSphereParameters(
            LightningProtectionService.LightningProtectionLevel.II, 50);
        var large = LightningProtectionService.GetRollingSphereParameters(
            LightningProtectionService.LightningProtectionLevel.II, 300);
        Assert.True(large.MinDownConductors > small.MinDownConductors);
    }

    // ── Grounding ────────────────────────────────────────────────────────────

    [Fact]
    public void Grounding_LevelI_LowerResistance()
    {
        var g1 = LightningProtectionService.SpecifyGrounding(400,
            LightningProtectionService.LightningProtectionLevel.I);
        var g4 = LightningProtectionService.SpecifyGrounding(400,
            LightningProtectionService.LightningProtectionLevel.IV);
        Assert.True(g1.TargetResistanceOhms <= g4.TargetResistanceOhms);
    }

    [Fact]
    public void Grounding_MinTwoRods()
    {
        var g = LightningProtectionService.SpecifyGrounding(20,
            LightningProtectionService.LightningProtectionLevel.IV);
        Assert.True(g.GroundRodCount >= 2);
    }

    [Fact]
    public void Grounding_LevelI_LargerConductor()
    {
        var g1 = LightningProtectionService.SpecifyGrounding(400,
            LightningProtectionService.LightningProtectionLevel.I);
        Assert.Equal("1/0", g1.ConductorSize);
    }
}
