using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class WirePullScheduleServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PanelSchedule MakePanel(string id, params Circuit[] circuits)
    {
        var panel = new PanelSchedule
        {
            PanelId = id,
            PanelName = id,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
        };
        panel.Circuits.AddRange(circuits);
        return panel;
    }

    private static Circuit MakeCircuit(string num, string wireSize = "12", int poles = 1,
        int voltage = 120, double lengthFt = 100, string ground = "12", string desc = "Receptacle")
    {
        return new Circuit
        {
            CircuitNumber = num,
            Description = desc,
            Poles = poles,
            Voltage = voltage,
            Phase = "A",
            ConnectedLoadVA = 1800,
            DemandFactor = 1.0,
            SlotType = CircuitSlotType.Circuit,
            WireLengthFeet = lengthFt,
            Wire = new WireSpec
            {
                Size = wireSize,
                Conductors = poles,
                GroundSize = ground,
                InsulationType = "THHN",
                Material = ConductorMaterial.Copper,
            },
        };
    }

    // ── Generate Basic ───────────────────────────────────────────────────────

    [Fact]
    public void Generate_SingleCircuit_ProducesOnePull()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1"));
        var schedule = WirePullScheduleService.Generate(new[] { panel }, "Test");

        Assert.Single(schedule.Entries);
        Assert.Equal("WP-001", schedule.Entries[0].PullId);
        Assert.Equal("LP-1", schedule.Entries[0].PanelId);
        Assert.Equal("1", schedule.Entries[0].CircuitNumber);
    }

    [Fact]
    public void Generate_SkipsSpareSlots()
    {
        var spare = new Circuit { CircuitNumber = "2", SlotType = CircuitSlotType.Spare };
        var panel = MakePanel("LP-1", MakeCircuit("1"), spare);
        var schedule = WirePullScheduleService.Generate(new[] { panel });

        Assert.Single(schedule.Entries);
    }

    [Fact]
    public void Generate_SkipsCircuitsWithoutWire()
    {
        var noWire = new Circuit
        {
            CircuitNumber = "3",
            SlotType = CircuitSlotType.Circuit,
            Wire = null,
        };
        var panel = MakePanel("LP-1", MakeCircuit("1"), noWire);
        var schedule = WirePullScheduleService.Generate(new[] { panel });

        Assert.Single(schedule.Entries);
    }

    [Fact]
    public void Generate_MultipleCircuits_SequentialPullIds()
    {
        var panel = MakePanel("LP-1",
            MakeCircuit("1"),
            MakeCircuit("3"),
            MakeCircuit("5"));
        var schedule = WirePullScheduleService.Generate(new[] { panel });

        Assert.Equal(3, schedule.Entries.Count);
        Assert.Equal("WP-001", schedule.Entries[0].PullId);
        Assert.Equal("WP-002", schedule.Entries[1].PullId);
        Assert.Equal("WP-003", schedule.Entries[2].PullId);
    }

    [Fact]
    public void Generate_MultiplePanels_ContinuesPullNumbering()
    {
        var p1 = MakePanel("LP-1", MakeCircuit("1"));
        var p2 = MakePanel("LP-2", MakeCircuit("1"), MakeCircuit("3"));
        var schedule = WirePullScheduleService.Generate(new[] { p1, p2 });

        Assert.Equal(3, schedule.Entries.Count);
        Assert.Equal("LP-2", schedule.Entries[2].PanelId);
        Assert.Equal("WP-003", schedule.Entries[2].PullId);
    }

    // ── Wire Properties ──────────────────────────────────────────────────────

    [Fact]
    public void Generate_CapturesWireProperties()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", wireSize: "10", ground: "10"));
        var entry = WirePullScheduleService.Generate(new[] { panel }).Entries[0];

        Assert.Equal("10", entry.WireSize);
        Assert.Equal("10", entry.GroundSize);
        Assert.Equal("THHN", entry.InsulationType);
        Assert.Equal("Copper", entry.Material);
    }

    [Fact]
    public void Generate_SinglePole_HasNeutral()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", poles: 1));
        var entry = WirePullScheduleService.Generate(new[] { panel }).Entries[0];

        Assert.True(entry.HasNeutral);
        Assert.Equal(1, entry.HotConductors);
    }

    [Fact]
    public void Generate_TwoPole208V_NoNeutral()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", poles: 2, voltage: 208));
        var entry = WirePullScheduleService.Generate(new[] { panel }).Entries[0];

        Assert.False(entry.HasNeutral);
        Assert.Equal(2, entry.HotConductors);
    }

    [Fact]
    public void Generate_ConductorSummary_Format()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", wireSize: "12", ground: "12"));
        var entry = WirePullScheduleService.Generate(new[] { panel }).Entries[0];

        // 1-pole → 1 hot + neutral + ground → "1#12, 1#12N, 1#12G"
        Assert.Contains("#12", entry.ConductorSummary);
        Assert.Contains("N", entry.ConductorSummary);
        Assert.Contains("G", entry.ConductorSummary);
    }

    // ── Length ────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_CapturesWireLength()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", lengthFt: 150));
        var entry = WirePullScheduleService.Generate(new[] { panel }).Entries[0];

        Assert.Equal(150, entry.LengthFeet);
    }

    [Fact]
    public void Generate_ZeroLengthCircuit_StillIncluded()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", lengthFt: 0));
        var schedule = WirePullScheduleService.Generate(new[] { panel });

        Assert.Single(schedule.Entries);
        Assert.Equal(0, schedule.Entries[0].LengthFeet);
    }

    // ── Summary ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_Summary_TotalPulls()
    {
        var panel = MakePanel("LP-1",
            MakeCircuit("1", lengthFt: 100),
            MakeCircuit("3", lengthFt: 200));
        var schedule = WirePullScheduleService.Generate(new[] { panel });

        Assert.Equal(2, schedule.Summary.TotalPulls);
        Assert.Equal(300, schedule.Summary.TotalWireFeet);
    }

    [Fact]
    public void Generate_Summary_WireFeetBySize()
    {
        var panel = MakePanel("LP-1",
            MakeCircuit("1", wireSize: "12", lengthFt: 100),
            MakeCircuit("3", wireSize: "10", lengthFt: 200));
        var schedule = WirePullScheduleService.Generate(new[] { panel });

        Assert.True(schedule.Summary.WireFeetBySize.ContainsKey("12"));
        Assert.True(schedule.Summary.WireFeetBySize.ContainsKey("10"));
    }

    // ── Text Report ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateTextReport_ContainsProjectName()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1"));
        var schedule = WirePullScheduleService.Generate(new[] { panel }, "My Project");
        var text = WirePullScheduleService.GenerateTextReport(schedule);

        Assert.Contains("My Project", text);
        Assert.Contains("WIRE PULL SCHEDULE", text);
    }

    [Fact]
    public void GenerateTextReport_ContainsPullEntries()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", desc: "Office Receptacle"));
        var schedule = WirePullScheduleService.Generate(new[] { panel });
        var text = WirePullScheduleService.GenerateTextReport(schedule);

        Assert.Contains("WP-001", text);
        Assert.Contains("LP-1", text);
        Assert.Contains("Office Receptacle", text);
    }

    [Fact]
    public void GenerateTextReport_ContainsWireSummary()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", wireSize: "12", lengthFt: 100));
        var schedule = WirePullScheduleService.Generate(new[] { panel });
        var text = WirePullScheduleService.GenerateTextReport(schedule);

        Assert.Contains("WIRE SUMMARY BY SIZE", text);
        Assert.Contains("#12", text);
    }

    // ── Empty Input ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_EmptySchedules_ReturnsEmptySchedule()
    {
        var schedule = WirePullScheduleService.Generate(Array.Empty<PanelSchedule>());

        Assert.Empty(schedule.Entries);
        Assert.Equal(0, schedule.Summary.TotalPulls);
    }

    // ── From / To ────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_FromField_IsPanelName()
    {
        var panel = MakePanel("LP-1", MakeCircuit("1", desc: "Break Room"));
        var entry = WirePullScheduleService.Generate(new[] { panel }).Entries[0];

        Assert.Equal("LP-1", entry.From);
        Assert.Equal("Break Room", entry.To);
    }
}
