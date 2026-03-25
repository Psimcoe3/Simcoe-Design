using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class DimensionStyleTests
{
    [Fact]
    public void Default_HasStandardName()
    {
        var style = new DimensionStyleDefinition();
        Assert.Equal("Standard", style.Name);
    }

    [Fact]
    public void Default_ArchitecturalFormat()
    {
        var style = new DimensionStyleDefinition();
        Assert.Equal(DimensionUnitFormat.Architectural, style.UnitFormat);
    }

    [Fact]
    public void Default_ClosedFilledArrow()
    {
        var style = new DimensionStyleDefinition();
        Assert.Equal(ArrowType.ClosedFilled, style.ArrowType);
    }

    [Fact]
    public void Default_NoTolerance()
    {
        var style = new DimensionStyleDefinition();
        Assert.Equal(ToleranceMode.None, style.ToleranceMode);
    }

    [Fact]
    public void TextHeight_CanBeModified()
    {
        var style = new DimensionStyleDefinition { TextHeight = 0.25 };
        Assert.Equal(0.25, style.TextHeight);
    }

    [Fact]
    public void ArrowSize_CanBeModified()
    {
        var style = new DimensionStyleDefinition { ArrowSize = 0.1875 };
        Assert.Equal(0.1875, style.ArrowSize);
    }

    [Fact]
    public void TextStyle_DefaultValues()
    {
        var ts = new TextStyle();
        Assert.Equal("Standard", ts.Name);
        Assert.Equal("Arial", ts.FontFamily);
        Assert.Equal(1.0, ts.WidthFactor);
        Assert.False(ts.IsBold);
        Assert.False(ts.IsItalic);
    }

    [Fact]
    public void AllUnitFormats_AreEnumerable()
    {
        var formats = Enum.GetValues<DimensionUnitFormat>();
        Assert.Contains(DimensionUnitFormat.Decimal, formats);
        Assert.Contains(DimensionUnitFormat.Architectural, formats);
        Assert.Contains(DimensionUnitFormat.Engineering, formats);
        Assert.Contains(DimensionUnitFormat.Fractional, formats);
        Assert.Contains(DimensionUnitFormat.Scientific, formats);
    }

    [Fact]
    public void AllArrowTypes_AreEnumerable()
    {
        var types = Enum.GetValues<ArrowType>();
        Assert.Equal(6, types.Length);
    }

    [Fact]
    public void ApplyStyle_ConfiguresDimension2DService()
    {
        var style = new DimensionStyleDefinition
        {
            TextHeight = 0.25,
            ArrowSize = 0.1875,
            ExtensionLineOffset = 0.1,
            LinearScaleFactor = 12.0,
            UnitSuffix = " ft",
            UnitFormat = DimensionUnitFormat.Decimal,
            Precision = 3,
            FontFamily = "Consolas",
            DimensionLineColor = "#FF0000",
            LineWeight = 2.0
        };

        var dimService = new ElectricalComponentSandbox.Services.Dimensioning.Dimension2DService();
        dimService.ApplyStyle(style);

        Assert.Equal(0.25, dimService.TextHeight);
        Assert.Equal(0.1875, dimService.ArrowSize);
        Assert.Equal(0.1, dimService.ExtLineOffset);
        Assert.Equal(12.0, dimService.ScaleFactor);
        Assert.Equal(" ft", dimService.UnitSuffix);
        Assert.Equal("F3", dimService.NumberFormat);
        Assert.Equal("#FF0000", dimService.DefaultAppearance.StrokeColor);
        Assert.Equal(2.0, dimService.DefaultAppearance.StrokeWidth);
        Assert.Equal("Consolas", dimService.DefaultAppearance.FontFamily);
    }
}
