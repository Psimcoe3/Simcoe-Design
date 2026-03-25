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
}
