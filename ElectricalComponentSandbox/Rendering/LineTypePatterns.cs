using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// Maps <see cref="LineType"/> enum values to SkiaSharp dash patterns
/// for use in <see cref="RenderStyle.DashPattern"/>.
/// Standard CAD line type definitions per ISO 128 / ANSI Y14.2.
/// </summary>
public static class LineTypePatterns
{
    /// <summary>
    /// Returns a float[] dash pattern for the given line type, or null for Continuous.
    /// Values are in screen pixels and should be scaled by zoom if needed.
    /// </summary>
    public static float[]? GetDashPattern(LineType lineType) => lineType switch
    {
        LineType.Continuous => null,
        LineType.Dashed     => new[] { 8f, 4f },
        LineType.Dotted     => new[] { 2f, 3f },
        LineType.Hidden     => new[] { 4f, 3f },
        LineType.Center     => new[] { 12f, 3f, 4f, 3f },
        LineType.Phantom    => new[] { 16f, 3f, 4f, 3f, 4f, 3f },
        LineType.DashDot    => new[] { 8f, 3f, 2f, 3f },
        LineType.DashDotDot => new[] { 8f, 3f, 2f, 3f, 2f, 3f },
        _ => null
    };

    /// <summary>
    /// Applies line type dash pattern and line weight from a layer to a RenderStyle.
    /// </summary>
    public static RenderStyle ApplyLayerStyle(RenderStyle baseStyle, Layer layer)
    {
        return new RenderStyle
        {
            StrokeColor = baseStyle.StrokeColor,
            StrokeWidth = layer.LineWeight > 0 ? layer.LineWeight : baseStyle.StrokeWidth,
            FillColor = baseStyle.FillColor,
            FontFamily = baseStyle.FontFamily,
            FontSize = baseStyle.FontSize,
            Opacity = baseStyle.Opacity,
            Bold = baseStyle.Bold,
            DashPattern = GetDashPattern(layer.LineType)
        };
    }
}
