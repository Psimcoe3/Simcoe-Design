using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class ProjectModelTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var project = new ProjectModel();

        Assert.Equal("Untitled Project", project.Name);
        Assert.Equal("1.0", project.Version);
        Assert.Empty(project.Components);
        Assert.Empty(project.Layers);
        Assert.Null(project.PdfUnderlay);
        Assert.Equal("Imperial", project.UnitSystem);
        Assert.Equal(1.0, project.GridSize);
        Assert.True(project.ShowGrid);
        Assert.True(project.SnapToGrid);
    }

    [Fact]
    public void Components_CanBeAdded()
    {
        var project = new ProjectModel();
        project.Components.Add(new ConduitComponent());
        project.Components.Add(new BoxComponent());

        Assert.Equal(2, project.Components.Count);
    }

    [Fact]
    public void PdfUnderlay_CanBeSet()
    {
        var project = new ProjectModel();
        project.PdfUnderlay = new PdfUnderlay
        {
            FilePath = "test.pdf",
            PageNumber = 2,
            Opacity = 0.7
        };

        Assert.NotNull(project.PdfUnderlay);
        Assert.Equal("test.pdf", project.PdfUnderlay.FilePath);
        Assert.Equal(2, project.PdfUnderlay.PageNumber);
    }

    [Fact]
    public void Constructor_NamedViewsDefaults()
    {
        var project = new ProjectModel();
        Assert.NotNull(project.NamedViews);
        Assert.Empty(project.NamedViews);
    }

    [Fact]
    public void Constructor_PlotStyleTablesDefaults()
    {
        var project = new ProjectModel();
        Assert.NotNull(project.PlotStyleTables);
        Assert.Empty(project.PlotStyleTables);
    }

    [Fact]
    public void Constructor_PlotLayoutDefaults()
    {
        var project = new ProjectModel();
        Assert.Null(project.PlotLayout);
    }

    [Fact]
    public void NamedViews_CanBeAdded()
    {
        var project = new ProjectModel();
        project.NamedViews.Add(new NamedView { Name = "Top View", PanX = 100, PanY = 200, Zoom = 1.5 });
        project.NamedViews.Add(new NamedView { Name = "Detail A", PanX = 50, PanY = 75, Zoom = 3.0 });

        Assert.Equal(2, project.NamedViews.Count);
        Assert.Equal("Top View", project.NamedViews[0].Name);
        Assert.Equal(3.0, project.NamedViews[1].Zoom);
    }

    [Fact]
    public void PlotStyleTables_CanBeAdded()
    {
        var project = new ProjectModel();
        project.PlotStyleTables.Add(PlotStyleTable.CreateMonochrome());

        Assert.Single(project.PlotStyleTables);
        Assert.Equal("monochrome.ctb", project.PlotStyleTables[0].Name);
    }

    [Fact]
    public void PlotLayout_CanBeSet()
    {
        var project = new ProjectModel();
        project.PlotLayout = new PlotLayout
        {
            PaperSize = PaperSize.ANSI_D,
            PlotScale = 24.0,
            PlotStyleTableName = "custom.ctb"
        };

        Assert.NotNull(project.PlotLayout);
        Assert.Equal(PaperSize.ANSI_D, project.PlotLayout.PaperSize);
        Assert.Equal(24.0, project.PlotLayout.PlotScale);
        Assert.Equal("custom.ctb", project.PlotLayout.PlotStyleTableName);
    }
}
