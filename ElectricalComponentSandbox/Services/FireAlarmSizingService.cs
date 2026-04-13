using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NFPA 72 fire alarm system sizing: battery calculations, NAC circuit loading,
/// IDC device counts, and audibility/visibility coverage.
/// </summary>
public static class FireAlarmSizingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum DeviceType
    {
        SmokeDetector,
        HeatDetector,
        DuctDetector,
        PullStation,
        Horn,
        Strobe,
        HornStrobe,
        SpeakerStrobe,
        MonitorModule,
        ControlModule,
        RelayModule,
    }

    /// <summary>A fire alarm device with its current draw.</summary>
    public record FaDevice
    {
        public string Id { get; init; } = "";
        public DeviceType Type { get; init; }
        public string Description { get; init; } = "";
        public double SupervisoryCurrentMA { get; init; }
        public double AlarmCurrentMA { get; init; }
        public string Circuit { get; init; } = "";
    }

    /// <summary>Battery calculation result per NFPA 72 §10.6.7.</summary>
    public record BatteryCalcResult
    {
        public double TotalSupervisoryCurrentMA { get; init; }
        public double TotalAlarmCurrentMA { get; init; }
        public double SupervisoryHours { get; init; } = 24;
        public double AlarmMinutes { get; init; } = 5;
        public double SupervisoryAH { get; init; }
        public double AlarmAH { get; init; }
        public double TotalRequiredAH { get; init; }
        public double RecommendedBatteryAH { get; init; }
        public double SafetyMarginPercent { get; init; } = 20;
    }

    /// <summary>NAC circuit loading summary.</summary>
    public record NacCircuitResult
    {
        public string CircuitId { get; init; } = "";
        public int DeviceCount { get; init; }
        public double TotalAlarmCurrentMA { get; init; }
        public double CircuitCapacityMA { get; init; } = 2500;
        public double UtilizationPercent => CircuitCapacityMA > 0
            ? Math.Round(TotalAlarmCurrentMA / CircuitCapacityMA * 100, 1) : 0;
        public bool Overloaded => TotalAlarmCurrentMA > CircuitCapacityMA;
    }

    /// <summary>IDC circuit summary.</summary>
    public record IdcCircuitResult
    {
        public string CircuitId { get; init; } = "";
        public int DeviceCount { get; init; }
        public double TotalSupervisoryCurrentMA { get; init; }
        public double TotalAlarmCurrentMA { get; init; }
        public int MaxDevicesRecommended { get; init; } = 20;
        public bool ExceedsRecommended => DeviceCount > MaxDevicesRecommended;
    }

    /// <summary>Complete fire alarm system sizing summary.</summary>
    public record SystemSummary
    {
        public int TotalDevices { get; init; }
        public int InitiatingDevices { get; init; }
        public int NotificationDevices { get; init; }
        public int ModuleDevices { get; init; }
        public BatteryCalcResult Battery { get; init; } = new();
        public List<NacCircuitResult> NacCircuits { get; init; } = new();
        public List<IdcCircuitResult> IdcCircuits { get; init; } = new();
        public bool AnyNacOverloaded => NacCircuits.Any(n => n.Overloaded);
        public bool AnyIdcExceedsRecommended => IdcCircuits.Any(i => i.ExceedsRecommended);
    }

    // ── Battery Sizing ───────────────────────────────────────────────────────

    /// <summary>
    /// Calculates battery capacity per NFPA 72 §10.6.7.
    /// Supervisory: 24 hours standby (default). Alarm: 5 minutes (default).
    /// 20% safety margin applied.
    /// </summary>
    public static BatteryCalcResult CalculateBattery(
        IEnumerable<FaDevice> devices,
        double panelSupervisoryMA = 100,
        double panelAlarmMA = 200,
        double supervisoryHours = 24,
        double alarmMinutes = 5,
        double safetyMarginPercent = 20)
    {
        var list = devices.ToList();
        double supTotal = panelSupervisoryMA + list.Sum(d => d.SupervisoryCurrentMA);
        double almTotal = panelAlarmMA + list.Sum(d => d.AlarmCurrentMA);

        double supAH = supTotal / 1000.0 * supervisoryHours;
        double almAH = almTotal / 1000.0 * (alarmMinutes / 60.0);
        double totalAH = supAH + almAH;
        double recommended = totalAH * (1.0 + safetyMarginPercent / 100.0);

        return new BatteryCalcResult
        {
            TotalSupervisoryCurrentMA = Math.Round(supTotal, 1),
            TotalAlarmCurrentMA = Math.Round(almTotal, 1),
            SupervisoryHours = supervisoryHours,
            AlarmMinutes = alarmMinutes,
            SupervisoryAH = Math.Round(supAH, 2),
            AlarmAH = Math.Round(almAH, 2),
            TotalRequiredAH = Math.Round(totalAH, 2),
            RecommendedBatteryAH = Math.Ceiling(recommended),
            SafetyMarginPercent = safetyMarginPercent,
        };
    }

    // ── NAC Circuit Loading ──────────────────────────────────────────────────

    /// <summary>
    /// Analyzes NAC (Notification Appliance Circuit) loading by circuit.
    /// </summary>
    public static List<NacCircuitResult> AnalyzeNacCircuits(
        IEnumerable<FaDevice> devices,
        double circuitCapacityMA = 2500)
    {
        var nacDevices = devices.Where(d => IsNotificationDevice(d.Type)).ToList();
        return nacDevices
            .GroupBy(d => d.Circuit)
            .Select(g => new NacCircuitResult
            {
                CircuitId = g.Key,
                DeviceCount = g.Count(),
                TotalAlarmCurrentMA = Math.Round(g.Sum(d => d.AlarmCurrentMA), 1),
                CircuitCapacityMA = circuitCapacityMA,
            })
            .OrderBy(r => r.CircuitId)
            .ToList();
    }

    // ── IDC Circuit Loading ──────────────────────────────────────────────────

    /// <summary>
    /// Analyzes IDC (Initiating Device Circuit) loading by circuit.
    /// </summary>
    public static List<IdcCircuitResult> AnalyzeIdcCircuits(
        IEnumerable<FaDevice> devices,
        int maxDevicesPerCircuit = 20)
    {
        var idcDevices = devices.Where(d => IsInitiatingDevice(d.Type)).ToList();
        return idcDevices
            .GroupBy(d => d.Circuit)
            .Select(g => new IdcCircuitResult
            {
                CircuitId = g.Key,
                DeviceCount = g.Count(),
                TotalSupervisoryCurrentMA = Math.Round(g.Sum(d => d.SupervisoryCurrentMA), 1),
                TotalAlarmCurrentMA = Math.Round(g.Sum(d => d.AlarmCurrentMA), 1),
                MaxDevicesRecommended = maxDevicesPerCircuit,
            })
            .OrderBy(r => r.CircuitId)
            .ToList();
    }

    // ── System Summary ───────────────────────────────────────────────────────

    /// <summary>
    /// Produces a complete fire alarm system sizing summary.
    /// </summary>
    public static SystemSummary AnalyzeSystem(
        IEnumerable<FaDevice> devices,
        double panelSupervisoryMA = 100,
        double panelAlarmMA = 200)
    {
        var list = devices.ToList();
        return new SystemSummary
        {
            TotalDevices = list.Count,
            InitiatingDevices = list.Count(d => IsInitiatingDevice(d.Type)),
            NotificationDevices = list.Count(d => IsNotificationDevice(d.Type)),
            ModuleDevices = list.Count(d => IsModuleDevice(d.Type)),
            Battery = CalculateBattery(list, panelSupervisoryMA, panelAlarmMA),
            NacCircuits = AnalyzeNacCircuits(list),
            IdcCircuits = AnalyzeIdcCircuits(list),
        };
    }

    // ── Coverage Area ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns NFPA 72 maximum coverage area per detector in sq ft.
    /// Smoke: 900 ft² (30×30), Heat: 400-900 ft² depends on spacing.
    /// </summary>
    public static double GetMaxCoverageAreaSqFt(DeviceType type)
    {
        return type switch
        {
            DeviceType.SmokeDetector => 900,
            DeviceType.HeatDetector => 400,
            DeviceType.DuctDetector => 0, // N/A, duct-mounted
            DeviceType.PullStation => 0,  // N/A, per egress
            _ => 0,
        };
    }

    /// <summary>
    /// Calculates minimum number of detectors for a given area.
    /// </summary>
    public static int MinimumDetectors(DeviceType type, double areaSqFt)
    {
        double coverage = GetMaxCoverageAreaSqFt(type);
        if (coverage <= 0) return 0;
        return Math.Max(1, (int)Math.Ceiling(areaSqFt / coverage));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsInitiatingDevice(DeviceType type) =>
        type is DeviceType.SmokeDetector or DeviceType.HeatDetector
            or DeviceType.DuctDetector or DeviceType.PullStation;

    private static bool IsNotificationDevice(DeviceType type) =>
        type is DeviceType.Horn or DeviceType.Strobe
            or DeviceType.HornStrobe or DeviceType.SpeakerStrobe;

    private static bool IsModuleDevice(DeviceType type) =>
        type is DeviceType.MonitorModule or DeviceType.ControlModule
            or DeviceType.RelayModule;
}
