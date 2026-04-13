using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// DC battery charger sizing for float service and post-outage recharge duty.
/// </summary>
public static class BatteryChargerService
{
    public enum ChargerTopology
    {
        Single,
        RedundantNPlus1,
        DualRedundant,
    }

    public record ChargerSizingResult
    {
        public double DcLoadAmps { get; init; }
        public double FloatCurrentAmps { get; init; }
        public double RechargeCurrentAmps { get; init; }
        public double RequiredCurrentAmps { get; init; }
        public int SelectedChargerAmps { get; init; }
        public double FloatVoltage { get; init; }
        public double EqualizeVoltage { get; init; }
        public bool SupportsEqualize { get; init; }
        public double OutputKW { get; init; }
        public ChargerTopology RecommendedTopology { get; init; }
    }

    public static double CalculateFloatCurrent(
        double batteryAH,
        BatterySizingService.BatteryChemistry chemistry)
    {
        if (batteryAH <= 0)
            throw new ArgumentException("Battery capacity must be positive.");

        double cRate = chemistry switch
        {
            BatterySizingService.BatteryChemistry.LeadAcidVRLA => 0.005,
            BatterySizingService.BatteryChemistry.LeadAcidFlooded => 0.01,
            BatterySizingService.BatteryChemistry.LithiumIon => 0.002,
            BatterySizingService.BatteryChemistry.NickelCadmium => 0.02,
            _ => 0.005,
        };

        return Math.Round(batteryAH * cRate, 2);
    }

    public static double CalculateRechargeCurrent(
        double batteryAH,
        double rechargeHours,
        double depthOfDischarge = 0.8,
        double rechargeFactor = 1.1)
    {
        if (batteryAH <= 0 || rechargeHours <= 0)
            throw new ArgumentException("Battery capacity and recharge time must be positive.");
        if (depthOfDischarge <= 0 || depthOfDischarge > 1)
            throw new ArgumentException("Depth of discharge must be greater than 0 and no more than 1.");
        if (rechargeFactor < 1)
            throw new ArgumentException("Recharge factor must be at least 1.");

        double dischargedAH = batteryAH * depthOfDischarge;
        return Math.Round(dischargedAH * rechargeFactor / rechargeHours, 2);
    }

    /// <summary>
    /// Sizes a DC charger to support continuous load, float current, and battery recharge.
    /// </summary>
    public static ChargerSizingResult SizeCharger(
        double dcLoadAmps,
        double batteryAH,
        BatterySizingService.BatteryChemistry chemistry,
        double rechargeHours = 8,
        double nominalDcVoltage = 125)
    {
        if (dcLoadAmps < 0 || batteryAH <= 0 || rechargeHours <= 0 || nominalDcVoltage <= 0)
            throw new ArgumentException("Inputs must be positive and load cannot be negative.");

        double floatCurrent = CalculateFloatCurrent(batteryAH, chemistry);
        double rechargeCurrent = CalculateRechargeCurrent(batteryAH, rechargeHours);
        double requiredCurrent = dcLoadAmps + floatCurrent + rechargeCurrent;
        int selectedCurrent = RoundToStandardChargerAmps(requiredCurrent);

        double floatVoltage = nominalDcVoltage;
        bool supportsEqualize = chemistry != BatterySizingService.BatteryChemistry.LithiumIon;
        double equalizeVoltage = supportsEqualize
            ? Math.Round(nominalDcVoltage * GetEqualizeFactor(chemistry), 1)
            : nominalDcVoltage;

        ChargerTopology topology = requiredCurrent > 100
            ? ChargerTopology.RedundantNPlus1
            : ChargerTopology.Single;

        return new ChargerSizingResult
        {
            DcLoadAmps = Math.Round(dcLoadAmps, 2),
            FloatCurrentAmps = floatCurrent,
            RechargeCurrentAmps = rechargeCurrent,
            RequiredCurrentAmps = Math.Round(requiredCurrent, 2),
            SelectedChargerAmps = selectedCurrent,
            FloatVoltage = floatVoltage,
            EqualizeVoltage = equalizeVoltage,
            SupportsEqualize = supportsEqualize,
            OutputKW = Math.Round(selectedCurrent * equalizeVoltage / 1000.0, 2),
            RecommendedTopology = topology,
        };
    }

    /// <summary>
    /// Estimates recharge time from the charger current remaining after serving the DC load.
    /// Returns positive infinity if no net charging current is available.
    /// </summary>
    public static double EstimateRechargeTimeHours(
        double chargerAmps,
        double dcLoadAmps,
        double batteryAH,
        double depthOfDischarge = 0.8,
        double rechargeFactor = 1.1)
    {
        if (chargerAmps <= 0 || dcLoadAmps < 0 || batteryAH <= 0)
            throw new ArgumentException("Charger, load, and battery values must be valid.");
        if (depthOfDischarge <= 0 || depthOfDischarge > 1)
            throw new ArgumentException("Depth of discharge must be greater than 0 and no more than 1.");

        double netChargeCurrent = chargerAmps - dcLoadAmps;
        if (netChargeCurrent <= 0)
            return double.PositiveInfinity;

        double requiredAH = batteryAH * depthOfDischarge * rechargeFactor;
        return Math.Round(requiredAH / netChargeCurrent, 2);
    }

    private static int RoundToStandardChargerAmps(double requiredCurrent)
    {
        int[] standardSizes = { 10, 20, 30, 40, 50, 60, 75, 100, 150, 200, 300, 400 };
        foreach (int size in standardSizes)
        {
            if (size >= requiredCurrent)
                return size;
        }

        return standardSizes[^1];
    }

    private static double GetEqualizeFactor(BatterySizingService.BatteryChemistry chemistry) => chemistry switch
    {
        BatterySizingService.BatteryChemistry.LeadAcidVRLA => 1.03,
        BatterySizingService.BatteryChemistry.LeadAcidFlooded => 1.10,
        BatterySizingService.BatteryChemistry.NickelCadmium => 1.15,
        _ => 1.0,
    };
}