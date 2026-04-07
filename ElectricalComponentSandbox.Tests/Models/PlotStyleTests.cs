using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class PlotStyleTests
{
    [Fact]
    public void PlotStylePen_Defaults()
    {
        var pen = new PlotStylePen();

        Assert.Equal(0, pen.PenNumber);
        Assert.Null(pen.OutputColor);
        Assert.Equal(0.0, pen.LineWeight);
        Assert.Equal(0, pen.LineEndStyle);
        Assert.Equal(100, pen.Screening);
    }

    [Fact]
    public void PlotStylePen_PropertiesRoundTrip()
    {
        var pen = new PlotStylePen
        {
            PenNumber = 7,
            OutputColor = "#FF0000",
            LineWeight = 0.50,
            LineEndStyle = 2,
            Screening = 50
        };

        Assert.Equal(7, pen.PenNumber);
        Assert.Equal("#FF0000", pen.OutputColor);
        Assert.Equal(0.50, pen.LineWeight);
        Assert.Equal(2, pen.LineEndStyle);
        Assert.Equal(50, pen.Screening);
    }

    [Fact]
    public void PlotStyleTable_Defaults()
    {
        var table = new PlotStyleTable();

        Assert.Equal("Default", table.Name);
        Assert.Equal(string.Empty, table.Description);
        Assert.Empty(table.Pens);
    }

    [Fact]
    public void PlotStyleTable_CreateMonochrome_Has256Pens()
    {
        var table = PlotStyleTable.CreateMonochrome();

        Assert.Equal("monochrome.ctb", table.Name);
        Assert.Equal(256, table.Pens.Count);
    }

    [Fact]
    public void PlotStyleTable_CreateMonochrome_AllPensBlackAt025mm()
    {
        var table = PlotStyleTable.CreateMonochrome();

        foreach (var pen in table.Pens)
        {
            Assert.Equal("#000000", pen.OutputColor);
            Assert.Equal(0.25, pen.LineWeight);
        }
    }

    [Fact]
    public void PlotStyleTable_CreateMonochrome_PenNumbersSequential()
    {
        var table = PlotStyleTable.CreateMonochrome();

        for (int i = 0; i < 256; i++)
        {
            Assert.Equal(i, table.Pens[i].PenNumber);
        }
    }

    [Fact]
    public void PlotLayout_Defaults()
    {
        var layout = new PlotLayout();

        Assert.Equal("Layout1", layout.Name);
        Assert.Equal(PaperSize.ANSI_D, layout.PaperSize);
        Assert.Equal(22.0, layout.CustomWidth);
        Assert.Equal(34.0, layout.CustomHeight);
        Assert.Equal(1.0, layout.PlotScale);
        Assert.Equal("monochrome.ctb", layout.PlotStyleTableName);
    }

    [Theory]
    [InlineData(PaperSize.Letter,  8.5,   11.0)]
    [InlineData(PaperSize.Legal,   8.5,   14.0)]
    [InlineData(PaperSize.Tabloid, 11.0,  17.0)]
    [InlineData(PaperSize.ANSI_C,  17.0,  22.0)]
    [InlineData(PaperSize.ANSI_D,  22.0,  34.0)]
    [InlineData(PaperSize.ANSI_E,  34.0,  44.0)]
    [InlineData(PaperSize.A4,      8.27,  11.69)]
    [InlineData(PaperSize.A3,      11.69, 16.54)]
    [InlineData(PaperSize.A2,      16.54, 23.39)]
    [InlineData(PaperSize.A1,      23.39, 33.11)]
    [InlineData(PaperSize.A0,      33.11, 46.81)]
    public void PlotLayout_GetPaperInches_StandardSizes(PaperSize size, double expectedW, double expectedH)
    {
        var layout = new PlotLayout { PaperSize = size };
        var (w, h) = layout.GetPaperInches();

        Assert.Equal(expectedW, w);
        Assert.Equal(expectedH, h);
    }

    [Fact]
    public void PlotLayout_GetPaperInches_CustomSize()
    {
        var layout = new PlotLayout
        {
            PaperSize = PaperSize.Custom,
            CustomWidth = 48.0,
            CustomHeight = 36.0
        };

        var (w, h) = layout.GetPaperInches();
        Assert.Equal(48.0, w);
        Assert.Equal(36.0, h);
    }

    [Fact]
    public void PlotLayout_Clone_CopiesValues()
    {
        var layout = new PlotLayout
        {
            Name = "Permit Set",
            PaperSize = PaperSize.ANSI_C,
            CustomWidth = 18.0,
            CustomHeight = 24.0,
            PlotScale = 12.0,
            PlotStyleTableName = "custom.ctb"
        };

        var clone = layout.Clone();

        Assert.NotSame(layout, clone);
        Assert.Equal(layout.Name, clone.Name);
        Assert.Equal(layout.PaperSize, clone.PaperSize);
        Assert.Equal(layout.CustomWidth, clone.CustomWidth);
        Assert.Equal(layout.CustomHeight, clone.CustomHeight);
        Assert.Equal(layout.PlotScale, clone.PlotScale);
        Assert.Equal(layout.PlotStyleTableName, clone.PlotStyleTableName);
    }

    [Fact]
    public void PlotLayout_ApplyFrom_UpdatesValues()
    {
        var target = new PlotLayout();
        var source = new PlotLayout
        {
            Name = "Issue Set",
            PaperSize = PaperSize.ANSI_E,
            PlotScale = 48.0,
            PlotStyleTableName = "issue.ctb"
        };

        target.ApplyFrom(source);

        Assert.Equal("Issue Set", target.Name);
        Assert.Equal(PaperSize.ANSI_E, target.PaperSize);
        Assert.Equal(48.0, target.PlotScale);
        Assert.Equal("issue.ctb", target.PlotStyleTableName);
    }

    [Fact]
    public void PlotLayout_GetSummaryText_IncludesPaperScaleAndCtb()
    {
        var layout = new PlotLayout
        {
            Name = "Permit Set",
            PaperSize = PaperSize.ANSI_D,
            PlotScale = 24.0,
            PlotStyleTableName = "permit.ctb"
        };

        var summary = layout.GetSummaryText();

        Assert.Contains("Permit Set", summary);
        Assert.Contains("ANSI_D", summary);
        Assert.Contains("24", summary);
        Assert.Contains("permit.ctb", summary);
    }

    [Fact]
    public void PaperSize_AllEnumValues_AreHandled()
    {
        var layout = new PlotLayout();
        foreach (PaperSize size in Enum.GetValues(typeof(PaperSize)))
        {
            layout.PaperSize = size;
            var (w, h) = layout.GetPaperInches();
            Assert.True(w > 0, $"Width should be > 0 for {size}");
            Assert.True(h > 0, $"Height should be > 0 for {size}");
        }
    }
}
