using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Simplified black-start restoration planning for seed-source adequacy,
/// auxiliary load support, and staged unit pickup.
/// </summary>
public static class BlackStartService
{
    public enum GenerationType
    {
        Diesel,
        GasTurbine,
        Hydro,
        BatteryStorage,
    }

    public record BlackStartUnit
    {
        public string Id { get; init; } = "";
        public GenerationType Type { get; init; }
        public double RatedKW { get; init; }
        public double AuxiliaryLoadPercent { get; init; }
        public bool IsBlackStartCapable { get; init; }
        public int Priority { get; init; }
    }

    public record StartStep
    {
        public string UnitId { get; init; } = "";
        public int OrderIndex { get; init; }
        public double ExternalStartPowerKW { get; init; }
        public double NetContributionKW { get; init; }
        public double AvailablePowerAfterStartKW { get; init; }
        public double StartTimeMinutes { get; init; }
    }

    public record BlackStartPlan
    {
        public double SeedSourceKW { get; init; }
        public int RestoredUnits { get; init; }
        public double TotalOnlineGenerationKW { get; init; }
        public double AvailableRestorationPowerKW { get; init; }
        public bool IsFeasible { get; init; }
        public List<StartStep> Steps { get; init; } = new();
        public string? Issue { get; init; }
    }

    public static double GetTypicalAuxiliaryLoadPercent(GenerationType type) => type switch
    {
        GenerationType.Diesel => 6,
        GenerationType.GasTurbine => 10,
        GenerationType.Hydro => 3,
        GenerationType.BatteryStorage => 1,
        _ => 6,
    };

    public static double GetTypicalStartTimeMinutes(GenerationType type) => type switch
    {
        GenerationType.Diesel => 1,
        GenerationType.GasTurbine => 10,
        GenerationType.Hydro => 5,
        GenerationType.BatteryStorage => 0.25,
        _ => 1,
    };

    /// <summary>
    /// Calculates the temporary external power required to start a unit.
    /// Gas turbines are given a higher multiplier for motoring and auxiliaries.
    /// </summary>
    public static double CalculateStartPowerKW(
        double ratedKW,
        GenerationType type,
        double auxiliaryLoadPercent = 0)
    {
        if (ratedKW <= 0)
            throw new ArgumentException("Unit rating must be positive.");

        double auxPercent = auxiliaryLoadPercent > 0 ? auxiliaryLoadPercent : GetTypicalAuxiliaryLoadPercent(type);
        double startMultiplier = type == GenerationType.GasTurbine ? 1.5 : 1.0;
        return Math.Round(ratedKW * auxPercent / 100.0 * startMultiplier, 1);
    }

    public static BlackStartPlan CreateRestorationPlan(
        IEnumerable<BlackStartUnit> units,
        double seedSourceKW)
    {
        if (seedSourceKW < 0)
            throw new ArgumentException("Seed source cannot be negative.");

        var pending = units
            .Where(unit => unit.RatedKW > 0)
            .ToList();

        double availablePower = seedSourceKW;
        var steps = new List<StartStep>();
        int order = 1;

        while (pending.Count > 0)
        {
            var candidate = pending
                .Where(unit => unit.IsBlackStartCapable || availablePower >= GetExternalStartPower(unit))
                .OrderBy(unit => unit.Priority)
                .ThenBy(unit => GetExternalStartPower(unit))
                .ThenByDescending(unit => unit.RatedKW)
                .FirstOrDefault();

            if (candidate is null)
                break;

            double externalStartPower = candidate.IsBlackStartCapable ? 0 : GetExternalStartPower(candidate);
            double auxPercent = candidate.AuxiliaryLoadPercent > 0
                ? candidate.AuxiliaryLoadPercent
                : GetTypicalAuxiliaryLoadPercent(candidate.Type);
            double netContribution = candidate.RatedKW * (1 - auxPercent / 100.0);

            availablePower += netContribution;
            steps.Add(new StartStep
            {
                UnitId = candidate.Id,
                OrderIndex = order++,
                ExternalStartPowerKW = Math.Round(externalStartPower, 1),
                NetContributionKW = Math.Round(netContribution, 1),
                AvailablePowerAfterStartKW = Math.Round(availablePower, 1),
                StartTimeMinutes = GetTypicalStartTimeMinutes(candidate.Type),
            });

            pending.Remove(candidate);
        }

        double totalOnlineGeneration = steps.Sum(step =>
        {
            var unit = units.First(source => source.Id == step.UnitId);
            return unit.RatedKW;
        });

        return new BlackStartPlan
        {
            SeedSourceKW = Math.Round(seedSourceKW, 1),
            RestoredUnits = steps.Count,
            TotalOnlineGenerationKW = Math.Round(totalOnlineGeneration, 1),
            AvailableRestorationPowerKW = Math.Round(availablePower, 1),
            IsFeasible = pending.Count == 0,
            Steps = steps,
            Issue = pending.Count == 0 ? null : "One or more units could not be started with the available seed source",
        };
    }

    private static double GetExternalStartPower(BlackStartUnit unit)
    {
        return CalculateStartPowerKW(unit.RatedKW, unit.Type, unit.AuxiliaryLoadPercent);
    }
}