using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// Enumerates the four 3D visual styles (matching AutoCAD / Revit modes).
/// Extracted from MainWindow code-behind for reuse and testability.
/// </summary>
public enum VisualStyle3D
{
    Realistic,
    Conceptual,
    Wireframe,
    XRay
}

/// <summary>
/// Builds WPF 3D materials according to the active visual style.
/// Extracted from MainWindow to reduce code-behind and improve testability.
/// </summary>
public static class MaterialFactory
{
    /// <summary>Builds the WPF 3D material for a component according to the active visual style.</summary>
    public static Material Build(VisualStyle3D style, Color baseColor, bool isSelected)
    {
        Material built = style switch
        {
            VisualStyle3D.Conceptual => BuildConceptual(baseColor),
            VisualStyle3D.Wireframe  => BuildWireframe(baseColor),
            VisualStyle3D.XRay       => BuildXRay(baseColor),
            _                        => new DiffuseMaterial(new SolidColorBrush(baseColor))
        };

        if (!isSelected) return built;

        var group = new MaterialGroup();
        group.Children.Add(built);
        group.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(80, 255, 165, 0))));
        return group;
    }

    public static Material BuildConceptual(Color c)
    {
        byte gray = (byte)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
        byte r = (byte)Math.Min(255, (c.R * 0.4 + gray * 0.6) * 1.15);
        byte g = (byte)Math.Min(255, (c.G * 0.4 + gray * 0.6) * 1.15);
        byte b = (byte)Math.Min(255, (c.B * 0.4 + gray * 0.6) * 1.15);
        var flat = Color.FromRgb(r, g, b);

        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(new SolidColorBrush(flat)));
        group.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(40, r, g, b))));
        return group;
    }

    public static Material BuildWireframe(Color c)
    {
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(
            new SolidColorBrush(Color.FromArgb(12, c.R, c.G, c.B))));
        group.Children.Add(new EmissiveMaterial(
            new SolidColorBrush(Color.FromArgb(200, c.R, c.G, c.B))));
        return group;
    }

    public static Material BuildXRay(Color c)
    {
        return new DiffuseMaterial(
            new SolidColorBrush(Color.FromArgb(90, c.R, c.G, c.B)));
    }
}
