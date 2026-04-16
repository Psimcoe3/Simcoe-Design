using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Duct bank thermal analysis and ampacity derating per NEC 310.60 and
/// Neher-McGrath methodology (IEEE Std 835).
/// </summary>
public static class DuctBankDesignService
{
    public enum SoilType
    {
        Concrete,
        Sand,
        Clay,
        Loam,
        Rock,
    }

    public enum DuctMaterial
    {
        PVC,
        FibreReinforced,
        SteelRigid,
        HDPE,
    }

    public record DuctSpec
    {
        public double InnerDiameterInches { get; init; }
        public double OuterDiameterInches { get; init; }
        public DuctMaterial Material { get; init; }
    }

    public record BankGeometry
    {
        public int Rows { get; init; } = 1;
        public int Columns { get; init; } = 1;
        public double CenterSpacingInches { get; init; } = 7.5;
        public double BurialDepthInches { get; init; } = 30;
    }

    public record ThermalEnvironment
    {
        public SoilType Soil { get; init; } = SoilType.Clay;
        public double AmbientTempC { get; init; } = 20.0;
        public double SoilThermalResistivityRhoK { get; init; } = 90.0;
        public double LoadFactorPercent { get; init; } = 100.0;
    }

    public record ThermalResult
    {
        public double EffectiveRhoK { get; init; }
        public double MutualHeatingFactor { get; init; }
        public double DeratingFactor { get; init; }
        public double AdjustedAmpacity { get; init; }
        public string GoverningFactor { get; init; } = string.Empty;
    }

    public record BankDesignResult
    {
        public int TotalDucts { get; init; }
        public int OccupiedDucts { get; init; }
        public int SpareDucts { get; init; }
        public double SparePercent { get; init; }
        public double DeratingFactor { get; init; }
        public double MinBurialDepthInches { get; init; }
        public bool MeetsNecDepth { get; init; }
        public List<string> Recommendations { get; init; } = new();
    }

    /// <summary>
    /// Soil thermal resistivity (rho-K) in °C·cm/W for common soil types.
    /// NEC Table 310.60(C)(11) reference values.
    /// </summary>
    public static double GetTypicalRhoK(SoilType soil)
    {
        return soil switch
        {
            SoilType.Concrete => 55.0,
            SoilType.Sand => 120.0,
            SoilType.Clay => 90.0,
            SoilType.Loam => 80.0,
            SoilType.Rock => 60.0,
            _ => 90.0,
        };
    }

    /// <summary>
    /// Duct material thermal resistivity (rho-d) in °C·cm/W.
    /// </summary>
    public static double GetDuctThermalResistivity(DuctMaterial material)
    {
        return material switch
        {
            DuctMaterial.PVC => 650.0,
            DuctMaterial.FibreReinforced => 480.0,
            DuctMaterial.SteelRigid => 50.0,
            DuctMaterial.HDPE => 550.0,
            _ => 650.0,
        };
    }

    /// <summary>
    /// Minimum burial depth per NEC 300.50 for direct-buried conduit or duct.
    /// Returns depth in inches.
    /// </summary>
    public static double GetMinBurialDepthInches(DuctMaterial material, double voltageV)
    {
        if (voltageV <= 0) return 18.0;

        // NEC 300.50: Rigid metal conduit, ≤600V = 6"; PVC ≤600V = 18"; >600V = 24"
        if (voltageV > 600)
            return 24.0;

        return material switch
        {
            DuctMaterial.SteelRigid => 6.0,
            _ => 18.0,
        };
    }

    /// <summary>
    /// Mutual heating factor for adjacent ducts in a duct bank.
    /// Simplified Neher-McGrath: F_mutual ≈ 1 + 0.1 × (N_adjacent - 1) for ducts
    /// at standard 7.5" spacing. Tighter spacing or larger banks increase the factor.
    /// </summary>
    public static double CalculateMutualHeatingFactor(BankGeometry bank, int occupiedDucts)
    {
        if (occupiedDucts <= 1) return 1.0;

        int totalDucts = bank.Rows * bank.Columns;
        int adjacentCount = Math.Min(occupiedDucts, totalDucts);

        // Spacing correction: reference is 7.5". Tighter spacing increases heating.
        double spacingFactor = bank.CenterSpacingInches > 0
            ? 7.5 / bank.CenterSpacingInches
            : 1.0;

        // Depth correction: deeper burial reduces dissipation
        double depthFactor = bank.BurialDepthInches > 30 ? 1.0 + (bank.BurialDepthInches - 30) / 200.0 : 1.0;

        double factor = 1.0 + 0.1 * (adjacentCount - 1) * spacingFactor * depthFactor;
        return Math.Round(factor, 3);
    }

    /// <summary>
    /// Ampacity derating factor for duct bank thermal conditions.
    /// Combines soil thermal resistance, duct material, mutual heating, and load factor.
    /// Based on Neher-McGrath simplified approach.
    /// </summary>
    public static double CalculateDeratingFactor(ThermalEnvironment env, DuctSpec duct,
        BankGeometry bank, int occupiedDucts)
    {
        double rhoK = env.SoilThermalResistivityRhoK > 0 ? env.SoilThermalResistivityRhoK : GetTypicalRhoK(env.Soil);

        // Reference soil rho is 90 °C·cm/W per NEC 310.60
        double soilFactor = Math.Sqrt(90.0 / rhoK);

        // Duct wall thermal penalty (minor for steel, significant for PVC)
        double ductRho = GetDuctThermalResistivity(duct.Material);
        double ductPenalty = 1.0 - (ductRho - 50.0) / 5000.0;
        ductPenalty = Math.Max(0.75, Math.Min(1.0, ductPenalty));

        // Mutual heating
        double mutualFactor = CalculateMutualHeatingFactor(bank, occupiedDucts);
        double mutualDerating = 1.0 / Math.Sqrt(mutualFactor);

        // Load factor: less than 100% allows some recovery
        double loadFactor = env.LoadFactorPercent > 0 ? env.LoadFactorPercent / 100.0 : 1.0;
        double loadAdjust = loadFactor < 1.0 ? 1.0 + (1.0 - loadFactor) * 0.15 : 1.0;
        loadAdjust = Math.Min(loadAdjust, 1.15);

        // Temperature correction: reference 20°C, derate for higher ambient
        double tempDerating = env.AmbientTempC <= 20 ? 1.0
            : Math.Sqrt((90.0 - env.AmbientTempC) / (90.0 - 20.0));
        tempDerating = Math.Max(0.5, tempDerating);

        double combined = soilFactor * ductPenalty * mutualDerating * loadAdjust * tempDerating;
        return Math.Round(Math.Max(0.30, Math.Min(1.0, combined)), 3);
    }

    /// <summary>
    /// Calculate derated ampacity for a cable in a duct bank.
    /// </summary>
    public static ThermalResult CalculateDeratedAmpacity(double baseAmpacity,
        ThermalEnvironment env, DuctSpec duct, BankGeometry bank, int occupiedDucts)
    {
        double rhoK = env.SoilThermalResistivityRhoK > 0 ? env.SoilThermalResistivityRhoK : GetTypicalRhoK(env.Soil);
        double mutualFactor = CalculateMutualHeatingFactor(bank, occupiedDucts);
        double derating = CalculateDeratingFactor(env, duct, bank, occupiedDucts);

        double adjusted = baseAmpacity * derating;

        // Determine governing constraint
        string governing;
        double soilFactor = Math.Sqrt(90.0 / rhoK);
        double mutualDerating = 1.0 / Math.Sqrt(mutualFactor);

        if (soilFactor < mutualDerating && soilFactor < 0.95)
            governing = "Soil thermal resistivity";
        else if (mutualDerating < soilFactor && mutualDerating < 0.95)
            governing = "Mutual heating";
        else if (env.AmbientTempC > 30)
            governing = "Ambient temperature";
        else
            governing = "Standard conditions";

        return new ThermalResult
        {
            EffectiveRhoK = rhoK,
            MutualHeatingFactor = mutualFactor,
            DeratingFactor = derating,
            AdjustedAmpacity = Math.Round(adjusted, 1),
            GoverningFactor = governing,
        };
    }

    /// <summary>
    /// Evaluate a complete duct bank design: derating, depth, spare capacity, and recommendations.
    /// </summary>
    public static BankDesignResult EvaluateBank(ThermalEnvironment env, DuctSpec duct,
        BankGeometry bank, int occupiedDucts, double voltageV)
    {
        int totalDucts = bank.Rows * bank.Columns;
        int spareDucts = totalDucts - occupiedDucts;
        double sparePercent = totalDucts > 0 ? (double)spareDucts / totalDucts * 100.0 : 0;

        double derating = CalculateDeratingFactor(env, duct, bank, occupiedDucts);
        double minDepth = GetMinBurialDepthInches(duct.Material, voltageV);
        bool meetsDepth = bank.BurialDepthInches >= minDepth;

        var recs = new List<string>();

        if (!meetsDepth)
            recs.Add($"Burial depth {bank.BurialDepthInches}\" is below NEC minimum {minDepth}\"");

        if (sparePercent < 25)
            recs.Add("Consider adding spare ducts (minimum 25% recommended for future expansion)");

        if (derating < 0.6)
            recs.Add("Heavy derating — consider wider duct spacing or fewer cables per bank");

        if (bank.CenterSpacingInches < 7.5)
            recs.Add("Duct spacing below 7.5\" standard — mutual heating will be elevated");

        if (env.SoilThermalResistivityRhoK > 120)
            recs.Add("High soil thermal resistivity — consider thermal backfill (Fluidized Thermal Backfill)");

        return new BankDesignResult
        {
            TotalDucts = totalDucts,
            OccupiedDucts = occupiedDucts,
            SpareDucts = spareDucts,
            SparePercent = Math.Round(sparePercent, 1),
            DeratingFactor = derating,
            MinBurialDepthInches = minDepth,
            MeetsNecDepth = meetsDepth,
            Recommendations = recs,
        };
    }
}
