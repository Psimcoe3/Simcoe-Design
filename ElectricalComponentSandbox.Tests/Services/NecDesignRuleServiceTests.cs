using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class NecDesignRuleServiceTests
{
    private readonly NecDesignRuleService _sut = new();
    private readonly ElectricalCalculationService _calc = new();

    private static Circuit CreateCircuit(string wireSize = "12", int tripAmps = 20,
        int voltage = 120, double loadVA = 1800, double lengthFt = 75,
        ConductorMaterial material = ConductorMaterial.Copper)
    {
        return new Circuit
        {
            CircuitNumber = "1",
            Description = "Test Circuit",
            PanelId = "panel-1",
            Phase = "A",
            Voltage = voltage,
            ConnectedLoadVA = loadVA,
            DemandFactor = 1.0,
            WireLengthFeet = lengthFt,
            Breaker = new CircuitBreaker { TripAmps = tripAmps, Poles = 1 },
            Wire = new WireSpec { Size = wireSize, Material = material }
        };
    }

    [Fact]
    public void ValidateCircuit_StandardCircuit_NoViolations()
    {
        var circuit = CreateCircuit("12", 20, 120, 1800, 75);
        var violations = _sut.ValidateCircuit(circuit, _calc);
        Assert.DoesNotContain(violations, v => v.Severity == ViolationSeverity.Error);
    }

    [Fact]
    public void ValidateCircuit_SmallWireOverProtection_Error()
    {
        // #14 wire with 20A breaker violates NEC 240.4(D)
        var circuit = CreateCircuit("14", 20);
        var violations = _sut.ValidateCircuit(circuit, _calc);
        Assert.Contains(violations, v => v.RuleId == "NEC 240.4(D)");
    }

    [Fact]
    public void ValidateCircuit_NonStandardBreakerSize_Error()
    {
        // 22A is not a standard breaker size per NEC 210.3
        var circuit = CreateCircuit("12", 22);
        var violations = _sut.ValidateCircuit(circuit, _calc);
        Assert.Contains(violations, v => v.RuleId == "NEC 210.3");
    }

    [Fact]
    public void ValidateCircuit_HighVoltageDrop_Warning()
    {
        // Very long run should trigger voltage drop warning
        var circuit = CreateCircuit("14", 15, 120, 1800, 500);
        var violations = _sut.ValidateCircuit(circuit, _calc);
        Assert.Contains(violations, v =>
            v.RuleId.Contains("210.19") && v.Severity == ViolationSeverity.Warning);
    }

    [Fact]
    public void ValidatePanel_OverloadedBus_Error()
    {
        var circuits = Enumerable.Range(1, 10).Select(i => new Circuit
        {
            CircuitNumber = i.ToString(),
            Description = $"Circuit {i}",
            PanelId = "panel-1",
            Phase = i % 3 == 1 ? "A" : i % 3 == 2 ? "B" : "C",
            Voltage = 120,
            ConnectedLoadVA = 5000,
            DemandFactor = 1.0,
            WireLengthFeet = 50,
            Breaker = new CircuitBreaker { TripAmps = 50, Poles = 1 },
            Wire = new WireSpec { Size = "6", Material = ConductorMaterial.Copper }
        }).ToList();

        var schedule = new PanelSchedule
        {
            PanelId = "panel-1",
            PanelName = "Test Panel",
            BusAmps = 100,
            MainBreakerAmps = 100,
            Circuits = circuits
        };

        var violations = _sut.ValidatePanel(schedule, _calc);
        Assert.Contains(violations, v => v.RuleId == "NEC 408.36");
    }

    [Fact]
    public void ValidateAll_CombinesCircuitAndPanelViolations()
    {
        var circuit = CreateCircuit("14", 20); // violates 240.4(D)
        var schedule = new PanelSchedule
        {
            PanelId = "panel-1",
            PanelName = "Panel A",
            BusAmps = 100,
            Circuits = new List<Circuit> { circuit }
        };

        var violations = _sut.ValidateAll(
            new List<Circuit> { circuit },
            new List<PanelSchedule> { schedule },
            _calc);

        Assert.True(violations.Count > 0);
    }

    [Fact]
    public void ValidateCircuit_AluminumWire_AppliesCorrectAmpacity()
    {
        // #12 Al at 75°C = 20A, with 20A breaker should pass 240.4(D)
        var circuit = CreateCircuit("12", 20, 120, 1800, 75, ConductorMaterial.Aluminum);
        var violations = _sut.ValidateCircuit(circuit, _calc);
        Assert.DoesNotContain(violations, v => v.RuleId == "NEC 240.4(D)");
    }
}
