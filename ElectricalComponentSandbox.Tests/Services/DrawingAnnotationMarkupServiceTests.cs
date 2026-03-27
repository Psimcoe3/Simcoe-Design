using System.Linq;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class DrawingAnnotationMarkupServiceTests
{
    private readonly DrawingAnnotationMarkupService _sut = new();

    [Fact]
    public void CreateScheduleTableMarkups_IncludesTitleAndCellContent()
    {
        var table = new ScheduleTable
        {
            Title = "TEST SCHEDULE",
            Columns =
            {
                new ScheduleColumn { Header = "NAME", Width = 80 },
                new ScheduleColumn { Header = "VALUE", Width = 60 }
            },
            Rows =
            {
                new[] { "PANEL-A", "480" }
            }
        };

        var markups = _sut.CreateScheduleTableMarkups(table, new Point(100, 120));

        Assert.Contains(markups, m => m.Type == MarkupType.Rectangle && m.Metadata.Subject == "Table Border");
        Assert.Contains(markups, m => m.Type == MarkupType.Text && m.TextContent == "TEST SCHEDULE");
        Assert.Contains(markups, m => m.Type == MarkupType.Text && m.TextContent == "PANEL-A");
        Assert.Contains(markups, m => m.Type == MarkupType.Text && m.TextContent == "480");

        var titleMarkup = Assert.Single(markups.Where(m => m.Type == MarkupType.Text && m.TextContent == "TEST SCHEDULE"));
        Assert.Equal(DrawingAnnotationMarkupService.ScheduleTableAnnotationKind, titleMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField]);
        Assert.Equal(DrawingAnnotationMarkupService.TextRoleTitle, titleMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField]);

        var cellMarkup = Assert.Single(markups.Where(m => m.Type == MarkupType.Text && m.TextContent == "PANEL-A"));
        Assert.Equal(DrawingAnnotationMarkupService.ScheduleTableAnnotationKind, cellMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField]);
        Assert.Equal(DrawingAnnotationMarkupService.TextRoleCell, cellMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField]);
        Assert.Equal("NAME", cellMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField]);
    }

    [Fact]
    public void CreateSymbolLegendMarkups_WithKnownSymbol_AddsSymbolGeometry()
    {
        var library = new ElectricalSymbolLibrary();
        var legend = new SymbolLegendService().GenerateFromSymbolNames(
            new[] { "Duplex Receptacle" },
            library);

        var markups = _sut.CreateSymbolLegendMarkups(legend, new Point(60, 80));

        Assert.Contains(markups, m => m.Type == MarkupType.Circle && m.Metadata.Subject == "Legend Symbol");
        var nameMarkup = Assert.Single(markups.Where(m => m.Type == MarkupType.Text && m.TextContent == "Duplex Receptacle"));
        Assert.Equal(DrawingAnnotationMarkupService.SymbolLegendAnnotationKind, nameMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField]);
        Assert.Equal(DrawingAnnotationMarkupService.TextRoleCell, nameMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField]);
    }

    [Fact]
    public void CreateTitleBlockMarkups_ConvertsInchesToPdfPoints()
    {
        var titleBlockService = new TitleBlockService();
        var template = titleBlockService.GetDefaultTemplate(PaperSizeType.ANSI_B);
        var geometry = titleBlockService.GenerateBorderGeometry(template);

        var markups = _sut.CreateTitleBlockMarkups(geometry, new Point(0, 0));
        var outerBorder = Assert.Single(markups.Where(m =>
            m.Type == MarkupType.Rectangle &&
            m.Metadata.Subject == "Sheet Border" &&
            m.Metadata.Label == "Outer Border"));

        Assert.Equal(17.0 * DrawingAnnotationMarkupService.PdfPointsPerInch, outerBorder.BoundingRect.Width, 6);
        Assert.Equal(11.0 * DrawingAnnotationMarkupService.PdfPointsPerInch, outerBorder.BoundingRect.Height, 6);
        Assert.Contains(markups, m => m.Type == MarkupType.Text && m.Metadata.Subject == "Zone Label");
        var valueMarkups = markups.Where(m => m.Type == MarkupType.Text && m.Metadata.Subject == "Title Block Value").ToList();
        Assert.NotEmpty(valueMarkups);
        Assert.All(valueMarkups, valueMarkup =>
        {
            Assert.Equal(DrawingAnnotationMarkupService.TitleBlockAnnotationKind, valueMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField]);
            Assert.Equal(DrawingAnnotationMarkupService.TextRoleFieldValue, valueMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField]);
        });
    }
}
