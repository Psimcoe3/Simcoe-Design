using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SpoolSheetBuilderTests
{
    private static ConduitModelStore CreateStore()
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "emt", Name = "EMT", Standard = ConduitMaterialType.EMT });
        store.Settings.DefaultConduitTypeId = "emt";
        return store;
    }

    private static ConduitSegment Seg(double x1, double x2, string size = "3/4")
        => new()
        {
            StartPoint = new XYZ(x1, 0, 0),
            EndPoint = new XYZ(x2, 0, 0),
            TradeSize = size,
        };

    private static ConduitRun AddRun(
        ConduitModelStore store,
        IEnumerable<ConduitSegment> segments,
        IEnumerable<ConduitFitting>? fittings = null)
    {
        var run = new ConduitRun
        {
            RunId = store.GenerateRunId(),
            TradeSize = "3/4",
            Material = ConduitMaterialType.EMT,
        };
        foreach (var s in segments)
        {
            store.AddSegment(s);
            run.SegmentIds.Add(s.Id);
        }
        foreach (var f in fittings ?? Enumerable.Empty<ConduitFitting>())
        {
            store.AddFitting(f);
            run.FittingIds.Add(f.Id);
        }
        store.AddRun(run);
        return run;
    }

    [Fact]
    public void ClassifyTemplate_StraightRunWithSingleTierHanger_PicksHangers1Tier()
    {
        var bends = new BendSchedule("r", "3/4", ConduitMaterialType.EMT, BendSchedulePattern.Straight, Array.Empty<BendScheduleRow>(), "");
        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateSingleTierDefault() };

        var template = SpoolSheetBuilder.ClassifyTemplate(bends, new[] { hanger });

        Assert.Equal(SpoolSheetTemplate.Hangers1Tier, template);
    }

    [Fact]
    public void ClassifyTemplate_StraightWith3TierHanger_PicksHangers3Tier()
    {
        var bends = new BendSchedule("r", "3/4", ConduitMaterialType.EMT, BendSchedulePattern.Straight, Array.Empty<BendScheduleRow>(), "");
        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateMultiTier(3) };

        var template = SpoolSheetBuilder.ClassifyTemplate(bends, new[] { hanger });

        Assert.Equal(SpoolSheetTemplate.Hangers3Tier, template);
    }

    [Fact]
    public void ClassifyTemplate_Stub90Pattern_PicksStub90Template()
    {
        var bends = new BendSchedule("r", "3/4", ConduitMaterialType.EMT, BendSchedulePattern.Stub90, Array.Empty<BendScheduleRow>(), "");

        var template = SpoolSheetBuilder.ClassifyTemplate(bends, Array.Empty<HangerComponent>());

        Assert.Equal(SpoolSheetTemplate.Stub90, template);
    }

    [Fact]
    public void ClassifyTemplate_3PointSaddlePattern_PicksSaddle3PointTemplate()
    {
        var bends = new BendSchedule("r", "3/4", ConduitMaterialType.EMT, BendSchedulePattern.Saddle3Point, Array.Empty<BendScheduleRow>(), "");

        var template = SpoolSheetBuilder.ClassifyTemplate(bends, Array.Empty<HangerComponent>());

        Assert.Equal(SpoolSheetTemplate.Saddle3Point, template);
    }

    [Fact]
    public void Build_StraightRunWithSingleTier_ProducesCompleteSheet()
    {
        var store = CreateStore();
        var run = AddRun(store, new[] { Seg(0, 20) });
        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateSingleTierDefault() };
        var builder = new SpoolSheetBuilder(store);

        var sheet = builder.Build(run.Id, new[] { hanger });

        Assert.Equal(SpoolSheetTemplate.Hangers1Tier, sheet.Template);
        Assert.Equal(20.0, sheet.GrossLengthFeet, 3);
        Assert.Single(sheet.CutList);
        Assert.Single(sheet.HangerSchedule);
        Assert.NotEmpty(sheet.TrapezeBom.Lines);
        Assert.NotEmpty(sheet.ConduitBom);
    }

    [Fact]
    public void Build_RunWith90_ProducesBendScheduleAndAdjustedCutList()
    {
        var store = CreateStore();
        var seg = Seg(0, 5);
        var elbow = new ConduitFitting
        {
            Type = FittingType.Elbow90,
            AngleDegrees = 90,
            TradeSize = "3/4",
            ConnectedSegmentIds = { seg.Id },
        };
        var run = AddRun(store, new[] { seg }, new[] { elbow });
        var builder = new SpoolSheetBuilder(store);

        var sheet = builder.Build(run.Id);

        Assert.Equal(SpoolSheetTemplate.Stub90, sheet.Template);
        Assert.Single(sheet.BendSchedule.Bends);
        Assert.Equal(BendSchedulePattern.Stub90, sheet.BendSchedule.Bends[0].Pattern);
        Assert.Single(sheet.CutList);
        // 60" gross - 6" full deduct (stub: one connected segment carries full take-up) = 54"
        Assert.Equal(54.0, sheet.CutList[0].CutLengthInches, 3);
    }

    [Fact]
    public void Build_InlineBendBetweenTwoSegments_HalfDeductPerSegment()
    {
        var store = CreateStore();
        var s1 = Seg(0, 5);
        var s2 = Seg(5, 10);
        var elbow = new ConduitFitting
        {
            Type = FittingType.Elbow90,
            AngleDegrees = 90,
            TradeSize = "3/4",
            ConnectedSegmentIds = { s1.Id, s2.Id },
        };
        var run = AddRun(store, new[] { s1, s2 }, new[] { elbow });
        var builder = new SpoolSheetBuilder(store);

        var sheet = builder.Build(run.Id);

        Assert.Equal(2, sheet.CutList.Count);
        // Both segments are 60"; each carries half of the 6" deduct → 57" each.
        Assert.Equal(57.0, sheet.CutList[0].CutLengthInches, 3);
        Assert.Equal(57.0, sheet.CutList[1].CutLengthInches, 3);
    }

    [Fact]
    public void Build_NullRunId_Throws()
    {
        var store = CreateStore();
        var builder = new SpoolSheetBuilder(store);

        Assert.Throws<ArgumentException>(() => builder.Build("no-such-run"));
    }

    [Fact]
    public void Build_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SpoolSheetBuilder(null!));
    }

    [Fact]
    public void ExportCsv_StraightRun_IncludesAllSections()
    {
        var store = CreateStore();
        var run = AddRun(store, new[] { Seg(0, 10) });
        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateSingleTierDefault() };
        var builder = new SpoolSheetBuilder(store);
        var sheet = builder.Build(run.Id, new[] { hanger });

        var csv = SpoolSheetBuilder.ExportCsv(sheet);

        Assert.Contains("## Bend Schedule", csv);
        Assert.Contains("## Cut List", csv);
        Assert.Contains("## Hanger Schedule", csv);
        Assert.Contains("## Trapeze BOM", csv);
        Assert.Contains("## Conduit BOM", csv);
    }

    [Fact]
    public void Build_TitleBlockDefault_HasSheetNumberDerivedFromRunId()
    {
        var store = CreateStore();
        var run = AddRun(store, new[] { Seg(0, 10) });
        var builder = new SpoolSheetBuilder(store);

        var sheet = builder.Build(run.Id);

        Assert.Equal($"SP-{run.RunId}", sheet.TitleBlock.SheetNumber);
        Assert.Contains(run.RunId, sheet.TitleBlock.SheetTitle);
    }

    [Fact]
    public void Build_CustomTitleBlock_FlowsThroughUnchanged()
    {
        var store = CreateStore();
        var run = AddRun(store, new[] { Seg(0, 10) });
        var builder = new SpoolSheetBuilder(store);
        var title = new SpoolSheetTitleBlock
        {
            ProjectNumber = "P-12345",
            ProjectName = "SMC Pilot",
            SheetNumber = "SP-001",
            SheetTitle = "Custom Title",
            SpoolPackage = "PKG-A",
        };

        var sheet = builder.Build(run.Id, titleBlock: title);

        Assert.Equal("P-12345", sheet.TitleBlock.ProjectNumber);
        Assert.Equal("SP-001", sheet.TitleBlock.SheetNumber);
        Assert.Equal("PKG-A", sheet.TitleBlock.SpoolPackage);
    }
}
