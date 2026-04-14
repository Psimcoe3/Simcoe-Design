using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Parallel transformer compatibility, load sharing, and circulating-current estimation.
/// </summary>
public static class TransformerParallelingService
{
    public record TransformerParallelUnit
    {
        public string Id { get; init; } = "";
        public double RatedKVA { get; init; }
        public double PrimaryVoltage { get; init; }
        public double SecondaryVoltage { get; init; }
        public double PercentImpedance { get; init; }
        public double TapPercent { get; init; }
        public int PhaseShiftDegrees { get; init; }
        public bool SamePolarity { get; init; } = true;
    }

    public record TransformerLoadShare
    {
        public string Id { get; init; } = "";
        public double AssignedKVA { get; init; }
        public double UtilizationPercent { get; init; }
        public bool Overloaded { get; init; }
    }

    public record ParallelAnalysis
    {
        public double RatioMismatchPercent { get; init; }
        public double ImpedanceMismatchPercent { get; init; }
        public double EstimatedCirculatingCurrentPercent { get; init; }
        public bool VoltageCompatible { get; init; }
        public bool ImpedanceCompatible { get; init; }
        public bool PolarityCompatible { get; init; }
        public bool PhaseCompatible { get; init; }
        public bool CanParallel { get; init; }
        public double TotalAppliedLoadKVA { get; init; }
        public List<TransformerLoadShare> Shares { get; init; } = new();
        public string? Issue { get; init; }
    }

    public static double CalculateVoltageRatio(double primaryVoltage, double secondaryVoltage)
    {
        if (primaryVoltage <= 0 || secondaryVoltage <= 0)
            throw new ArgumentException("Transformer voltages must be positive.");

        return primaryVoltage / secondaryVoltage;
    }

    public static double CalculateRatioMismatchPercent(TransformerParallelUnit first, TransformerParallelUnit second)
    {
        double firstRatio = CalculateVoltageRatio(first.PrimaryVoltage, first.SecondaryVoltage * (1 + first.TapPercent / 100.0));
        double secondRatio = CalculateVoltageRatio(second.PrimaryVoltage, second.SecondaryVoltage * (1 + second.TapPercent / 100.0));
        return Math.Round(Math.Abs(firstRatio - secondRatio) / secondRatio * 100.0, 3);
    }

    public static double CalculateImpedanceMismatchPercent(TransformerParallelUnit first, TransformerParallelUnit second)
    {
        if (first.PercentImpedance <= 0 || second.PercentImpedance <= 0)
            throw new ArgumentException("Percent impedance must be positive.");

        double average = (first.PercentImpedance + second.PercentImpedance) / 2.0;
        return Math.Round(Math.Abs(first.PercentImpedance - second.PercentImpedance) / average * 100.0, 2);
    }

    public static double EstimateCirculatingCurrentPercent(double ratioMismatchPercent, double averageImpedancePercent)
    {
        if (averageImpedancePercent <= 0)
            throw new ArgumentException("Average impedance must be positive.");

        return Math.Round(ratioMismatchPercent / averageImpedancePercent * 100.0, 2);
    }

    public static List<TransformerLoadShare> CalculateLoadShares(
        IEnumerable<TransformerParallelUnit> units,
        double totalLoadKVA)
    {
        if (totalLoadKVA < 0)
            throw new ArgumentException("Total load cannot be negative.");

        var unitList = units.ToList();
        if (unitList.Count == 0)
            return new List<TransformerLoadShare>();

        double totalWeight = unitList.Sum(unit => unit.RatedKVA / unit.PercentImpedance);
        return unitList
            .Select(unit =>
            {
                double weight = unit.RatedKVA / unit.PercentImpedance;
                double assigned = totalWeight > 0 ? totalLoadKVA * weight / totalWeight : 0;
                double utilization = unit.RatedKVA > 0 ? assigned / unit.RatedKVA * 100.0 : 0;

                return new TransformerLoadShare
                {
                    Id = unit.Id,
                    AssignedKVA = Math.Round(assigned, 2),
                    UtilizationPercent = Math.Round(utilization, 2),
                    Overloaded = assigned > unit.RatedKVA,
                };
            })
            .ToList();
    }

    public static ParallelAnalysis AnalyzeParalleling(
        TransformerParallelUnit first,
        TransformerParallelUnit second,
        double totalLoadKVA = 0,
        double maxRatioMismatchPercent = 0.5,
        double maxImpedanceMismatchPercent = 10)
    {
        double ratioMismatch = CalculateRatioMismatchPercent(first, second);
        double impedanceMismatch = CalculateImpedanceMismatchPercent(first, second);
        double averageImpedance = (first.PercentImpedance + second.PercentImpedance) / 2.0;
        double circulatingCurrent = EstimateCirculatingCurrentPercent(ratioMismatch, averageImpedance);

        bool voltageCompatible = ratioMismatch <= maxRatioMismatchPercent;
        bool impedanceCompatible = impedanceMismatch <= maxImpedanceMismatchPercent;
        bool polarityCompatible = first.SamePolarity == second.SamePolarity;
        bool phaseCompatible = first.PhaseShiftDegrees == second.PhaseShiftDegrees;
        bool canParallel = voltageCompatible && impedanceCompatible && polarityCompatible && phaseCompatible;

        string? issue = null;
        if (!polarityCompatible) issue = "Transformer polarity does not match";
        else if (!phaseCompatible) issue = "Transformer phase shift does not match";
        else if (!voltageCompatible) issue = "Transformer voltage ratio mismatch exceeds limit";
        else if (!impedanceCompatible) issue = "Transformer impedance mismatch exceeds limit";

        var shares = canParallel && totalLoadKVA > 0
            ? CalculateLoadShares(new[] { first, second }, totalLoadKVA)
            : new List<TransformerLoadShare>();

        return new ParallelAnalysis
        {
            RatioMismatchPercent = ratioMismatch,
            ImpedanceMismatchPercent = impedanceMismatch,
            EstimatedCirculatingCurrentPercent = circulatingCurrent,
            VoltageCompatible = voltageCompatible,
            ImpedanceCompatible = impedanceCompatible,
            PolarityCompatible = polarityCompatible,
            PhaseCompatible = phaseCompatible,
            CanParallel = canParallel,
            TotalAppliedLoadKVA = Math.Round(totalLoadKVA, 2),
            Shares = shares,
            Issue = issue,
        };
    }
}