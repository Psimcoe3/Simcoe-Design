using System.Collections.ObjectModel;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ScheduleTableServiceTests
{
    private readonly ScheduleTableService _sut = new();

    [Fact]
    public void GenerateEquipmentSchedule_WithComponents_ReturnsTable()
    {
        var components = new[]
        {
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box),
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Panel)
        };

        var table = _sut.GenerateEquipmentSchedule(components);

        Assert.Equal("EQUIPMENT SCHEDULE", table.Title);
        Assert.Equal(2, table.Rows.Count);
        Assert.True(table.Columns.Count >= 5);
    }

    [Fact]
    public void GenerateEquipmentSchedule_WithProjectParameterBindings_IncludesBindingSummaryColumn()
    {
        var component = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box);
        var parameter = new ProjectParameterDefinition { Id = "width-param", Name = "Shared Width", Value = 4.25 };
        component.Parameters.SetBinding(ProjectParameterBindingTarget.Width, parameter.Id);

        var table = _sut.GenerateEquipmentSchedule(new[] { component }, new[] { parameter });

        Assert.Contains(table.Columns, column => column.Header == "PARAMETERS");
        Assert.Contains("W=Shared Width", table.Rows[0]);
    }

    [Fact]
    public void GenerateEquipmentSchedule_Empty_ReturnsEmptyRows()
    {
        var table = _sut.GenerateEquipmentSchedule(Array.Empty<ElectricalComponent>());

        Assert.Equal("EQUIPMENT SCHEDULE", table.Title);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void GenerateConduitSchedule_WithConduits_ReturnsTable()
    {
        var conduit = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Conduit);
        var box = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box);
        var components = new ElectricalComponent[] { conduit, box };

        var table = _sut.GenerateConduitSchedule(components);

        Assert.Equal("CONDUIT SCHEDULE", table.Title);
        Assert.Single(table.Rows); // only conduit, not box
    }

    [Fact]
    public void GeneratePanelSchedule_WithCircuits_ReturnsTable()
    {
        var circuits = new[]
        {
            new Circuit
            {
                CircuitNumber = "1",
                Description = "Lighting",
                Voltage = 120,
                ConnectedLoadVA = 1800,
                Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                Wire = new WireSpec { Size = "12" }
            },
            new Circuit
            {
                CircuitNumber = "3",
                Description = "Receptacles",
                Voltage = 120,
                ConnectedLoadVA = 2400,
                Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                Wire = new WireSpec { Size = "12" }
            }
        };

        var table = _sut.GeneratePanelSchedule("Panel A", circuits);

        Assert.Contains("Panel A", table.Title);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("1", table.Rows[0][0]); // sorted by circuit number
    }

    [Fact]
    public void GenerateCircuitSummary_IncludesVoltageDropColumn()
    {
        var circuits = new[]
        {
            new Circuit
            {
                CircuitNumber = "1",
                Description = "Test",
                Voltage = 120,
                ConnectedLoadVA = 1200,
                WireLengthFeet = 100,
                Breaker = new CircuitBreaker { TripAmps = 15, Poles = 1 },
                Wire = new WireSpec { Size = "12" }
            }
        };

        var table = _sut.GenerateCircuitSummary(circuits);

        Assert.Equal("CIRCUIT SUMMARY", table.Title);
        Assert.Single(table.Rows);
        // Last column should be voltage drop percentage
        var lastCol = table.Rows[0][^1];
        Assert.Contains("%", lastCol);
    }

    [Fact]
    public void GenerateProjectParameterSchedule_IncludesFormulaValuesAndUsageSummary()
    {
        var parameter = new ProjectParameterDefinition
        {
            Id = "width-param",
            Name = "Shared Width",
            Value = 4.25,
            Formula = "2 + 2.25"
        };
        var component = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box);
        component.Parameters.SetBinding(ProjectParameterBindingTarget.Width, parameter.Id);

        var table = _sut.GenerateProjectParameterSchedule(new[] { parameter }, new[] { component });

        Assert.Equal("PROJECT PARAMETERS", table.Title);
        Assert.Single(table.Rows);
        Assert.Equal("Shared Width", table.Rows[0][0]);
        Assert.Equal("Length", table.Rows[0][1]);
        Assert.Equal("4.25", table.Rows[0][2]);
        Assert.Equal("2 + 2.25", table.Rows[0][3]);
        Assert.Equal("W", table.Rows[0][4]);
        Assert.Contains("1 comp", table.Rows[0][5]);
        Assert.Equal("OK", table.Rows[0][6]);
    }

    [Fact]
    public void TotalWidth_SumsColumnWidths()
    {
        var table = _sut.GenerateEquipmentSchedule(new[]
        {
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box)
        });

        double expected = table.Columns.Sum(c => c.Width);
        Assert.Equal(expected, table.TotalWidth);
    }

    [Fact]
    public void TotalHeight_IncludesTitleAndRows()
    {
        var table = _sut.GenerateEquipmentSchedule(new[]
        {
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box)
        });

        // TitleHeight + header RowHeight + 1 data row * RowHeight
        double expected = table.TitleHeight + table.RowHeight + (1 * table.RowHeight);
        Assert.Equal(expected, table.TotalHeight);
    }

    // ── Phase 2: AssignSlotNumbers ────────────────────────────────────────────

    [Fact]
    public void AssignSlotNumbers_Numerical_AssignsSequentialSlots()
    {
        var schedule = new PanelSchedule
        {
            CircuitSequence = CircuitSequence.Numerical,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "3", Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "1", Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "2", Breaker = new() { Poles = 1 } },
            }
        };

        _sut.AssignSlotNumbers(schedule);

        // Should be sorted by number: 1→slot1, 2→slot2, 3→slot3
        var byNum = schedule.Circuits.OrderBy(c => int.Parse(c.CircuitNumber)).ToList();
        Assert.Equal(1, byNum[0].SlotNumber);
        Assert.Equal(2, byNum[1].SlotNumber);
        Assert.Equal(3, byNum[2].SlotNumber);
    }

    [Fact]
    public void AssignSlotNumbers_MultiPole_ConsumesConsecutiveSlots()
    {
        var schedule = new PanelSchedule
        {
            CircuitSequence = CircuitSequence.Numerical,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Breaker = new() { Poles = 2 } },
                new() { CircuitNumber = "3", Breaker = new() { Poles = 1 } },
            }
        };

        _sut.AssignSlotNumbers(schedule);

        var c1 = schedule.Circuits[0]; // 2-pole at slot 1
        var c3 = schedule.Circuits[1]; // 1-pole at slot 3
        Assert.Equal(1, c1.SlotNumber);
        Assert.Equal(3, c3.SlotNumber);
    }

    [Fact]
    public void AssignSlotNumbers_GroupByPhase_OrdersPhaseAThenBThenC()
    {
        var schedule = new PanelSchedule
        {
            CircuitSequence = CircuitSequence.GroupByPhase,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "C", Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "2", Phase = "A", Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "3", Phase = "B", Breaker = new() { Poles = 1 } },
            }
        };

        _sut.AssignSlotNumbers(schedule);

        // Phase A circuit should get the lowest slot
        var phaseA = schedule.Circuits.First(c => c.Phase == "A");
        var phaseB = schedule.Circuits.First(c => c.Phase == "B");
        var phaseC = schedule.Circuits.First(c => c.Phase == "C");
        Assert.True(phaseA.SlotNumber < phaseB.SlotNumber);
        Assert.True(phaseB.SlotNumber < phaseC.SlotNumber);
    }

    [Fact]
    public void AssignSlotNumbers_OddThenEven_PlacesOddCircuitNumbersFirst()
    {
        var schedule = new PanelSchedule
        {
            CircuitSequence = CircuitSequence.OddThenEven,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "2", Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "4", Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "1", Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "3", Breaker = new() { Poles = 1 } },
            }
        };

        _sut.AssignSlotNumbers(schedule);

        // Odd circuit numbers (1, 3) should get lower slot numbers than even (2, 4)
        int slot1 = schedule.Circuits.First(c => c.CircuitNumber == "1").SlotNumber;
        int slot3 = schedule.Circuits.First(c => c.CircuitNumber == "3").SlotNumber;
        int slot2 = schedule.Circuits.First(c => c.CircuitNumber == "2").SlotNumber;
        int slot4 = schedule.Circuits.First(c => c.CircuitNumber == "4").SlotNumber;
        Assert.True(slot1 < slot2);
        Assert.True(slot3 < slot4);
    }

    [Fact]
    public void AssignSlotNumbers_SpareAndSpaceCircuits_ConsumeSlots()
    {
        var schedule = new PanelSchedule
        {
            CircuitSequence = CircuitSequence.Numerical,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Breaker = new() { Poles = 1 }, SlotType = CircuitSlotType.Circuit },
                new() { CircuitNumber = "2", Breaker = new() { Poles = 1 }, SlotType = CircuitSlotType.Spare },
                new() { CircuitNumber = "3", Breaker = new() { Poles = 1 }, SlotType = CircuitSlotType.Space },
            }
        };

        _sut.AssignSlotNumbers(schedule);

        // All three consume one slot each
        Assert.Equal(1, schedule.Circuits[0].SlotNumber);
        Assert.Equal(2, schedule.Circuits[1].SlotNumber);
        Assert.Equal(3, schedule.Circuits[2].SlotNumber);
    }

    // ── Phase 2: GeneratePanelSchedule(PanelSchedule) ────────────────────────

    [Fact]
    public void GeneratePanelSchedule_SlotMap_HasSubheaderAndFooterRows()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-1",
            BusAmps = 200,
            MainBreakerAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1800, Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "2", Phase = "B", ConnectedLoadVA = 1500, Breaker = new() { Poles = 1 } },
            }
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        Assert.True(table.RowStyles.Count >= 2);
        Assert.Contains(ScheduleRowStyle.Subheader, table.RowStyles);
        Assert.Contains(ScheduleRowStyle.Footer, table.RowStyles);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_SubheaderContainsPanelInfo()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-1",
            BusAmps = 225,
            MainBreakerAmps = 200,
            IsMainLugsOnly = false,
            AvailableFaultCurrentKA = 22,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>()
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        int subheaderIdx = table.RowStyles.IndexOf(ScheduleRowStyle.Subheader);
        Assert.True(subheaderIdx >= 0);
        var row = table.Rows[subheaderIdx];
        Assert.Contains("LP-1", row[0]);
        Assert.Contains("225", row[2]);
        Assert.Contains("MB 200A", row[3]);
        Assert.Contains("22", row[4]);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_FooterContainsPhaseLoads()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-1",
            BusAmps = 200,
            MainBreakerAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1800, DemandFactor = 1.0, Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "2", Phase = "B", ConnectedLoadVA = 1200, DemandFactor = 1.0, Breaker = new() { Poles = 1 } },
            }
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        int footerIdx = table.RowStyles.IndexOf(ScheduleRowStyle.Footer);
        Assert.True(footerIdx >= 0);
        var footer = table.Rows[footerIdx];
        // Phase A load should appear
        Assert.Contains("1800", footer[0]);
        // Phase B load should appear
        Assert.Contains("1200", footer[1]);
        // Total demand (3000 VA for 208V 3-phase → current ≈ 8.3A)
        Assert.Contains("3000", footer[3]);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_SpareSlot_HasSpareStyle()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-2",
            BusAmps = 200,
            MainBreakerAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1800, Breaker = new() { Poles = 1 }, SlotType = CircuitSlotType.Circuit, SlotNumber = 1 },
                new() { CircuitNumber = "2", SlotType = CircuitSlotType.Spare, Breaker = new() { Poles = 1 }, SlotNumber = 2 },
            }
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        // Find the row that contains SPARE text
        int spareRowIdx = table.Rows.IndexOf(table.Rows.First(r => r.Any(c => c == "SPARE")));
        Assert.Equal(ScheduleRowStyle.Spare, table.RowStyles[spareRowIdx]);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_SpaceSlot_HasSpaceStyle()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-3",
            BusAmps = 200,
            MainBreakerAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1800, Breaker = new() { Poles = 1 }, SlotType = CircuitSlotType.Circuit, SlotNumber = 1 },
                new() { CircuitNumber = "2", SlotType = CircuitSlotType.Space, Breaker = new() { Poles = 1 }, SlotNumber = 2 },
            }
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        int spaceRowIdx = table.Rows.IndexOf(table.Rows.First(r => r.Any(c => c == "SPACE")));
        Assert.Equal(ScheduleRowStyle.Space, table.RowStyles[spaceRowIdx]);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_TwoPoleCircuit_ShowsContinuationArrow()
    {
        // 2-pole circuit at slot 1,2 (primary on left-slot=1, right-slot=2 is continuation of same circuit)
        // Actually with SlotNumber=1 and Poles=2: occupies slots 1 and 2
        // Row 1 (slots 1,2): left=primary, right=continuation
        var schedule = new PanelSchedule
        {
            PanelName = "LP-4",
            BusAmps = 200,
            MainBreakerAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "AB", Description = "Motor", ConnectedLoadVA = 3000,
                    Breaker = new() { Poles = 2 }, SlotNumber = 1 },
            }
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        // Row 1 (index 1 because index 0 is subheader): left=primary, right=continuation ↓
        int firstCircuitRow = table.RowStyles
            .Select((style, i) => (style, i))
            .First(x => x.style == ScheduleRowStyle.Normal).i;
        var row = table.Rows[firstCircuitRow];
        // Left cells: CKT, Desc, Bkr, VA — primary
        Assert.Equal("1", row[0]);
        Assert.Equal("Motor", row[1]);
        // Right cells: VA, Bkr, Desc, CKT — continuation (↓ marker in description position)
        Assert.Equal("↓", row[6]); // right description = ↓
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_ThreePoleCircuit_OccupiesThreeSlots()
    {
        // 3-pole circuit at slot 1,2,3 — takes up rows 1 (slots 1,2) and row 2 left (slot 3)
        var schedule = new PanelSchedule
        {
            PanelName = "LP-5",
            BusAmps = 200,
            MainBreakerAmps = 200,
            VoltageConfig = PanelVoltageConfig.V277_480_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "ABC", Description = "AHU-1", ConnectedLoadVA = 12000,
                    Breaker = new() { Poles = 3 }, SlotNumber = 1 },
                new() { CircuitNumber = "4", Phase = "A",   ConnectedLoadVA = 1000,
                    Breaker = new() { Poles = 1 }, SlotNumber = 4 },
            }
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        // 4 slots → 2 rows of circuit data, plus subheader + footer = 4 total rows
        Assert.Equal(4, table.Rows.Count);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_MLO_ShowsMLOInSubheader()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "PP-1",
            BusAmps = 400,
            IsMainLugsOnly = true,
            VoltageConfig = PanelVoltageConfig.V277_480_3Ph,
            Circuits = new List<Circuit>()
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        int subheaderIdx = table.RowStyles.IndexOf(ScheduleRowStyle.Subheader);
        Assert.Contains("MLO", table.Rows[subheaderIdx]);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_EmptyPanel_HasSubheaderAndFooterOnly()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "EMPTY",
            BusAmps = 100,
            MainBreakerAmps = 100,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>()
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        // Only subheader + footer, no circuit rows
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(ScheduleRowStyle.Subheader, table.RowStyles[0]);
        Assert.Equal(ScheduleRowStyle.Footer,    table.RowStyles[1]);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_AutoAssignsSlotNumbers_WhenNotSet()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "AUTO",
            BusAmps = 100,
            MainBreakerAmps = 100,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", ConnectedLoadVA = 1000, Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "2", ConnectedLoadVA = 1000, Breaker = new() { Poles = 1 } },
            }
        };
        // Slot numbers default to 0 (unassigned)
        Assert.All(schedule.Circuits, c => Assert.Equal(0, c.SlotNumber));

        _sut.GeneratePanelSchedule(schedule);

        // After generation, slots should be assigned
        Assert.All(schedule.Circuits, c => Assert.True(c.SlotNumber > 0));
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_RowStylesCountMatchesRowsCount()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-6",
            BusAmps = 200,
            MainBreakerAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1800, Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "2", Phase = "B", ConnectedLoadVA = 1200, Breaker = new() { Poles = 1 } },
                new() { CircuitNumber = "3", Phase = "C", ConnectedLoadVA = 2000, Breaker = new() { Poles = 1 } },
            }
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        Assert.Equal(table.Rows.Count, table.RowStyles.Count);
    }

    [Fact]
    public void GeneratePanelSchedule_SlotMap_HasEightColumns()
    {
        var schedule = new PanelSchedule
        {
            PanelName = "LP-7",
            BusAmps = 100,
            MainBreakerAmps = 100,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>()
        };

        var table = _sut.GeneratePanelSchedule(schedule);

        Assert.Equal(8, table.Columns.Count);
    }
}
