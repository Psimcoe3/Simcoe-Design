using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Simple microgrid dispatch planning for unit commitment, reserve margin, and economic loading.
/// </summary>
public static class MicrogridDispatchService
{
    public enum DispatchPriority
    {
        Renewable = 1,
        MustRun = 2,
        Economic = 3,
        Peaking = 4,
        Storage = 5,
    }

    public record DispatchUnit
    {
        public string Id { get; init; } = "";
        public double RatedKW { get; init; }
        public double MinimumKW { get; init; }
        public double CostPerKWh { get; init; }
        public DispatchPriority Priority { get; init; } = DispatchPriority.Economic;
        public bool IsAvailable { get; init; } = true;
    }

    public record DispatchAllocation
    {
        public string Id { get; init; } = "";
        public double AssignedKW { get; init; }
        public double UtilizationPercent { get; init; }
        public bool AtMinimum { get; init; }
        public bool AtMaximum { get; init; }
        public double IncrementalCostPerHour { get; init; }
    }

    public record DispatchPlan
    {
        public double DemandKW { get; init; }
        public double RequiredCapacityKW { get; init; }
        public double OnlineCapacityKW { get; init; }
        public double SpinningReserveKW { get; init; }
        public double TotalOperatingCostPerHour { get; init; }
        public bool IsAdequate { get; init; }
        public string? Issue { get; init; }
        public List<DispatchAllocation> Allocations { get; init; } = new();
    }

    public static double CalculateRequiredCapacity(double demandKW, double reservePercent = 15)
    {
        if (demandKW < 0)
            throw new ArgumentException("Demand cannot be negative.");
        if (reservePercent < 0)
            throw new ArgumentException("Reserve percent cannot be negative.");

        return Math.Round(demandKW * (1 + reservePercent / 100.0), 2);
    }

    public static DispatchPlan CreateDispatchPlan(
        IEnumerable<DispatchUnit> units,
        double demandKW,
        double reservePercent = 15)
    {
        if (demandKW < 0)
            throw new ArgumentException("Demand cannot be negative.");

        var availableUnits = units
            .Where(unit => unit.IsAvailable && unit.RatedKW > 0)
            .OrderBy(unit => unit.Priority)
            .ThenBy(unit => unit.CostPerKWh)
            .ThenByDescending(unit => unit.RatedKW)
            .ToList();

        double requiredCapacity = CalculateRequiredCapacity(demandKW, reservePercent);
        var committedUnits = new List<DispatchUnit>();
        double committedCapacity = 0;

        foreach (var unit in availableUnits)
        {
            committedUnits.Add(unit);
            committedCapacity += unit.RatedKW;
            if (committedCapacity >= requiredCapacity)
                break;
        }

        if (committedCapacity < demandKW)
        {
            return new DispatchPlan
            {
                DemandKW = Math.Round(demandKW, 2),
                RequiredCapacityKW = requiredCapacity,
                OnlineCapacityKW = Math.Round(committedCapacity, 2),
                Issue = "Available microgrid capacity is below demand",
            };
        }

        var allocations = committedUnits
            .Select(unit => new DispatchAllocation
            {
                Id = unit.Id,
                AssignedKW = unit.MinimumKW,
                UtilizationPercent = unit.RatedKW > 0 ? Math.Round(unit.MinimumKW / unit.RatedKW * 100.0, 2) : 0,
                AtMinimum = unit.MinimumKW > 0,
                IncrementalCostPerHour = Math.Round(unit.MinimumKW * unit.CostPerKWh, 2),
            })
            .ToList();

        double assignedKW = allocations.Sum(allocation => allocation.AssignedKW);
        if (assignedKW > demandKW)
        {
            return new DispatchPlan
            {
                DemandKW = Math.Round(demandKW, 2),
                RequiredCapacityKW = requiredCapacity,
                OnlineCapacityKW = Math.Round(committedCapacity, 2),
                SpinningReserveKW = Math.Round(Math.Max(0, committedCapacity - assignedKW), 2),
                TotalOperatingCostPerHour = Math.Round(allocations.Sum(allocation => allocation.IncrementalCostPerHour), 2),
                IsAdequate = false,
                Issue = "Committed minimum generation exceeds demand",
                Allocations = allocations,
            };
        }

        double remainingKW = demandKW - assignedKW;
        foreach (var unit in committedUnits)
        {
            if (remainingKW <= 0)
                break;

            var allocation = allocations.First(item => item.Id == unit.Id);
            double availableIncrement = unit.RatedKW - allocation.AssignedKW;
            double increment = Math.Min(remainingKW, availableIncrement);

            allocations[allocations.IndexOf(allocation)] = allocation with
            {
                AssignedKW = Math.Round(allocation.AssignedKW + increment, 2),
                UtilizationPercent = Math.Round((allocation.AssignedKW + increment) / unit.RatedKW * 100.0, 2),
                AtMaximum = Math.Abs(allocation.AssignedKW + increment - unit.RatedKW) < 0.001,
                IncrementalCostPerHour = Math.Round((allocation.AssignedKW + increment) * unit.CostPerKWh, 2),
            };

            remainingKW -= increment;
        }

        double finalAssigned = allocations.Sum(allocation => allocation.AssignedKW);
        return new DispatchPlan
        {
            DemandKW = Math.Round(demandKW, 2),
            RequiredCapacityKW = requiredCapacity,
            OnlineCapacityKW = Math.Round(committedCapacity, 2),
            SpinningReserveKW = Math.Round(Math.Max(0, committedCapacity - finalAssigned), 2),
            TotalOperatingCostPerHour = Math.Round(allocations.Sum(allocation => allocation.IncrementalCostPerHour), 2),
            IsAdequate = Math.Abs(finalAssigned - demandKW) < 0.01,
            Issue = Math.Abs(finalAssigned - demandKW) < 0.01 ? null : "Committed units could not fully follow demand",
            Allocations = allocations,
        };
    }
}