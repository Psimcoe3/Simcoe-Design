using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Standby generator paralleling calculations for synchronizing checks,
/// spinning reserve assessment, and proportional kW/kVAR load sharing.
/// </summary>
public static class GeneratorParallelingService
{
    public enum LoadShareMode
    {
        Isochronous,
        RealPowerDroop,
        BaseLoad,
    }

    public record GeneratorUnit
    {
        public string Id { get; init; } = "";
        public double RatedKW { get; init; }
        public double RatedKVAR { get; init; }
        public double MinStableLoadPercent { get; init; } = 30;
        public bool IsAvailable { get; init; } = true;
        public bool IsPreferred { get; init; }
    }

    public record SynchronizingResult
    {
        public double VoltageDifferencePercent { get; init; }
        public double FrequencyDifferenceHz { get; init; }
        public double PhaseAngleDifferenceDeg { get; init; }
        public bool IsAcceptable { get; init; }
        public string? Issue { get; init; }
    }

    public record UnitLoadShare
    {
        public string Id { get; init; } = "";
        public double AssignedKW { get; init; }
        public double AssignedKVAR { get; init; }
        public double UtilizationPercent { get; init; }
        public bool BelowMinimumStableLoad { get; init; }
    }

    public record ParallelingPlan
    {
        public int RequiredUnits { get; init; }
        public int OnlineUnits { get; init; }
        public double TotalDemandKW { get; init; }
        public double TotalDemandKVAR { get; init; }
        public double OnlineCapacityKW { get; init; }
        public double RequiredCapacityKW { get; init; }
        public double SpinningReserveKW { get; init; }
        public bool IsAdequate { get; init; }
        public bool SupportsNPlusOne { get; init; }
        public LoadShareMode Mode { get; init; }
        public List<UnitLoadShare> UnitShares { get; init; } = new();
        public string? Issue { get; init; }
    }

    /// <summary>
    /// Evaluates whether an incoming generator is within a typical synchronizing window.
    /// Typical checks: 5% voltage, 0.2 Hz frequency, and 10 electrical degrees.
    /// </summary>
    public static SynchronizingResult CheckSynchronizing(
        double incomingVoltage,
        double busVoltage,
        double incomingFrequency,
        double busFrequency,
        double phaseAngleDifferenceDeg,
        double maxVoltageDifferencePercent = 5.0,
        double maxFrequencyDifferenceHz = 0.2,
        double maxPhaseAngleDifferenceDeg = 10.0)
    {
        if (incomingVoltage <= 0 || busVoltage <= 0)
            throw new ArgumentException("Voltages must be positive.");
        if (incomingFrequency <= 0 || busFrequency <= 0)
            throw new ArgumentException("Frequencies must be positive.");

        double voltageDiffPercent = Math.Abs(incomingVoltage - busVoltage) / busVoltage * 100.0;
        double frequencyDiffHz = Math.Abs(incomingFrequency - busFrequency);
        double phaseDiffDeg = Math.Abs(phaseAngleDifferenceDeg);

        var issues = new List<string>();
        if (voltageDiffPercent > maxVoltageDifferencePercent)
            issues.Add("Voltage difference exceeds synchronizing window");
        if (frequencyDiffHz > maxFrequencyDifferenceHz)
            issues.Add("Frequency difference exceeds synchronizing window");
        if (phaseDiffDeg > maxPhaseAngleDifferenceDeg)
            issues.Add("Phase angle exceeds synchronizing window");

        return new SynchronizingResult
        {
            VoltageDifferencePercent = Math.Round(voltageDiffPercent, 2),
            FrequencyDifferenceHz = Math.Round(frequencyDiffHz, 3),
            PhaseAngleDifferenceDeg = Math.Round(phaseDiffDeg, 2),
            IsAcceptable = issues.Count == 0,
            Issue = issues.Count == 0 ? null : string.Join("; ", issues),
        };
    }

    /// <summary>
    /// Recommends a simple control mode for standby generator paralleling.
    /// Utility-parallel plants typically require droop control, while isolated
    /// single-unit or large transient-load systems prefer isochronous control.
    /// </summary>
    public static LoadShareMode RecommendMode(
        bool utilityParallel,
        int onlineUnitCount,
        bool hasLargeStepLoads = false)
    {
        if (utilityParallel)
            return LoadShareMode.RealPowerDroop;

        if (onlineUnitCount <= 1 || hasLargeStepLoads)
            return LoadShareMode.Isochronous;

        return LoadShareMode.RealPowerDroop;
    }

    /// <summary>
    /// Creates a generator paralleling plan that selects the minimum number of units
    /// needed to satisfy demand plus reserve, then shares kW/kVAR in proportion to rating.
    /// </summary>
    public static ParallelingPlan CreateParallelingPlan(
        IEnumerable<GeneratorUnit> units,
        double demandKW,
        double demandKVAR = 0,
        double reservePercent = 15,
        bool requireNPlusOne = false,
        LoadShareMode mode = LoadShareMode.RealPowerDroop)
    {
        if (demandKW < 0 || demandKVAR < 0)
            throw new ArgumentException("Demand values cannot be negative.");
        if (reservePercent < 0)
            throw new ArgumentException("Reserve percent cannot be negative.");

        var availableUnits = units
            .Where(unit => unit.IsAvailable && unit.RatedKW > 0)
            .OrderByDescending(unit => unit.IsPreferred)
            .ThenByDescending(unit => unit.RatedKW)
            .ToList();

        if (availableUnits.Count == 0)
        {
            return new ParallelingPlan
            {
                TotalDemandKW = Math.Round(demandKW, 1),
                TotalDemandKVAR = Math.Round(demandKVAR, 1),
                RequiredCapacityKW = Math.Round(demandKW * (1 + reservePercent / 100.0), 1),
                Mode = mode,
                Issue = "No available generator units",
            };
        }

        double requiredCapacity = demandKW * (1 + reservePercent / 100.0);
        var onlineUnits = new List<GeneratorUnit>();
        double onlineCapacity = 0;

        foreach (var unit in availableUnits)
        {
            onlineUnits.Add(unit);
            onlineCapacity += unit.RatedKW;

            if (onlineCapacity >= requiredCapacity)
            {
                bool nPlusOneMet = SupportsNPlusOne(onlineUnits, demandKW);
                if (!requireNPlusOne || nPlusOneMet)
                    break;
            }
        }

        bool supportsNPlusOne = SupportsNPlusOne(onlineUnits, demandKW);
        bool hasDemandCapacity = onlineCapacity >= demandKW;
        bool hasReserveCapacity = onlineCapacity >= requiredCapacity;
        bool isAdequate = hasDemandCapacity && hasReserveCapacity && (!requireNPlusOne || supportsNPlusOne);
        double spinningReserve = Math.Max(0, onlineCapacity - demandKW);

        string? issue = null;
        if (!hasDemandCapacity)
            issue = "Online generator capacity is below demand";
        else if (!hasReserveCapacity)
            issue = "Online generator capacity does not meet reserve target";
        else if (requireNPlusOne && !supportsNPlusOne)
            issue = "N+1 criterion is not achievable with available units";

        return new ParallelingPlan
        {
            RequiredUnits = onlineUnits.Count,
            OnlineUnits = onlineUnits.Count,
            TotalDemandKW = Math.Round(demandKW, 1),
            TotalDemandKVAR = Math.Round(demandKVAR, 1),
            OnlineCapacityKW = Math.Round(onlineCapacity, 1),
            RequiredCapacityKW = Math.Round(requiredCapacity, 1),
            SpinningReserveKW = Math.Round(spinningReserve, 1),
            IsAdequate = isAdequate,
            SupportsNPlusOne = supportsNPlusOne,
            Mode = mode,
            UnitShares = CreateUnitShares(onlineUnits, demandKW, demandKVAR),
            Issue = issue,
        };
    }

    private static List<UnitLoadShare> CreateUnitShares(
        IReadOnlyList<GeneratorUnit> onlineUnits,
        double demandKW,
        double demandKVAR)
    {
        if (onlineUnits.Count == 0)
            return new List<UnitLoadShare>();

        double totalRatedKW = onlineUnits.Sum(unit => unit.RatedKW);
        double totalRatedKVAR = onlineUnits.Sum(unit => unit.RatedKVAR > 0 ? unit.RatedKVAR : unit.RatedKW * 0.75);

        return onlineUnits
            .Select(unit =>
            {
                double kvarBasis = unit.RatedKVAR > 0 ? unit.RatedKVAR : unit.RatedKW * 0.75;
                double assignedKW = totalRatedKW > 0 ? demandKW * unit.RatedKW / totalRatedKW : 0;
                double assignedKVAR = totalRatedKVAR > 0 ? demandKVAR * kvarBasis / totalRatedKVAR : 0;
                double utilization = unit.RatedKW > 0 ? assignedKW / unit.RatedKW * 100.0 : 0;

                return new UnitLoadShare
                {
                    Id = unit.Id,
                    AssignedKW = Math.Round(assignedKW, 1),
                    AssignedKVAR = Math.Round(assignedKVAR, 1),
                    UtilizationPercent = Math.Round(utilization, 1),
                    BelowMinimumStableLoad = utilization < unit.MinStableLoadPercent,
                };
            })
            .ToList();
    }

    private static bool SupportsNPlusOne(IReadOnlyList<GeneratorUnit> onlineUnits, double demandKW)
    {
        if (onlineUnits.Count <= 1)
            return false;

        double onlineCapacity = onlineUnits.Sum(unit => unit.RatedKW);
        double largestUnit = onlineUnits.Max(unit => unit.RatedKW);
        return onlineCapacity - largestUnit >= demandKW;
    }
}