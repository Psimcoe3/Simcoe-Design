using System.IO;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services.Export;

namespace ElectricalComponentSandbox.Tests.Services;

public class IfcExportServiceTests
{
    private readonly IfcExportService _svc = new();

    [Fact]
    public void Export_ProducesValidIfcFile()
    {
        var components = new ElectricalComponent[]
        {
            new BoxComponent
            {
                Name = "JB-1",
                Position = new Point3D(5, 10, 0),
                Parameters = { Width = 4, Height = 4, Depth = 2, Elevation = 8 }
            }
        };

        var path = Path.GetTempFileName() + ".ifc";
        try
        {
            _svc.ExportToIfc(components, path);

            var content = File.ReadAllText(path);

            Assert.Contains("ISO-10303-21", content);
            Assert.Contains("IFC4", content);
            Assert.Contains("ENDSEC", content);
            Assert.Contains("END-ISO-10303-21", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_WritesRealGeometry()
    {
        var components = new ElectricalComponent[]
        {
            new BoxComponent
            {
                Name = "JB-1",
                Position = new Point3D(5, 10, 0),
                Parameters = { Width = 4, Height = 4, Depth = 2, Elevation = 8 }
            }
        };

        var path = Path.GetTempFileName() + ".ifc";
        try
        {
            _svc.ExportToIfc(components, path);
            var content = File.ReadAllText(path);

            // Should contain IfcExtrudedAreaSolid for real geometry
            Assert.Contains("IFCEXTRUDEDAREASOLID", content);
            // Should contain IfcShapeRepresentation
            Assert.Contains("IFCSHAPEREPRESENTATION", content);
            // Should contain IfcProductDefinitionShape
            Assert.Contains("IFCPRODUCTDEFINITIONSHAPE", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_WritesActualPosition()
    {
        var components = new ElectricalComponent[]
        {
            new PanelComponent
            {
                Name = "LP-1",
                Position = new Point3D(15.5, 22.3, 0),
                Parameters = { Elevation = 5.0 }
            }
        };

        var path = Path.GetTempFileName() + ".ifc";
        try
        {
            _svc.ExportToIfc(components, path);
            var content = File.ReadAllText(path);

            // Should contain the actual XY coordinates
            Assert.Contains("15.5", content);
            Assert.Contains("22.3", content);
            Assert.Contains("5.", content); // Elevation
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_ConduitUsesCircularProfile()
    {
        var components = new ElectricalComponent[]
        {
            new ConduitComponent
            {
                Name = "C-1",
                Diameter = 1.0,
                Length = 10.0,
                Position = new Point3D(0, 0, 0)
            }
        };

        var path = Path.GetTempFileName() + ".ifc";
        try
        {
            _svc.ExportToIfc(components, path);
            var content = File.ReadAllText(path);

            Assert.Contains("IFCCIRCLEPROFILEDEF", content);
            Assert.Contains("IFCCABLECARRIERSEGMENT", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_WritesPropertySets()
    {
        var components = new ElectricalComponent[]
        {
            new ConduitComponent
            {
                Name = "C-1",
                ConduitType = "EMT",
                Diameter = 0.75,
                Length = 12.5,
                Parameters = { Manufacturer = "Allied Tube" }
            }
        };

        var path = Path.GetTempFileName() + ".ifc";
        try
        {
            _svc.ExportToIfc(components, path);
            var content = File.ReadAllText(path);

            Assert.Contains("Pset_CableCarrierSegmentTypeConduit", content);
            Assert.Contains("NominalLength", content);
            Assert.Contains("TradeSize", content);
            Assert.Contains("ConduitType", content);
            Assert.Contains("EMT", content);
            Assert.Contains("Allied Tube", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_PanelWritesPanelPSet()
    {
        var components = new ElectricalComponent[]
        {
            new PanelComponent
            {
                Name = "LP-1",
                Amperage = 200,
                CircuitCount = 42,
                PanelType = "Distribution Panel"
            }
        };

        var path = Path.GetTempFileName() + ".ifc";
        try
        {
            _svc.ExportToIfc(components, path);
            var content = File.ReadAllText(path);

            Assert.Contains("Pset_ElectricalPanel", content);
            Assert.Contains("CircuitCount", content);
            Assert.Contains("Amperage", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_MultipleComponents_AllContainedInStorey()
    {
        var components = new ElectricalComponent[]
        {
            new BoxComponent { Name = "JB-1" },
            new BoxComponent { Name = "JB-2" },
            new ConduitComponent { Name = "C-1" },
        };

        var path = Path.GetTempFileName() + ".ifc";
        try
        {
            _svc.ExportToIfc(components, path);
            var content = File.ReadAllText(path);

            Assert.Contains("IFCRELCONTAINEDINSPATIALSTRUCTURE", content);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
