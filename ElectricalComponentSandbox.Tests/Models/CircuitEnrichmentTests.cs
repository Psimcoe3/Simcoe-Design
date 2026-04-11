using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

/// <summary>
/// Phase 1 model enrichment — verifies defaults, derived properties, and the
/// new enum members added to Circuit and PanelSchedule.
/// </summary>
public class CircuitEnrichmentTests
{
    // ── Circuit defaults ──────────────────────────────────────────────────────

    [Fact]
    public void Circuit_DefaultLoadClassification_IsPower()
    {
        var circuit = new Circuit();
        Assert.Equal(LoadClassification.Power, circuit.LoadClassification);
    }

    [Fact]
    public void Circuit_DefaultSlotType_IsCircuit()
    {
        var circuit = new Circuit();
        Assert.Equal(CircuitSlotType.Circuit, circuit.SlotType);
    }

    [Fact]
    public void Circuit_DefaultPowerFactor_IsUnity()
    {
        var circuit = new Circuit();
        Assert.Equal(1.0, circuit.PowerFactor);
    }

    [Fact]
    public void Circuit_DefaultPowerFactorState_IsLagging()
    {
        var circuit = new Circuit();
        Assert.Equal(PowerFactorState.Lagging, circuit.PowerFactorState);
    }

    [Fact]
    public void Circuit_DefaultTrueLoadW_IsZero()
    {
        var circuit = new Circuit();
        Assert.Equal(0.0, circuit.TrueLoadW);
    }

    // ── EffectiveTrueLoadW derived property ───────────────────────────────────

    [Fact]
    public void Circuit_EffectiveTrueLoadW_UsesExplicitValueWhenNonZero()
    {
        var circuit = new Circuit
        {
            ConnectedLoadVA = 2000,
            PowerFactor = 0.8,
            TrueLoadW = 1500
        };

        Assert.Equal(1500, circuit.EffectiveTrueLoadW);
    }

    [Fact]
    public void Circuit_EffectiveTrueLoadW_DerivesFromVaTimesPfWhenTrueLoadWIsZero()
    {
        var circuit = new Circuit
        {
            ConnectedLoadVA = 2000,
            PowerFactor = 0.85,
            TrueLoadW = 0
        };

        Assert.Equal(1700, circuit.EffectiveTrueLoadW, precision: 6);
    }

    [Fact]
    public void Circuit_EffectiveTrueLoadW_UnityPowerFactor_MatchesVA()
    {
        var circuit = new Circuit
        {
            ConnectedLoadVA = 1800,
            PowerFactor = 1.0,
            TrueLoadW = 0
        };

        Assert.Equal(1800, circuit.EffectiveTrueLoadW);
    }

    // ── PanelSchedule defaults ────────────────────────────────────────────────

    [Fact]
    public void PanelSchedule_DefaultCircuitSequence_IsOddThenEven()
    {
        var schedule = new PanelSchedule();
        Assert.Equal(CircuitSequence.OddThenEven, schedule.CircuitSequence);
    }

    // ── Enum completeness (compile-time contract) ─────────────────────────────

    [Fact]
    public void LoadClassification_HasFourValues()
    {
        var values = Enum.GetValues<LoadClassification>();
        Assert.Equal(4, values.Length);
        Assert.Contains(LoadClassification.Power, values);
        Assert.Contains(LoadClassification.Lighting, values);
        Assert.Contains(LoadClassification.HVAC, values);
        Assert.Contains(LoadClassification.Other, values);
    }

    [Fact]
    public void CircuitSlotType_HasThreeValues()
    {
        var values = Enum.GetValues<CircuitSlotType>();
        Assert.Equal(3, values.Length);
        Assert.Contains(CircuitSlotType.Circuit, values);
        Assert.Contains(CircuitSlotType.Spare, values);
        Assert.Contains(CircuitSlotType.Space, values);
    }

    [Fact]
    public void PowerFactorState_HasTwoValues()
    {
        var values = Enum.GetValues<PowerFactorState>();
        Assert.Equal(2, values.Length);
        Assert.Contains(PowerFactorState.Leading, values);
        Assert.Contains(PowerFactorState.Lagging, values);
    }

    [Fact]
    public void CircuitSequence_HasThreeValues()
    {
        var values = Enum.GetValues<CircuitSequence>();
        Assert.Equal(3, values.Length);
        Assert.Contains(CircuitSequence.Numerical, values);
        Assert.Contains(CircuitSequence.GroupByPhase, values);
        Assert.Contains(CircuitSequence.OddThenEven, values);
    }

    // ── Round-trip via property assignment ────────────────────────────────────

    [Fact]
    public void Circuit_AllNewFields_CanBeSetAndRead()
    {
        var circuit = new Circuit
        {
            LoadClassification = LoadClassification.HVAC,
            SlotType = CircuitSlotType.Spare,
            TrueLoadW = 3500,
            PowerFactor = 0.9,
            PowerFactorState = PowerFactorState.Leading
        };

        Assert.Equal(LoadClassification.HVAC, circuit.LoadClassification);
        Assert.Equal(CircuitSlotType.Spare, circuit.SlotType);
        Assert.Equal(3500, circuit.TrueLoadW);
        Assert.Equal(0.9, circuit.PowerFactor);
        Assert.Equal(PowerFactorState.Leading, circuit.PowerFactorState);
    }

    [Fact]
    public void PanelSchedule_CircuitSequence_CanBeOverridden()
    {
        var schedule = new PanelSchedule { CircuitSequence = CircuitSequence.GroupByPhase };
        Assert.Equal(CircuitSequence.GroupByPhase, schedule.CircuitSequence);
    }
}
