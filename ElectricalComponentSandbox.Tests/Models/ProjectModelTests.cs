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
        Assert.Empty(project.ProjectParameters);
        Assert.Empty(project.Layers);
        Assert.Empty(project.Sheets);
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
    public void ProjectParameters_CanBeAdded()
    {
        var project = new ProjectModel();
        project.ProjectParameters.Add(new ProjectParameterDefinition { Name = "Shared Width", Value = 4.25 });

        Assert.Single(project.ProjectParameters);
        Assert.Equal("Shared Width", project.ProjectParameters[0].Name);
        Assert.Equal(4.25, project.ProjectParameters[0].Value, 6);
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

    [Fact]
    public void Sheets_CanBeAdded()
    {
        var project = new ProjectModel();
        project.Sheets.Add(DrawingSheet.CreateDefault(1));
        project.Sheets.Add(new DrawingSheet { Number = "A201", Name = "Lighting Plan" });

        Assert.Equal(2, project.Sheets.Count);
        Assert.Equal("S001 - Sheet 1", project.Sheets[0].DisplayName);
        Assert.Equal("A201 - Lighting Plan", project.Sheets[1].DisplayName);
    }
}
