using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Shared geometry helpers for selecting and moving rendered markups on the 2D canvas.
/// </summary>
public sealed class MarkupInteractionService
{
    public IReadOnlyList<MarkupRecord> GetSelectionSet(MarkupRecord selectedMarkup, IEnumerable<MarkupRecord> allMarkups)
    {
        var groupId = GetGroupId(selectedMarkup);
        if (string.IsNullOrWhiteSpace(groupId))
            return new[] { selectedMarkup };

        return allMarkups
            .Where(markup => string.Equals(GetGroupId(markup), groupId, StringComparison.Ordinal))
            .ToList();
    }

    public Rect GetAggregateBounds(IEnumerable<MarkupRecord> markups)
    {
        Rect bounds = Rect.Empty;

        foreach (var markup in markups)
        {
            var markupBounds = GetBounds(markup);
            if (markupBounds == Rect.Empty)
                continue;

            bounds = bounds == Rect.Empty ? markupBounds : Rect.Union(bounds, markupBounds);
        }

        return bounds;
    }

    public void Translate(MarkupRecord markup, Vector delta)
    {
        if (delta.X == 0 && delta.Y == 0)
            return;

        for (int i = 0; i < markup.Vertices.Count; i++)
            markup.Vertices[i] = markup.Vertices[i] + delta;

        if (markup.BoundingRect != Rect.Empty)
        {
            markup.BoundingRect = new Rect(
                markup.BoundingRect.X + delta.X,
                markup.BoundingRect.Y + delta.Y,
                markup.BoundingRect.Width,
                markup.BoundingRect.Height);
        }
        else
        {
            markup.UpdateBoundingRect();
        }

        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    public MarkupGeometrySnapshot Capture(MarkupRecord markup)
    {
        return new MarkupGeometrySnapshot
        {
            Vertices = markup.Vertices.ToList(),
            BoundingRect = markup.BoundingRect,
            ModifiedUtc = markup.Metadata.ModifiedUtc
        };
    }

    public void Apply(MarkupRecord markup, MarkupGeometrySnapshot snapshot)
    {
        markup.Vertices.Clear();
        markup.Vertices.AddRange(snapshot.Vertices);
        markup.BoundingRect = snapshot.BoundingRect;
        markup.Metadata.ModifiedUtc = snapshot.ModifiedUtc;
    }

    public string? GetGroupId(MarkupRecord markup)
    {
        return markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationGroupIdField, out var groupId)
            ? groupId
            : null;
    }

    private static Rect GetBounds(MarkupRecord markup)
    {
        if (markup.BoundingRect != Rect.Empty)
            return markup.BoundingRect;

        if ((markup.Type == MarkupType.Circle || markup.Type == MarkupType.Arc) && markup.Vertices.Count >= 1)
        {
            var center = markup.Vertices[0];
            return new Rect(center.X - markup.Radius, center.Y - markup.Radius, markup.Radius * 2, markup.Radius * 2);
        }

        if (markup.Vertices.Count == 0)
            return Rect.Empty;

        var minX = markup.Vertices.Min(point => point.X);
        var minY = markup.Vertices.Min(point => point.Y);
        var maxX = markup.Vertices.Max(point => point.X);
        var maxY = markup.Vertices.Max(point => point.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }
}

public sealed class MarkupGeometrySnapshot
{
    public List<Point> Vertices { get; init; } = new();
    public Rect BoundingRect { get; init; }
    public DateTime ModifiedUtc { get; init; }
}
