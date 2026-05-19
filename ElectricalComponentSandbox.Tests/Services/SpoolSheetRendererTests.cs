using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SpoolSheetRendererTests
{
    private static ConduitModelStore CreateStore()
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "emt", Name = "EMT", Standard = ConduitMaterialType.EMT });
        store.Settings.DefaultConduitTypeId = "emt";
        return store;
    }

    private static ConduitSegment Seg(double x1, double x2)
        => new()
        {
            StartPoint = new XYZ(x1, 0, 0),
            EndPoint = new XYZ(x2, 0, 0),
            TradeSize = "3/4",
        };

    private static SpoolSheet BuildSheet(int segmentCount = 1)
    {
        var store = CreateStore();
        var run = new ConduitRun
        {
            RunId = "CR-001",
            TradeSize = "3/4",
            Material = ConduitMaterialType.EMT,
        };
        for (int i = 0; i < segmentCount; i++)
        {
            var seg = Seg(i, i + 1);
            store.AddSegment(seg);
            run.SegmentIds.Add(seg.Id);
        }
        store.AddRun(run);

        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateSingleTierDefault() };
        var builder = new SpoolSheetBuilder(store);
        return builder.Build(run.Id, new[] { hanger });
    }

    [Fact]
    public void Render_NullSheet_Throws()
    {
        var renderer = new SpoolSheetRenderer();
        Assert.Throws<ArgumentNullException>(() => renderer.Render(null!));
    }

    [Fact]
    public void Render_DefaultPaperSize_IsAnsiB()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet);

        Assert.Equal(17.0, geom.PaperWidthInches);
        Assert.Equal(11.0, geom.PaperHeightInches);
    }

    [Fact]
    public void Render_ProducesBorderAndDrawingArea()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet);

        Assert.NotNull(geom.Border.TitleBlockCells);
        Assert.NotEmpty(geom.Border.TitleBlockCells);
        Assert.NotEmpty(geom.Rects);
    }

    [Fact]
    public void Render_EmitsHeaderTextWithRunIdAndTemplate()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet);

        Assert.Contains(geom.Texts, t => t.Value.Contains("SPOOL —") && t.Value.Contains("CR-001"));
    }

    [Fact]
    public void Render_EmitsBendScheduleAndCutListTables()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet);

        Assert.Contains(geom.Tables, t => t.Title == "CUT LIST");
        Assert.Contains(geom.Tables, t => t.Title == "BEND SCHEDULE");
    }

    [Fact]
    public void Render_HangerSheet_IncludesHangerScheduleAndTrapezeBom()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet);

        Assert.Contains(geom.Tables, t => t.Title == "HANGER SCHEDULE");
        Assert.Contains(geom.Tables, t => t.Title == "TRAPEZE BOM");
    }

    [Fact]
    public void Render_ArchD_UsesLargerPaperDimensions()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet, PaperSizeType.ARCH_D);

        Assert.Equal(36.0, geom.PaperWidthInches);
        Assert.Equal(24.0, geom.PaperHeightInches);
    }

    [Fact]
    public void Render_TablesAreStackedTopToBottom_WithoutOverlap()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet);

        for (int i = 1; i < geom.Tables.Count; i++)
        {
            var prev = geom.Tables[i - 1];
            var cur = geom.Tables[i];
            Assert.True(cur.Bounds.Y >= prev.Bounds.Bottom - 0.001,
                $"Table '{cur.Title}' (Y={cur.Bounds.Y}) overlaps '{prev.Title}' (Bottom={prev.Bounds.Bottom})");
        }
    }

    [Fact]
    public void Render_HeaderRowExistsForEveryTable()
    {
        var renderer = new SpoolSheetRenderer();
        var sheet = BuildSheet();

        var geom = renderer.Render(sheet);

        foreach (var table in geom.Tables)
        {
            // Title text appears
            Assert.Contains(geom.Texts, t => t.Value == table.Title && t.Bold);
            // Each header label appears at least once
            foreach (var header in table.Headers)
            {
                Assert.Contains(geom.Texts, t => t.Value == header);
            }
        }
    }
}
