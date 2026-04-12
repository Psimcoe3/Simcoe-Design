using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class BundleDeratingServiceTests
{
    // ── NEC Table 310.15(C)(1) Adjustment Factors ────────────────────────────

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.0)]
    [InlineData(3, 1.0)]
    [InlineData(4, 0.80)]
    [InlineData(6, 0.80)]
    [InlineData(7, 0.70)]
    [InlineData(9, 0.70)]
    [InlineData(10, 0.50)]
    [InlineData(15, 0.50)]
    [InlineData(20, 0.50)]
    [InlineData(21, 0.45)]
    [InlineData(30, 0.45)]
    [InlineData(31, 0.40)]
    [InlineData(40, 0.40)]
    [InlineData(41, 0.35)]
    [InlineData(100, 0.35)]
    public void GetAdjustmentFactor_ReturnsNecValue(int conductors, double expected)
    {
        Assert.Equal(expected, BundleDeratingService.GetAdjustmentFactor(conductors));
    }

    // ── Derated Ampacity ─────────────────────────────────────────────────────

    [Fact]
    public void GetDeratedAmpacity_3OrFewer_NoDerating()
    {
        // #12 Cu 75°C = 25A base, 3 conductors → factor 1.0 → 25A
        double result = BundleDeratingService.GetDeratedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 3);
        Assert.Equal(25.0, result);
    }

    [Fact]
    public void GetDeratedAmpacity_6Conductors_80Percent()
    {
        // #12 Cu 75°C = 25A, 6 conductors → 25 × 0.80 = 20A
        double result = BundleDeratingService.GetDeratedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 6);
        Assert.Equal(20.0, result);
    }

    [Fact]
    public void GetDeratedAmpacity_9Conductors_70Percent()
    {
        // #10 Cu 75°C = 35A, 9 conductors → 35 × 0.70 = 24.5
        double result = BundleDeratingService.GetDeratedAmpacity(
            "10", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 9);
        Assert.Equal(24.5, result);
    }

    [Fact]
    public void GetDeratedAmpacity_12Conductors_50Percent()
    {
        // #8 Cu 75°C = 50A, 12 conductors → 50 × 0.50 = 25
        double result = BundleDeratingService.GetDeratedAmpacity(
            "8", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 12);
        Assert.Equal(25.0, result);
    }

    [Fact]
    public void GetDeratedAmpacity_WithAmbientTemp_CombinesFactors()
    {
        // #12 Cu 75°C = 25A, 6 conductors (0.80), 40°C ambient (factor ~0.88 for 75°C)
        double result = BundleDeratingService.GetDeratedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 6, ambientTempC: 40);
        // 25 × 0.80 × 0.88 = 17.6
        Assert.Equal(17.6, result);
    }

    [Fact]
    public void GetDeratedAmpacity_NoAmbient_OnlyBundleFactor()
    {
        double withoutTemp = BundleDeratingService.GetDeratedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 6);
        double withDefaultTemp = BundleDeratingService.GetDeratedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 6, ambientTempC: 30);

        // 26-30°C is factor 1.0, so should be the same
        Assert.Equal(withoutTemp, withDefaultTemp);
    }

    // ── Wire Size Recommendation with Bundle ─────────────────────────────────

    [Fact]
    public void RecommendWireSizeWithBundle_3OrFewer_SameAsNormal()
    {
        string? bundled = BundleDeratingService.RecommendWireSizeWithBundle(
            20, ConductorMaterial.Copper, InsulationTemperatureRating.C75, 3);
        string? normal = NecAmpacityService.RecommendWireSize(
            20, ConductorMaterial.Copper, InsulationTemperatureRating.C75);
        Assert.Equal(normal, bundled);
    }

    [Fact]
    public void RecommendWireSizeWithBundle_6Conductors_LargerWire()
    {
        // 20A load with 6 conductors (80% derating)
        // Need base ampacity ≥ 20/0.80 = 25, so #12 Cu 75°C (25A) just works
        string? size = BundleDeratingService.RecommendWireSizeWithBundle(
            20, ConductorMaterial.Copper, InsulationTemperatureRating.C75, 6);
        Assert.Equal("12", size);
    }

    [Fact]
    public void RecommendWireSizeWithBundle_LargeBundle_RequiresUpsize()
    {
        // 20A load with 12 conductors (50% derating)
        // Need base ampacity ≥ 20/0.50 = 40, so #8 Cu 75°C (50A) → 25A derated
        // #10 Cu 75°C (35A) → 17.5A derated — not enough
        // #8 Cu 75°C (50A) → 25A derated — works
        string? size = BundleDeratingService.RecommendWireSizeWithBundle(
            20, ConductorMaterial.Copper, InsulationTemperatureRating.C75, 12);
        Assert.Equal("8", size);
    }

    [Fact]
    public void RecommendWireSizeWithBundle_Aluminum_Works()
    {
        string? size = BundleDeratingService.RecommendWireSizeWithBundle(
            30, ConductorMaterial.Aluminum, InsulationTemperatureRating.C75, 6);
        Assert.NotNull(size);
        // Aluminum requires larger wire than copper
        string? cuSize = BundleDeratingService.RecommendWireSizeWithBundle(
            30, ConductorMaterial.Copper, InsulationTemperatureRating.C75, 6);
        Assert.NotNull(cuSize);
    }

    // ── Circuit Validation in Bundle ─────────────────────────────────────────

    [Fact]
    public void ValidateCircuitInBundle_Adequate_ReturnsTrue()
    {
        var circuit = MakeCircuit("12", 20, 1);
        // 3 conductors → no derating, #12 Cu 75°C = 25A ≥ 20A
        var result = BundleDeratingService.ValidateCircuitInBundle(circuit, 3);
        Assert.True(result.IsAdequate);
        Assert.Equal(1.0, result.BundleFactor);
    }

    [Fact]
    public void ValidateCircuitInBundle_BundledButAdequate_ReturnsTrue()
    {
        var circuit = MakeCircuit("12", 20, 1);
        // 6 conductors → 80%, #12 Cu 75°C = 25A × 0.80 = 20A ≥ 20A
        var result = BundleDeratingService.ValidateCircuitInBundle(circuit, 6);
        Assert.True(result.IsAdequate);
        Assert.Equal(20.0, result.DeratedAmpacity);
    }

    [Fact]
    public void ValidateCircuitInBundle_Undersized_ReturnsFalse()
    {
        var circuit = MakeCircuit("12", 20, 1);
        // 10 conductors → 50%, #12 Cu 75°C = 25A × 0.50 = 12.5A < 20A
        var result = BundleDeratingService.ValidateCircuitInBundle(circuit, 10);
        Assert.False(result.IsAdequate);
        Assert.Equal(12.5, result.DeratedAmpacity);
        Assert.NotNull(result.RecommendedWireSize);
    }

    [Fact]
    public void ValidateCircuitInBundle_Undersized_RecommendsUpsize()
    {
        var circuit = MakeCircuit("12", 20, 1);
        var result = BundleDeratingService.ValidateCircuitInBundle(circuit, 10);
        // Recommended wire at 50% derating for 20A means base ≥ 40A → #8 (50A)
        Assert.Equal("8", result.RecommendedWireSize);
    }

    [Fact]
    public void ValidateCircuitInBundle_LargeWire_AdequateAtHighBundle()
    {
        // #4 Cu 75°C = 85A, 20A breaker, 20 conductors (50%), derated = 42.5A ≥ 20A
        var circuit = MakeCircuit("4", 20, 1);
        var result = BundleDeratingService.ValidateCircuitInBundle(circuit, 20);
        Assert.True(result.IsAdequate);
    }

    [Fact]
    public void ValidateCircuitInBundle_WithHighAmbient()
    {
        var circuit = MakeCircuit("12", 20, 1);
        // 6 conductors (0.80) + 40°C (0.88) = 0.704
        // #12 Cu 75°C = 25A × 0.704 = 17.6A < 20A
        var result = BundleDeratingService.ValidateCircuitInBundle(circuit, 6, ambientTempC: 40);
        Assert.False(result.IsAdequate);
    }

    // ── Count Current-Carrying Conductors ────────────────────────────────────

    [Fact]
    public void CountCurrentCarrying_SinglePhaseCircuits_2Each()
    {
        var circuits = new[]
        {
            MakeCircuit("12", 20, 1),
            MakeCircuit("12", 20, 1),
            MakeCircuit("12", 20, 1),
        };
        Assert.Equal(6, BundleDeratingService.CountCurrentCarrying(circuits));
    }

    [Fact]
    public void CountCurrentCarrying_ThreePhaseCircuits_3Each()
    {
        var circuits = new[]
        {
            MakeCircuit("8", 50, 3),
            MakeCircuit("8", 50, 3),
        };
        Assert.Equal(6, BundleDeratingService.CountCurrentCarrying(circuits));
    }

    [Fact]
    public void CountCurrentCarrying_ThreePhaseWithHarmonics_4Each()
    {
        var circuits = new[]
        {
            MakeCircuit("8", 50, 3),
            MakeCircuit("8", 50, 3),
        };
        Assert.Equal(8, BundleDeratingService.CountCurrentCarrying(circuits, harmonicLoads: true));
    }

    [Fact]
    public void CountCurrentCarrying_Mixed()
    {
        var circuits = new[]
        {
            MakeCircuit("12", 20, 1),
            MakeCircuit("8", 50, 3),
        };
        // 2 + 3 = 5
        Assert.Equal(5, BundleDeratingService.CountCurrentCarrying(circuits));
    }

    [Fact]
    public void CountCurrentCarrying_Empty_Zero()
    {
        Assert.Equal(0, BundleDeratingService.CountCurrentCarrying(Array.Empty<Circuit>()));
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void GetAdjustmentFactor_Zero_Returns1()
    {
        Assert.Equal(1.0, BundleDeratingService.GetAdjustmentFactor(0));
    }

    [Fact]
    public void GetAdjustmentFactor_Negative_Returns1()
    {
        Assert.Equal(1.0, BundleDeratingService.GetAdjustmentFactor(-5));
    }

    [Fact]
    public void GetDeratedAmpacity_90C_HigherBase()
    {
        double c75 = BundleDeratingService.GetDeratedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 6);
        double c90 = BundleDeratingService.GetDeratedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C90, 6);
        Assert.True(c90 > c75);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static Circuit MakeCircuit(string wireSize, int breakerAmps, int poles)
    {
        return new Circuit
        {
            Id = Guid.NewGuid().ToString(),
            Description = $"Test {wireSize} AWG {breakerAmps}A",
            Wire = new WireSpec { Size = wireSize, Material = ConductorMaterial.Copper },
            Breaker = new CircuitBreaker { TripAmps = breakerAmps, Poles = poles },
            Poles = poles,
        };
    }
}
