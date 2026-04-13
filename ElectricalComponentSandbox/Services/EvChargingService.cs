using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC 625 electric vehicle charging infrastructure sizing.
/// Covers EVSE demand factors, branch circuit sizing, load management,
/// and power sharing calculations.
/// </summary>
public static class EvChargingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum EvseLevel
    {
        Level1,   // 120V, up to 1.9 kW
        Level2,   // 240V, up to 19.2 kW
        DcFast,   // 480V 3-phase, 50-350 kW
    }

    public enum LoadManagementMode
    {
        None,                // Each EVSE at full rated capacity
        ScheduledSharing,    // Time-of-use rotation
        DynamicPowerSharing, // Real-time load balancing via OCPP/ISO 15118
    }

    public record EvseStation
    {
        public string Id { get; init; } = "";
        public EvseLevel Level { get; init; } = EvseLevel.Level2;
        public double RatedKW { get; init; }
        public double Voltage { get; init; } = 240;
        public int Phases { get; init; } = 1;
        public bool IsContinuousLoad { get; init; } = true;
    }

    public record BranchCircuitResult
    {
        public double LoadAmps { get; init; }
        public double ContinuousAmps { get; init; }
        public double MinBreakerAmps { get; init; }
        public double SelectedBreakerAmps { get; init; }
        public string MinWireSize { get; init; } = "";
    }

    public record DemandResult
    {
        public int StationCount { get; init; }
        public double TotalConnectedKW { get; init; }
        public double DemandFactor { get; init; }
        public double DemandKW { get; init; }
        public double DemandAmps { get; init; }
        public double Voltage { get; init; }
    }

    public record PowerSharingResult
    {
        public double AvailableKW { get; init; }
        public int StationCount { get; init; }
        public double KWPerStation { get; init; }
        public double MinKWPerStation { get; init; }
        public bool IsAdequate { get; init; }
        public LoadManagementMode Mode { get; init; }
    }

    public record InfrastructureSummary
    {
        public int Level1Count { get; init; }
        public int Level2Count { get; init; }
        public int DcFastCount { get; init; }
        public double TotalConnectedKW { get; init; }
        public double TotalDemandKW { get; init; }
        public double TotalDemandAmps { get; init; }
        public double EstimatedMonthlyKWh { get; init; }
    }

    // ── Standard Breaker Sizes (NEC 240.6(A)) ───────────────────────────────

    private static readonly double[] StandardBreakers =
    {
        15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100,
        110, 125, 150, 175, 200, 225, 250, 300, 350, 400,
    };

    // ── Wire Size Table (75°C copper, NEC 310.16) ────────────────────────────

    private static readonly (string Size, double Ampacity)[] WireTable =
    {
        ("14", 20), ("12", 25), ("10", 35), ("8", 50), ("6", 65),
        ("4", 85), ("3", 100), ("2", 115), ("1", 130),
        ("1/0", 150), ("2/0", 175), ("3/0", 200), ("4/0", 230),
        ("250", 255), ("300", 285), ("350", 310), ("400", 335),
        ("500", 380), ("600", 420), ("750", 475),
    };

    // ── Branch Circuit Sizing (NEC 625.40) ───────────────────────────────────

    /// <summary>
    /// Sizes branch circuit for a single EVSE per NEC 625.40.
    /// EVSE is a continuous load → 125% factor unless load management qualifies.
    /// </summary>
    public static BranchCircuitResult SizeBranchCircuit(EvseStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (station.RatedKW <= 0) throw new ArgumentException("Rated kW must be positive.");

        double loadAmps = CalculateAmps(station.RatedKW, station.Voltage, station.Phases);
        double continuousFactor = station.IsContinuousLoad ? 1.25 : 1.0;
        double continuousAmps = loadAmps * continuousFactor;

        double selectedBreaker = StandardBreakers.FirstOrDefault(b => b >= continuousAmps);
        if (selectedBreaker == 0) selectedBreaker = StandardBreakers[^1];

        string wireSize = GetMinWireSize(selectedBreaker);

        return new BranchCircuitResult
        {
            LoadAmps = Math.Round(loadAmps, 2),
            ContinuousAmps = Math.Round(continuousAmps, 2),
            MinBreakerAmps = Math.Round(continuousAmps, 2),
            SelectedBreakerAmps = selectedBreaker,
            MinWireSize = wireSize,
        };
    }

    // ── NEC 625.42 Demand Factors ────────────────────────────────────────────

    /// <summary>
    /// Calculates demand load for multiple EVSE per NEC 625.42 Table.
    /// Demand factors apply when an energy management system (EMS) is NOT used.
    /// NEC 2023 625.42: 1-10 stations can use Table 625.42 demand factors.
    /// </summary>
    public static DemandResult CalculateDemand(IReadOnlyList<EvseStation> stations, double serviceVoltage, int servicePhases)
    {
        ArgumentNullException.ThrowIfNull(stations);
        if (stations.Count == 0) throw new ArgumentException("At least one station is required.");

        double totalKW = stations.Sum(s => s.RatedKW);
        double demandFactor = GetDemandFactor(stations.Count);
        double demandKW = totalKW * demandFactor;
        double demandAmps = CalculateAmps(demandKW, serviceVoltage, servicePhases);

        return new DemandResult
        {
            StationCount = stations.Count,
            TotalConnectedKW = Math.Round(totalKW, 2),
            DemandFactor = demandFactor,
            DemandKW = Math.Round(demandKW, 2),
            DemandAmps = Math.Round(demandAmps, 2),
            Voltage = serviceVoltage,
        };
    }

    /// <summary>
    /// NEC 625.42 demand factor table (simplified per 2023 NEC).
    /// </summary>
    public static double GetDemandFactor(int stationCount)
    {
        if (stationCount <= 0) return 1.0;
        return stationCount switch
        {
            1 => 1.00,
            2 => 1.00,
            3 => 1.00,
            4 or 5 => 0.95,
            6 or 7 => 0.90,
            8 or 9 or 10 => 0.85,
            >= 11 and <= 20 => 0.80,
            >= 21 and <= 40 => 0.75,
            _ => 0.70,
        };
    }

    // ── Power Sharing / Load Management (NEC 625.42(B)) ──────────────────────

    /// <summary>
    /// Evaluates dynamic power sharing for a given circuit capacity.
    /// NEC 625.42(B) allows EMS-controlled EVSE to share circuit capacity.
    /// Minimum 1.4 kW (6A @ 240V) per SAE J1772 for Level 2.
    /// </summary>
    public static PowerSharingResult EvaluatePowerSharing(
        double availableKW,
        IReadOnlyList<EvseStation> stations,
        LoadManagementMode mode)
    {
        ArgumentNullException.ThrowIfNull(stations);
        if (stations.Count == 0) throw new ArgumentException("At least one station is required.");

        double kwPerStation = availableKW / stations.Count;

        // Minimum per SAE J1772: 6A × 240V = 1.44 kW for Level 2
        // DC Fast has higher minimums; use 10 kW as practical minimum
        double minKW = stations.Any(s => s.Level == EvseLevel.DcFast) ? 10.0 : 1.4;

        bool adequate = kwPerStation >= minKW && mode != LoadManagementMode.None;

        return new PowerSharingResult
        {
            AvailableKW = Math.Round(availableKW, 2),
            StationCount = stations.Count,
            KWPerStation = Math.Round(kwPerStation, 2),
            MinKWPerStation = minKW,
            IsAdequate = adequate,
            Mode = mode,
        };
    }

    // ── Infrastructure Summary ───────────────────────────────────────────────

    /// <summary>
    /// Generates a summary of EV charging infrastructure including demand
    /// and estimated monthly energy consumption.
    /// </summary>
    public static InfrastructureSummary Summarize(
        IReadOnlyList<EvseStation> stations,
        double serviceVoltage,
        int servicePhases,
        double avgDailyHoursPerStation = 4.0)
    {
        ArgumentNullException.ThrowIfNull(stations);
        if (stations.Count == 0) throw new ArgumentException("At least one station is required.");

        var demand = CalculateDemand(stations, serviceVoltage, servicePhases);
        // Estimate monthly kWh: each station at avg utilization × 30 days
        double monthlyKWh = stations.Sum(s => s.RatedKW * avgDailyHoursPerStation * 30.0);

        return new InfrastructureSummary
        {
            Level1Count = stations.Count(s => s.Level == EvseLevel.Level1),
            Level2Count = stations.Count(s => s.Level == EvseLevel.Level2),
            DcFastCount = stations.Count(s => s.Level == EvseLevel.DcFast),
            TotalConnectedKW = demand.TotalConnectedKW,
            TotalDemandKW = demand.DemandKW,
            TotalDemandAmps = demand.DemandAmps,
            EstimatedMonthlyKWh = Math.Round(monthlyKWh, 1),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static double CalculateAmps(double kw, double voltage, int phases)
    {
        if (voltage <= 0) throw new ArgumentException("Voltage must be positive.");
        return phases >= 3
            ? (kw * 1000.0) / (voltage * Math.Sqrt(3))
            : (kw * 1000.0) / voltage;
    }

    private static string GetMinWireSize(double requiredAmps)
    {
        foreach (var (size, ampacity) in WireTable)
        {
            if (ampacity >= requiredAmps) return size;
        }
        return WireTable[^1].Size;
    }
}
