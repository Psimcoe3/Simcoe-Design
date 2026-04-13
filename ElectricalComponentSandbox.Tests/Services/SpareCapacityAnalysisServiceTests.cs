using System.Collections.Generic;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SpareCapacityAnalysisServiceTests
{
    // ── Panel Analysis ───────────────────────────────────────────────────────

    private static PanelSchedule MakeSchedule(
        int busAmps = 200,
        PanelVoltageConfig vc = PanelVoltageConfig.V120_208_3Ph,
        params (double VA, int poles)[] circuits)
    {
        var sched = new PanelSchedule
        {
            PanelId = "P1",
            PanelName = "Panel-1",
            BusAmps = busAmps,
            VoltageConfig = vc,
        };
        int n = 1;
        foreach (var (va, p) in circuits)
        {
            sched.Circuits.Add(new Circuit
            {
                CircuitNumber = $"{n++}",
                ConnectedLoadVA = va,
                DemandFactor = 1.0,
                Poles = p,
                Phase = "A",
            });
        }
        return sched;
    }

    [Fact]
    public void AnalyzePanel_LightLoad_OK()
    {
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_208_3Ph,
            (5000, 1), (5000, 1), (5000, 1));
        var report = SpareCapacityAnalysisService.AnalyzePanel(sched);
        Assert.Equal("OK", report.Status);
        Assert.False(report.Overloaded);
        Assert.False(report.ExceedsContinuousLimit);
    }

    [Fact]
    public void AnalyzePanel_Overloaded_Detected()
    {
        // 200A 3PH 208V bus = ~72kVA. Load 80000 VA → overloaded
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_208_3Ph,
            (80000, 1));
        var report = SpareCapacityAnalysisService.AnalyzePanel(sched);
        Assert.True(report.Overloaded);
        Assert.Equal("OVERLOADED", report.Status);
    }

    [Fact]
    public void AnalyzePanel_ExceedsContinuous()
    {
        // 200A 3PH 208V ≈ 72kVA. 60kVA = ~83% → exceeds 80%
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_208_3Ph,
            (60000, 1));
        var report = SpareCapacityAnalysisService.AnalyzePanel(sched);
        Assert.True(report.ExceedsContinuousLimit);
    }

    [Fact]
    public void AnalyzePanel_SpareCapacity_Correct()
    {
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_208_3Ph,
            (10000, 1));
        var report = SpareCapacityAnalysisService.AnalyzePanel(sched);
        Assert.True(report.SpareCapacity > 0);
        Assert.Equal(report.RatedCapacity - report.UsedCapacity, report.SpareCapacity);
    }

    [Fact]
    public void AnalyzePanel_SinglePhase()
    {
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_240_1Ph,
            (10000, 1));
        var report = SpareCapacityAnalysisService.AnalyzePanel(sched);
        // 200A × 240V × 1 = 48000 VA
        Assert.Equal(48000, report.RatedCapacity);
    }

    [Fact]
    public void AnalyzePanel_UtilizationPercent()
    {
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_240_1Ph,
            (24000, 1));
        var report = SpareCapacityAnalysisService.AnalyzePanel(sched);
        Assert.Equal(50.0, report.UtilizationPercent);
    }

    // ── Slot Analysis ────────────────────────────────────────────────────────

    [Fact]
    public void AnalyzePanelSlots_24Slot_Partial()
    {
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_208_3Ph,
            (1000, 1), (1000, 2), (1000, 3));
        var report = SpareCapacityAnalysisService.AnalyzePanelSlots(sched, 24);
        Assert.Equal(24, report.RatedCapacity);
        Assert.Equal(6, report.UsedCapacity); // 1+2+3 poles
        Assert.Equal(18, report.SpareCapacity);
    }

    [Fact]
    public void AnalyzePanelSlots_Full()
    {
        var circuits = new List<(double, int)>();
        for (int i = 0; i < 42; i++) circuits.Add((500, 1));
        var sched = MakeSchedule(200, PanelVoltageConfig.V120_208_3Ph, circuits.ToArray());
        var report = SpareCapacityAnalysisService.AnalyzePanelSlots(sched, 42);
        Assert.Equal(100.0, report.UtilizationPercent);
    }

    // ── Transformer Analysis ─────────────────────────────────────────────────

    [Fact]
    public void AnalyzeTransformer_50Percent()
    {
        var xfmr = new TransformerComponent { Id = "T1", Name = "TX-1", KVA = 75 };
        var report = SpareCapacityAnalysisService.AnalyzeTransformer(xfmr, 37500);
        Assert.Equal(75000, report.RatedCapacity);
        Assert.Equal(50.0, report.UtilizationPercent);
        Assert.Equal("OK", report.Status);
    }

    [Fact]
    public void AnalyzeTransformer_Overloaded()
    {
        var xfmr = new TransformerComponent { Id = "T1", Name = "TX-1", KVA = 75 };
        var report = SpareCapacityAnalysisService.AnalyzeTransformer(xfmr, 80000);
        Assert.True(report.Overloaded);
    }

    // ── Bus Analysis ─────────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeBus_UnderCapacity()
    {
        var bus = new BusComponent { Id = "B1", Name = "Bus-1", BusAmps = 800 };
        var report = SpareCapacityAnalysisService.AnalyzeBus(bus, 400);
        Assert.Equal(50.0, report.UtilizationPercent);
        Assert.Equal("OK", report.Status);
    }

    [Fact]
    public void AnalyzeBus_Overloaded()
    {
        var bus = new BusComponent { Id = "B1", Name = "Bus-1", BusAmps = 800 };
        var report = SpareCapacityAnalysisService.AnalyzeBus(bus, 900);
        Assert.True(report.Overloaded);
    }

    // ── Power Source Analysis ────────────────────────────────────────────────

    [Fact]
    public void AnalyzePowerSource_OK()
    {
        var src = new PowerSourceComponent { Id = "S1", Name = "Utility", KVA = 1500 };
        var report = SpareCapacityAnalysisService.AnalyzePowerSource(src, 500000);
        Assert.Equal(1500000, report.RatedCapacity);
        Assert.InRange(report.UtilizationPercent, 33, 34);
    }

    // ── Summary ──────────────────────────────────────────────────────────────

    [Fact]
    public void Summarize_AggregatesReports()
    {
        var reports = new[]
        {
            new SpareCapacityAnalysisService.CapacityReport
            {
                ComponentId = "A", ComponentName = "A", ComponentType = "Panel",
                RatedCapacity = 100, UsedCapacity = 90, CapacityUnit = "VA",
            },
            new SpareCapacityAnalysisService.CapacityReport
            {
                ComponentId = "B", ComponentName = "B", ComponentType = "Panel",
                RatedCapacity = 100, UsedCapacity = 50, CapacityUnit = "VA",
            },
        };
        var summary = SpareCapacityAnalysisService.Summarize(reports);
        Assert.Equal(2, summary.TotalComponents);
        Assert.Equal(90.0, summary.WorstUtilizationPercent);
        Assert.Equal("A", summary.BottleneckId);
    }

    [Fact]
    public void Summarize_OverloadedCount()
    {
        var reports = new[]
        {
            new SpareCapacityAnalysisService.CapacityReport
            {
                ComponentId = "A", RatedCapacity = 100, UsedCapacity = 120,
                ComponentName = "A", ComponentType = "Panel", CapacityUnit = "VA",
            },
            new SpareCapacityAnalysisService.CapacityReport
            {
                ComponentId = "B", RatedCapacity = 100, UsedCapacity = 50,
                ComponentName = "B", ComponentType = "Panel", CapacityUnit = "VA",
            },
        };
        var summary = SpareCapacityAnalysisService.Summarize(reports);
        Assert.Equal(1, summary.OverloadedCount);
    }

    // ── Bottleneck Finder ────────────────────────────────────────────────────

    [Fact]
    public void FindBottlenecks_FiltersAboveThreshold()
    {
        var reports = new[]
        {
            new SpareCapacityAnalysisService.CapacityReport
            {
                ComponentId = "A", RatedCapacity = 100, UsedCapacity = 85,
                ComponentName = "A", ComponentType = "Panel", CapacityUnit = "VA",
            },
            new SpareCapacityAnalysisService.CapacityReport
            {
                ComponentId = "B", RatedCapacity = 100, UsedCapacity = 50,
                ComponentName = "B", ComponentType = "Panel", CapacityUnit = "VA",
            },
        };
        var bottlenecks = SpareCapacityAnalysisService.FindBottlenecks(reports, 80);
        Assert.Single(bottlenecks);
        Assert.Equal("A", bottlenecks[0].ComponentId);
    }

    [Fact]
    public void FindBottlenecks_NoneAboveThreshold_Empty()
    {
        var reports = new[]
        {
            new SpareCapacityAnalysisService.CapacityReport
            {
                ComponentId = "A", RatedCapacity = 100, UsedCapacity = 30,
                ComponentName = "A", ComponentType = "Panel", CapacityUnit = "VA",
            },
        };
        var bottlenecks = SpareCapacityAnalysisService.FindBottlenecks(reports, 80);
        Assert.Empty(bottlenecks);
    }

    // ── Required Upsize ──────────────────────────────────────────────────────

    [Fact]
    public void RequiredUpsize_OverloadedPanel()
    {
        var report = new SpareCapacityAnalysisService.CapacityReport
        {
            ComponentId = "P1", RatedCapacity = 48000, UsedCapacity = 50000,
            ComponentName = "P1", ComponentType = "Panel", CapacityUnit = "VA",
        };
        double needed = SpareCapacityAnalysisService.CalculateRequiredUpsize(report, 80);
        Assert.True(needed >= 62500); // 50000/0.80 = 62500
    }

    [Fact]
    public void RequiredUpsize_Under80_ReturnsZero()
    {
        var report = new SpareCapacityAnalysisService.CapacityReport
        {
            ComponentId = "P1", RatedCapacity = 48000, UsedCapacity = 20000,
            ComponentName = "P1", ComponentType = "Panel", CapacityUnit = "VA",
        };
        double needed = SpareCapacityAnalysisService.CalculateRequiredUpsize(report, 80);
        Assert.Equal(0, needed);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void CapacityReport_ZeroCapacity_ZeroUtilization()
    {
        var report = new SpareCapacityAnalysisService.CapacityReport
        {
            ComponentId = "X", RatedCapacity = 0, UsedCapacity = 100,
            ComponentName = "X", ComponentType = "Panel", CapacityUnit = "VA",
        };
        Assert.Equal(0, report.UtilizationPercent);
    }

    [Fact]
    public void Summarize_Empty_Defaults()
    {
        var summary = SpareCapacityAnalysisService.Summarize(new List<SpareCapacityAnalysisService.CapacityReport>());
        Assert.Equal(0, summary.TotalComponents);
        Assert.Null(summary.BottleneckId);
        Assert.Equal(0, summary.WorstUtilizationPercent);
    }
}
