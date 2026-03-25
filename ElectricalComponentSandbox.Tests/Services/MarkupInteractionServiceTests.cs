using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MarkupInteractionServiceTests
{
    private readonly MarkupInteractionService _sut = new();

    [Fact]
    public void GetSelectionSet_GroupedMarkup_ReturnsWholeGroup()
    {
        var groupId = "group-1";
        var first = CreateMarkup(new Rect(0, 0, 10, 10), groupId);
        var second = CreateMarkup(new Rect(20, 0, 10, 10), groupId);
        var third = CreateMarkup(new Rect(40, 0, 10, 10), "group-2");

        var selection = _sut.GetSelectionSet(first, new[] { first, second, third });

        Assert.Equal(2, selection.Count);
        Assert.Contains(first, selection);
        Assert.Contains(second, selection);
        Assert.DoesNotContain(third, selection);
    }

    [Fact]
    public void Translate_TextMarkup_ShiftsVertexAndBounds()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "NOTE",
            BoundingRect = new Rect(10, 20, 40, 12)
        };
        markup.Vertices.Add(new Point(10, 32));

        _sut.Translate(markup, new Vector(15, -5));

        Assert.Equal(new Point(25, 27), markup.Vertices[0]);
        Assert.Equal(new Rect(25, 15, 40, 12), markup.BoundingRect);
    }

    [Fact]
    public void GetAggregateBounds_UnionsAllMarkupBounds()
    {
        var first = CreateMarkup(new Rect(0, 0, 10, 10), "group-1");
        var second = CreateMarkup(new Rect(20, 5, 10, 15), "group-1");

        var bounds = _sut.GetAggregateBounds(new[] { first, second });

        Assert.Equal(new Rect(0, 0, 30, 20), bounds);
    }

    private static MarkupRecord CreateMarkup(Rect rect, string groupId)
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = rect
        };
        markup.Vertices.Add(rect.TopLeft);
        markup.Vertices.Add(rect.BottomRight);
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = groupId;
        return markup;
    }
}
