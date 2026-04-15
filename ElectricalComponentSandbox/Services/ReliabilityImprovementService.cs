using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Compares annual reliability programs using the shared PowerQualityService reliability indices.
/// </summary>
public static class ReliabilityImprovementService
{
    public record ReliabilityEvent
    {
        public string Name { get; init; } = string.Empty;
        public double DurationMinutes { get; init; }
        public int CustomersAffected { get; init; }
        public int AnnualOccurrenceCount { get; init; } = 1;
    }

    public record ReliabilityProgram
    {
        public string Name { get; init; } = string.Empty;
        public List<ReliabilityEvent> Events { get; init; } = new();
    }

    public record ReliabilityComparison
    {
        public string BaselineProgramName { get; init; } = string.Empty;
        public string ImprovedProgramName { get; init; } = string.Empty;
        public PowerQualityService.ReliabilityIndices BaselineIndices { get; init; } = new();
        public PowerQualityService.ReliabilityIndices ImprovedIndices { get; init; } = new();
        public double SaidiReduction { get; init; }
        public double SaifiReduction { get; init; }
        public double CaidiReduction { get; init; }
        public double AvailabilityGain { get; init; }
    }

    public static (List<double> durations, List<int> customersAffected) BuildAnnualInterruptionSeries(ReliabilityProgram program)
    {
        var durations = new List<double>();
        var customersAffected = new List<int>();

        foreach (var outage in program.Events ?? Enumerable.Empty<ReliabilityEvent>())
        {
            if (outage.AnnualOccurrenceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(program), "Annual occurrence count must be non-negative.");

            for (int i = 0; i < outage.AnnualOccurrenceCount; i++)
            {
                durations.Add(outage.DurationMinutes);
                customersAffected.Add(outage.CustomersAffected);
            }
        }

        return (durations, customersAffected);
    }

    public static PowerQualityService.ReliabilityIndices CalculateProgramIndices(ReliabilityProgram program, int totalCustomers)
    {
        var annualSeries = BuildAnnualInterruptionSeries(program);
        return PowerQualityService.CalculateReliability(annualSeries.durations, annualSeries.customersAffected, totalCustomers);
    }

    public static ReliabilityProgram ApplyHardeningMeasure(
        ReliabilityProgram baselineProgram,
        string eventName,
        StormHardeningService.HardeningMeasure measure)
    {
        int reducedOccurrenceCount(int count)
        {
            double remaining = count * (1 - (measure.FailureRateReductionPercent / 100.0));
            return Math.Max(0, (int)Math.Round(remaining, MidpointRounding.AwayFromZero));
        }

        return new ReliabilityProgram
        {
            Name = $"{baselineProgram.Name} + {measure.Name}",
            Events = baselineProgram.Events.Select(outage =>
            {
                if (!string.Equals(outage.Name, eventName, StringComparison.OrdinalIgnoreCase))
                    return outage;

                return outage with
                {
                    DurationMinutes = Math.Round(outage.DurationMinutes * (1 - (measure.DurationReductionPercent / 100.0)), 2),
                    AnnualOccurrenceCount = reducedOccurrenceCount(outage.AnnualOccurrenceCount),
                };
            }).ToList(),
        };
    }

    public static ReliabilityComparison ComparePrograms(ReliabilityProgram baselineProgram, ReliabilityProgram improvedProgram, int totalCustomers)
    {
        var baseline = CalculateProgramIndices(baselineProgram, totalCustomers);
        var improved = CalculateProgramIndices(improvedProgram, totalCustomers);

        return new ReliabilityComparison
        {
            BaselineProgramName = baselineProgram.Name,
            ImprovedProgramName = improvedProgram.Name,
            BaselineIndices = baseline,
            ImprovedIndices = improved,
            SaidiReduction = Math.Round(baseline.SAIDI - improved.SAIDI, 2),
            SaifiReduction = Math.Round(baseline.SAIFI - improved.SAIFI, 4),
            CaidiReduction = Math.Round(baseline.CAIDI - improved.CAIDI, 2),
            AvailabilityGain = Math.Round(improved.ASAI - baseline.ASAI, 6),
        };
    }

    public static List<ReliabilityProgram> RankPrograms(IEnumerable<ReliabilityProgram> programs, int totalCustomers)
    {
        return (programs ?? Array.Empty<ReliabilityProgram>())
            .Select(program => new { Program = program, Indices = CalculateProgramIndices(program, totalCustomers) })
            .OrderBy(result => result.Indices.SAIDI)
            .ThenBy(result => result.Indices.SAIFI)
            .ThenByDescending(result => result.Indices.ASAI)
            .Select(result => result.Program)
            .ToList();
    }
}