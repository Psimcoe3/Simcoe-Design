using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC 706 battery energy storage system (BESS) sizing.
/// Covers peak shaving, load shifting, SOC management, round-trip
/// efficiency, and interconnection sizing.
/// </summary>
public static class EnergyStorageService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum BatteryChemistry
    {
        LithiumIon,
        LithiumIronPhosphate,
        LeadAcid,
        FlowVanadium,
    }

    public enum ApplicationMode
    {
        PeakShaving,
        LoadShifting,
        BackupPower,
        FrequencyRegulation,
    }

    public record BessInput
    {
        public double TargetEnergyKWh { get; init; }
        public double TargetPowerKW { get; init; }
        public BatteryChemistry Chemistry { get; init; } = BatteryChemistry.LithiumIon;
        public ApplicationMode Application { get; init; } = ApplicationMode.PeakShaving;
        public double MinSocPercent { get; init; } = 10;
        public double MaxSocPercent { get; init; } = 90;
        public double SystemVoltage { get; init; } = 480;
        public int SystemPhases { get; init; } = 3;
    }

    public record BessSizingResult
    {
        public double UsableEnergyKWh { get; init; }
        public double GrossEnergyKWh { get; init; }
        public double RatedPowerKW { get; init; }
        public double RoundTripEfficiency { get; init; }
        public double CRate { get; init; }
        public double DischargeDurationHours { get; init; }
        public double SystemVoltage { get; init; }
        public double MaxCurrentAmps { get; init; }
    }

    public record InterconnectionResult
    {
        public double MaxAcAmps { get; init; }
        public double ContinuousAmps { get; init; }
        public double MinBreakerAmps { get; init; }
        public double SelectedBreakerAmps { get; init; }
        public string MinWireSize { get; init; } = "";
        public bool RequiresDisconnect { get; init; }
    }

    public record PeakShavingResult
    {
        public double OriginalPeakKW { get; init; }
        public double TargetPeakKW { get; init; }
        public double ShavingKW { get; init; }
        public double RequiredEnergyKWh { get; init; }
        public double EstimatedDailyKWhDischarge { get; init; }
        public int EstimatedCycles { get; init; }
    }

    // ── Standard breaker sizes ───────────────────────────────────────────────

    private static readonly double[] StandardBreakers =
    {
        15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100,
        110, 125, 150, 175, 200, 225, 250, 300, 350, 400,
        450, 500, 600, 700, 800,
    };

    private static readonly (string Size, double Ampacity)[] WireTable =
    {
        ("14", 20), ("12", 25), ("10", 35), ("8", 50), ("6", 65),
        ("4", 85), ("3", 100), ("2", 115), ("1", 130),
        ("1/0", 150), ("2/0", 175), ("3/0", 200), ("4/0", 230),
        ("250", 255), ("300", 285), ("350", 310), ("400", 335),
        ("500", 380), ("600", 420), ("750", 475),
    };

    // ── Chemistry Parameters ─────────────────────────────────────────────────

    /// <summary>Returns typical round-trip AC-to-AC efficiency for a chemistry.</summary>
    public static double GetRoundTripEfficiency(BatteryChemistry chemistry) => chemistry switch
    {
        BatteryChemistry.LithiumIon => 0.90,
        BatteryChemistry.LithiumIronPhosphate => 0.92,
        BatteryChemistry.LeadAcid => 0.80,
        BatteryChemistry.FlowVanadium => 0.75,
        _ => 0.85,
    };

    /// <summary>Returns typical cycle life at 80% DOD.</summary>
    public static int GetTypicalCycleLife(BatteryChemistry chemistry) => chemistry switch
    {
        BatteryChemistry.LithiumIon => 5000,
        BatteryChemistry.LithiumIronPhosphate => 8000,
        BatteryChemistry.LeadAcid => 1500,
        BatteryChemistry.FlowVanadium => 15000,
        _ => 3000,
    };

    // ── BESS Sizing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sizes a BESS based on target usable energy and power.
    /// Gross capacity accounts for SOC window and round-trip efficiency.
    /// </summary>
    public static BessSizingResult SizeBess(BessInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TargetEnergyKWh <= 0) throw new ArgumentException("Target energy must be positive.");
        if (input.TargetPowerKW <= 0) throw new ArgumentException("Target power must be positive.");

        double socWindow = (input.MaxSocPercent - input.MinSocPercent) / 100.0;
        if (socWindow <= 0) throw new ArgumentException("Max SOC must exceed Min SOC.");

        double rte = GetRoundTripEfficiency(input.Chemistry);
        double usableKWh = input.TargetEnergyKWh;
        double grossKWh = usableKWh / socWindow;

        double cRate = input.TargetPowerKW / grossKWh;
        double durationHours = usableKWh / input.TargetPowerKW;
        double maxAmps = CalculateAmps(input.TargetPowerKW, input.SystemVoltage, input.SystemPhases);

        return new BessSizingResult
        {
            UsableEnergyKWh = Math.Round(usableKWh, 2),
            GrossEnergyKWh = Math.Round(grossKWh, 2),
            RatedPowerKW = Math.Round(input.TargetPowerKW, 2),
            RoundTripEfficiency = rte,
            CRate = Math.Round(cRate, 3),
            DischargeDurationHours = Math.Round(durationHours, 2),
            SystemVoltage = input.SystemVoltage,
            MaxCurrentAmps = Math.Round(maxAmps, 2),
        };
    }

    // ── Interconnection Sizing (NEC 706) ────────────────────────────────────

    /// <summary>
    /// Sizes the AC interconnection circuit per NEC 706.
    /// ESS is a continuous load (125% factor). Disconnect required per 706.15.
    /// </summary>
    public static InterconnectionResult SizeInterconnection(BessInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TargetPowerKW <= 0) throw new ArgumentException("Target power must be positive.");

        double acAmps = CalculateAmps(input.TargetPowerKW, input.SystemVoltage, input.SystemPhases);
        double continuousAmps = acAmps * 1.25; // NEC continuous load

        double breaker = StandardBreakers.FirstOrDefault(b => b >= continuousAmps);
        if (breaker == 0) breaker = StandardBreakers[^1];

        string wireSize = GetMinWireSize(breaker);

        return new InterconnectionResult
        {
            MaxAcAmps = Math.Round(acAmps, 2),
            ContinuousAmps = Math.Round(continuousAmps, 2),
            MinBreakerAmps = Math.Round(continuousAmps, 2),
            SelectedBreakerAmps = breaker,
            MinWireSize = wireSize,
            RequiresDisconnect = true, // NEC 706.15 always requires
        };
    }

    // ── Peak Shaving Analysis ────────────────────────────────────────────────

    /// <summary>
    /// Calculates BESS requirements for peak shaving a facility load.
    /// </summary>
    public static PeakShavingResult AnalyzePeakShaving(
        double currentPeakKW,
        double targetPeakKW,
        double peakDurationHours)
    {
        if (currentPeakKW <= 0) throw new ArgumentException("Current peak must be positive.");
        if (targetPeakKW <= 0) throw new ArgumentException("Target peak must be positive.");
        if (targetPeakKW >= currentPeakKW)
        {
            return new PeakShavingResult
            {
                OriginalPeakKW = Math.Round(currentPeakKW, 2),
                TargetPeakKW = Math.Round(targetPeakKW, 2),
                ShavingKW = 0,
                RequiredEnergyKWh = 0,
                EstimatedDailyKWhDischarge = 0,
                EstimatedCycles = 0,
            };
        }

        double shavingKW = currentPeakKW - targetPeakKW;
        // Trapezoidal approximation: energy = shavingKW × duration × 0.75 (typical shape factor)
        double energyKWh = shavingKW * peakDurationHours * 0.75;
        double dailyDischarge = energyKWh; // One peak shave per day
        int annualCycles = 260; // ~weekdays/year

        return new PeakShavingResult
        {
            OriginalPeakKW = Math.Round(currentPeakKW, 2),
            TargetPeakKW = Math.Round(targetPeakKW, 2),
            ShavingKW = Math.Round(shavingKW, 2),
            RequiredEnergyKWh = Math.Round(energyKWh, 2),
            EstimatedDailyKWhDischarge = Math.Round(dailyDischarge, 2),
            EstimatedCycles = annualCycles,
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
