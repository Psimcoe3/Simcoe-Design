using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Rendering;

public class LineTypePatternsTests
{
    [Fact]
    public void GetDashPattern_Continuous_ReturnsNull()
    {
        Assert.Null(LineTypePatterns.GetDashPattern(LineType.Continuous));
    }

    [Theory]
    [InlineData(LineType.Dashed)]
    [InlineData(LineType.Dotted)]
    [InlineData(LineType.Hidden)]
    [InlineData(LineType.Center)]
    [InlineData(LineType.Phantom)]
    [InlineData(LineType.DashDot)]
    [InlineData(LineType.DashDotDot)]
    public void GetDashPattern_NonContinuous_ReturnsNonEmptyArray(LineType lineType)
    {
        var pattern = LineTypePatterns.GetDashPattern(lineType);
        Assert.NotNull(pattern);
        Assert.True(pattern!.Length >= 2);
        Assert.All(pattern, val => Assert.True(val > 0));
    }

    [Fact]
    public void GetDashPattern_Center_HasLongDashShortDash()
    {
        var pattern = LineTypePatterns.GetDashPattern(LineType.Center);
        Assert.NotNull(pattern);
        // Center: long-dash, gap, short-dash, gap
        Assert.Equal(4, pattern!.Length);
        Assert.True(pattern[0] > pattern[2], "First dash should be longer than second");
    }

    [Fact]
    public void GetDashPattern_Phantom_HasSixElements()
    {
        var pattern = LineTypePatterns.GetDashPattern(LineType.Phantom);
        Assert.NotNull(pattern);
        // Phantom: long-dash, gap, short-dash, gap, short-dash, gap
        Assert.Equal(6, pattern!.Length);
    }

    [Fact]
    public void ApplyLayerStyle_UsesLayerLineWeight()
    {
        var baseStyle = new RenderStyle { StrokeWidth = 1.0f };
        var layer = new Layer { LineWeight = 2.5, LineType = LineType.Continuous };

        var result = LineTypePatterns.ApplyLayerStyle(baseStyle, layer);

        Assert.Equal(2.5f, result.StrokeWidth);
        Assert.Null(result.DashPattern);
    }

    [Fact]
    public void ApplyLayerStyle_ZeroLineWeight_UsesBaseStyle()
    {
        var baseStyle = new RenderStyle { StrokeWidth = 1.5f };
        var layer = new Layer { LineWeight = 0, LineType = LineType.Dashed };

        var result = LineTypePatterns.ApplyLayerStyle(baseStyle, layer);

        Assert.Equal(1.5f, result.StrokeWidth);
        Assert.NotNull(result.DashPattern);
    }

    [Fact]
    public void ApplyLayerStyle_PreservesBaseStyleColors()
    {
        var baseStyle = new RenderStyle
        {
            StrokeColor = "#FF0000",
            FillColor = "#00FF00",
            FontFamily = "Arial",
            FontSize = 14,
            Opacity = 0.8f,
            Bold = true
        };
        var layer = new Layer { LineWeight = 1.0, LineType = LineType.Hidden };

        var result = LineTypePatterns.ApplyLayerStyle(baseStyle, layer);

        Assert.Equal("#FF0000", result.StrokeColor);
        Assert.Equal("#00FF00", result.FillColor);
        Assert.Equal("Arial", result.FontFamily);
        Assert.Equal(14, result.FontSize);
        Assert.Equal(0.8f, result.Opacity);
        Assert.True(result.Bold);
    }
}
