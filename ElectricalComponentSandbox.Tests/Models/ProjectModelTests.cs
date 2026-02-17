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
}
