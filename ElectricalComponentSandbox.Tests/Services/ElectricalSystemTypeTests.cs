using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalSystemTypeTests
{
    // ── SystemType default ───────────────────────────────────────────────────

    [Fact]
    public void Circuit_DefaultSystemType_IsPowerCircuit()
    {
        var circuit = new Circuit();

        Assert.Equal(ElectricalSystemType.PowerCircuit, circuit.SystemType);
        Assert.True(circuit.IsPowerCircuit);
    }

    [Theory]
    [InlineData(ElectricalSystemType.Data)]
    [InlineData(ElectricalSystemType.Telephone)]
    [InlineData(ElectricalSystemType.FireAlarm)]
    [InlineData(ElectricalSystemType.Security)]
    [InlineData(ElectricalSystemType.NurseCall)]
    [InlineData(ElectricalSystemType.Controls)]
    [InlineData(ElectricalSystemType.Communication)]
    public void Circuit_SignalSystemTypes_AreNotPower(ElectricalSystemType systemType)
    {
        var circuit = new Circuit { SystemType = systemType };

        Assert.False(circuit.IsPowerCircuit);
    }

    // ── AnalyzePanelLoad excludes signal circuits ────────────────────────────

    [Fact]
    public void AnalyzePanelLoad_ExcludesSignalCircuitsFromLoadTotal()
    {
        var panel = new PanelSchedule
        {
            PanelName = "LP-1",
            BusAmps = 200,
            MainBreakerAmps = 200,
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    SlotNumber = 1,
                    ConnectedLoadVA = 1_000,
                    Phase = "A",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.PowerCircuit,
                },
                new Circuit
                {
                    CircuitNumber = "2",
                    SlotNumber = 2,
                    ConnectedLoadVA = 500,
                    Phase = "B",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.Data,
                },
            }
        };

        var svc = new ElectricalCalculationService();
        var summary = svc.AnalyzePanelLoad(panel);

        // Only the power circuit should count as active
        Assert.Equal(1, summary.CircuitCount);
        // Signal circuit excluded from classification totals
        Assert.Single(summary.ClassificationTotals);
        Assert.True(summary.ClassificationTotals.ContainsKey(LoadClassification.Power));
    }

    [Fact]
    public void AnalyzePanelLoad_AllSignalCircuits_ReturnsZeroCircuits()
    {
        var panel = new PanelSchedule
        {
            PanelName = "LP-2",
            BusAmps = 100,
            MainBreakerAmps = 100,
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    SlotNumber = 1,
                    ConnectedLoadVA = 200,
                    Phase = "A",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.FireAlarm,
                }
            }
        };

        var svc = new ElectricalCalculationService();
        var summary = svc.AnalyzePanelLoad(panel);

        Assert.Equal(0, summary.CircuitCount);
        Assert.Empty(summary.ClassificationTotals);
    }

    // ── CalculateDemandLoad excludes signal circuits ─────────────────────────

    [Fact]
    public void CalculateDemandLoad_ExcludesSignalCircuits()
    {
        var circuits = new List<Circuit>
        {
            new Circuit
            {
                CircuitNumber = "1",
                ConnectedLoadVA = 5_000,
                LoadClassification = LoadClassification.Lighting,
                SystemType = ElectricalSystemType.PowerCircuit,
                Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            },
            new Circuit
            {
                CircuitNumber = "2",
                ConnectedLoadVA = 3_000,
                SystemType = ElectricalSystemType.Security,
                Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            },
        };

        var result = ElectricalCalculationService.CalculateDemandLoad(
            circuits, DemandSchedule.GetNec220Defaults());

        // Only lighting circuit should be included
        Assert.Equal(5_000, result.TotalConnectedVA, precision: 2);
        Assert.Single(result.ClassificationDetails);
    }

    // ── Panel schedule signal summary row ────────────────────────────────────

    [Fact]
    public void GeneratePanelSchedule_WithSignalCircuits_HasSignalSummaryRow()
    {
        var panel = new PanelSchedule
        {
            PanelName = "MDP-1",
            BusAmps = 200,
            MainBreakerAmps = 200,
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    SlotNumber = 1,
                    ConnectedLoadVA = 1_000,
                    Phase = "A",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.PowerCircuit,
                },
                new Circuit
                {
                    CircuitNumber = "2",
                    SlotNumber = 2,
                    ConnectedLoadVA = 0,
                    Phase = "B",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.Data,
                },
                new Circuit
                {
                    CircuitNumber = "3",
                    SlotNumber = 3,
                    ConnectedLoadVA = 0,
                    Phase = "A",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.FireAlarm,
                },
            }
        };

        var svc = new ScheduleTableService();
        var table = svc.GeneratePanelSchedule(panel);

        // Last row should be signal summary footer
        var lastRow = table.Rows[^1];
        var lastStyle = table.RowStyles[^1];

        Assert.Equal(ScheduleRowStyle.Footer, lastStyle);
        Assert.Equal("Signal Systems", lastRow[0]);
        Assert.Contains("2 circuits", lastRow[2]);
        Assert.Contains("Data", lastRow[1]);
        Assert.Contains("FireAlarm", lastRow[1]);
    }

    [Fact]
    public void GeneratePanelSchedule_NoPowerCircuitsOnlySignal_StillShowsSignalSummary()
    {
        var panel = new PanelSchedule
        {
            PanelName = "SIG-1",
            BusAmps = 100,
            MainBreakerAmps = 100,
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    SlotNumber = 1,
                    ConnectedLoadVA = 0,
                    Phase = "A",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.Telephone,
                },
                new Circuit
                {
                    CircuitNumber = "2",
                    SlotNumber = 2,
                    ConnectedLoadVA = 0,
                    Phase = "B",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.Telephone,
                },
            }
        };

        var svc = new ScheduleTableService();
        var table = svc.GeneratePanelSchedule(panel);

        var lastRow = table.Rows[^1];
        Assert.Contains("Telephone: 2", lastRow[1]);
    }

    [Fact]
    public void GeneratePanelSchedule_AllPowerCircuits_NoSignalSummaryRow()
    {
        var panel = new PanelSchedule
        {
            PanelName = "P-1",
            BusAmps = 100,
            MainBreakerAmps = 100,
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    SlotNumber = 1,
                    ConnectedLoadVA = 1_000,
                    Phase = "A",
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    SystemType = ElectricalSystemType.PowerCircuit,
                }
            }
        };

        var svc = new ScheduleTableService();
        var table = svc.GeneratePanelSchedule(panel);

        // Only one footer row (phase totals), no signal summary
        int footerCount = table.RowStyles.Count(s => s == ScheduleRowStyle.Footer);
        Assert.Equal(1, footerCount);

        // Last row should be Phase totals footer, not signal
        var lastRow = table.Rows[^1];
        Assert.DoesNotContain("Signal Systems", lastRow[0]);
    }

    // ── SystemType persistence (round-trip via property) ─────────────────────

    [Theory]
    [InlineData(ElectricalSystemType.PowerCircuit)]
    [InlineData(ElectricalSystemType.Data)]
    [InlineData(ElectricalSystemType.Telephone)]
    [InlineData(ElectricalSystemType.FireAlarm)]
    [InlineData(ElectricalSystemType.Security)]
    [InlineData(ElectricalSystemType.NurseCall)]
    [InlineData(ElectricalSystemType.Controls)]
    [InlineData(ElectricalSystemType.Communication)]
    public void Circuit_SystemType_RoundTrips(ElectricalSystemType systemType)
    {
        var circuit = new Circuit { SystemType = systemType };

        Assert.Equal(systemType, circuit.SystemType);
    }

    // ── Mixed panel: correct power vs signal counts ──────────────────────────

    [Fact]
    public void AnalyzePanelLoad_MixedPanel_CorrectPowerCircuitCount()
    {
        var panel = new PanelSchedule
        {
            PanelName = "MIX-1",
            BusAmps = 200,
            MainBreakerAmps = 200,
            Circuits =
            {
                MakePowerCircuit("1", 1, 1_000),
                MakePowerCircuit("2", 2, 2_000),
                MakeSignalCircuit("3", 3, ElectricalSystemType.Data),
                MakeSignalCircuit("4", 4, ElectricalSystemType.FireAlarm),
                MakeSignalCircuit("5", 5, ElectricalSystemType.Security),
            }
        };

        var svc = new ElectricalCalculationService();
        var summary = svc.AnalyzePanelLoad(panel);

        Assert.Equal(2, summary.CircuitCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Circuit MakePowerCircuit(string number, int slot, double va)
    {
        return new Circuit
        {
            CircuitNumber = number,
            SlotNumber = slot,
            ConnectedLoadVA = va,
            Phase = "A",
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            SystemType = ElectricalSystemType.PowerCircuit,
        };
    }

    private static Circuit MakeSignalCircuit(string number, int slot, ElectricalSystemType type)
    {
        return new Circuit
        {
            CircuitNumber = number,
            SlotNumber = slot,
            ConnectedLoadVA = 0,
            Phase = "A",
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            SystemType = type,
        };
    }
}
