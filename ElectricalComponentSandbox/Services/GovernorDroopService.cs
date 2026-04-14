using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Governor droop calculations for single-unit frequency response and multi-unit load sharing.
/// </summary>
public static class GovernorDroopService
{
    public record GovernorUnit
    {
        public string Id { get; init; } = "";
        public double RatedKW { get; init; }
        public double DroopPercent { get; init; } = 5;
        public double SpeedBiasPercent { get; init; }
        public bool IsAvailable { get; init; } = true;
    }

    public record GovernorShare
    {
        public string Id { get; init; } = "";
        public double AssignedKW { get; init; }
        public double UtilizationPercent { get; init; }
        public double ReferenceFrequencyHz { get; init; }
    }

    public record GovernorPlan
    {
        public double SystemFrequencyHz { get; init; }
        public double TotalDemandKW { get; init; }
        public bool IsAdequate { get; init; }
        public string? Issue { get; init; }
        public List<GovernorShare> UnitShares { get; init; } = new();
    }

    public static double CalculateOperatingFrequency(
        double nominalFrequencyHz,
        double loadPercent,
        double droopPercent = 5,
        double speedBiasPercent = 0)
    {
        if (nominalFrequencyHz <= 0)
            throw new ArgumentException("Nominal frequency must be positive.");
        if (loadPercent < 0 || loadPercent > 100)
            throw new ArgumentException("Load percent must be between 0 and 100.");
        if (droopPercent <= 0)
            throw new ArgumentException("Droop percent must be positive.");

        double referenceFrequency = nominalFrequencyHz * (1 + speedBiasPercent / 100.0);
        double frequencyDrop = nominalFrequencyHz * droopPercent / 100.0 * (loadPercent / 100.0);
        return Math.Round(referenceFrequency - frequencyDrop, 3);
    }

    public static double CalculateLoadPercent(
        double nominalFrequencyHz,
        double operatingFrequencyHz,
        double droopPercent = 5,
        double speedBiasPercent = 0)
    {
        if (nominalFrequencyHz <= 0 || operatingFrequencyHz <= 0)
            throw new ArgumentException("Frequencies must be positive.");
        if (droopPercent <= 0)
            throw new ArgumentException("Droop percent must be positive.");

        double referenceFrequency = nominalFrequencyHz * (1 + speedBiasPercent / 100.0);
        double fullLoadDrop = nominalFrequencyHz * droopPercent / 100.0;
        double loadPercent = (referenceFrequency - operatingFrequencyHz) / fullLoadDrop * 100.0;
        return Math.Round(Math.Clamp(loadPercent, 0, 100), 2);
    }

    public static GovernorPlan ShareLoad(
        IEnumerable<GovernorUnit> units,
        double totalDemandKW,
        double nominalFrequencyHz = 60)
    {
        if (totalDemandKW < 0)
            throw new ArgumentException("Total demand cannot be negative.");
        if (nominalFrequencyHz <= 0)
            throw new ArgumentException("Nominal frequency must be positive.");

        var onlineUnits = units
            .Where(unit => unit.IsAvailable && unit.RatedKW > 0 && unit.DroopPercent > 0)
            .ToList();

        if (onlineUnits.Count == 0)
        {
            return new GovernorPlan
            {
                SystemFrequencyHz = nominalFrequencyHz,
                TotalDemandKW = totalDemandKW,
                Issue = "No available governor-controlled units",
            };
        }

        double totalCapacity = onlineUnits.Sum(unit => unit.RatedKW);
        if (totalDemandKW > totalCapacity)
        {
            return new GovernorPlan
            {
                SystemFrequencyHz = nominalFrequencyHz,
                TotalDemandKW = totalDemandKW,
                IsAdequate = false,
                Issue = "Governor-controlled capacity is below demand",
            };
        }

        var stiffnessTerms = onlineUnits
            .Select(unit => new
            {
                Unit = unit,
                ReferenceFrequencyHz = nominalFrequencyHz * (1 + unit.SpeedBiasPercent / 100.0),
                Stiffness = unit.RatedKW / (nominalFrequencyHz * unit.DroopPercent / 100.0),
            })
            .ToList();

        double sumStiffness = stiffnessTerms.Sum(term => term.Stiffness);
        double weightedReference = stiffnessTerms.Sum(term => term.Stiffness * term.ReferenceFrequencyHz);
        double systemFrequency = (weightedReference - totalDemandKW) / sumStiffness;

        var shares = stiffnessTerms
            .Select(term =>
            {
                double assignedKW = term.Stiffness * (term.ReferenceFrequencyHz - systemFrequency);
                assignedKW = Math.Max(0, assignedKW);

                return new GovernorShare
                {
                    Id = term.Unit.Id,
                    AssignedKW = Math.Round(assignedKW, 2),
                    UtilizationPercent = Math.Round(assignedKW / term.Unit.RatedKW * 100.0, 2),
                    ReferenceFrequencyHz = Math.Round(term.ReferenceFrequencyHz, 3),
                };
            })
            .ToList();

        return new GovernorPlan
        {
            SystemFrequencyHz = Math.Round(systemFrequency, 3),
            TotalDemandKW = Math.Round(totalDemandKW, 2),
            IsAdequate = true,
            UnitShares = shares,
        };
    }
}