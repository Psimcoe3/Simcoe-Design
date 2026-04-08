using System.IO;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Newtonsoft.Json.Linq;

namespace ElectricalComponentSandbox.Tests.Services;

public class ComponentFileServiceTests : IDisposable
{
    private readonly ComponentFileService _service;
    private readonly string _tempDir;

    public ComponentFileServiceTests()
    {
        _service = new ComponentFileService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ECS_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_ConduitComponent_RoundTrips()
    {
        var conduit = new ConduitComponent
        {
            Name = "Test Conduit",
            Diameter = 2.0,
            Length = 20.0,
            ConduitType = "PVC",
            Position = new Point3D(1, 2, 3)
        };
        var filePath = Path.Combine(_tempDir, "test.ecomp");

        await _service.SaveComponentAsync(conduit, filePath);
        var loaded = await _service.LoadComponentAsync(filePath);

        Assert.NotNull(loaded);
        Assert.IsType<ConduitComponent>(loaded);
        var loadedConduit = (ConduitComponent)loaded;
        Assert.Equal("Test Conduit", loadedConduit.Name);
        Assert.Equal(2.0, loadedConduit.Diameter);
        Assert.Equal(20.0, loadedConduit.Length);
        Assert.Equal("PVC", loadedConduit.ConduitType);
    }

    [Fact]
    public async Task SaveAndLoad_ComponentWithInteropMetadata_RoundTrips()
    {
        var importedUtc = new DateTime(2026, 4, 7, 12, 30, 0, DateTimeKind.Utc);
        var exportedUtc = new DateTime(2026, 4, 7, 18, 45, 0, DateTimeKind.Utc);
        var reviewedUtc = new DateTime(2026, 4, 8, 9, 15, 0, DateTimeKind.Utc);
        var conduit = new ConduitComponent
        {
            Name = "Interop Conduit",
            InteropMetadata = new ComponentInteropMetadata
            {
                SourceSystem = "Revit",
                SourceDocumentId = "project-guid-001",
                SourceDocumentName = "Campus Power.rvt",
                SourceElementId = "element-12345",
                SourceFamilyName = "Conduit",
                SourceTypeName = "EMT 3-4",
                LastInterchangeFormat = "IFC4",
                LastImportedUtc = importedUtc,
                LastExportedUtc = exportedUtc,
                ReviewStatus = ComponentInteropReviewStatus.Reviewed,
                ReviewedBy = "QA Lead",
                ReviewNote = "Verified imported conduit mapping.",
                LastReviewedUtc = reviewedUtc
            }
        };
        var filePath = Path.Combine(_tempDir, "interop.ecomp");

        await _service.SaveComponentAsync(conduit, filePath);
        var loaded = await _service.LoadComponentAsync(filePath);

        var loadedConduit = Assert.IsType<ConduitComponent>(loaded);
        Assert.Equal("Revit", loadedConduit.InteropMetadata.SourceSystem);
        Assert.Equal("project-guid-001", loadedConduit.InteropMetadata.SourceDocumentId);
        Assert.Equal("Campus Power.rvt", loadedConduit.InteropMetadata.SourceDocumentName);
        Assert.Equal("element-12345", loadedConduit.InteropMetadata.SourceElementId);
        Assert.Equal("Conduit", loadedConduit.InteropMetadata.SourceFamilyName);
        Assert.Equal("EMT 3-4", loadedConduit.InteropMetadata.SourceTypeName);
        Assert.Equal("IFC4", loadedConduit.InteropMetadata.LastInterchangeFormat);
        Assert.Equal(importedUtc, loadedConduit.InteropMetadata.LastImportedUtc);
        Assert.Equal(exportedUtc, loadedConduit.InteropMetadata.LastExportedUtc);
        Assert.Equal(ComponentInteropReviewStatus.Reviewed, loadedConduit.InteropMetadata.ReviewStatus);
        Assert.Equal("QA Lead", loadedConduit.InteropMetadata.ReviewedBy);
        Assert.Equal("Verified imported conduit mapping.", loadedConduit.InteropMetadata.ReviewNote);
        Assert.Equal(reviewedUtc, loadedConduit.InteropMetadata.LastReviewedUtc);
    }

    [Fact]
    public async Task SaveAndLoad_BoxComponent_RoundTrips()
    {
        var box = new BoxComponent
        {
            Name = "Test Box",
            KnockoutCount = 8,
            BoxType = "Device Box"
        };
        var filePath = Path.Combine(_tempDir, "test-box.ecomp");

        await _service.SaveComponentAsync(box, filePath);
        var loaded = await _service.LoadComponentAsync(filePath);

        Assert.NotNull(loaded);
        Assert.IsType<BoxComponent>(loaded);
        var loadedBox = (BoxComponent)loaded;
        Assert.Equal("Test Box", loadedBox.Name);
        Assert.Equal(8, loadedBox.KnockoutCount);
    }

    [Fact]
    public async Task SaveAndLoad_ConduitWithBendPoints_PreservesBendPoints()
    {
        var conduit = new ConduitComponent { Name = "Bent Conduit" };
        conduit.BendPoints.Add(new Point3D(5, 0, 5));
        conduit.BendPoints.Add(new Point3D(10, 0, 10));
        var filePath = Path.Combine(_tempDir, "bent.ecomp");

        await _service.SaveComponentAsync(conduit, filePath);
        var loaded = await _service.LoadComponentAsync(filePath) as ConduitComponent;

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.BendPoints.Count);
        Assert.Equal(new Point3D(5, 0, 5), loaded.BendPoints[0]);
        Assert.Equal(new Point3D(10, 0, 10), loaded.BendPoints[1]);
    }

    [Fact]
    public async Task ExportToJson_CreatesFile()
    {
        var conduit = new ConduitComponent { Name = "Export Test" };
        var filePath = Path.Combine(_tempDir, "export.json");

        await _service.ExportToJsonAsync(conduit, filePath);

        Assert.True(File.Exists(filePath));
        var json = await File.ReadAllTextAsync(filePath);
        Assert.Contains("Export Test", json);
    }

    [Fact]
    public async Task ExportToJson_MultipleComponents_CreatesJsonArray()
    {
        var components = new ElectricalComponent[]
        {
            new ConduitComponent { Name = "Conduit 1" },
            new BoxComponent { Name = "Box 1" }
        };
        var filePath = Path.Combine(_tempDir, "export-multiple.json");

        await _service.ExportToJsonAsync(components, filePath);

        Assert.True(File.Exists(filePath));
        var json = await File.ReadAllTextAsync(filePath);
        var array = JArray.Parse(json);
        Assert.Equal(2, array.Count);
        Assert.Equal("Conduit 1", array[0]?["Name"]?.Value<string>());
        Assert.Equal("Box 1", array[1]?["Name"]?.Value<string>());
    }

    [Fact]
    public async Task LoadComponent_LegacyFileWithoutInteropMetadata_InitializesEmptyMetadata()
    {
        var conduit = new ConduitComponent { Name = "Legacy Component" };
        var filePath = Path.Combine(_tempDir, "legacy.ecomp");

        await _service.SaveComponentAsync(conduit, filePath);

        var json = JObject.Parse(await File.ReadAllTextAsync(filePath));
        json.Remove(nameof(ElectricalComponent.InteropMetadata));
        await File.WriteAllTextAsync(filePath, json.ToString());

        var loaded = await _service.LoadComponentAsync(filePath);

        var loadedConduit = Assert.IsType<ConduitComponent>(loaded);
        Assert.NotNull(loadedConduit.InteropMetadata);
        Assert.False(loadedConduit.InteropMetadata.HasAnyValue);
    }

    [Fact]
    public async Task LoadComponent_InvalidFile_ThrowsException()
    {
        var filePath = Path.Combine(_tempDir, "invalid.ecomp");
        await File.WriteAllTextAsync(filePath, "not valid json");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LoadComponentAsync(filePath));
    }

    [Fact]
    public void GetFileFilter_ReturnsExpectedFilter()
    {
        var filter = ComponentFileService.GetFileFilter();

        Assert.Contains("*.ecomp", filter);
    }
}
