using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.LoadSheddingService;

namespace ElectricalComponentSandbox.Tests.Services;

public class LoadSheddingServiceTests
{
    private static LoadBlock MakeBlock(string id, SheddingPriority priority, double kw, bool isShed = false) =>
        new() { Id = id, Description = $"Load {id}", Priority = priority, DemandKW = kw, DemandKVA = kw / 0.85 };

    // ── Demand Tracking ──────────────────────────────────────────────────────

    [Fact]
    public void TrackDemand_BelowThreshold_NoExceed()
    {
        var loads = new[]
        {
            MakeBlock("L1", SheddingPriority.NonCritical, 50),
            MakeBlock("L2", SheddingPriority.NonCritical, 30),
        };

        var snap = LoadSheddingService.TrackDemand(loads, 100);

        Assert.Equal(80, snap.CurrentDemandKW);
        Assert.False(snap.ExceedsThreshold);
        Assert.Equal(20, snap.MarginKW);
        Assert.Equal(80, snap.UtilizationPercent);
    }

    [Fact]
    public void TrackDemand_AboveThreshold_Exceeds()
    {
        var loads = new[]
        {
            MakeBlock("L1", SheddingPriority.CriticalOptional, 60),
            MakeBlock("L2", SheddingPriority.NonCritical, 50),
        };

        var snap = LoadSheddingService.TrackDemand(loads, 100);

        Assert.True(snap.ExceedsThreshold);
        Assert.Equal(-10, snap.MarginKW);
    }

    [Fact]
    public void TrackDemand_ShedLoads_ExcludedFromCurrent()
    {
        var loads = new[]
        {
            MakeBlock("L1", SheddingPriority.NonCritical, 60),
            new LoadBlock { Id = "L2", DemandKW = 50, IsShed = true },
        };

        var snap = LoadSheddingService.TrackDemand(loads, 100);

        Assert.Equal(60, snap.CurrentDemandKW);
        Assert.False(snap.ExceedsThreshold);
    }

    [Fact]
    public void TrackDemand_Empty_Zero()
    {
        var snap = LoadSheddingService.TrackDemand(Array.Empty<LoadBlock>(), 100);
        Assert.Equal(0, snap.CurrentDemandKW);
        Assert.Equal(100, snap.MarginKW);
    }

    // ── Shedding Plan ────────────────────────────────────────────────────────

    [Fact]
    public void CreateSheddingPlan_NoDemandExceed_NoShedding()
    {
        var loads = new[]
        {
            MakeBlock("L1", SheddingPriority.NonCritical, 30),
            MakeBlock("L2", SheddingPriority.NonCritical, 40),
        };

        var plan = LoadSheddingService.CreateSheddingPlan(loads, 100);

        Assert.True(plan.IsAdequate);
        Assert.Empty(plan.BlocksToShed);
        Assert.Equal(0, plan.OverloadKW);
    }

    [Fact]
    public void CreateSheddingPlan_ShedsLowestPriorityFirst()
    {
        var loads = new[]
        {
            MakeBlock("LS1", SheddingPriority.LifeSafety, 20),
            MakeBlock("LR1", SheddingPriority.LegallyRequired, 30),
            MakeBlock("C1", SheddingPriority.CriticalOptional, 40),
            MakeBlock("NC1", SheddingPriority.NonCritical, 50),
        };

        // Total = 140, target = 100 → need to shed 40
        var plan = LoadSheddingService.CreateSheddingPlan(loads, 100);

        Assert.True(plan.IsAdequate);
        // NonCritical (50) should be shed first, which covers the 40kW overload
        Assert.Single(plan.BlocksToShed);
        Assert.Equal("NC1", plan.BlocksToShed[0].Id);
    }

    [Fact]
    public void CreateSheddingPlan_NeverShedsLifeSafety()
    {
        var loads = new[]
        {
            MakeBlock("LS1", SheddingPriority.LifeSafety, 80),
            MakeBlock("NC1", SheddingPriority.NonCritical, 40),
        };

        // Total = 120, target = 50 → need to shed 70, but only 40 available (NC1)
        var plan = LoadSheddingService.CreateSheddingPlan(loads, 50);

        Assert.False(plan.IsAdequate);
        Assert.Single(plan.BlocksToShed);
        Assert.Equal("NC1", plan.BlocksToShed[0].Id);
        Assert.Contains("life safety", plan.Issue!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateSheddingPlan_ShedsLargestAtSamePriority()
    {
        var loads = new[]
        {
            MakeBlock("NC1", SheddingPriority.NonCritical, 20),
            MakeBlock("NC2", SheddingPriority.NonCritical, 50),
            MakeBlock("NC3", SheddingPriority.NonCritical, 30),
        };

        // Total = 100, target = 60 → need to shed 40. NC2 (50) covers it
        var plan = LoadSheddingService.CreateSheddingPlan(loads, 60);

        Assert.True(plan.IsAdequate);
        Assert.Single(plan.BlocksToShed);
        Assert.Equal("NC2", plan.BlocksToShed[0].Id);
    }

    [Fact]
    public void CreateSheddingPlan_MultipleBlocksShed()
    {
        var loads = new[]
        {
            MakeBlock("LS1", SheddingPriority.LifeSafety, 30),
            MakeBlock("C1", SheddingPriority.CriticalOptional, 25),
            MakeBlock("NC1", SheddingPriority.NonCritical, 25),
            MakeBlock("NC2", SheddingPriority.NonCritical, 20),
        };

        // Total = 100, target = 50 → need to shed 50. NC1(25)+NC2(20)=45 not enough, + C1(25)=70 enough
        var plan = LoadSheddingService.CreateSheddingPlan(loads, 50);

        Assert.True(plan.IsAdequate);
        // Should shed NC loads first, then CriticalOptional
        var shedIds = plan.BlocksToShed.Select(b => b.Id).ToList();
        Assert.Contains("NC1", shedIds);
        Assert.Contains("NC2", shedIds);
    }

    [Fact]
    public void CreateSheddingPlan_AlreadyShedLoads_Excluded()
    {
        var loads = new[]
        {
            MakeBlock("NC1", SheddingPriority.NonCritical, 50),
            new LoadBlock { Id = "NC2", DemandKW = 30, Priority = SheddingPriority.NonCritical, IsShed = true },
            MakeBlock("NC3", SheddingPriority.NonCritical, 40),
        };

        // Active = NC1(50) + NC3(40) = 90, target = 80, need to shed 10
        var plan = LoadSheddingService.CreateSheddingPlan(loads, 80);

        Assert.True(plan.IsAdequate);
        // NC2 is already shed — should not appear in plan
        Assert.DoesNotContain(plan.BlocksToShed, b => b.Id == "NC2");
    }

    // ── Restore Sequence ─────────────────────────────────────────────────────

    [Fact]
    public void CreateRestoreSequence_HighestPriorityFirst()
    {
        var shedBlocks = new[]
        {
            MakeBlock("NC1", SheddingPriority.NonCritical, 30),
            MakeBlock("LR1", SheddingPriority.LegallyRequired, 20),
            MakeBlock("C1", SheddingPriority.CriticalOptional, 25),
        };

        var sequence = LoadSheddingService.CreateRestoreSequence(shedBlocks, staggerDelaySec: 10);

        Assert.Equal(3, sequence.Count);
        Assert.Equal("LR1", sequence[0].Block.Id); // Priority 2 restored first
        Assert.Equal("C1", sequence[1].Block.Id);   // Priority 3 next
        Assert.Equal("NC1", sequence[2].Block.Id);  // Priority 4 last
    }

    [Fact]
    public void CreateRestoreSequence_StaggeredDelays()
    {
        var shedBlocks = new[]
        {
            MakeBlock("A", SheddingPriority.NonCritical, 10) with { MinOffTimeSec = 0 },
            MakeBlock("B", SheddingPriority.NonCritical, 20) with { MinOffTimeSec = 0 },
        };

        var sequence = LoadSheddingService.CreateRestoreSequence(shedBlocks, staggerDelaySec: 15);

        // First restore at 0, second at 15
        Assert.Equal(0, sequence[0].CumulativeDelaySec);
        Assert.Equal(15, sequence[1].CumulativeDelaySec);
    }

    [Fact]
    public void CreateRestoreSequence_MinOffTimeRespected()
    {
        var shedBlocks = new[]
        {
            MakeBlock("A", SheddingPriority.NonCritical, 10) with { MinOffTimeSec = 600 },
        };

        var sequence = LoadSheddingService.CreateRestoreSequence(shedBlocks, staggerDelaySec: 10);

        // MinOffTimeSec (600) > stagger (0 for first), so 600 used
        Assert.True(sequence[0].CumulativeDelaySec >= 600);
    }

    [Fact]
    public void CreateRestoreSequence_Empty_ReturnsEmpty()
    {
        var sequence = LoadSheddingService.CreateRestoreSequence(Array.Empty<LoadBlock>());
        Assert.Empty(sequence);
    }

    // ── Generator Capacity ───────────────────────────────────────────────────

    [Fact]
    public void EvaluateGeneratorCapacity_AdequateCapacity()
    {
        var loads = new[]
        {
            MakeBlock("LS1", SheddingPriority.LifeSafety, 30),
            MakeBlock("LR1", SheddingPriority.LegallyRequired, 20),
        };

        var plan = LoadSheddingService.EvaluateGeneratorCapacity(loads, 100);

        Assert.True(plan.IsAdequate);
        Assert.Empty(plan.BlocksToShed);
    }

    [Fact]
    public void EvaluateGeneratorCapacity_NeedsShedding()
    {
        var loads = new[]
        {
            MakeBlock("LS1", SheddingPriority.LifeSafety, 30),
            MakeBlock("NC1", SheddingPriority.NonCritical, 50),
            MakeBlock("NC2", SheddingPriority.NonCritical, 40),
        };

        // Generator = 80kW, load = 120kW → shed 40kW
        var plan = LoadSheddingService.EvaluateGeneratorCapacity(loads, 80);

        Assert.True(plan.IsAdequate);
        Assert.NotEmpty(plan.BlocksToShed);
        Assert.True(plan.RemainingDemandKW <= 80);
    }

    // ── Real-World Scenario ──────────────────────────────────────────────────

    [Fact]
    public void RealWorld_CommercialBuilding_DemandResponse()
    {
        var loads = new[]
        {
            MakeBlock("Emergency-Lights", SheddingPriority.LifeSafety, 15),
            MakeBlock("Fire-Alarm", SheddingPriority.LifeSafety, 5),
            MakeBlock("Elevators", SheddingPriority.LegallyRequired, 40),
            MakeBlock("Server-Room", SheddingPriority.CriticalOptional, 60),
            MakeBlock("HVAC-Main", SheddingPriority.NonCritical, 120),
            MakeBlock("Lighting-Office", SheddingPriority.NonCritical, 45),
            MakeBlock("Kitchen", SheddingPriority.NonCritical, 30),
        };

        // Utility demand response: reduce to 200kW (total is 315kW)
        var demand = LoadSheddingService.TrackDemand(loads, 200);
        Assert.True(demand.ExceedsThreshold);

        var plan = LoadSheddingService.CreateSheddingPlan(loads, 200);
        Assert.True(plan.IsAdequate);
        Assert.True(plan.RemainingDemandKW <= 200);

        // Life safety loads should never be shed
        Assert.DoesNotContain(plan.BlocksToShed, b => b.Priority == SheddingPriority.LifeSafety);

        // Restore sequence
        var restore = LoadSheddingService.CreateRestoreSequence(plan.BlocksToShed);
        Assert.Equal(plan.BlocksToShed.Count, restore.Count);
    }
}
