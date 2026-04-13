namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Sizes UPS systems and battery banks based on connected load, required
/// runtime, and battery characteristics.
///
/// Key standards:
/// - IEEE 485 (Lead-Acid battery sizing)
/// - IEEE 1188 (VRLA battery sizing)  
/// - IEEE 1115 (Nickel-Cadmium battery sizing)
///
/// The service computes:
/// 1. Required UPS kVA rating from connected load + power factor + growth
/// 2. Battery amp-hour capacity for the target runtime
/// 3. Number of battery strings / cells
/// 4. Estimated battery weight and footprint
/// </summary>
public static class BatterySizingService
{
    /// <summary>Battery chemistry type.</summary>
    public enum BatteryChemistry
    {
        LeadAcidVRLA,
        LeadAcidFlooded,
        LithiumIon,
        NickelCadmium,
    }

    /// <summary>UPS topology.</summary>
    public enum UpsTopology
    {
        Online,         // Double-conversion
        LineInteractive,
        Standby,
    }

    /// <summary>Input parameters for UPS/battery sizing.</summary>
    public record BatterySizingInput
    {
        public double LoadKW { get; init; }
        public double PowerFactor { get; init; } = 0.9;
        public double TargetRuntimeMinutes { get; init; } = 15;
        public double GrowthFactor { get; init; } = 1.2; // 20% growth margin
        public BatteryChemistry Chemistry { get; init; } = BatteryChemistry.LeadAcidVRLA;
        public UpsTopology Topology { get; init; } = UpsTopology.Online;

        /// <summary>Nominal DC bus voltage (typical: 240, 384, 480 VDC).</summary>
        public double NominalDCVoltage { get; init; } = 480;

        /// <summary>End-of-discharge voltage per cell (volts).</summary>
        public double EndCellVoltage { get; init; } = 1.75; // VRLA default

        /// <summary>Ambient temperature in °C. IEEE 485 derate above 25°C.</summary>
        public double AmbientTempC { get; init; } = 25;

        /// <summary>Battery aging factor (typically 1.25 per IEEE 485).</summary>
        public double AgingFactor { get; init; } = 1.25;

        /// <summary>Design margin applied to final capacity (e.g. 1.1 = 10%).</summary>
        public double DesignMargin { get; init; } = 1.1;
    }

    /// <summary>Sizing result.</summary>
    public record BatterySizingResult
    {
        public double UpsKVA { get; init; }
        public double UpsKW { get; init; }
        public double BatteryAH { get; init; }
        public int CellCount { get; init; }
        public int StringCount { get; init; } = 1;
        public double NominalDCVoltage { get; init; }
        public double TotalEnergyKWH { get; init; }
        public double RuntimeMinutes { get; init; }
        public double TemperatureDerateFactor { get; init; }
        public double EstimatedWeightLbs { get; init; }
        public BatteryChemistry Chemistry { get; init; }
        public UpsTopology Topology { get; init; }
        public List<string> Notes { get; init; } = new();
    }

    /// <summary>
    /// Sizes the UPS and battery system.
    /// </summary>
    public static BatterySizingResult Size(BatterySizingInput input)
    {
        var notes = new List<string>();

        // 1. UPS kVA rating
        double designKW = input.LoadKW * input.GrowthFactor;
        double upsKVA = designKW / input.PowerFactor;
        double upsKVARounded = RoundToStandardKVA(upsKVA);

        // 2. Calculate battery discharge current
        double upsEfficiency = GetUpsEfficiency(input.Topology);
        double dcLoadKW = designKW / upsEfficiency;
        double dcLoadAmps = (dcLoadKW * 1000.0) / input.NominalDCVoltage;

        // 3. Temperature derate (IEEE 485: capacity reduces above 25°C / below 25°C)
        double tempDerate = GetTemperatureDerateFactor(input.AmbientTempC, input.Chemistry);

        // 4. Cell count from DC bus voltage
        double nominalCellVoltage = GetNominalCellVoltage(input.Chemistry);
        int cellCount = (int)Math.Ceiling(input.NominalDCVoltage / nominalCellVoltage);

        // 5. Required AH
        double runtimeHours = input.TargetRuntimeMinutes / 60.0;
        double rawAH = dcLoadAmps * runtimeHours;

        // Apply derating factors
        double designAH = rawAH * input.AgingFactor * tempDerate * input.DesignMargin;
        double roundedAH = Math.Ceiling(designAH / 10.0) * 10; // round up to nearest 10 AH

        // 6. Total stored energy
        double totalKWH = roundedAH * input.NominalDCVoltage / 1000.0;

        // 7. Weight estimate
        double weightLbs = EstimateWeight(roundedAH, cellCount, input.Chemistry);

        // 8. Notes
        if (input.AmbientTempC > 25)
            notes.Add($"Temperature derate applied: {tempDerate:F2} (ambient {input.AmbientTempC}°C > 25°C reference)");
        if (upsKVARounded > upsKVA * 1.3)
            notes.Add($"UPS rounded from {upsKVA:F1} to {upsKVARounded:F0} kVA — consider verifying next smaller frame");
        if (input.TargetRuntimeMinutes > 60)
            notes.Add("Runtime >60 min — consider generator backup instead of extended battery");

        return new BatterySizingResult
        {
            UpsKVA = upsKVARounded,
            UpsKW = designKW,
            BatteryAH = roundedAH,
            CellCount = cellCount,
            NominalDCVoltage = input.NominalDCVoltage,
            TotalEnergyKWH = totalKWH,
            RuntimeMinutes = input.TargetRuntimeMinutes,
            TemperatureDerateFactor = tempDerate,
            EstimatedWeightLbs = weightLbs,
            Chemistry = input.Chemistry,
            Topology = input.Topology,
            Notes = notes,
        };
    }

    /// <summary>
    /// Calculates actual runtime in minutes for a given battery AH and load.
    /// </summary>
    public static double CalculateRuntime(double batteryAH, double loadKW, double dcVoltage, double efficiency = 0.92)
    {
        if (loadKW <= 0 || dcVoltage <= 0) return 0;
        double dcAmps = (loadKW * 1000.0) / (dcVoltage * efficiency);
        if (dcAmps <= 0) return 0;
        double hours = batteryAH / dcAmps;
        return hours * 60.0; // convert to minutes
    }

    // ── Standard UPS sizes ───────────────────────────────────────────────────

    private static readonly double[] StandardKVA =
    {
        1, 1.5, 2, 3, 5, 6, 7.5, 10, 12, 15, 20, 25, 30, 40, 50,
        60, 75, 80, 100, 120, 150, 160, 200, 225, 250, 300, 400,
        500, 600, 750, 800, 1000
    };

    internal static double RoundToStandardKVA(double kva)
    {
        foreach (var std in StandardKVA)
        {
            if (std >= kva) return std;
        }
        return Math.Ceiling(kva / 100) * 100; // above range, round to nearest 100
    }

    private static double GetUpsEfficiency(UpsTopology topology) => topology switch
    {
        UpsTopology.Online => 0.92,
        UpsTopology.LineInteractive => 0.95,
        UpsTopology.Standby => 0.97,
        _ => 0.92,
    };

    private static double GetNominalCellVoltage(BatteryChemistry chemistry) => chemistry switch
    {
        BatteryChemistry.LeadAcidVRLA => 2.0,
        BatteryChemistry.LeadAcidFlooded => 2.0,
        BatteryChemistry.LithiumIon => 3.6,
        BatteryChemistry.NickelCadmium => 1.2,
        _ => 2.0,
    };

    /// <summary>
    /// IEEE 485 temperature correction factor.
    /// Capacity decreases ~1% per °F below 77°F (25°C) for lead-acid.
    /// </summary>
    private static double GetTemperatureDerateFactor(double ambientC, BatteryChemistry chemistry)
    {
        if (chemistry == BatteryChemistry.LithiumIon)
        {
            // Li-ion: less sensitive but still derate below 25°C
            if (ambientC >= 25) return 1.0;
            return 1.0 + (25 - ambientC) * 0.005; // ~0.5% per °C below 25
        }

        // Lead-acid / NiCd: IEEE 485 Table
        if (ambientC >= 25) return 1.0;
        double deltaF = (25 - ambientC) * 1.8; // convert °C delta to °F delta
        return 1.0 + deltaF * 0.01; // 1% per °F
    }

    private static double EstimateWeight(double ah, int cells, BatteryChemistry chemistry)
    {
        // Approximate weight per cell based on chemistry and AH
        double lbsPerAhPerCell = chemistry switch
        {
            BatteryChemistry.LeadAcidVRLA => 0.09,
            BatteryChemistry.LeadAcidFlooded => 0.10,
            BatteryChemistry.LithiumIon => 0.04,
            BatteryChemistry.NickelCadmium => 0.07,
            _ => 0.09,
        };
        return ah * cells * lbsPerAhPerCell;
    }
}
