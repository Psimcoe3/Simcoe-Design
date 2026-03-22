using System.IO;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class ProjectFileServiceTests : IDisposable
{
    private readonly ProjectFileService _service;
    private readonly string _tempDir;

    public ProjectFileServiceTests()
    {
        _service = new ProjectFileService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ECS_Proj_Tests_{Guid.NewGuid()}");
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
    public async Task SaveAndLoad_EmptyProject_RoundTrips()
    {
        var project = new ProjectModel { Name = "Test Project" };
        var filePath = Path.Combine(_tempDir, "test.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal("Test Project", loaded.Name);
    }

    [Fact]
    public async Task SaveAndLoad_ProjectWithComponents_RoundTrips()
    {
        var project = new ProjectModel { Name = "Full Project" };
        project.Components.Add(new BoxComponent { Name = "Box 1" });
        project.Components.Add(new ConduitComponent { Name = "Conduit 1" });
        project.Components.Add(new CableTrayComponent { Name = "Tray 1" });
        project.Components.Add(new HangerComponent { Name = "Hanger 1" });
        project.Layers.Add(Layer.CreateDefault());
        var filePath = Path.Combine(_tempDir, "full.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(4, loaded.Components.Count);
        Assert.Single(loaded.Layers);
        Assert.IsType<BoxComponent>(loaded.Components[0]);
        Assert.IsType<CableTrayComponent>(loaded.Components[2]);
        Assert.IsType<HangerComponent>(loaded.Components[3]);
    }

    [Fact]
    public async Task SaveAndLoad_ProjectSettings_Preserved()
    {
        var project = new ProjectModel
        {
            UnitSystem = "Metric",
            GridSize = 2.5,
            ShowGrid = false,
            SnapToGrid = false
        };
        var filePath = Path.Combine(_tempDir, "settings.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal("Metric", loaded.UnitSystem);
        Assert.Equal(2.5, loaded.GridSize);
        Assert.False(loaded.ShowGrid);
        Assert.False(loaded.SnapToGrid);
    }

    [Fact]
    public async Task SaveAndLoad_PdfUnderlay_Preserved()
    {
        var project = new ProjectModel();
        project.PdfUnderlay = new PdfUnderlay
        {
            FilePath = "test.pdf",
            PageNumber = 2,
            Opacity = 0.7,
            Scale = 1.5,
            IsCalibrated = true,
            PixelsPerUnit = 48.0
        };
        var filePath = Path.Combine(_tempDir, "pdf.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded?.PdfUnderlay);
        Assert.Equal("test.pdf", loaded.PdfUnderlay.FilePath);
        Assert.Equal(2, loaded.PdfUnderlay.PageNumber);
        Assert.Equal(0.7, loaded.PdfUnderlay.Opacity);
        Assert.True(loaded.PdfUnderlay.IsCalibrated);
        Assert.Equal(48.0, loaded.PdfUnderlay.PixelsPerUnit);
    }

    [Fact]
    public async Task LoadProject_InvalidFile_ThrowsException()
    {
        var filePath = Path.Combine(_tempDir, "invalid.ecproj");
        await File.WriteAllTextAsync(filePath, "not valid json");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LoadProjectAsync(filePath));
    }

    [Fact]
    public void GetFileFilter_ReturnsExpectedFilter()
    {
        var filter = ProjectFileService.GetFileFilter();

        Assert.Contains("*.ecproj", filter);
    }

    [Fact]
    public async Task SaveAndLoad_NamedViews_RoundTrips()
    {
        var project = new ProjectModel();
        project.NamedViews.Add(new NamedView { Name = "Plan View", PanX = 100, PanY = 200, Zoom = 1.5, VisibleLayerIds = new List<string> { "layer-1" } });
        project.NamedViews.Add(new NamedView { Name = "Detail A", PanX = 50, PanY = 75, Zoom = 3.0 });
        var filePath = Path.Combine(_tempDir, "namedviews.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.NamedViews.Count);
        Assert.Equal("Plan View", loaded.NamedViews[0].Name);
        Assert.Equal(100, loaded.NamedViews[0].PanX);
        Assert.Equal(200, loaded.NamedViews[0].PanY);
        Assert.Equal(1.5, loaded.NamedViews[0].Zoom);
        var visibleLayerIds = loaded.NamedViews[0].VisibleLayerIds;
        Assert.NotNull(visibleLayerIds);
        Assert.Single(visibleLayerIds);
        Assert.Equal("layer-1", visibleLayerIds[0]);
        Assert.Equal("Detail A", loaded.NamedViews[1].Name);
        Assert.Equal(3.0, loaded.NamedViews[1].Zoom);
    }

    [Fact]
    public async Task SaveAndLoad_PlotStyleTables_RoundTrips()
    {
        var project = new ProjectModel();
        project.PlotStyleTables.Add(PlotStyleTable.CreateMonochrome());
        var filePath = Path.Combine(_tempDir, "plotstyles.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.PlotStyleTables);
        Assert.Equal("monochrome.ctb", loaded.PlotStyleTables[0].Name);
        Assert.Equal(256, loaded.PlotStyleTables[0].Pens.Count);
    }

    [Fact]
    public async Task SaveAndLoad_PlotLayout_RoundTrips()
    {
        var project = new ProjectModel();
        project.PlotLayout = new PlotLayout
        {
            PaperSize = PaperSize.ANSI_D,
            PlotScale = 24.0,
            CustomWidth = 11.0,
            CustomHeight = 8.5
        };
        var filePath = Path.Combine(_tempDir, "plotlayout.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded?.PlotLayout);
        Assert.Equal(PaperSize.ANSI_D, loaded.PlotLayout.PaperSize);
        Assert.Equal(24.0, loaded.PlotLayout.PlotScale);
    }
}
