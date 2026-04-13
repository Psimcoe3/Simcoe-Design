using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Generator fuel storage sizing per NFPA 110 runtime classes.
/// Covers burn-rate estimation, bulk/day-tank sizing, and refill planning.
/// </summary>
public static class FuelStorageService
{
    public enum FuelType
    {
        Diesel,
        Propane,
    }

    public enum EpssClass
    {
        Class2,
        Class6,
        Class24,
        Class48,
        Class72,
        Class96,
    }

    public record FuelConsumptionResult
    {
        public FuelType FuelType { get; init; }
        public double GeneratorKW { get; init; }
        public double LoadFactor { get; init; }
        public double OutputKW { get; init; }
        public double GallonsPerHour { get; init; }
    }

    public record TankSizingResult
    {
        public FuelType FuelType { get; init; }
        public double RuntimeHours { get; init; }
        public double BurnRateGallonsPerHour { get; init; }
        public double RequiredUsableGallons { get; init; }
        public double TotalTankGallons { get; init; }
        public double DayTankGallons { get; init; }
        public bool SubBaseTankIsPractical { get; init; }
    }

    public record RefillPlan
    {
        public double UsableGallonsPerFill { get; init; }
        public double TotalFuelRequiredGallons { get; init; }
        public int RequiredDeliveries { get; init; }
        public double DeliveryIntervalHours { get; init; }
        public double RemainingGallonsAfterEvent { get; init; }
        public bool NeedsMidEventRefill { get; init; }
    }

    public static double GetMinimumRuntimeHours(EpssClass epssClass) => epssClass switch
    {
        EpssClass.Class2 => 2,
        EpssClass.Class6 => 6,
        EpssClass.Class24 => 24,
        EpssClass.Class48 => 48,
        EpssClass.Class72 => 72,
        EpssClass.Class96 => 96,
        _ => 2,
    };

    /// <summary>
    /// Estimates liquid-fuel burn rate from generator output.
    /// Diesel is approximated at 0.071 gal/kWh and propane at 0.11 gal/kWh.
    /// </summary>
    public static FuelConsumptionResult EstimateConsumption(
        double generatorKW,
        double loadFactor,
        FuelType fuelType = FuelType.Diesel)
    {
        if (generatorKW <= 0)
            throw new ArgumentException("Generator rating must be positive.");
        if (loadFactor <= 0 || loadFactor > 1.0)
            throw new ArgumentException("Load factor must be greater than 0 and no more than 1.0.");

        double gallonsPerKWh = fuelType switch
        {
            FuelType.Diesel => 0.071,
            FuelType.Propane => 0.11,
            _ => 0.071,
        };

        double outputKW = generatorKW * loadFactor;
        double burnRate = outputKW * gallonsPerKWh;

        return new FuelConsumptionResult
        {
            FuelType = fuelType,
            GeneratorKW = generatorKW,
            LoadFactor = loadFactor,
            OutputKW = Math.Round(outputKW, 1),
            GallonsPerHour = Math.Round(burnRate, 2),
        };
    }

    /// <summary>
    /// Sizes a main tank and representative day tank for the selected NFPA 110 class.
    /// Unusable fuel and design margin are added on top of the usable runtime requirement.
    /// </summary>
    public static TankSizingResult SizeTank(
        double generatorKW,
        double loadFactor,
        EpssClass epssClass,
        FuelType fuelType = FuelType.Diesel,
        double unusableFraction = 0.1,
        double safetyMargin = 0.15)
    {
        if (unusableFraction < 0 || unusableFraction >= 1)
            throw new ArgumentException("Unusable fraction must be between 0 and 1.");
        if (safetyMargin < 0)
            throw new ArgumentException("Safety margin cannot be negative.");

        var consumption = EstimateConsumption(generatorKW, loadFactor, fuelType);
        double runtimeHours = GetMinimumRuntimeHours(epssClass);
        double requiredUsable = consumption.GallonsPerHour * runtimeHours;
        double totalTank = requiredUsable * (1 + safetyMargin) / (1 - unusableFraction);
        double dayTank = Math.Min(totalTank, consumption.GallonsPerHour * 4 * 1.1);

        return new TankSizingResult
        {
            FuelType = fuelType,
            RuntimeHours = runtimeHours,
            BurnRateGallonsPerHour = consumption.GallonsPerHour,
            RequiredUsableGallons = Math.Round(requiredUsable, 1),
            TotalTankGallons = Math.Round(totalTank, 1),
            DayTankGallons = Math.Round(dayTank, 1),
            SubBaseTankIsPractical = totalTank <= 1000,
        };
    }

    /// <summary>
    /// Plans refills for an outage or endurance event using the usable portion of each fill.
    /// </summary>
    public static RefillPlan PlanRefills(
        double tankGallons,
        double burnRateGallonsPerHour,
        double eventDurationHours,
        double unusableFraction = 0.1)
    {
        if (tankGallons <= 0 || burnRateGallonsPerHour <= 0 || eventDurationHours <= 0)
            throw new ArgumentException("Tank, burn rate, and duration must be positive.");
        if (unusableFraction < 0 || unusableFraction >= 1)
            throw new ArgumentException("Unusable fraction must be between 0 and 1.");

        double usableGallons = tankGallons * (1 - unusableFraction);
        double totalFuelRequired = burnRateGallonsPerHour * eventDurationHours;
        int fillCycles = (int)Math.Ceiling(totalFuelRequired / usableGallons);
        int deliveries = Math.Max(0, fillCycles - 1);
        double remainingGallons = Math.Max(0, fillCycles * usableGallons - totalFuelRequired);

        return new RefillPlan
        {
            UsableGallonsPerFill = Math.Round(usableGallons, 1),
            TotalFuelRequiredGallons = Math.Round(totalFuelRequired, 1),
            RequiredDeliveries = deliveries,
            DeliveryIntervalHours = deliveries > 0 ? Math.Round(usableGallons / burnRateGallonsPerHour, 2) : 0,
            RemainingGallonsAfterEvent = Math.Round(remainingGallons, 1),
            NeedsMidEventRefill = deliveries > 0,
        };
    }
}