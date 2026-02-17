using System.Windows;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Interface for providing parsed PDF vector geometry for snap-to-PDF features.
/// Stub for future implementation (e.g., via PdfPig or iTextSharp).
/// </summary>
public interface IPdfGeometryProvider
{
    /// <summary>
    /// Gets all line segment endpoints from the current PDF page (in document space)
    /// </summary>
    IReadOnlyList<Point> GetEndpoints();

    /// <summary>
    /// Gets all line segments from the current PDF page (in document space)
    /// </summary>
    IReadOnlyList<(Point Start, Point End)> GetSegments();

    /// <summary>
    /// Gets the page dimensions in document units (PDF points)
    /// </summary>
    (double Width, double Height) GetPageSize();

    /// <summary>
    /// Whether geometry has been parsed and is available
    /// </summary>
    bool IsLoaded { get; }
}

/// <summary>
/// Null implementation that returns empty geometry (used until real PDF parsing is added)
/// </summary>
public class NullPdfGeometryProvider : IPdfGeometryProvider
{
    public bool IsLoaded => false;

    public IReadOnlyList<Point> GetEndpoints() => Array.Empty<Point>();

    public IReadOnlyList<(Point Start, Point End)> GetSegments() =>
        Array.Empty<(Point, Point)>();

    public (double Width, double Height) GetPageSize() => (612, 792); // Letter size in points
}
