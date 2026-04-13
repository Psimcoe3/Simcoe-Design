using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Solar PV system sizing per NEC 690. Covers module string sizing,
/// inverter selection, conductor sizing factors, and DC/AC ratio.
/// </summary>
public static class SolarPvSizingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum InverterType
    {
        StringInverter,
        Microinverter,
        CentralInverter,
    }

    public enum MountingType
    {
        RooftopFlush,
        RooftopTilted,
        GroundMount,
        Carport,
    }

    /// <summary>PV module electrical parameters (STC).</summary>
    public record ModuleSpec
    {
        public double WattsSTC { get; init; }       // Rated power at STC
        public double VocVolts { get; init; }        // Open-circuit voltage
        public double VmpVolts { get; init; }        // Max power voltage
        public double IscAmps { get; init; }         // Short-circuit current
        public double ImpAmps { get; init; }         // Max power current
        public double TempCoeffVocPerC { get; init; } = -0.003; // %/°C typical
        public double TempCoeffIscPerC { get; init; } = 0.0005; // %/°C typical
    }

    /// <summary>Inverter parameters.</summary>
    public record InverterSpec
    {
        public double MaxDcVoltage { get; init; }
        public double MpptMinVoltage { get; init; }
        public double MpptMaxVoltage { get; init; }
        public double MaxDcCurrentAmps { get; init; }
        public double RatedAcWatts { get; init; }
        public double MaxDcWatts { get; init; }
        public InverterType Type { get; init; }
    }

    /// <summary>String sizing result.</summary>
    public record StringSizingResult
    {
        public int MinModulesPerString { get; init; }
        public int MaxModulesPerString { get; init; }
        public int RecommendedModulesPerString { get; init; }
        public double CorrectedVocMax { get; init; }
        public double CorrectedVmpMin { get; init; }
        public bool IsValid { get; init; }
        public string Reason { get; init; } = "";
    }

    /// <summary>Complete PV system sizing result.</summary>
    public record SystemSizingResult
    {
        public double SystemDcKw { get; init; }
        public double SystemAcKw { get; init; }
        public double DcAcRatio { get; init; }
        public int TotalModules { get; init; }
        public int StringsCount { get; init; }
        public int ModulesPerString { get; init; }
        public double AnnualProductionKwh { get; init; }
        public double MaxIscCorrected { get; init; }
        public double ConductorMinAmps { get; init; }
    }

    // ── Temperature Correction (NEC 690.7) ───────────────────────────────────

    /// <summary>
    /// Corrects Voc for lowest expected temperature per NEC 690.7(A).
    /// Higher Voc at lower temperatures (negative temp coefficient).
    /// </summary>
    /// <param name="vocStc">Open-circuit voltage at STC (25°C).</param>
    /// <param name="tempCoeffPerC">Temperature coefficient of Voc (%/°C), typically negative.</param>
    /// <param name="minAmbientC">Minimum expected ambient temperature (°C).</param>
    public static double CorrectVocForTemp(double vocStc, double tempCoeffPerC, double minAmbientC)
    {
        double deltaT = minAmbientC - 25.0;  // STC = 25°C
        double correctionFactor = 1.0 + (tempCoeffPerC * deltaT);
        return vocStc * correctionFactor;
    }

    /// <summary>
    /// Corrects Vmp for highest expected temperature (Vmp decreases with heat).
    /// </summary>
    public static double CorrectVmpForTemp(double vmpStc, double tempCoeffPerC, double maxCellTempC)
    {
        double deltaT = maxCellTempC - 25.0;
        double correctionFactor = 1.0 + (tempCoeffPerC * deltaT);
        return vmpStc * correctionFactor;
    }

    /// <summary>
    /// Corrects Isc for highest expected temperature per NEC 690.8(A)(1).
    /// Isc increases with temperature (positive temp coefficient).
    /// Then multiply by 1.25 per NEC 690.8(A)(1) for continuous current.
    /// </summary>
    public static double CorrectIscForTemp(double iscStc, double tempCoeffPerC, double maxCellTempC)
    {
        double deltaT = maxCellTempC - 25.0;
        double correctionFactor = 1.0 + (tempCoeffPerC * deltaT);
        return iscStc * correctionFactor * 1.25; // NEC 690.8(A)(1): × 1.25
    }

    // ── String Sizing ────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the allowable number of modules per string based on
    /// inverter voltage window and temperature-corrected module voltages.
    /// </summary>
    /// <param name="module">PV module parameters.</param>
    /// <param name="inverter">Inverter parameters.</param>
    /// <param name="minAmbientC">Minimum ambient temp for Voc correction (default -10°C).</param>
    /// <param name="maxCellTempC">Maximum cell temp for Vmp correction (default 65°C).</param>
    public static StringSizingResult SizeString(
        ModuleSpec module, InverterSpec inverter,
        double minAmbientC = -10, double maxCellTempC = 65)
    {
        // Corrected Voc at coldest temp (highest voltage)
        double vocCorrected = CorrectVocForTemp(module.VocVolts, module.TempCoeffVocPerC, minAmbientC);
        // Corrected Vmp at hottest temp (lowest voltage)
        double vmpCorrected = CorrectVmpForTemp(module.VmpVolts, module.TempCoeffVocPerC, maxCellTempC);

        if (vocCorrected <= 0 || vmpCorrected <= 0)
            return new StringSizingResult { IsValid = false, Reason = "Invalid module voltages" };

        // Max modules: Voc_corrected × N ≤ inverter max DC voltage
        int maxModules = (int)Math.Floor(inverter.MaxDcVoltage / vocCorrected);

        // Min modules: Vmp_corrected × N ≥ inverter MPPT min voltage
        int minModules = (int)Math.Ceiling(inverter.MpptMinVoltage / vmpCorrected);

        if (minModules > maxModules)
            return new StringSizingResult
            {
                MinModulesPerString = minModules,
                MaxModulesPerString = maxModules,
                IsValid = false,
                CorrectedVocMax = Math.Round(vocCorrected, 2),
                CorrectedVmpMin = Math.Round(vmpCorrected, 2),
                Reason = "No valid string length — temperature range too wide for this inverter",
            };

        // Recommended: target MPPT sweet spot (~75% of range)
        double targetVmp = inverter.MpptMinVoltage + (inverter.MpptMaxVoltage - inverter.MpptMinVoltage) * 0.6;
        int recommended = vmpCorrected > 0 ? (int)Math.Round(targetVmp / vmpCorrected) : minModules;
        recommended = Math.Clamp(recommended, minModules, maxModules);

        return new StringSizingResult
        {
            MinModulesPerString = minModules,
            MaxModulesPerString = maxModules,
            RecommendedModulesPerString = recommended,
            CorrectedVocMax = Math.Round(vocCorrected, 2),
            CorrectedVmpMin = Math.Round(vmpCorrected, 2),
            IsValid = true,
        };
    }

    // ── System Sizing ────────────────────────────────────────────────────────

    /// <summary>
    /// Sizes a complete PV system for a target capacity.
    /// </summary>
    /// <param name="targetDcKw">Target array DC capacity in kW.</param>
    /// <param name="module">Module spec.</param>
    /// <param name="inverter">Inverter spec.</param>
    /// <param name="peakSunHours">Average peak sun hours per day (default 4.5).</param>
    /// <param name="systemLosses">Total system losses fraction (default 0.14 = 14%).</param>
    /// <param name="minAmbientC">Min ambient temp for string sizing.</param>
    /// <param name="maxCellTempC">Max cell temp for string sizing.</param>
    public static SystemSizingResult SizeSystem(
        double targetDcKw, ModuleSpec module, InverterSpec inverter,
        double peakSunHours = 4.5, double systemLosses = 0.14,
        double minAmbientC = -10, double maxCellTempC = 65)
    {
        var stringSizing = SizeString(module, inverter, minAmbientC, maxCellTempC);
        int modulesPerString = stringSizing.IsValid ? stringSizing.RecommendedModulesPerString : 1;

        int totalModules = (int)Math.Ceiling(targetDcKw * 1000.0 / module.WattsSTC);
        int strings = Math.Max(1, (int)Math.Ceiling((double)totalModules / modulesPerString));
        totalModules = strings * modulesPerString; // Round up to full strings

        double systemDcKw = totalModules * module.WattsSTC / 1000.0;
        double systemAcKw = inverter.RatedAcWatts / 1000.0;
        double dcAcRatio = systemAcKw > 0 ? systemDcKw / systemAcKw : 0;

        // Annual production estimate (simplified)
        double annualKwh = systemDcKw * peakSunHours * 365 * (1 - systemLosses);

        // NEC 690.8: conductor sizing based on corrected Isc × 1.25
        double maxIsc = CorrectIscForTemp(module.IscAmps, module.TempCoeffIscPerC, maxCellTempC);
        double conductorMinAmps = maxIsc * strings; // Parallel string current

        return new SystemSizingResult
        {
            SystemDcKw = Math.Round(systemDcKw, 2),
            SystemAcKw = Math.Round(systemAcKw, 2),
            DcAcRatio = Math.Round(dcAcRatio, 2),
            TotalModules = totalModules,
            StringsCount = strings,
            ModulesPerString = modulesPerString,
            AnnualProductionKwh = Math.Round(annualKwh, 0),
            MaxIscCorrected = Math.Round(maxIsc, 2),
            ConductorMinAmps = Math.Round(conductorMinAmps, 2),
        };
    }

    // ── DC/AC Ratio ──────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates whether a DC/AC ratio is within acceptable limits.
    /// Typical range: 1.0 to 1.5 (up to 1.3 is common, 1.5 for high-clipping sites).
    /// </summary>
    public static (bool IsAcceptable, string Assessment) EvaluateDcAcRatio(double ratio)
    {
        if (ratio < 0.9)
            return (false, "Undersized array — inverter underutilized");
        if (ratio <= 1.15)
            return (true, "Conservative — minimal clipping losses");
        if (ratio <= 1.35)
            return (true, "Optimal — balanced clipping and production");
        if (ratio <= 1.50)
            return (true, "Aggressive — higher clipping, good for high-irradiance sites");
        return (false, "Oversized array — excessive clipping losses");
    }
}
