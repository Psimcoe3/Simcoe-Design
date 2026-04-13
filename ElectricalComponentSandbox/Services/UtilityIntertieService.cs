using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Point-of-common-coupling utility intertie calculations for import/export state,
/// zero-export compliance, and reverse-power relay pickup settings.
/// </summary>
public static class UtilityIntertieService
{
    public enum IntertieMode
    {
        Import,
        Export,
        ZeroExport,
        Islanded,
    }

    public record IntertieAssessment
    {
        public double LoadKW { get; init; }
        public double OnsiteGenerationKW { get; init; }
        public double NetExchangeKW { get; init; }
        public double ExportKW { get; init; }
        public double ImportKW { get; init; }
        public double ExportLimitKW { get; init; }
        public double ExportViolationKW { get; init; }
        public double PowerFactorAtPcc { get; init; }
        public bool IsWithinExportLimit { get; init; }
        public IntertieMode Mode { get; init; }
    }

    public record ReversePowerRelaySetting
    {
        public double GeneratorKW { get; init; }
        public double PickupPercent { get; init; }
        public double PickupKW { get; init; }
        public double TimeDelaySeconds { get; init; }
    }

    /// <summary>
    /// Positive values indicate import from the utility. Negative values indicate export.
    /// </summary>
    public static double CalculateNetExchange(double loadKW, double onsiteGenerationKW)
    {
        if (loadKW < 0 || onsiteGenerationKW < 0)
            throw new ArgumentException("Load and generation cannot be negative.");

        return Math.Round(loadKW - onsiteGenerationKW, 1);
    }

    public static IntertieMode DetermineMode(
        double loadKW,
        double onsiteGenerationKW,
        bool islanded = false,
        double zeroExportBandKW = 5)
    {
        if (islanded)
            return IntertieMode.Islanded;

        double netExchange = CalculateNetExchange(loadKW, onsiteGenerationKW);
        if (Math.Abs(netExchange) <= zeroExportBandKW)
            return IntertieMode.ZeroExport;

        return netExchange > 0 ? IntertieMode.Import : IntertieMode.Export;
    }

    public static IntertieAssessment AssessIntertie(
        double loadKW,
        double onsiteGenerationKW,
        double exportLimitKW = 0,
        double reactivePowerKvar = 0,
        bool islanded = false,
        double zeroExportBandKW = 5)
    {
        double netExchange = CalculateNetExchange(loadKW, onsiteGenerationKW);
        double exportKW = Math.Max(0, -netExchange);
        double importKW = Math.Max(0, netExchange);
        double exportViolation = Math.Max(0, exportKW - exportLimitKW);
        double apparentPower = Math.Sqrt(netExchange * netExchange + reactivePowerKvar * reactivePowerKvar);
        double powerFactor = apparentPower > 0 ? Math.Abs(netExchange) / apparentPower : 1.0;

        return new IntertieAssessment
        {
            LoadKW = Math.Round(loadKW, 1),
            OnsiteGenerationKW = Math.Round(onsiteGenerationKW, 1),
            NetExchangeKW = netExchange,
            ExportKW = Math.Round(exportKW, 1),
            ImportKW = Math.Round(importKW, 1),
            ExportLimitKW = Math.Round(exportLimitKW, 1),
            ExportViolationKW = Math.Round(exportViolation, 1),
            PowerFactorAtPcc = Math.Round(powerFactor, 3),
            IsWithinExportLimit = islanded || exportViolation <= 0,
            Mode = DetermineMode(loadKW, onsiteGenerationKW, islanded, zeroExportBandKW),
        };
    }

    public static ReversePowerRelaySetting SizeReversePowerRelay(
        double generatorKW,
        double pickupPercent = 5,
        double timeDelaySeconds = 0.5)
    {
        if (generatorKW <= 0)
            throw new ArgumentException("Generator rating must be positive.");
        if (pickupPercent <= 0 || pickupPercent > 100)
            throw new ArgumentException("Pickup percent must be greater than 0 and no more than 100.");
        if (timeDelaySeconds <= 0)
            throw new ArgumentException("Time delay must be positive.");

        return new ReversePowerRelaySetting
        {
            GeneratorKW = Math.Round(generatorKW, 1),
            PickupPercent = Math.Round(pickupPercent, 2),
            PickupKW = Math.Round(generatorKW * pickupPercent / 100.0, 1),
            TimeDelaySeconds = Math.Round(timeDelaySeconds, 2),
        };
    }
}