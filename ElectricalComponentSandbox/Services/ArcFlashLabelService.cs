namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NFPA 70E Article 130 — Arc flash warning label generation.
/// Produces equipment labels per NEC 110.16 and NFPA 70E 130.5(H)
/// using incident energy data from ShortCircuitService.
///
/// Required label information per NEC 110.16(B):
/// - Nominal system voltage
/// - Arc flash boundary
/// - At least one of: incident energy + working distance, or PPE category
/// - Date of study
///
/// Additional NFPA 70E 130.5(H) information:
/// - Flash hazard boundary
/// - Shock hazard approach boundaries (limited/restricted)
/// - Required PPE description
/// - Equipment name/ID
/// </summary>
public static class ArcFlashLabelService
{
    /// <summary>
    /// Complete arc flash label data per NEC 110.16 and NFPA 70E.
    /// </summary>
    public record ArcFlashLabel
    {
        public string EquipmentId { get; init; } = "";
        public string EquipmentName { get; init; } = "";
        public string Location { get; init; } = "";
        public double NominalVoltage { get; init; }
        public double IncidentEnergyCal { get; init; }
        public double WorkingDistanceInches { get; init; }
        public double ArcFlashBoundaryInches { get; init; }
        public int HazardCategory { get; init; }
        public string RequiredPPE { get; init; } = "";
        public string HazardRiskCategory { get; init; } = "";

        /// <summary>NFPA 70E Table 130.4(E)(a): limited approach boundary (inches).</summary>
        public double LimitedApproachBoundaryInches { get; init; }

        /// <summary>NFPA 70E Table 130.4(E)(a): restricted approach boundary (inches).</summary>
        public double RestrictedApproachBoundaryInches { get; init; }

        public string DateOfStudy { get; init; } = "";
        public double BoltedFaultCurrentKA { get; init; }
        public string WarningHeader { get; init; } = "DANGER — ARC FLASH HAZARD";
        public List<string> LabelLines { get; init; } = new();
    }

    /// <summary>
    /// NFPA 70E Table 130.7(C)(15)(a) — PPE category descriptions.
    /// </summary>
    private static readonly Dictionary<int, string> PPEDescriptions = new()
    {
        [0] = "No PPE required beyond safety glasses and hearing protection",
        [1] = "Arc-rated FR shirt/pants or FR coverall (min 4 cal/cm²), arc-rated face shield, hard hat, safety glasses, hearing protection, leather gloves",
        [2] = "Arc-rated FR shirt/pants or FR coverall (min 8 cal/cm²), arc-rated flash suit hood, hard hat, safety glasses, hearing protection, leather gloves",
        [3] = "Arc-rated FR shirt/pants + FR coverall (min 25 cal/cm²), arc-rated flash suit hood, hard hat, safety glasses, hearing protection, rubber insulating gloves with leather protectors",
        [4] = "Arc-rated flash suit (min 40 cal/cm²), arc-rated flash suit hood, hard hat, safety glasses, hearing protection, rubber insulating gloves with leather protectors",
    };

    /// <summary>
    /// NFPA 70E Table 130.7(C)(15)(a) — Maximum incident energy per category.
    /// </summary>
    private static readonly (int Category, double MaxCalPerCm2)[] CategoryBoundaries =
    {
        (0, 1.2),
        (1, 4.0),
        (2, 8.0),
        (3, 25.0),
        (4, 40.0),
    };

    /// <summary>
    /// NFPA 70E Table 130.4(E)(a) — Shock hazard approach boundaries (inches) by voltage.
    /// (voltage threshold, limited approach movable, restricted approach)
    /// </summary>
    private static readonly (double MaxVoltage, double LimitedInches, double RestrictedInches)[] ShockBoundaries =
    {
        (50, 0, 0),          // ≤ 50V: no boundary
        (150, 42, 12),       // 51-150V
        (300, 42, 12),       // 151-300V
        (600, 42, 12),       // 301-600V
        (2000, 42, 24),      // 601-2kV
        (15000, 60, 24),     // 2.001-15kV
        (36000, 72, 36),     // 15.001–36kV
    };

    /// <summary>
    /// Generates a complete arc flash label from ShortCircuitService results.
    /// </summary>
    public static ArcFlashLabel GenerateLabel(
        ArcFlashResult arcFlashData,
        double nominalVoltage,
        double workingDistanceInches = 18,
        string location = "",
        string? dateOfStudy = null)
    {
        int category = DetermineHazardCategory(arcFlashData.IncidentEnergyCal);
        string ppe = GetPPEDescription(category);
        string hrc = $"Category {category}";
        var (limited, restricted) = GetShockBoundaries(nominalVoltage);

        string date = dateOfStudy ?? DateTime.Today.ToString("yyyy-MM-dd");

        var lines = new List<string>
        {
            $"Equipment: {arcFlashData.NodeName}",
            $"Location: {(string.IsNullOrEmpty(location) ? "N/A" : location)}",
            $"Nominal Voltage: {nominalVoltage:F0}V",
            $"Bolted Fault Current: {arcFlashData.BoltedFaultCurrentKA:F1} kA",
            $"Incident Energy: {arcFlashData.IncidentEnergyCal:F1} cal/cm² at {workingDistanceInches}\"",
            $"Arc Flash Boundary: {arcFlashData.ArcFlashBoundaryInches:F0}\"",
            $"Hazard Risk Category: {hrc}",
            $"Required PPE: {ppe}",
            $"Limited Approach: {limited:F0}\"",
            $"Restricted Approach: {restricted:F0}\"",
            $"Date of Study: {date}",
        };

        return new ArcFlashLabel
        {
            EquipmentId = arcFlashData.NodeId,
            EquipmentName = arcFlashData.NodeName,
            Location = location,
            NominalVoltage = nominalVoltage,
            IncidentEnergyCal = arcFlashData.IncidentEnergyCal,
            WorkingDistanceInches = workingDistanceInches,
            ArcFlashBoundaryInches = arcFlashData.ArcFlashBoundaryInches,
            HazardCategory = category,
            RequiredPPE = ppe,
            HazardRiskCategory = hrc,
            LimitedApproachBoundaryInches = limited,
            RestrictedApproachBoundaryInches = restricted,
            DateOfStudy = date,
            BoltedFaultCurrentKA = arcFlashData.BoltedFaultCurrentKA,
            LabelLines = lines,
        };
    }

    /// <summary>
    /// Generates labels for all nodes in a hierarchy.
    /// </summary>
    public static List<ArcFlashLabel> GenerateLabelsFromStudy(
        IEnumerable<ArcFlashResult> arcFlashResults,
        double nominalVoltage,
        double workingDistanceInches = 18,
        string? dateOfStudy = null)
    {
        return arcFlashResults
            .Select(r => GenerateLabel(r, nominalVoltage, workingDistanceInches, dateOfStudy: dateOfStudy))
            .ToList();
    }

    /// <summary>
    /// Determines if equipment requires an arc flash label per NEC 110.16.
    /// Labels are required on equipment likely to be examined, adjusted, serviced, or maintained
    /// while energized, operating at ≥ 50V.
    /// </summary>
    public static bool RequiresLabel(double nominalVoltage, bool isServiceEquipment = true)
    {
        return nominalVoltage >= 50 && isServiceEquipment;
    }

    public static int DetermineHazardCategory(double incidentEnergyCal)
    {
        foreach (var (cat, maxCal) in CategoryBoundaries)
        {
            if (incidentEnergyCal <= maxCal) return cat;
        }
        return 4; // > 40 cal: Category 4 (or Dangerous — do not work live)
    }

    public static string GetPPEDescription(int category)
    {
        return PPEDescriptions.TryGetValue(category, out var desc) ? desc : PPEDescriptions[4];
    }

    public static (double LimitedInches, double RestrictedInches) GetShockBoundaries(double voltage)
    {
        foreach (var (maxV, limited, restricted) in ShockBoundaries)
        {
            if (voltage <= maxV) return (limited, restricted);
        }
        return (ShockBoundaries[^1].LimitedInches, ShockBoundaries[^1].RestrictedInches);
    }
}
