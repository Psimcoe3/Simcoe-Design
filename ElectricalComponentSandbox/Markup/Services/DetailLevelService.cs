using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Detail level for rendering markup objects (Revit-inspired)
/// </summary>
public enum DetailLevel
{
    /// <summary>Simplified single-line/symbol representation</summary>
    Coarse,
    /// <summary>Intermediate detail</summary>
    Medium,
    /// <summary>Full geometric detail (3D mesh-ready)</summary>
    Fine
}

/// <summary>
/// Describes how a markup should be rendered at a given detail level.
/// Coarse = symbolic lines; Medium = simplified outlines; Fine = full geometry.
/// </summary>
public class DetailLevelRepresentation
{
    /// <summary>
    /// Symbolic 2D line segments for Coarse view (start/end pairs)
    /// </summary>
    public List<(Point Start, Point End)> SymbolicLines { get; set; } = new();

    /// <summary>
    /// Outline vertices for Medium view
    /// </summary>
    public List<Point> OutlineVertices { get; set; } = new();

    /// <summary>
    /// Whether to show fill at this detail level
    /// </summary>
    public bool ShowFill { get; set; }

    /// <summary>
    /// Whether to show dimensions/annotations at this level
    /// </summary>
    public bool ShowAnnotations { get; set; }
}

/// <summary>
/// Generates detail-level representations for markup records
/// </summary>
public static class DetailLevelService
{
    /// <summary>
    /// Gets the appropriate representation for a markup at the given detail level
    /// </summary>
    public static DetailLevelRepresentation GetRepresentation(MarkupRecord markup, DetailLevel level)
    {
        return level switch
        {
            DetailLevel.Coarse => GetCoarseRepresentation(markup),
            DetailLevel.Medium => GetMediumRepresentation(markup),
            DetailLevel.Fine => GetFineRepresentation(markup),
            _ => GetFineRepresentation(markup)
        };
    }

    private static DetailLevelRepresentation GetCoarseRepresentation(MarkupRecord markup)
    {
        var rep = new DetailLevelRepresentation
        {
            ShowFill = false,
            ShowAnnotations = true
        };

        switch (markup.Type)
        {
            case MarkupType.Polyline:
            case MarkupType.ConduitRun:
                // Single center-line
                for (int i = 1; i < markup.Vertices.Count; i++)
                    rep.SymbolicLines.Add((markup.Vertices[i - 1], markup.Vertices[i]));
                break;

            case MarkupType.Rectangle:
                if (markup.Vertices.Count >= 2)
                {
                    // Diagonal cross
                    var tl = markup.Vertices[0];
                    var br = markup.Vertices[1];
                    rep.SymbolicLines.Add((tl, br));
                    rep.SymbolicLines.Add((new Point(br.X, tl.Y), new Point(tl.X, br.Y)));
                }
                break;

            case MarkupType.Circle:
                if (markup.Vertices.Count >= 1)
                {
                    // Cross through center
                    var c = markup.Vertices[0];
                    double r = markup.Radius;
                    rep.SymbolicLines.Add((new Point(c.X - r, c.Y), new Point(c.X + r, c.Y)));
                    rep.SymbolicLines.Add((new Point(c.X, c.Y - r), new Point(c.X, c.Y + r)));
                }
                break;

            default:
                // For polygon-based types, show edges only
                for (int i = 0; i < markup.Vertices.Count; i++)
                {
                    var a = markup.Vertices[i];
                    var b = markup.Vertices[(i + 1) % markup.Vertices.Count];
                    rep.SymbolicLines.Add((a, b));
                }
                break;
        }

        return rep;
    }

    private static DetailLevelRepresentation GetMediumRepresentation(MarkupRecord markup)
    {
        var rep = new DetailLevelRepresentation
        {
            ShowFill = false,
            ShowAnnotations = true,
            OutlineVertices = new List<Point>(markup.Vertices)
        };

        // Show edges
        for (int i = 0; i < markup.Vertices.Count; i++)
        {
            int next = (markup.Type == MarkupType.Polyline || markup.Type == MarkupType.ConduitRun)
                ? i + 1
                : (i + 1) % markup.Vertices.Count;
            if (next < markup.Vertices.Count)
                rep.SymbolicLines.Add((markup.Vertices[i], markup.Vertices[next]));
        }

        return rep;
    }

    private static DetailLevelRepresentation GetFineRepresentation(MarkupRecord markup)
    {
        var rep = new DetailLevelRepresentation
        {
            ShowFill = true,
            ShowAnnotations = true,
            OutlineVertices = new List<Point>(markup.Vertices)
        };

        // Full edges with fill
        for (int i = 0; i < markup.Vertices.Count; i++)
        {
            int next = (markup.Type == MarkupType.Polyline || markup.Type == MarkupType.ConduitRun)
                ? i + 1
                : (i + 1) % markup.Vertices.Count;
            if (next < markup.Vertices.Count)
                rep.SymbolicLines.Add((markup.Vertices[i], markup.Vertices[next]));
        }

        return rep;
    }
}
