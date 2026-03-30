using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services.Export;

namespace ElectricalComponentSandbox.Tests.Services;

public class XfdfExportServiceTests
{
    private readonly XfdfExportService _svc = new();

    [Fact]
    public void Export_ProducesValidXfdf()
    {
        var markups = new[]
        {
            new MarkupRecord
            {
                Id = "test-1",
                Type = MarkupType.Rectangle,
                Vertices = { new Point(10, 20), new Point(100, 80) },
                Metadata = { Label = "Test markup", Author = "TestUser" }
            }
        };

        var xfdf = _svc.Export(markups);

        Assert.Contains("<?xml", xfdf);
        Assert.Contains("<xfdf", xfdf);
        Assert.Contains("<annots>", xfdf);
        Assert.Contains("square", xfdf); // Rectangle → square in XFDF
        Assert.Contains("test-1", xfdf);
    }

    [Fact]
    public void Export_MapsTypesCorrectly()
    {
        var markups = new[]
        {
            new MarkupRecord { Type = MarkupType.Rectangle },
            new MarkupRecord { Type = MarkupType.Circle },
            new MarkupRecord { Type = MarkupType.Text },
            new MarkupRecord { Type = MarkupType.Polyline, Vertices = { new Point(0,0), new Point(10,10) } },
            new MarkupRecord { Type = MarkupType.Polygon, Vertices = { new Point(0,0), new Point(10,0), new Point(5,10) } },
            new MarkupRecord { Type = MarkupType.Dimension, Vertices = { new Point(0,0), new Point(100,0) } },
            new MarkupRecord { Type = MarkupType.Stamp },
        };

        var xfdf = _svc.Export(markups);

        Assert.Contains("square", xfdf);
        Assert.Contains("circle", xfdf);
        Assert.Contains("freetext", xfdf);
        Assert.Contains("ink", xfdf);
        Assert.Contains("polygon", xfdf);
        Assert.Contains("line", xfdf);
        Assert.Contains("stamp", xfdf);
    }

    [Fact]
    public void RoundTrip_PreservesBasicProperties()
    {
        var original = new MarkupRecord
        {
            Id = "roundtrip-1",
            Type = MarkupType.Rectangle,
            AssignedTo = "Field Team",
            Metadata = { Author = "Tester", Subject = "Electrical" },
            Appearance = { StrokeColor = "#0000FF", StrokeWidth = 3.0, Opacity = 0.8 },
            BoundingRect = new Rect(10, 20, 90, 60),
            Replies =
            {
                new MarkupReply
                {
                    Author = "Coordinator",
                    Text = "Need updated panel schedule before closeout."
                }
            }
        };

        var xfdf = _svc.Export(new[] { original });
        var imported = _svc.Import(xfdf);

        Assert.Single(imported);
        var result = imported[0];

        Assert.Equal("roundtrip-1", result.Id);
        Assert.Equal(MarkupType.Rectangle, result.Type);
        Assert.Equal("Tester", result.Metadata.Author);
        Assert.Equal("Field Team", result.AssignedTo);
        Assert.Equal("#0000FF", result.Appearance.StrokeColor);
        Assert.Equal(3.0, result.Appearance.StrokeWidth);
        Assert.Equal(0.8, result.Appearance.Opacity);
        Assert.Single(result.Replies);
        Assert.Equal("Coordinator", result.Replies[0].Author);
        Assert.Equal("Need updated panel schedule before closeout.", result.Replies[0].Text);
    }

    [Fact]
    public void RoundTrip_PreservesPolylineVertices()
    {
        var original = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(50, 25), new Point(100, 0) }
        };

        var xfdf = _svc.Export(new[] { original });
        var imported = _svc.Import(xfdf);

        Assert.Single(imported);
        Assert.Equal(3, imported[0].Vertices.Count);
        Assert.Equal(0, imported[0].Vertices[0].X);
        Assert.Equal(50, imported[0].Vertices[1].X);
        Assert.Equal(100, imported[0].Vertices[2].X);
    }

    [Fact]
    public void RoundTrip_PreservesLineStartEnd()
    {
        var original = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 20), new Point(110, 20) }
        };

        var xfdf = _svc.Export(new[] { original });
        var imported = _svc.Import(xfdf);

        Assert.Single(imported);
        Assert.Equal(2, imported[0].Vertices.Count);
        Assert.Equal(10, imported[0].Vertices[0].X);
        Assert.Equal(110, imported[0].Vertices[1].X);
    }

    [Fact]
    public void Import_EmptyXfdf_ReturnsEmptyList()
    {
        var xfdf = "<?xml version=\"1.0\"?><xfdf xmlns=\"http://ns.adobe.com/xfdf/\"><annots></annots></xfdf>";
        var result = _svc.Import(xfdf);
        Assert.Empty(result);
    }
}
