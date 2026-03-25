using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class SymbolLegendServiceTests
{
    [Fact]
    public void GenerateLegend_WithComponents_ReturnsEntries()
    {
        var service = new SymbolLegendService();
        var library = new ElectricalSymbolLibrary();
        var components = new[]
        {
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box),
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Panel)
        };

        var legend = service.GenerateLegend(components, library);

        Assert.NotEmpty(legend.Entries);
        Assert.All(legend.Entries, entry => Assert.NotEmpty(entry.SymbolName));
    }

    [Fact]
    public void GenerateLegend_Empty_ReturnsEmptyEntries()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateLegend(Array.Empty<ElectricalComponent>(), new ElectricalSymbolLibrary());

        Assert.Empty(legend.Entries);
    }

    [Fact]
    public void GenerateLegend_CountsMultipleSameType()
    {
        var service = new SymbolLegendService();
        var library = new ElectricalSymbolLibrary();
        var components = new[]
        {
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box),
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box),
            ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box)
        };

        var legend = service.GenerateLegend(components, library);
        var boxEntry = legend.Entries.Single(entry => entry.ComponentType == ComponentType.Box);

        Assert.Equal(3, boxEntry.Count);
    }

    [Fact]
    public void GenerateLegend_TitleIsSymbolLegend()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateLegend(Array.Empty<ElectricalComponent>(), new ElectricalSymbolLibrary());

        Assert.Equal("SYMBOL LEGEND", legend.Title);
    }

    [Fact]
    public void GenerateFromSymbolNames_KnownSymbol_ReturnsEntry()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateFromSymbolNames(new[] { "Duplex Receptacle" }, new ElectricalSymbolLibrary());

        var entry = Assert.Single(legend.Entries);
        Assert.Equal("Duplex Receptacle", entry.SymbolName);
        Assert.Equal("Receptacles", entry.Category);
        Assert.NotNull(entry.SymbolDefinition);
    }

    [Fact]
    public void GenerateFromSymbolNames_UnknownSymbol_SkipsIt()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateFromSymbolNames(new[] { "Unknown Symbol" }, new ElectricalSymbolLibrary());

        Assert.Empty(legend.Entries);
    }

    [Fact]
    public void ToScheduleTable_HasCorrectTitle()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateFromSymbolNames(new[] { "Duplex Receptacle" }, new ElectricalSymbolLibrary());

        var table = service.ToScheduleTable(legend);

        Assert.Equal("SYMBOL LEGEND", table.Title);
    }

    [Fact]
    public void ToScheduleTable_ColumnsIncludeCountColumn()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateFromSymbolNames(new[] { "Duplex Receptacle" }, new ElectricalSymbolLibrary());

        var table = service.ToScheduleTable(legend);

        Assert.Contains(table.Columns, column => column.Header.Contains("COUNT", StringComparison.OrdinalIgnoreCase) ||
                                                column.Header.Contains("QTY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TotalWidth_SumsColumnWidths()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateFromSymbolNames(new[] { "Duplex Receptacle" }, new ElectricalSymbolLibrary());

        var table = service.ToScheduleTable(legend);
        var expected = table.Columns.Sum(column => column.Width);

        Assert.Equal(expected, table.TotalWidth);
    }

    [Fact]
    public void TotalHeight_IncludesTitleAndRows()
    {
        var service = new SymbolLegendService();
        var legend = service.GenerateFromSymbolNames(
            new[] { "Duplex Receptacle", "GFCI Receptacle" },
            new ElectricalSymbolLibrary());

        var table = service.ToScheduleTable(legend);
        var expected = table.TitleHeight + table.RowHeight + (table.Rows.Count * table.RowHeight);

        Assert.Equal(expected, table.TotalHeight);
    }
}
