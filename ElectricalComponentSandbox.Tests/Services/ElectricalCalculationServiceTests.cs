using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalCalculationServiceTests
{
    private readonly ElectricalCalculationService _svc = new();

    // ── Voltage Drop ─────────────────────────────────────────────────────────

    [Fact]
    public void VoltageDrop_120V_20A_100ft_12AWG_IsValid()
    {
        var circuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400, // 20A × 120V
            DemandFactor = 1.0,
            Poles = 1,
            WireLengthFeet = 100,
            Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper }
        };

        var result = _svc.CalculateVoltageDrop(circuit);

        Assert.True(result.IsValid);
        Assert.True(result.VoltageDropVolts > 0);
        Assert.True(result.VoltageDropPercent > 0);
        Assert.True(result.VoltageAtLoad < 120);
        Assert.True(result.VoltageAtLoad > 100); // Reasonable range
    }

    [Fact]
    public void VoltageDrop_LongRun_Exceeds3Percent()
    {
        var circuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400,
            DemandFactor = 1.0,
            Poles = 1,
            WireLengthFeet = 300, // Very long run
            Wire = new WireSpec { Size = "14", Material = ConductorMaterial.Copper }
        };

        var result = _svc.CalculateVoltageDrop(circuit);

        Assert.True(result.IsValid);
        Assert.True(result.ExceedsNecRecommendation);
    }

    [Fact]
    public void VoltageDrop_ZeroLength_ReturnsInvalid()
    {
        var circuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 1000,
            WireLengthFeet = 0,
            Wire = new WireSpec { Size = "12" }
        };

        var result = _svc.CalculateVoltageDrop(circuit);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void VoltageDrop_ThreePhase_UsesCorrectMultiplier()
    {
        var circuit = new Circuit
        {
            Voltage = 208,
            ConnectedLoadVA = 5000,
            DemandFactor = 1.0,
            Poles = 3,
            WireLengthFeet = 150,
            Wire = new WireSpec { Size = "10", Material = ConductorMaterial.Copper }
        };

        var result = _svc.CalculateVoltageDrop(circuit);

        Assert.True(result.IsValid);
        Assert.True(result.VoltageDropVolts > 0);
    }

    [Fact]
    public void VoltageDrop_Aluminum_HigherThanCopper()
    {
        var baseCircuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400,
            DemandFactor = 1.0,
            Poles = 1,
            WireLengthFeet = 100,
        };

        baseCircuit.Wire = new WireSpec { Size = "10", Material = ConductorMaterial.Copper };
        var copperResult = _svc.CalculateVoltageDrop(baseCircuit);

        baseCircuit.Wire = new WireSpec { Size = "10", Material = ConductorMaterial.Aluminum };
        var aluminumResult = _svc.CalculateVoltageDrop(baseCircuit);

        Assert.True(copperResult.IsValid);
        Assert.True(aluminumResult.IsValid);
        Assert.True(aluminumResult.VoltageDropPercent > copperResult.VoltageDropPercent);
    }

    // ── Wire Sizing ──────────────────────────────────────────────────────────

    [Fact]
    public void WireSize_20ACircuit_Recommends12AWGMinimum()
    {
        var circuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400,
            DemandFactor = 1.0,
            Poles = 1,
            WireLengthFeet = 50,
            Wire = new WireSpec { Material = ConductorMaterial.Copper }
        };

        var result = _svc.RecommendWireSize(circuit);

        Assert.NotNull(result.RecommendedSize);
        // 20A requires at least #12 copper at 75°C
        Assert.True(result.CurrentAmps > 0);
    }

    [Fact]
    public void WireSize_LongRun_UpSizesForVoltageDrop()
    {
        var shortCircuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400,
            DemandFactor = 1.0,
            WireLengthFeet = 25,
            Wire = new WireSpec { Material = ConductorMaterial.Copper }
        };

        var longCircuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400,
            DemandFactor = 1.0,
            WireLengthFeet = 250,
            Wire = new WireSpec { Material = ConductorMaterial.Copper }
        };

        var shortRec = _svc.RecommendWireSize(shortCircuit);
        var longRec = _svc.RecommendWireSize(longCircuit);

        // Long run should recommend same or larger wire
        var sizes = new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1",
            "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500" };

        int shortIdx = Array.IndexOf(sizes, shortRec.RecommendedSize);
        int longIdx = Array.IndexOf(sizes, longRec.RecommendedSize);

        Assert.True(longIdx >= shortIdx);
    }

    // ── Panel Load Analysis ──────────────────────────────────────────────────

    [Fact]
    public void PanelLoad_BasicAnalysis_ComputesTotals()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-1",
            MainBreakerAmps = 200,
            BusAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1800, DemandFactor = 1.0 },
                new() { CircuitNumber = "3", Phase = "B", ConnectedLoadVA = 1500, DemandFactor = 1.0 },
                new() { CircuitNumber = "5", Phase = "C", ConnectedLoadVA = 2000, DemandFactor = 0.8 },
            }
        };

        var result = _svc.AnalyzePanelLoad(schedule);

        Assert.Equal(5300, result.TotalConnectedVA);
        Assert.True(result.TotalDemandVA < result.TotalConnectedVA); // Because of 0.8 demand factor
        Assert.Equal(1800, result.PhaseALoadVA);
        Assert.Equal(1500, result.PhaseBLoadVA);
        Assert.Equal(3, result.CircuitCount);
        Assert.False(result.IsOverloaded);
    }

    [Fact]
    public void PanelLoad_Overloaded_FlagsCorrectly()
    {
        var schedule = new PanelSchedule
        {
            MainBreakerAmps = 100,
            BusAmps = 100,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = Enumerable.Range(1, 40).Select(i => new Circuit
            {
                Phase = i % 3 == 0 ? "A" : i % 3 == 1 ? "B" : "C",
                ConnectedLoadVA = 2400,
                DemandFactor = 1.0,
            }).ToList()
        };

        var result = _svc.AnalyzePanelLoad(schedule);

        Assert.True(result.IsOverloaded);
        Assert.True(result.BusUtilizationPercent > 100);
    }

    // ── Circuit Model ────────────────────────────────────────────────────────

    [Fact]
    public void Circuit_DemandLoad_AppliesFactor()
    {
        var circuit = new Circuit
        {
            ConnectedLoadVA = 1000,
            DemandFactor = 0.75
        };

        Assert.Equal(750, circuit.DemandLoadVA);
    }

    [Fact]
    public void PanelSchedule_PhaseDemand_DistributesCorrectly()
    {
        var schedule = new PanelSchedule
        {
            Circuits = new List<Circuit>
            {
                new() { Phase = "A", ConnectedLoadVA = 1000, DemandFactor = 1.0, Poles = 1 },
                new() { Phase = "B", ConnectedLoadVA = 2000, DemandFactor = 1.0, Poles = 1 },
                new() { Phase = "AB", ConnectedLoadVA = 3000, DemandFactor = 1.0, Poles = 2 },
            }
        };

        var (a, b, c) = schedule.PhaseDemandVA;

        // Phase A: 1000 + 1500 (half of 3000) = 2500
        Assert.Equal(2500, a);
        // Phase B: 2000 + 1500 = 3500
        Assert.Equal(3500, b);
        // Phase C: 0
        Assert.Equal(0, c);
    }
}
