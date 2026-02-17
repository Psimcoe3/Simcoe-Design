using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class PdfUnderlayTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var underlay = new PdfUnderlay();

        Assert.Equal(string.Empty, underlay.FilePath);
        Assert.Equal(1, underlay.PageNumber);
        Assert.Equal(0.5, underlay.Opacity);
        Assert.True(underlay.IsLocked);
        Assert.Equal(1.0, underlay.Scale);
        Assert.Equal(0.0, underlay.OffsetX);
        Assert.Equal(0.0, underlay.OffsetY);
        Assert.False(underlay.IsCalibrated);
        Assert.Equal(1.0, underlay.PixelsPerUnit);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var underlay = new PdfUnderlay();

        underlay.FilePath = "floor_plan.pdf";
        underlay.PageNumber = 3;
        underlay.Opacity = 0.3;
        underlay.IsLocked = false;
        underlay.Scale = 2.5;
        underlay.IsCalibrated = true;
        underlay.PixelsPerUnit = 48.0;

        Assert.Equal("floor_plan.pdf", underlay.FilePath);
        Assert.Equal(3, underlay.PageNumber);
        Assert.Equal(0.3, underlay.Opacity);
        Assert.False(underlay.IsLocked);
        Assert.Equal(2.5, underlay.Scale);
        Assert.True(underlay.IsCalibrated);
        Assert.Equal(48.0, underlay.PixelsPerUnit);
    }
}
