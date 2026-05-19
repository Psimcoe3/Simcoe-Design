using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Persistence;
using ElectricalComponentSandbox.Conduit.ViewModels;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Conduit;

public class ConduitViewModelSpoolTests
{
    private static (ConduitViewModel vm, ConduitRun run) CreateViewModelWithRun()
    {
        var vm = new ConduitViewModel();
        var seg = new ConduitSegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0),
            TradeSize = "3/4",
        };
        vm.Store.AddSegment(seg);
        var run = new ConduitRun
        {
            RunId = "CR-PILOT-01",
            TradeSize = "3/4",
            Material = ConduitMaterialType.EMT,
        };
        run.SegmentIds.Add(seg.Id);
        vm.Store.AddRun(run);
        return (vm, run);
    }

    [Fact]
    public void BuildSpoolSheet_ByUserFacingRunId_ResolvesAndBuildsSheet()
    {
        var (vm, run) = CreateViewModelWithRun();

        var sheet = vm.BuildSpoolSheet(run.RunId);

        Assert.Equal(run.RunId, sheet.RunId);
        Assert.Equal(SpoolSheetTemplate.StraightSection, sheet.Template);
        Assert.NotEmpty(sheet.CutList);
    }

    [Fact]
    public void BuildSpoolSheet_ByInternalId_AlsoResolves()
    {
        var (vm, run) = CreateViewModelWithRun();

        var sheet = vm.BuildSpoolSheet(run.Id);

        Assert.Equal(run.RunId, sheet.RunId);
    }

    [Fact]
    public void BuildSpoolSheet_UnknownRunId_Throws()
    {
        var vm = new ConduitViewModel();

        Assert.Throws<ArgumentException>(() => vm.BuildSpoolSheet("missing"));
    }

    [Fact]
    public void BuildSpoolSheet_WithHangers_PopulatesHangerSchedule()
    {
        var (vm, run) = CreateViewModelWithRun();
        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateMultiTier(2) };

        var sheet = vm.BuildSpoolSheet(run.RunId, new[] { hanger });

        Assert.Single(sheet.HangerSchedule);
        Assert.Equal(SpoolSheetTemplate.Hangers2Tier, sheet.Template);
        Assert.NotEmpty(sheet.TrapezeBom.Lines);
    }

    [Fact]
    public void RenderSpoolSheet_DefaultsToAnsiB_AndProducesGeometry()
    {
        var (vm, run) = CreateViewModelWithRun();
        var sheet = vm.BuildSpoolSheet(run.RunId);

        var geom = vm.RenderSpoolSheet(sheet);

        Assert.Equal(17.0, geom.PaperWidthInches);
        Assert.Equal(11.0, geom.PaperHeightInches);
        Assert.NotEmpty(geom.Tables);
    }

    [Fact]
    public void BuildSpoolSheetsForPackage_CachesSheetsOnPackage()
    {
        var (vm, run) = CreateViewModelWithRun();
        var spool = vm.SpoolManager.CreateSpool(new List<string> { run.Id }, "SP-1");

        var sheets = vm.BuildSpoolSheetsForPackage(spool.SpoolId);

        Assert.Single(sheets);
        Assert.Same(sheets[0], spool.Sheets[0]);
    }
}
