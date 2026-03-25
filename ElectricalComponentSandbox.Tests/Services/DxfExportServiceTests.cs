using System.IO;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Export;

namespace ElectricalComponentSandbox.Tests.Services;

public class DxfExportServiceTests
{
    private readonly DxfExportService _svc = new();

    [Fact]
    public void Export_EmptyComponents_CreatesValidDxf()
    {
        var components = Array.Empty<ElectricalComponent>();
        var layers = new List<Layer> { Layer.CreateDefault() };

        var result = ExportToString(components, layers);

        Assert.Contains("HEADER", result);
        Assert.Contains("TABLES", result);
        Assert.Contains("BLOCKS", result);
        Assert.Contains("ENTITIES", result);
        Assert.Contains("EOF", result);
        Assert.Contains("AC1024", result);
    }

    [Fact]
    public void Export_WithLayers_IncludesLayerTable()
    {
        var layers = new List<Layer>
        {
            new Layer { Name = "E-CONDUIT", Color = "#FF0000", LineType = LineType.Continuous },
            new Layer { Name = "E-PANEL", Color = "#00FF00", LineType = LineType.Dashed }
        };
        var components = Array.Empty<ElectricalComponent>();

        var result = ExportToString(components, layers);

        Assert.Contains("E-CONDUIT", result);
        Assert.Contains("E-PANEL", result);
        // Red = ACI 1
        Assert.Contains("  1", result);
        // Dashed linetype name
        Assert.Contains("DASHED", result);
    }

    [Fact]
    public void Export_BoxComponent_HasInsertEntity()
    {
        var box = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box);
        box.Position = new Point3D(10, 5, 20);
        box.LayerId = "E-BOX";

        var components = new List<ElectricalComponent> { box };
        var layers = new List<Layer> { Layer.CreateDefault() };

        var result = ExportToString(components, layers);

        Assert.Contains("INSERT", result);
        Assert.Contains("BOX_BLOCK", result);
        Assert.Contains("E-BOX", result);
    }

    [Fact]
    public void Export_ConduitWithBendPoints_HasPolyline()
    {
        var conduit = (ConduitComponent)ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Conduit);
        conduit.Position = new Point3D(0, 0, 0);
        conduit.BendPoints = new List<Point3D>
        {
            new Point3D(5, 0, 0),
            new Point3D(5, 0, 10),
            new Point3D(10, 0, 10)
        };
        conduit.LayerId = "E-CONDUIT";

        var components = new List<ElectricalComponent> { conduit };
        var layers = new List<Layer> { Layer.CreateDefault() };

        var result = ExportToString(components, layers);

        Assert.Contains("LWPOLYLINE", result);
        Assert.Contains("E-CONDUIT", result);
        // Should have 4 points total (origin + 3 bend points from GetPathPoints)
        Assert.Contains(" 90\r\n4\r\n", result);
    }

    [Fact]
    public void Export_ColorMapping_MapsKnownColors()
    {
        Assert.Equal(1, DxfExportService.MapColorToAci("#FF0000"));
        Assert.Equal(2, DxfExportService.MapColorToAci("#FFFF00"));
        Assert.Equal(3, DxfExportService.MapColorToAci("#00FF00"));
        Assert.Equal(4, DxfExportService.MapColorToAci("#00FFFF"));
        Assert.Equal(5, DxfExportService.MapColorToAci("#0000FF"));
        Assert.Equal(6, DxfExportService.MapColorToAci("#FF00FF"));
        Assert.Equal(7, DxfExportService.MapColorToAci("#FFFFFF"));
        Assert.Equal(8, DxfExportService.MapColorToAci("#808080"));
        Assert.Equal(7, DxfExportService.MapColorToAci("#123456")); // unknown defaults to 7
        Assert.Equal(7, DxfExportService.MapColorToAci(null));
    }

    [Fact]
    public void Export_WritesToFile()
    {
        var box = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box);
        var components = new List<ElectricalComponent> { box };
        var layers = new List<Layer> { Layer.CreateDefault() };

        var path = Path.GetTempFileName() + ".dxf";
        try
        {
            _svc.Export(components, layers, path);

            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("HEADER", content);
            Assert.Contains("EOF", content);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private string ExportToString(
        IReadOnlyList<ElectricalComponent> components,
        IReadOnlyList<Layer> layers)
    {
        var path = Path.GetTempFileName() + ".dxf";
        try
        {
            _svc.Export(components, layers, path);
            return File.ReadAllText(path);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
