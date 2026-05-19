using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Persistence;
using ElectricalComponentSandbox.Models;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Conduit;

public class SpoolManagerTests
{
    private static ConduitModelStore CreateStore()
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "emt", Name = "EMT", Standard = ConduitMaterialType.EMT });
        store.Settings.DefaultConduitTypeId = "emt";
        return store;
    }

    private static (ConduitModelStore store, ConduitRun run) StoreWithSingleRun()
    {
        var store = CreateStore();
        var seg = new ConduitSegment { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(10, 0, 0), TradeSize = "3/4" };
        store.AddSegment(seg);
        var run = new ConduitRun { RunId = "CR-001", TradeSize = "3/4", Material = ConduitMaterialType.EMT };
        run.SegmentIds.Add(seg.Id);
        store.AddRun(run);
        return (store, run);
    }

    [Fact]
    public void NewPackage_DefaultsToDrawnStatus()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);

        var spool = manager.CreateSpool(new List<string> { run.Id });

        Assert.Equal(SpoolPackageStatus.Drawn, spool.Status);
        Assert.Empty(spool.StatusHistory);
        Assert.Null(spool.ReleasedUtc);
    }

    [Fact]
    public void TransitionStatus_DrawnToReleased_RecordsHistoryAndReleaseStamp()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });

        var entry = manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Released, changedBy: "psimcoe3", reason: "Released for fab");

        Assert.Equal(SpoolPackageStatus.Released, spool.Status);
        Assert.Single(spool.StatusHistory);
        Assert.Equal(SpoolPackageStatus.Drawn, entry.From);
        Assert.Equal(SpoolPackageStatus.Released, entry.To);
        Assert.Equal("psimcoe3", entry.ChangedBy);
        Assert.Equal("Released for fab", entry.Reason);
        Assert.NotNull(spool.ReleasedUtc);
        Assert.Equal("psimcoe3", spool.ReleasedBy);
    }

    [Fact]
    public void TransitionStatus_FullLifecycle_RecordsFourEntries()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });

        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Released);
        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.InFab);
        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Shipped);
        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Installed);

        Assert.Equal(SpoolPackageStatus.Installed, spool.Status);
        Assert.Equal(4, spool.StatusHistory.Count);
        Assert.NotNull(spool.ReleasedUtc); // released stamp persisted past further transitions
    }

    [Fact]
    public void TransitionStatus_SameStatus_Throws()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });

        Assert.Throws<InvalidOperationException>(() =>
            manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Drawn));
    }

    [Fact]
    public void TransitionStatus_Backwards_ThrowsByDefault()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });
        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Released);

        Assert.Throws<InvalidOperationException>(() =>
            manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Drawn));
    }

    [Fact]
    public void TransitionStatus_BackwardsWithOverride_Succeeds()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });
        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Released);

        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Drawn, allowBackwards: true, reason: "Pulled release");

        Assert.Equal(SpoolPackageStatus.Drawn, spool.Status);
        Assert.Equal(2, spool.StatusHistory.Count);
    }

    [Fact]
    public void TransitionStatus_UnknownSpool_Throws()
    {
        var manager = new SpoolManager(CreateStore());

        Assert.Throws<ArgumentException>(() =>
            manager.TransitionStatus("no-such-spool", SpoolPackageStatus.Released));
    }

    [Fact]
    public void BuildSheets_CachesPerRunSheetOnPackage()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id }, "SP-PILOT");

        var sheets = manager.BuildSheets(spool.SpoolId);

        Assert.Single(sheets);
        Assert.Single(spool.Sheets);
        Assert.Equal(run.RunId, spool.Sheets[0].RunId);
        Assert.Contains("SP-PILOT", spool.Sheets[0].TitleBlock.SpoolPackage);
    }

    [Fact]
    public void BuildSheets_ResolvesByUserFacingRunId()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        // Use the user-facing RunId rather than the internal GUID
        var spool = manager.CreateSpool(new List<string> { run.RunId });

        var sheets = manager.BuildSheets(spool.SpoolId);

        Assert.Single(sheets);
    }

    [Fact]
    public void BuildSheets_HangerSelectorFlowsThroughToSheet()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });
        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateMultiTier(2) };

        var sheets = manager.BuildSheets(spool.SpoolId, _ => new[] { hanger });

        Assert.Single(sheets);
        Assert.Single(sheets[0].HangerSchedule);
        Assert.Equal(2, sheets[0].HangerSchedule[0].TierCount);
        Assert.NotEmpty(sheets[0].TrapezeBom.Lines);
    }

    [Fact]
    public void BuildSheets_RebuildReplacesCachedSheets()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });

        manager.BuildSheets(spool.SpoolId);
        var firstSheet = spool.Sheets[0];

        manager.BuildSheets(spool.SpoolId);

        Assert.Single(spool.Sheets);
        Assert.NotSame(firstSheet, spool.Sheets[0]);
    }

    [Fact]
    public void BuildSheets_SkipsRunsThatNoLongerExist()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id, "ghost-run" });

        var sheets = manager.BuildSheets(spool.SpoolId);

        Assert.Single(sheets);
    }

    [Fact]
    public void BuildSheets_PropagatesPackageStatusToTitleBlock()
    {
        var (store, run) = StoreWithSingleRun();
        var manager = new SpoolManager(store);
        var spool = manager.CreateSpool(new List<string> { run.Id });
        manager.TransitionStatus(spool.SpoolId, SpoolPackageStatus.Released);

        var sheets = manager.BuildSheets(spool.SpoolId);

        Assert.Equal("RELEASED", sheets[0].TitleBlock.Status);
    }
}
