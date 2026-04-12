using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class GroundingServiceTests
{
    // ── NEC Table 250.122 EGC Sizing — Copper ────────────────────────────────

    [Theory]
    [InlineData(15, "14")]
    [InlineData(20, "12")]
    [InlineData(30, "10")]
    [InlineData(40, "10")]
    [InlineData(60, "10")]
    [InlineData(100, "8")]
    [InlineData(200, "6")]
    [InlineData(300, "4")]
    [InlineData(400, "3")]
    [InlineData(500, "2")]
    [InlineData(600, "1")]
    [InlineData(800, "1/0")]
    [InlineData(1000, "2/0")]
    [InlineData(1200, "3/0")]
    [InlineData(1600, "4/0")]
    [InlineData(2000, "250")]
    public void GetMinEGCSize_Copper_MatchesTable250_122(int ocpdAmps, string expectedSize)
    {
        var result = GroundingService.GetMinEGCSize(ocpdAmps, ConductorMaterial.Copper);
        Assert.Equal(expectedSize, result);
    }

    // ── NEC Table 250.122 EGC Sizing — Aluminum ─────────────────────────────

    [Theory]
    [InlineData(15, "12")]
    [InlineData(20, "10")]
    [InlineData(100, "6")]
    [InlineData(200, "4")]
    [InlineData(400, "1")]
    [InlineData(800, "3/0")]
    [InlineData(1000, "4/0")]
    public void GetMinEGCSize_Aluminum_MatchesTable250_122(int ocpdAmps, string expectedSize)
    {
        var result = GroundingService.GetMinEGCSize(ocpdAmps, ConductorMaterial.Aluminum);
        Assert.Equal(expectedSize, result);
    }

    [Fact]
    public void GetMinEGCSize_BetweenTableEntries_UsesNextHigher()
    {
        // 25A is between 20A (→#12) and 30A (→#10) → should pick 30A entry (#10)
        var result = GroundingService.GetMinEGCSize(25, ConductorMaterial.Copper);
        Assert.Equal("10", result);
    }

    [Fact]
    public void GetMinEGCSize_AboveMaxTable_ReturnsLargest()
    {
        var result = GroundingService.GetMinEGCSize(10000, ConductorMaterial.Copper);
        Assert.Equal("800", result);
    }

    // ── NEC Table 250.66 GEC Sizing ─────────────────────────────────────────

    [Theory]
    [InlineData("2", "8")]
    [InlineData("1", "6")]
    [InlineData("1/0", "6")]
    [InlineData("2/0", "4")]
    [InlineData("3/0", "2")]
    [InlineData("4/0", "2")]
    [InlineData("350", "1/0")]
    [InlineData("500", "1/0")]
    [InlineData("600", "2/0")]
    [InlineData("1000", "3/0")]
    public void GetMinGECSize_Copper_MatchesTable250_66(string serviceCondSize, string expectedGEC)
    {
        var result = GroundingService.GetMinGECSize(serviceCondSize, ConductorMaterial.Copper);
        Assert.Equal(expectedGEC, result);
    }

    [Theory]
    [InlineData("2", "6")]
    [InlineData("1/0", "4")]
    [InlineData("350", "3/0")]
    [InlineData("600", "4/0")]
    [InlineData("1000", "250")]
    public void GetMinGECSize_Aluminum_MatchesTable250_66(string serviceCondSize, string expectedGEC)
    {
        var result = GroundingService.GetMinGECSize(serviceCondSize, ConductorMaterial.Aluminum);
        Assert.Equal(expectedGEC, result);
    }

    [Fact]
    public void GetMinGECSize_AboveMaxTable_ReturnsLargest()
    {
        var result = GroundingService.GetMinGECSize("2000", ConductorMaterial.Copper);
        Assert.Equal("3/0", result);
    }

    // ── ValidateGroundSize ──────────────────────────────────────────────────

    [Fact]
    public void ValidateGroundSize_AdequateGround_ReturnsAdequate()
    {
        var circuit = MakeCircuit(breakerTrip: 20, groundSize: "12");
        var result = GroundingService.ValidateGroundSize(circuit);

        Assert.True(result.IsAdequate);
        Assert.Equal("12", result.MinimumEGCSize);
        Assert.Equal("12", result.ActualGroundSize);
    }

    [Fact]
    public void ValidateGroundSize_OversizedGround_ReturnsAdequate()
    {
        var circuit = MakeCircuit(breakerTrip: 20, groundSize: "10");
        var result = GroundingService.ValidateGroundSize(circuit);

        Assert.True(result.IsAdequate);
    }

    [Fact]
    public void ValidateGroundSize_UndersizedGround_ReturnsInadequate()
    {
        var circuit = MakeCircuit(breakerTrip: 20, groundSize: "14");
        var result = GroundingService.ValidateGroundSize(circuit);

        Assert.False(result.IsAdequate);
        Assert.Equal("12", result.MinimumEGCSize);
        Assert.Equal("14", result.ActualGroundSize);
    }

    [Fact]
    public void ValidateGroundSize_100A_Requires8Copper()
    {
        var circuit = MakeCircuit(breakerTrip: 100, groundSize: "10");
        var result = GroundingService.ValidateGroundSize(circuit);

        Assert.False(result.IsAdequate);
        Assert.Equal("8", result.MinimumEGCSize);
    }

    [Fact]
    public void ValidateGroundSize_200A_Requires6Copper()
    {
        var circuit = MakeCircuit(breakerTrip: 200, groundSize: "6");
        var result = GroundingService.ValidateGroundSize(circuit);

        Assert.True(result.IsAdequate);
    }

    // ── ValidateAll ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidateAll_ReturnsOneResultPerCircuit()
    {
        var circuits = new[]
        {
            MakeCircuit(breakerTrip: 20, groundSize: "12"),
            MakeCircuit(breakerTrip: 30, groundSize: "10"),
            MakeCircuit(breakerTrip: 100, groundSize: "8"),
        };

        var results = GroundingService.ValidateAll(circuits);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.IsAdequate));
    }

    [Fact]
    public void ValidateAll_MixedResults_IdentifiesViolations()
    {
        var circuits = new[]
        {
            MakeCircuit(breakerTrip: 20, groundSize: "12"), // OK
            MakeCircuit(breakerTrip: 100, groundSize: "14"), // FAIL
        };

        var results = GroundingService.ValidateAll(circuits);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].IsAdequate);
        Assert.False(results[1].IsAdequate);
    }

    // ── NecDesignRuleService Integration ─────────────────────────────────────

    [Fact]
    public void NecDesignRuleService_ValidateCircuit_UndersizedGround_ProducesNEC250_122Violation()
    {
        var circuit = MakeCircuit(breakerTrip: 20, groundSize: "14");
        var necService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();

        var violations = necService.ValidateCircuit(circuit, calcService);

        Assert.Contains(violations, v => v.RuleId == "NEC 250.122");
    }

    [Fact]
    public void NecDesignRuleService_ValidateCircuit_AdequateGround_NoNEC250Violation()
    {
        var circuit = MakeCircuit(breakerTrip: 20, groundSize: "12");
        var necService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();

        var violations = necService.ValidateCircuit(circuit, calcService);

        Assert.DoesNotContain(violations, v => v.RuleId == "NEC 250.122");
    }

    // ── SizeIndex ───────────────────────────────────────────────────────────

    [Fact]
    public void SizeIndex_KnownSizes_ReturnsMonotonicOrder()
    {
        int prev = -1;
        foreach (var size in new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "500" })
        {
            int idx = GroundingService.SizeIndex(size);
            Assert.True(idx > prev, $"Size {size} should be > previous index {prev}");
            prev = idx;
        }
    }

    [Fact]
    public void SizeIndex_UnknownSize_ReturnsNegativeOne()
    {
        Assert.Equal(-1, GroundingService.SizeIndex("unknown"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Circuit MakeCircuit(int breakerTrip = 20, string groundSize = "12", ConductorMaterial material = ConductorMaterial.Copper)
    {
        return new Circuit
        {
            Id = $"ckt-{breakerTrip}",
            CircuitNumber = "1",
            Description = "General Purpose",
            Voltage = 120,
            Poles = 1,
            Breaker = new CircuitBreaker
            {
                TripAmps = breakerTrip,
                FrameAmps = breakerTrip,
                Poles = 1,
            },
            Wire = new WireSpec
            {
                Size = breakerTrip <= 20 ? "12" : breakerTrip <= 30 ? "10" : breakerTrip <= 100 ? "3" : "2/0",
                Material = material,
                InsulationType = "THHN",
                GroundSize = groundSize,
            },
        };
    }
}
