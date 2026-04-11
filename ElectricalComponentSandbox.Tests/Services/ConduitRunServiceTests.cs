using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ConduitRunServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static ConduitModelStore CreateStore()
    {
        var store = new ConduitModelStore();
        var emt = new ConduitType
        {
            Id = "emt-type",
            Name = "EMT Conduit",
            Standard = ConduitMaterialType.EMT,
            IsWithFitting = true
        };
        store.AddType(emt);
        store.Settings.DefaultConduitTypeId = emt.Id;
        store.Settings.AutoInsertFittings = true;
        return store;
    }

    /// <summary>Creates a horizontal segment along the X axis.</summary>
    private static ConduitSegment Seg(double x1, double x2, double y = 0)
    {
        return new ConduitSegment
        {
            StartPoint = new XYZ(x1, y, 0),
            EndPoint = new XYZ(x2, y, 0),
            TradeSize = "3/4"
        };
    }

    /// <summary>Creates a segment from (x1,y1) to (x2,y2).</summary>
    private static ConduitSegment Seg2D(double x1, double y1, double x2, double y2)
    {
        return new ConduitSegment
        {
            StartPoint = new XYZ(x1, y1, 0),
            EndPoint = new XYZ(x2, y2, 0),
            TradeSize = "3/4"
        };
    }

    // ── CreateRun ────────────────────────────────────────────────────────

    [Fact]
    public void CreateRun_SingleSegment_CreatesRun()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 10) });

        Assert.NotNull(run);
        Assert.Single(run.SegmentIds);
        Assert.NotEmpty(run.RunId);
    }

    [Fact]
    public void CreateRun_MultipleCollinearSegments_InsertsCouplings()
    {
        var store = CreateStore();
        var segments = new List<ConduitSegment> { Seg(0, 5), Seg(5, 10), Seg(10, 15) };
        var run = ConduitRunService.CreateRun(store, segments);

        Assert.Equal(3, run.SegmentIds.Count);
        // Collinear segments (angle ≈ 0°) produce coupling fittings per routing preferences
        Assert.Equal(2, run.FittingIds.Count);
        var fitting = store.GetFitting(run.FittingIds[0]);
        Assert.Equal(FittingType.Coupling, fitting!.Type);
    }

    [Fact]
    public void CreateRun_WithBend_InsertsFitting()
    {
        var store = CreateStore();
        // Horizontal then vertical = 90 degree bend
        var segments = new List<ConduitSegment>
        {
            Seg2D(0, 0, 10, 0),
            Seg2D(10, 0, 10, 10)
        };
        var run = ConduitRunService.CreateRun(store, segments);

        Assert.Equal(2, run.SegmentIds.Count);
        Assert.Single(run.FittingIds);
    }

    [Fact]
    public void CreateRun_EmptySegments_Throws()
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() =>
            ConduitRunService.CreateRun(store, new List<ConduitSegment>()));
    }

    [Fact]
    public void CreateRun_WithTypeOverride_AppliesType()
    {
        var store = CreateStore();
        var seg = Seg(0, 10);

        ConduitRunService.CreateRun(store, new List<ConduitSegment> { seg },
            conduitTypeId: "emt-type", tradeSize: "1");

        Assert.Equal("emt-type", seg.ConduitTypeId);
        Assert.Equal("1", seg.TradeSize);
    }

    // ── GetTotalLength ───────────────────────────────────────────────────

    [Fact]
    public void GetTotalLength_SingleSegment_ReturnsLength()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 10) });

        double len = ConduitRunService.GetTotalLength(store, run.Id);
        Assert.Equal(10.0, len, 3);
    }

    [Fact]
    public void GetTotalLength_MultipleSegments_SumsLengths()
    {
        var store = CreateStore();
        var segments = new List<ConduitSegment> { Seg(0, 5), Seg(5, 12) };
        var run = ConduitRunService.CreateRun(store, segments);

        double len = ConduitRunService.GetTotalLength(store, run.Id);
        Assert.Equal(12.0, len, 3);
    }

    [Fact]
    public void GetTotalLength_InvalidRunId_Throws()
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() =>
            ConduitRunService.GetTotalLength(store, "nonexistent"));
    }

    // ── AppendSegment ────────────────────────────────────────────────────

    [Fact]
    public void AppendSegment_AddsToRun()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 10) });

        ConduitRunService.AppendSegment(store, run.Id, Seg(10, 20));

        Assert.Equal(2, run.SegmentIds.Count);
        Assert.Equal(20.0, ConduitRunService.GetTotalLength(store, run.Id), 3);
    }

    [Fact]
    public void AppendSegment_WithBend_InsertsFitting()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg2D(0, 0, 10, 0) });

        // Append a vertical segment → 90° bend
        ConduitRunService.AppendSegment(store, run.Id, Seg2D(10, 0, 10, 10));

        Assert.Equal(2, run.SegmentIds.Count);
        Assert.Single(run.FittingIds);
    }

    [Fact]
    public void AppendSegment_InheritsRunProperties()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 10) });

        var newSeg = new ConduitSegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(20, 0, 0)
        };

        ConduitRunService.AppendSegment(store, run.Id, newSeg);

        Assert.Equal(run.ConduitTypeId, newSeg.ConduitTypeId);
        Assert.Equal(run.TradeSize, newSeg.TradeSize);
        Assert.Equal(run.Material, newSeg.Material);
    }

    [Fact]
    public void AppendSegment_InvalidRunId_Throws()
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() =>
            ConduitRunService.AppendSegment(store, "nonexistent", Seg(0, 10)));
    }

    // ── SplitAt ──────────────────────────────────────────────────────────

    [Fact]
    public void SplitAt_ProducesTwoRuns()
    {
        var store = CreateStore();
        var segments = new List<ConduitSegment> { Seg(0, 5), Seg(5, 10), Seg(10, 15), Seg(15, 20) };
        var run = ConduitRunService.CreateRun(store, segments);

        var newRun = ConduitRunService.SplitAt(store, run.Id, 2);

        Assert.Equal(2, run.SegmentIds.Count);
        Assert.Equal(2, newRun.SegmentIds.Count);
    }

    [Fact]
    public void SplitAt_PreservesTotalLength()
    {
        var store = CreateStore();
        var segments = new List<ConduitSegment> { Seg(0, 5), Seg(5, 10), Seg(10, 15) };
        var run = ConduitRunService.CreateRun(store, segments);

        var newRun = ConduitRunService.SplitAt(store, run.Id, 1);

        double total = ConduitRunService.GetTotalLength(store, run.Id)
                     + ConduitRunService.GetTotalLength(store, newRun.Id);
        Assert.Equal(15.0, total, 3);
    }

    [Fact]
    public void SplitAt_NewRunGetsEndEquipment()
    {
        var store = CreateStore();
        var segments = new List<ConduitSegment> { Seg(0, 5), Seg(5, 10) };
        var run = ConduitRunService.CreateRun(store, segments);
        run.EndEquipment = "Panel-A";

        var newRun = ConduitRunService.SplitAt(store, run.Id, 1);

        Assert.Equal(string.Empty, run.EndEquipment);
        Assert.Equal("Panel-A", newRun.EndEquipment);
    }

    [Fact]
    public void SplitAt_IndexZero_Throws()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 5), Seg(5, 10) });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConduitRunService.SplitAt(store, run.Id, 0));
    }

    [Fact]
    public void SplitAt_IndexAtEnd_Throws()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 5), Seg(5, 10) });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConduitRunService.SplitAt(store, run.Id, 2));
    }

    // ── MergeRuns ────────────────────────────────────────────────────────

    [Fact]
    public void MergeRuns_CombinesSegments()
    {
        var store = CreateStore();
        var run1 = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 5), Seg(5, 10) });
        var run2 = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(10, 15), Seg(15, 20) });

        ConduitRunService.MergeRuns(store, run1.Id, run2.Id);

        Assert.Equal(4, run1.SegmentIds.Count);
        Assert.Equal(20.0, ConduitRunService.GetTotalLength(store, run1.Id), 3);
    }

    [Fact]
    public void MergeRuns_RemovesSecondary()
    {
        var store = CreateStore();
        var run1 = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 5) });
        var run2 = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(5, 10) });
        string secondaryId = run2.Id;

        ConduitRunService.MergeRuns(store, run1.Id, secondaryId);

        Assert.Null(store.GetRun(secondaryId));
    }

    [Fact]
    public void MergeRuns_InheritsEndEquipment()
    {
        var store = CreateStore();
        var run1 = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 5) });
        var run2 = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(5, 10) });
        run2.EndEquipment = "Panel-B";

        ConduitRunService.MergeRuns(store, run1.Id, run2.Id);

        Assert.Equal("Panel-B", run1.EndEquipment);
    }

    [Fact]
    public void MergeRuns_InvalidPrimaryId_Throws()
    {
        var store = CreateStore();
        var run2 = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 5) });

        Assert.Throws<ArgumentException>(() =>
            ConduitRunService.MergeRuns(store, "nonexistent", run2.Id));
    }

    // ── Fitting insertion at bends ───────────────────────────────────────

    [Fact]
    public void CreateRun_90DegreeBend_InsertsElbow90()
    {
        var store = CreateStore();
        var segments = new List<ConduitSegment>
        {
            Seg2D(0, 0, 10, 0),
            Seg2D(10, 0, 10, 10)
        };
        var run = ConduitRunService.CreateRun(store, segments);

        Assert.Single(run.FittingIds);
        var fitting = store.GetFitting(run.FittingIds[0]);
        Assert.NotNull(fitting);
        Assert.Equal(FittingType.Elbow90, fitting!.Type);
    }

    [Fact]
    public void CreateRun_FittingsDisabled_NoFittings()
    {
        var store = CreateStore();
        store.Settings.AutoInsertFittings = false;

        var segments = new List<ConduitSegment>
        {
            Seg2D(0, 0, 10, 0),
            Seg2D(10, 0, 10, 10)
        };
        var run = ConduitRunService.CreateRun(store, segments);

        Assert.Empty(run.FittingIds);
    }

    // ── Tag association ──────────────────────────────────────────────────

    [Fact]
    public void CreateTag_ReturnsTagInfo()
    {
        var store = CreateStore();
        var run = ConduitRunService.CreateRun(store, new List<ConduitSegment> { Seg(0, 10) });

        var tag = ConduitRunService.CreateTag(store, run.Id);

        Assert.Equal(run.Id, tag.RunId);
        Assert.Contains("EMT", tag.Label);
        Assert.NotEmpty(tag.TradeSize);
    }

    [Fact]
    public void CreateTag_InvalidRunId_Throws()
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() =>
            ConduitRunService.CreateTag(store, "nonexistent"));
    }

    [Fact]
    public void ConduitTagMarkupType_Exists()
    {
        // Verify the ConduitTag enum value is available
        Assert.Equal("ConduitTag", MarkupType.ConduitTag.ToString());
    }

    // ── SplitAt with fittings ────────────────────────────────────────────

    [Fact]
    public void SplitAt_WithFittings_PartitionsFittings()
    {
        var store = CreateStore();
        // Three segments: horizontal, vertical, horizontal → two 90° bends
        var segments = new List<ConduitSegment>
        {
            Seg2D(0, 0, 10, 0),
            Seg2D(10, 0, 10, 10),
            Seg2D(10, 10, 20, 10)
        };
        var run = ConduitRunService.CreateRun(store, segments);

        Assert.Equal(2, run.FittingIds.Count); // two 90° fittings

        var newRun = ConduitRunService.SplitAt(store, run.Id, 1);

        // First run has segment 0, no fittings (fitting at split point removed)
        Assert.Single(run.SegmentIds);
        Assert.Empty(run.FittingIds);

        // Second run has segments 1+2, one fitting between them
        Assert.Equal(2, newRun.SegmentIds.Count);
        Assert.Single(newRun.FittingIds);
    }

    // ── ProjectModel integration ─────────────────────────────────────────

    [Fact]
    public void ProjectModel_ConduitRuns_DefaultsToEmpty()
    {
        var project = new ElectricalComponentSandbox.Models.ProjectModel();
        Assert.NotNull(project.ConduitRuns);
        Assert.Empty(project.ConduitRuns);
    }
}
