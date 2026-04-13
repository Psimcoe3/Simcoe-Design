using System;
using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Analyses spare capacity of panels, transformers, feeders, and buses.
/// Identifies utilization percentages, bottlenecks, and NEC 80% continuous load limits.
/// </summary>
public static class SpareCapacityAnalysisService
{
    // ── Records ──────────────────────────────────────────────────────────────

    public record CapacityReport
    {
        public string ComponentId { get; init; } = "";
        public string ComponentName { get; init; } = "";
        public string ComponentType { get; init; } = "";
        public double RatedCapacity { get; init; }
        public string CapacityUnit { get; init; } = "";
        public double UsedCapacity { get; init; }
        public double SpareCapacity => RatedCapacity - UsedCapacity;
        public double UtilizationPercent => RatedCapacity > 0
            ? Math.Round(UsedCapacity / RatedCapacity * 100, 1)
            : 0;
        public bool Overloaded => UsedCapacity > RatedCapacity;
        public bool ExceedsContinuousLimit => UsedCapacity > RatedCapacity * 0.80;
        public string Status => Overloaded ? "OVERLOADED"
            : ExceedsContinuousLimit ? "EXCEEDS_80PCT"
            : UtilizationPercent > 60 ? "CAUTION"
            : "OK";
    }

    public record SystemCapacitySummary
    {
        public List<CapacityReport> Reports { get; init; } = new();
        public int TotalComponents => Reports.Count;
        public int OverloadedCount => Reports.Count(r => r.Overloaded);
        public int ExceedsContinuousCount => Reports.Count(r => r.ExceedsContinuousLimit);
        public double WorstUtilizationPercent => Reports.Count > 0
            ? Reports.Max(r => r.UtilizationPercent) : 0;
        public string? BottleneckId => Reports.Count > 0
            ? Reports.OrderByDescending(r => r.UtilizationPercent).First().ComponentId
            : null;
    }

    // ── Panel Analysis ───────────────────────────────────────────────────────

    /// <summary>
    /// Analyses a panel's spare capacity based on its schedule.
    /// Uses demand VA vs. bus capacity.
    /// </summary>
    public static CapacityReport AnalyzePanel(PanelSchedule schedule, PanelVoltageConfig? voltageOverride = null)
    {
        var vc = voltageOverride ?? schedule.VoltageConfig;
        double voltage = GetLineToLineVoltage(vc);
        int phases = GetPhaseCount(vc);

        double busCapacityVA = schedule.BusAmps * voltage * (phases == 3 ? Math.Sqrt(3) : 1.0);
        double demandVA = schedule.TotalDemandVA;

        return new CapacityReport
        {
            ComponentId = schedule.PanelId,
            ComponentName = schedule.PanelName,
            ComponentType = "Panel",
            RatedCapacity = Math.Round(busCapacityVA, 0),
            CapacityUnit = "VA",
            UsedCapacity = Math.Round(demandVA, 0),
        };
    }

    /// <summary>
    /// Analyses spare circuit slots in a panel.
    /// </summary>
    public static CapacityReport AnalyzePanelSlots(PanelSchedule schedule, int totalSlots)
    {
        int usedSlots = schedule.Circuits.Sum(c => c.Poles);
        return new CapacityReport
        {
            ComponentId = schedule.PanelId,
            ComponentName = schedule.PanelName + " (Slots)",
            ComponentType = "PanelSlots",
            RatedCapacity = totalSlots,
            CapacityUnit = "Poles",
            UsedCapacity = usedSlots,
        };
    }

    // ── Transformer Analysis ─────────────────────────────────────────────────

    /// <summary>
    /// Analyses a transformer's spare capacity.
    /// </summary>
    public static CapacityReport AnalyzeTransformer(TransformerComponent xfmr, double loadDemandVA)
    {
        double capacityVA = xfmr.KVA * 1000;
        return new CapacityReport
        {
            ComponentId = xfmr.Id,
            ComponentName = xfmr.Name,
            ComponentType = "Transformer",
            RatedCapacity = capacityVA,
            CapacityUnit = "VA",
            UsedCapacity = Math.Round(loadDemandVA, 0),
        };
    }

    // ── Bus Analysis ─────────────────────────────────────────────────────────

    /// <summary>
    /// Analyses a bus duct's spare capacity.
    /// </summary>
    public static CapacityReport AnalyzeBus(BusComponent bus, double loadAmps, int phases = 3)
    {
        return new CapacityReport
        {
            ComponentId = bus.Id,
            ComponentName = bus.Name,
            ComponentType = "Bus",
            RatedCapacity = bus.BusAmps,
            CapacityUnit = "Amps",
            UsedCapacity = Math.Round(loadAmps, 1),
        };
    }

    // ── Power Source Analysis ────────────────────────────────────────────────

    /// <summary>
    /// Analyses a power source's spare capacity.
    /// </summary>
    public static CapacityReport AnalyzePowerSource(PowerSourceComponent source, double loadDemandVA)
    {
        double capacityVA = source.KVA * 1000;
        return new CapacityReport
        {
            ComponentId = source.Id,
            ComponentName = source.Name,
            ComponentType = "PowerSource",
            RatedCapacity = capacityVA,
            CapacityUnit = "VA",
            UsedCapacity = Math.Round(loadDemandVA, 0),
        };
    }

    // ── System-wide Analysis ─────────────────────────────────────────────────

    /// <summary>
    /// Aggregates multiple capacity reports into a summary.
    /// </summary>
    public static SystemCapacitySummary Summarize(IEnumerable<CapacityReport> reports)
    {
        return new SystemCapacitySummary
        {
            Reports = reports.ToList(),
        };
    }

    /// <summary>
    /// Finds components that exceed a given utilization threshold.
    /// </summary>
    public static List<CapacityReport> FindBottlenecks(
        IEnumerable<CapacityReport> reports,
        double thresholdPercent = 80)
    {
        return reports
            .Where(r => r.UtilizationPercent >= thresholdPercent)
            .OrderByDescending(r => r.UtilizationPercent)
            .ToList();
    }

    /// <summary>
    /// Calculates the minimum upsizing needed to bring utilization below the target.
    /// Returns the new capacity needed. Returns 0 if already below target.
    /// </summary>
    public static double CalculateRequiredUpsize(CapacityReport report, double targetUtilizationPercent = 80)
    {
        if (report.UtilizationPercent <= targetUtilizationPercent) return 0;
        return Math.Ceiling(report.UsedCapacity / (targetUtilizationPercent / 100.0));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static double GetLineToLineVoltage(PanelVoltageConfig vc)
    {
        return vc switch
        {
            PanelVoltageConfig.V120_240_1Ph => 240,
            PanelVoltageConfig.V120_208_3Ph => 208,
            PanelVoltageConfig.V277_480_3Ph => 480,
            PanelVoltageConfig.V240_3Ph => 240,
            _ => 208,
        };
    }

    private static int GetPhaseCount(PanelVoltageConfig vc)
    {
        return vc switch
        {
            PanelVoltageConfig.V120_240_1Ph => 1,
            _ => 3,
        };
    }
}
