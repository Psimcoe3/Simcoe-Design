using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Electrical safety calculations per NFPA 70E.
/// Covers approach boundaries, PPE category selection,
/// energized work permits, and labeling requirements.
/// </summary>
public static class ElectricalSafetyService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum EquipmentClass
    {
        Panelboard,          // 240V and below
        Mcc600V,             // Motor control center ≤600V
        Switchgear600V,      // LV switchgear ≤600V
        Switchgear15kV,      // MV switchgear 1-15 kV
        OverheadLine,        // Utility overhead lines
        TransformerPrimary,  // Transformer primary > 600V
    }

    public enum PpeCategory
    {
        Category0,  // 0 cal/cm² — no arc flash PPE required
        Category1,  // 4 cal/cm² — arc-rated shirt/pants, safety glasses
        Category2,  // 8 cal/cm² — arc-rated clothing + face shield
        Category3,  // 25 cal/cm² — arc flash suit + hood
        Category4,  // 40 cal/cm² — multi-layer arc flash suit
    }

    public record ApproachBoundaries
    {
        public double LimitedApproachFt { get; init; }
        public double RestrictedApproachFt { get; init; }
        public double ProhibitedApproachFt { get; init; }
        public double ArcFlashBoundaryFt { get; init; }
    }

    public record PpeResult
    {
        public PpeCategory Category { get; init; }
        public double IncidentEnergyCalCm2 { get; init; }
        public double ArcFlashBoundaryFt { get; init; }
        public double WorkingDistanceInches { get; init; }
        public bool EnergizedWorkPermitRequired { get; init; }
    }

    public record LabelInfo
    {
        public double IncidentEnergyCalCm2 { get; init; }
        public double ArcFlashBoundaryInches { get; init; }
        public PpeCategory RequiredPpe { get; init; }
        public double NominalVoltage { get; init; }
        public string HazardWarning { get; init; } = "";
    }

    // ── Approach Boundaries (NFPA 70E Table 130.4(E)(a)) ─────────────────────

    /// <summary>
    /// Returns shock approach boundaries per NFPA 70E Table 130.4(E)(a).
    /// </summary>
    public static ApproachBoundaries GetApproachBoundaries(double nominalVoltage)
    {
        if (nominalVoltage <= 0)
            throw new ArgumentException("Voltage must be positive.");

        // Movable conductor boundaries (worst case)
        return nominalVoltage switch
        {
            <= 50 => new ApproachBoundaries
            {
                LimitedApproachFt = 0,
                RestrictedApproachFt = 0,
                ProhibitedApproachFt = 0,
                ArcFlashBoundaryFt = 0,
            },
            <= 150 => new ApproachBoundaries
            {
                LimitedApproachFt = 3.5,
                RestrictedApproachFt = 1.0,
                ProhibitedApproachFt = 0.083, // 1 inch
                ArcFlashBoundaryFt = 4.0,
            },
            <= 300 => new ApproachBoundaries
            {
                LimitedApproachFt = 3.5,
                RestrictedApproachFt = 1.0,
                ProhibitedApproachFt = 0.167, // 2 inches
                ArcFlashBoundaryFt = 4.0,
            },
            <= 750 => new ApproachBoundaries
            {
                LimitedApproachFt = 3.5,
                RestrictedApproachFt = 1.0,
                ProhibitedApproachFt = 0.25,  // 3 inches
                ArcFlashBoundaryFt = 4.0,
            },
            <= 15000 => new ApproachBoundaries
            {
                LimitedApproachFt = 5.0,
                RestrictedApproachFt = 2.167,  // 2 ft 2 in
                ProhibitedApproachFt = 0.583,  // 7 inches
                ArcFlashBoundaryFt = 10.0,
            },
            <= 36000 => new ApproachBoundaries
            {
                LimitedApproachFt = 6.0,
                RestrictedApproachFt = 2.833,
                ProhibitedApproachFt = 0.833,
                ArcFlashBoundaryFt = 12.0,
            },
            _ => new ApproachBoundaries
            {
                LimitedApproachFt = 8.0,
                RestrictedApproachFt = 3.5,
                ProhibitedApproachFt = 1.167,
                ArcFlashBoundaryFt = 15.0,
            },
        };
    }

    // ── PPE Category (Table Method — NFPA 70E Table 130.7(C)(15)) ────────────

    /// <summary>
    /// Determines PPE category using the table method per NFPA 70E Table 130.7(C)(15).
    /// Also calculates incident energy for labeling.
    /// </summary>
    public static PpeResult DeterminePpe(
        EquipmentClass equipment,
        double availableFaultKA,
        double clearingTimeSec,
        double workingDistanceInches = 18)
    {
        if (availableFaultKA <= 0)
            throw new ArgumentException("Fault current must be positive.");
        if (clearingTimeSec <= 0)
            throw new ArgumentException("Clearing time must be positive.");
        if (workingDistanceInches <= 0)
            throw new ArgumentException("Working distance must be positive.");

        // Simplified IEEE 1584 incident energy estimate (cal/cm²)
        // E = 4.184 × Cf × En × (t/0.2) × (610^x / D^x)
        // Simplified: E ≈ K × Ibf × t / D²
        // Using simplified formula for estimation:
        double cf = equipment switch
        {
            EquipmentClass.Panelboard => 1.0,
            EquipmentClass.Mcc600V => 1.5,
            EquipmentClass.Switchgear600V => 1.5,
            EquipmentClass.Switchgear15kV => 2.0,
            EquipmentClass.TransformerPrimary => 2.0,
            EquipmentClass.OverheadLine => 2.0,
            _ => 1.5,
        };

        // Simplified incident energy: E ≈ Cf × 5 × Iarc × t / D² (cal/cm²)
        // Iarc ≈ 0.85 × Ibf for < 1kV, Iarc ≈ 0.9 × Ibf for MV
        double iarcMultiple = equipment == EquipmentClass.Switchgear15kV
            || equipment == EquipmentClass.TransformerPrimary ? 0.9 : 0.85;
        double iarc = availableFaultKA * iarcMultiple;
        double distCm = workingDistanceInches * 2.54;
        double incidentEnergy = cf * 5.0 * iarc * clearingTimeSec / (distCm / 30.48);

        // PPE category from incident energy
        PpeCategory category;
        if (incidentEnergy <= 1.2) category = PpeCategory.Category0;
        else if (incidentEnergy <= 4.0) category = PpeCategory.Category1;
        else if (incidentEnergy <= 8.0) category = PpeCategory.Category2;
        else if (incidentEnergy <= 25.0) category = PpeCategory.Category3;
        else if (incidentEnergy <= 40.0) category = PpeCategory.Category4;
        else category = PpeCategory.Category4; // Exceeds Cat 4 — do not work energized

        // Arc flash boundary: distance where incident energy = 1.2 cal/cm²
        double afbFt = Math.Sqrt(incidentEnergy / 1.2) * (workingDistanceInches / 12.0);

        bool ewpRequired = incidentEnergy > 1.2;

        return new PpeResult
        {
            Category = category,
            IncidentEnergyCalCm2 = Math.Round(incidentEnergy, 2),
            ArcFlashBoundaryFt = Math.Round(afbFt, 2),
            WorkingDistanceInches = workingDistanceInches,
            EnergizedWorkPermitRequired = ewpRequired,
        };
    }

    // ── Label Generation ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates arc flash label information per NFPA 70E 130.5(H).
    /// </summary>
    public static LabelInfo GenerateLabel(
        double nominalVoltage,
        double incidentEnergyCalCm2,
        double arcFlashBoundaryInches)
    {
        PpeCategory category;
        if (incidentEnergyCalCm2 <= 1.2) category = PpeCategory.Category0;
        else if (incidentEnergyCalCm2 <= 4.0) category = PpeCategory.Category1;
        else if (incidentEnergyCalCm2 <= 8.0) category = PpeCategory.Category2;
        else if (incidentEnergyCalCm2 <= 25.0) category = PpeCategory.Category3;
        else category = PpeCategory.Category4;

        string warning = incidentEnergyCalCm2 > 40
            ? "DANGER: Exceeds 40 cal/cm² — DO NOT WORK ENERGIZED"
            : incidentEnergyCalCm2 > 1.2
            ? "WARNING: Arc Flash and Shock Hazard — Appropriate PPE Required"
            : "CAUTION: Arc Flash Hazard — PPE Required per NFPA 70E";

        return new LabelInfo
        {
            IncidentEnergyCalCm2 = Math.Round(incidentEnergyCalCm2, 2),
            ArcFlashBoundaryInches = Math.Round(arcFlashBoundaryInches, 1),
            RequiredPpe = category,
            NominalVoltage = nominalVoltage,
            HazardWarning = warning,
        };
    }
}
