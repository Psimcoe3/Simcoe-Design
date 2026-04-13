using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Demand load profiling service. Analyzes time-based load data for
/// peak demand, load factor, demand diversity, and billing demand.
/// </summary>
public static class DemandLoadProfilingService
{
    // ── Records ──────────────────────────────────────────────────────────────

    /// <summary>Single demand interval reading (typically 15-min or 30-min).</summary>
    public record DemandReading
    {
        public DateTime Timestamp { get; init; }
        public double KwDemand { get; init; }
        public double KvarDemand { get; init; }
    }

    /// <summary>Load profile analysis result.</summary>
    public record LoadProfileResult
    {
        public double PeakDemandKw { get; init; }
        public DateTime PeakTimestamp { get; init; }
        public double AverageDemandKw { get; init; }
        public double MinDemandKw { get; init; }
        public double LoadFactor { get; init; }            // Avg / Peak
        public double DemandFactor { get; init; }           // Peak / Connected
        public double TotalEnergyKwh { get; init; }
        public double PeakKvar { get; init; }
        public double PeakPowerFactor { get; init; }
    }

    /// <summary>Time-of-use summary.</summary>
    public record TimeOfUseSummary
    {
        public double OnPeakAvgKw { get; init; }
        public double MidPeakAvgKw { get; init; }
        public double OffPeakAvgKw { get; init; }
        public double OnPeakEnergyKwh { get; init; }
        public double OffPeakEnergyKwh { get; init; }
        public double OnToOffRatio { get; init; }
    }

    /// <summary>Billing demand calculation.</summary>
    public record BillingDemandResult
    {
        public double MeasuredPeakKw { get; init; }
        public double RatchetDemandKw { get; init; }    // Highest of current or ratchet %
        public double BillingDemandKw { get; init; }    // Final billed demand
        public double PowerFactorPenaltyPercent { get; init; }
    }

    // ── Load Profile Analysis ────────────────────────────────────────────────

    /// <summary>
    /// Analyzes a set of demand readings to produce a load profile.
    /// </summary>
    /// <param name="readings">Time-series demand readings.</param>
    /// <param name="connectedLoadKw">Total connected load for demand factor calc.</param>
    /// <param name="intervalMinutes">Demand interval duration (default 15).</param>
    public static LoadProfileResult AnalyzeProfile(
        IReadOnlyList<DemandReading> readings,
        double connectedLoadKw,
        int intervalMinutes = 15)
    {
        if (readings.Count == 0)
            return new LoadProfileResult();

        var peak = readings.OrderByDescending(r => r.KwDemand).First();
        double avg = readings.Average(r => r.KwDemand);
        double min = readings.Min(r => r.KwDemand);
        double totalKwh = readings.Sum(r => r.KwDemand) * intervalMinutes / 60.0;
        double loadFactor = peak.KwDemand > 0 ? avg / peak.KwDemand : 0;
        double demandFactor = connectedLoadKw > 0 ? peak.KwDemand / connectedLoadKw : 0;

        double peakKvar = readings.OrderByDescending(r => r.KwDemand).First().KvarDemand;
        double peakKva = Math.Sqrt(peak.KwDemand * peak.KwDemand + peakKvar * peakKvar);
        double peakPf = peakKva > 0 ? peak.KwDemand / peakKva : 1.0;

        return new LoadProfileResult
        {
            PeakDemandKw = Math.Round(peak.KwDemand, 2),
            PeakTimestamp = peak.Timestamp,
            AverageDemandKw = Math.Round(avg, 2),
            MinDemandKw = Math.Round(min, 2),
            LoadFactor = Math.Round(loadFactor, 4),
            DemandFactor = Math.Round(demandFactor, 4),
            TotalEnergyKwh = Math.Round(totalKwh, 1),
            PeakKvar = Math.Round(peakKvar, 2),
            PeakPowerFactor = Math.Round(peakPf, 4),
        };
    }

    // ── Time-of-Use ──────────────────────────────────────────────────────────

    /// <summary>
    /// Splits demand data into on-peak (12–18), mid-peak (8–12, 18–21),
    /// and off-peak (21–8) periods and calculates usage.
    /// </summary>
    public static TimeOfUseSummary AnalyzeTimeOfUse(
        IReadOnlyList<DemandReading> readings, int intervalMinutes = 15)
    {
        var onPeak = readings.Where(r => r.Timestamp.Hour >= 12 && r.Timestamp.Hour < 18).ToList();
        var midPeak = readings.Where(r =>
            (r.Timestamp.Hour >= 8 && r.Timestamp.Hour < 12) ||
            (r.Timestamp.Hour >= 18 && r.Timestamp.Hour < 21)).ToList();
        var offPeak = readings.Where(r => r.Timestamp.Hour >= 21 || r.Timestamp.Hour < 8).ToList();

        double onAvg = onPeak.Count > 0 ? onPeak.Average(r => r.KwDemand) : 0;
        double midAvg = midPeak.Count > 0 ? midPeak.Average(r => r.KwDemand) : 0;
        double offAvg = offPeak.Count > 0 ? offPeak.Average(r => r.KwDemand) : 0;

        double onEnergy = onPeak.Sum(r => r.KwDemand) * intervalMinutes / 60.0;
        double offEnergy = offPeak.Sum(r => r.KwDemand) * intervalMinutes / 60.0;

        double ratio = offAvg > 0 ? onAvg / offAvg : 0;

        return new TimeOfUseSummary
        {
            OnPeakAvgKw = Math.Round(onAvg, 2),
            MidPeakAvgKw = Math.Round(midAvg, 2),
            OffPeakAvgKw = Math.Round(offAvg, 2),
            OnPeakEnergyKwh = Math.Round(onEnergy, 1),
            OffPeakEnergyKwh = Math.Round(offEnergy, 1),
            OnToOffRatio = Math.Round(ratio, 2),
        };
    }

    // ── Billing Demand ───────────────────────────────────────────────────────

    /// <summary>
    /// Calculates billing demand with ratchet clause and power factor penalty.
    /// </summary>
    /// <param name="currentPeakKw">Current billing period peak demand.</param>
    /// <param name="previous11MonthsPeakKw">Highest peak from previous 11 months.</param>
    /// <param name="ratchetPercent">Ratchet percentage (e.g., 0.80 = 80%).</param>
    /// <param name="powerFactor">Measured power factor.</param>
    /// <param name="pfThreshold">PF below which penalty applies (default 0.90).</param>
    public static BillingDemandResult CalculateBillingDemand(
        double currentPeakKw, double previous11MonthsPeakKw,
        double ratchetPercent = 0.80, double powerFactor = 0.95,
        double pfThreshold = 0.90)
    {
        double ratchetKw = previous11MonthsPeakKw * ratchetPercent;
        double billingKw = Math.Max(currentPeakKw, ratchetKw);

        double pfPenalty = 0;
        if (powerFactor < pfThreshold && powerFactor > 0)
        {
            // Common adjustment: bill at kVA instead of kW
            pfPenalty = (1.0 / powerFactor - 1.0 / pfThreshold) / (1.0 / pfThreshold) * 100;
            billingKw *= pfThreshold / powerFactor;
        }

        return new BillingDemandResult
        {
            MeasuredPeakKw = Math.Round(currentPeakKw, 2),
            RatchetDemandKw = Math.Round(ratchetKw, 2),
            BillingDemandKw = Math.Round(billingKw, 2),
            PowerFactorPenaltyPercent = Math.Round(pfPenalty, 2),
        };
    }

    // ── Diversity Factor ─────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the diversity factor from individual peak demands.
    /// Diversity Factor = Sum of individual peaks / Coincident peak.
    /// Typical values: 1.0 – 3.0 (higher = more diverse).
    /// </summary>
    public static double CalculateDiversityFactor(
        IReadOnlyList<double> individualPeaksKw, double coincidentPeakKw)
    {
        if (coincidentPeakKw <= 0) return 0;
        double sumPeaks = individualPeaksKw.Sum();
        return Math.Round(sumPeaks / coincidentPeakKw, 4);
    }
}
