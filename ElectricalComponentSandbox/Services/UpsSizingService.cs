using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Uninterruptible Power Supply sizing per IEEE 1184 / NEC 708.
/// Covers load analysis, battery runtime, inverter sizing,
/// and system configuration.
/// </summary>
public static class UpsSizingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum UpsTopology
    {
        Standby,           // Offline — switches on power loss (~4ms transfer)
        LineInteractive,   // AVR + battery, ~2-4ms transfer
        DoubleConversion,  // Online — continuous inverter, 0ms transfer
    }

    public enum RedundancyMode
    {
        None,       // N — single UPS
        NPlus1,     // N+1 — one extra module
        TwoN,       // 2N — fully redundant parallel paths
    }

    public enum LoadType
    {
        ITEquipment,     // Servers, switches, storage — PF 0.9-1.0
        LinearLoad,      // Resistive/lighting — PF ~1.0
        MotorLoad,       // Small fans, pumps — PF 0.8, high inrush
        MixedLoad,       // Combination — PF 0.85
    }

    public record UpsLoad
    {
        public string Name { get; init; } = "";
        public double Watts { get; init; }
        public LoadType Type { get; init; } = LoadType.ITEquipment;
        public bool IsCritical { get; init; } = true;
    }

    public record UpsLoadAnalysis
    {
        public double TotalWatts { get; init; }
        public double TotalVA { get; init; }
        public double WeightedPowerFactor { get; init; }
        public double CriticalWatts { get; init; }
        public int LoadCount { get; init; }
    }

    public record BatteryResult
    {
        public double RequiredKWh { get; init; }
        public double RuntimeMinutes { get; init; }
        public double DesignMargin { get; init; }
        public double AgingFactor { get; init; }
        public double TemperatureDerating { get; init; }
    }

    public record UpsSizingResult
    {
        public double RecommendedKVA { get; init; }
        public double RecommendedKW { get; init; }
        public UpsTopology Topology { get; init; }
        public RedundancyMode Redundancy { get; init; }
        public BatteryResult Battery { get; init; } = null!;
        public double UtilizationPercent { get; init; }
        public int ModuleCount { get; init; }
    }

    // ── Power Factor by Load Type ────────────────────────────────────────────

    public static double GetTypicalPowerFactor(LoadType type) => type switch
    {
        LoadType.ITEquipment => 0.95,
        LoadType.LinearLoad => 1.0,
        LoadType.MotorLoad => 0.80,
        LoadType.MixedLoad => 0.85,
        _ => 0.90,
    };

    // ── Load Analysis ────────────────────────────────────────────────────────

    /// <summary>
    /// Analyzes UPS loads to determine total W, VA, and weighted PF.
    /// </summary>
    public static UpsLoadAnalysis AnalyzeLoads(IReadOnlyList<UpsLoad> loads)
    {
        ArgumentNullException.ThrowIfNull(loads);
        if (loads.Count == 0)
            throw new ArgumentException("At least one load is required.");

        double totalW = 0;
        double totalVA = 0;
        double criticalW = 0;

        foreach (var load in loads)
        {
            double pf = GetTypicalPowerFactor(load.Type);
            double va = load.Watts / pf;
            totalW += load.Watts;
            totalVA += va;
            if (load.IsCritical) criticalW += load.Watts;
        }

        return new UpsLoadAnalysis
        {
            TotalWatts = Math.Round(totalW, 2),
            TotalVA = Math.Round(totalVA, 2),
            WeightedPowerFactor = Math.Round(totalW / totalVA, 3),
            CriticalWatts = Math.Round(criticalW, 2),
            LoadCount = loads.Count,
        };
    }

    // ── Battery Sizing ───────────────────────────────────────────────────────

    /// <summary>
    /// Sizes battery capacity for required runtime.
    /// Applies design margin (1.25), aging factor (0.8 at end of life),
    /// and temperature derating per IEEE 1184.
    /// </summary>
    public static BatteryResult SizeBattery(
        double loadKW,
        double runtimeMinutes,
        double ambientTempCelsius = 25,
        double designMargin = 1.25,
        double agingFactor = 0.80)
    {
        if (loadKW <= 0) throw new ArgumentException("Load must be positive.");
        if (runtimeMinutes <= 0) throw new ArgumentException("Runtime must be positive.");

        // IEEE 1184 temperature derating: capacity drops ~1% per °C above 25°C
        double tempDerating = ambientTempCelsius <= 25 ? 1.0
            : Math.Max(0.5, 1.0 - (ambientTempCelsius - 25) * 0.01);

        double rawKWh = loadKW * (runtimeMinutes / 60.0);
        double requiredKWh = rawKWh * designMargin / agingFactor / tempDerating;

        return new BatteryResult
        {
            RequiredKWh = Math.Round(requiredKWh, 2),
            RuntimeMinutes = runtimeMinutes,
            DesignMargin = designMargin,
            AgingFactor = agingFactor,
            TemperatureDerating = Math.Round(tempDerating, 3),
        };
    }

    // ── UPS Sizing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sizes a complete UPS system: topology, capacity, redundancy, and battery.
    /// </summary>
    public static UpsSizingResult SizeUps(
        IReadOnlyList<UpsLoad> loads,
        double runtimeMinutes,
        UpsTopology topology = UpsTopology.DoubleConversion,
        RedundancyMode redundancy = RedundancyMode.None,
        double ambientTempCelsius = 25)
    {
        var analysis = AnalyzeLoads(loads);

        // Standard UPS frame sizes (kVA)
        double[] frameSizes = { 1, 1.5, 2, 3, 5, 6, 8, 10, 15, 20, 30, 40, 50, 60, 80, 100, 120, 150, 200, 250, 300, 400, 500, 750, 1000 };

        double requiredKVA = analysis.TotalVA / 1000.0;

        // De-rate to 80% maximum utilization per best practice
        double derated = requiredKVA / 0.80;

        // Select frame size
        double frameKVA = frameSizes.FirstOrDefault(f => f >= derated);
        if (frameKVA == 0) frameKVA = frameSizes[^1];

        // Module count for redundancy
        int modules = redundancy switch
        {
            RedundancyMode.NPlus1 => 2,   // N+1 with one module
            RedundancyMode.TwoN => 2,      // Full 2N
            _ => 1,
        };

        // For 2N, each path handles full load
        double perModuleKVA = redundancy == RedundancyMode.TwoN ? frameKVA : frameKVA;
        double totalSystemKVA = frameKVA * modules;

        double loadKW = analysis.TotalWatts / 1000.0;
        var battery = SizeBattery(loadKW, runtimeMinutes, ambientTempCelsius);

        double utilization = requiredKVA / frameKVA * 100;

        return new UpsSizingResult
        {
            RecommendedKVA = frameKVA,
            RecommendedKW = Math.Round(frameKVA * analysis.WeightedPowerFactor, 2),
            Topology = topology,
            Redundancy = redundancy,
            Battery = battery,
            UtilizationPercent = Math.Round(utilization, 1),
            ModuleCount = modules,
        };
    }

    // ── Topology Recommendation ──────────────────────────────────────────────

    /// <summary>
    /// Recommends UPS topology based on load criticality.
    /// </summary>
    public static UpsTopology RecommendTopology(double criticalLoadPercent)
    {
        return criticalLoadPercent switch
        {
            >= 80 => UpsTopology.DoubleConversion,
            >= 40 => UpsTopology.LineInteractive,
            _ => UpsTopology.Standby,
        };
    }
}
