using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class TitleBlockServiceTests
{
    [Fact]
    public void GetStandardPaperSize_ANSI_B_Returns17x11()
    {
        var size = TitleBlockService.GetStandardPaperSize(PaperSizeType.ANSI_B);

        Assert.Equal(17.0, size.Width);
        Assert.Equal(11.0, size.Height);
    }

    [Fact]
    public void GetStandardPaperSize_ISO_A4_Returns11_69x8_27()
    {
        var size = TitleBlockService.GetStandardPaperSize(PaperSizeType.ISO_A4);

        Assert.Equal(11.69, size.Width, 2);
        Assert.Equal(8.27, size.Height, 2);
    }

    [Fact]
    public void GetDefaultTemplate_HasName()
    {
        var template = new TitleBlockService().GetDefaultTemplate(PaperSizeType.ANSI_D);

        Assert.False(string.IsNullOrWhiteSpace(template.Name));
        Assert.Equal(PaperSizeType.ANSI_D, template.PaperSize);
    }

    [Fact]
    public void GenerateBorderGeometry_HasOuterBorder()
    {
        var service = new TitleBlockService();
        var template = service.GetDefaultTemplate(PaperSizeType.ANSI_D);

        var geometry = service.GenerateBorderGeometry(template);

        Assert.Equal(0, geometry.OuterBorder.X);
        Assert.Equal(0, geometry.OuterBorder.Y);
        Assert.Equal(template.PaperWidth, geometry.OuterBorder.Width);
        Assert.Equal(template.PaperHeight, geometry.OuterBorder.Height);
    }

    [Fact]
    public void GenerateBorderGeometry_InnerBorderSmallerThanOuter()
    {
        var service = new TitleBlockService();
        var template = service.GetDefaultTemplate(PaperSizeType.ANSI_D);

        var geometry = service.GenerateBorderGeometry(template);

        Assert.True(geometry.InnerBorder.Width < geometry.OuterBorder.Width);
        Assert.True(geometry.InnerBorder.Height < geometry.OuterBorder.Height);
    }

    [Fact]
    public void GenerateBorderGeometry_HasZoneMarks()
    {
        var service = new TitleBlockService();
        var geometry = service.GenerateBorderGeometry(service.GetDefaultTemplate(PaperSizeType.ANSI_D));

        Assert.NotEmpty(geometry.ZoneMarks);
        Assert.Contains(geometry.ZoneMarks, m => m.Label == "A" && m.IsHorizontal);
        Assert.Contains(geometry.ZoneMarks, m => m.Label == "1" && !m.IsHorizontal);
    }

    [Fact]
    public void GenerateBorderGeometry_HasTitleBlockCells()
    {
        var service = new TitleBlockService();
        var geometry = service.GenerateBorderGeometry(service.GetDefaultTemplate(PaperSizeType.ANSI_D));

        Assert.NotEmpty(geometry.TitleBlockCells);
        Assert.Contains(geometry.TitleBlockCells, c => c.Label == "COMPANY");
        Assert.Contains(geometry.TitleBlockCells, c => c.Label == "PROJECT");
        Assert.Contains(geometry.TitleBlockCells, c => c.Label == "DRAWING NO");
        Assert.Contains(geometry.TitleBlockCells, c => c.Label == "REVISIONS");
    }
}
