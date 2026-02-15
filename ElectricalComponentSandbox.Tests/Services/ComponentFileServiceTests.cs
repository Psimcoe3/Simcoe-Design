using System.IO;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

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
