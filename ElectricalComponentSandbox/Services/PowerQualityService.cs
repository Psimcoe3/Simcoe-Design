using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Power quality monitoring and analysis per IEEE 519/1159.
/// THD assessment, voltage event classification, and PQ indices.
/// </summary>
public static class PowerQualityService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum VoltageEventType
    {
        Interruption,       // < 0.1 pu
        Sag,                // 0.1 – 0.9 pu
        Swell,              // 1.1 – 1.8 pu
        Overvoltage,        // > 1.1 pu sustained
        Undervoltage,       // < 0.9 pu sustained
    }

    public enum VoltageEventDuration
    {
        Instantaneous,      // 0.5 – 30 cycles
        Momentary,          // 30 cycles – 3 seconds
        Temporary,          // 3 – 60 seconds
        Sustained,          // > 60 seconds
    }

    /// <summary>IEEE 519 current distortion limits based on ISC/IL ratio.</summary>
    public record HarmonicLimits
    {
        public double TddPercent { get; init; }  // Total demand distortion
        public double H3To11 { get; init; }
        public double H11To17 { get; init; }
        public double H17To23 { get; init; }
        public double H23To35 { get; init; }
        public double H35Plus { get; init; }
    }

    /// <summary>THD measurement result.</summary>
    public record ThdResult
    {
        public double ThdPercent { get; init; }
        public bool MeetsIeee519 { get; init; }
        public double LimitPercent { get; init; }
        public string Assessment { get; init; } = "";
    }

    /// <summary>Voltage event classification.</summary>
    public record VoltageEvent
    {
        public VoltageEventType Type { get; init; }
        public VoltageEventDuration Duration { get; init; }
        public double MagnitudePu { get; init; }
        public double DurationSeconds { get; init; }
        public string Description { get; init; } = "";
    }

    /// <summary>Reliability index result.</summary>
    public record ReliabilityIndices
    {
        public double SAIDI { get; init; }  // System Average Interruption Duration Index
        public double SAIFI { get; init; }  // System Average Interruption Frequency Index
        public double CAIDI { get; init; }  // Customer Average Interruption Duration Index
        public double ASAI { get; init; }   // Average Service Availability Index
    }

    // ── IEEE 519 Harmonic Limits ─────────────────────────────────────────────

    /// <summary>
    /// Returns IEEE 519-2022 Table 2: current distortion limits
    /// for general distribution systems (120V–69kV).
    /// ISC/IL = ratio of short circuit current to maximum demand load current.
    /// </summary>
    public static HarmonicLimits GetIeee519Limits(double iscOverIl)
    {
        if (iscOverIl < 20)
            return new HarmonicLimits { TddPercent = 5.0, H3To11 = 4.0, H11To17 = 2.0, H17To23 = 1.5, H23To35 = 0.6, H35Plus = 0.3 };
        if (iscOverIl < 50)
            return new HarmonicLimits { TddPercent = 8.0, H3To11 = 7.0, H11To17 = 3.5, H17To23 = 2.5, H23To35 = 1.0, H35Plus = 0.5 };
        if (iscOverIl < 100)
            return new HarmonicLimits { TddPercent = 12.0, H3To11 = 10.0, H11To17 = 4.5, H17To23 = 4.0, H23To35 = 1.5, H35Plus = 0.7 };
        if (iscOverIl < 1000)
            return new HarmonicLimits { TddPercent = 15.0, H3To11 = 12.0, H11To17 = 5.5, H17To23 = 5.0, H23To35 = 2.0, H35Plus = 1.0 };
        return new HarmonicLimits { TddPercent = 20.0, H3To11 = 15.0, H11To17 = 7.0, H17To23 = 6.0, H23To35 = 2.5, H35Plus = 1.4 };
    }

    // ── THD Assessment ───────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates voltage THD against IEEE 519 limits.
    /// IEEE 519: ≤ 5% THD for bus voltage ≤ 69 kV,
    /// ≤ 8% THD for individual harmonics.
    /// </summary>
    public static ThdResult EvaluateVoltageTHD(double thdPercent, double busVoltageKv = 0.48)
    {
        double limit = busVoltageKv <= 1.0 ? 8.0
                     : busVoltageKv <= 69 ? 5.0
                     : busVoltageKv <= 161 ? 2.5
                     : 1.5;

        bool meets = thdPercent <= limit;
        string assessment = meets
            ? $"Voltage THD {thdPercent:F1}% within {limit}% limit"
            : $"Voltage THD {thdPercent:F1}% exceeds {limit}% limit — mitigation required";

        return new ThdResult
        {
            ThdPercent = Math.Round(thdPercent, 2),
            MeetsIeee519 = meets,
            LimitPercent = limit,
            Assessment = assessment,
        };
    }

    /// <summary>
    /// Evaluates current TDD against IEEE 519 limits.
    /// </summary>
    public static ThdResult EvaluateCurrentTDD(double tddPercent, double iscOverIl)
    {
        var limits = GetIeee519Limits(iscOverIl);
        bool meets = tddPercent <= limits.TddPercent;
        string assessment = meets
            ? $"Current TDD {tddPercent:F1}% within {limits.TddPercent}% limit (ISC/IL={iscOverIl:F0})"
            : $"Current TDD {tddPercent:F1}% exceeds {limits.TddPercent}% limit — reduce nonlinear loads or add filtering";

        return new ThdResult
        {
            ThdPercent = Math.Round(tddPercent, 2),
            MeetsIeee519 = meets,
            LimitPercent = limits.TddPercent,
            Assessment = assessment,
        };
    }

    // ── Voltage Event Classification (IEEE 1159) ─────────────────────────────

    /// <summary>
    /// Classifies a voltage event per IEEE 1159 categories.
    /// </summary>
    /// <param name="magnitudePu">Voltage magnitude in per-unit.</param>
    /// <param name="durationSec">Duration in seconds.</param>
    public static VoltageEvent ClassifyVoltageEvent(double magnitudePu, double durationSec)
    {
        var duration = durationSec switch
        {
            < 0.5 / 60.0 => VoltageEventDuration.Instantaneous,  // < 0.5 cycles
            < 0.5        => VoltageEventDuration.Instantaneous,
            < 3.0        => VoltageEventDuration.Momentary,
            < 60.0       => VoltageEventDuration.Temporary,
            _            => VoltageEventDuration.Sustained,
        };

        VoltageEventType type;
        string desc;
        if (magnitudePu < 0.1)
        {
            type = VoltageEventType.Interruption;
            desc = $"Interruption ({magnitudePu:F2} pu, {durationSec:F2}s)";
        }
        else if (magnitudePu < 0.9)
        {
            type = VoltageEventType.Sag;
            desc = $"Voltage sag to {magnitudePu:F2} pu for {durationSec:F2}s";
        }
        else if (magnitudePu > 1.1)
        {
            type = duration == VoltageEventDuration.Sustained
                ? VoltageEventType.Overvoltage
                : VoltageEventType.Swell;
            desc = $"Voltage {(type == VoltageEventType.Swell ? "swell" : "overvoltage")} to {magnitudePu:F2} pu for {durationSec:F2}s";
        }
        else
        {
            type = VoltageEventType.Sag; // Normal range — classify as mild event
            desc = $"Normal range ({magnitudePu:F2} pu)";
        }

        return new VoltageEvent
        {
            Type = type,
            Duration = duration,
            MagnitudePu = magnitudePu,
            DurationSeconds = durationSec,
            Description = desc,
        };
    }

    // ── Reliability Indices ──────────────────────────────────────────────────

    /// <summary>
    /// Calculates distribution reliability indices (IEEE 1366).
    /// </summary>
    /// <param name="interruptionMinutes">List of each interruption duration in minutes.</param>
    /// <param name="customersAffected">Customers affected per interruption.</param>
    /// <param name="totalCustomers">Total customers served.</param>
    public static ReliabilityIndices CalculateReliability(
        IReadOnlyList<double> interruptionMinutes,
        IReadOnlyList<int> customersAffected,
        int totalCustomers)
    {
        if (totalCustomers <= 0 || interruptionMinutes.Count == 0)
            return new ReliabilityIndices { ASAI = 1.0 };

        int count = Math.Min(interruptionMinutes.Count, customersAffected.Count);

        double sumCustomerMinutes = 0;
        double sumCustomersAffected = 0;
        for (int i = 0; i < count; i++)
        {
            sumCustomerMinutes += interruptionMinutes[i] * customersAffected[i];
            sumCustomersAffected += customersAffected[i];
        }

        double saidi = sumCustomerMinutes / totalCustomers;
        double saifi = sumCustomersAffected / totalCustomers;
        double caidi = saifi > 0 ? saidi / saifi : 0;
        double totalMinutesInYear = 525600; // 365 × 24 × 60
        double asai = (totalMinutesInYear * totalCustomers - sumCustomerMinutes)
                      / (totalMinutesInYear * totalCustomers);

        return new ReliabilityIndices
        {
            SAIDI = Math.Round(saidi, 2),
            SAIFI = Math.Round(saifi, 4),
            CAIDI = Math.Round(caidi, 2),
            ASAI = Math.Round(asai, 6),
        };
    }
}
