using System.IO;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class BomExportServiceTests
{
    [Fact]
    public void GenerateBomCsv_EmptyList_ReturnsHeaderOnly()
    {
        var service = new BomExportService();
        var csv = service.GenerateBomCsv(Array.Empty<ElectricalComponent>());

        Assert.Contains("Item,Name,Type,Material,Width,Height,Depth,Elevation,Quantity", csv);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void GenerateBomCsv_SingleComponent_ReturnsOneRow()
    {
        var service = new BomExportService();
        var components = new[] { new BoxComponent { Name = "Test Box" } };

        var csv = service.GenerateBomCsv(components);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("Test Box", csv);
        Assert.Contains("Box", csv);
    }

    [Fact]
    public void GenerateBomCsv_DuplicateComponents_GroupedWithQuantity()
    {
        var service = new BomExportService();
        var components = new[]
        {
            new BoxComponent { Name = "JB-4x4" },
            new BoxComponent { Name = "JB-4x4" }
        };

        var csv = service.GenerateBomCsv(components);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains(",2", lines[1]); // Quantity = 2
    }

    [Fact]
    public void GenerateBomCsv_DifferentTypes_SeparateRows()
    {
        var service = new BomExportService();
        var components = new ElectricalComponent[]
        {
            new BoxComponent { Name = "Box" },
            new PanelComponent { Name = "Panel" },
            new HangerComponent { Name = "Hanger" }
        };

        var csv = service.GenerateBomCsv(components);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(4, lines.Length); // header + 3 rows
    }

    [Fact]
    public async Task ExportToCsvAsync_CreatesFile()
    {
        var service = new BomExportService();
        var components = new[] { new BoxComponent { Name = "Test" } };
        var path = Path.Combine(Path.GetTempPath(), $"bom_test_{Guid.NewGuid()}.csv");

        try
        {
            await service.ExportToCsvAsync(components, path);
            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            Assert.Contains("Test", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
