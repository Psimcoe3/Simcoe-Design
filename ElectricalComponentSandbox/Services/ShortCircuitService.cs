using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Result of an AIC (Available Interrupting Capacity) adequacy check at a distribution node.
/// NEC 110.9 requires equipment interrupting rating ≥ available fault current.
/// </summary>
public record ShortCircuitResult
{
    public string NodeId { get; init; } = "";
    public string NodeName { get; init; } = "";
    public ComponentType NodeType { get; init; }
    public double AvailableFaultKA { get; init; }
    public double EquipmentAICKA { get; init; }
    public bool IsAdequate { get; init; }
    public double MarginPercent { get; init; }
}

/// <summary>
/// IEEE 1584-2018 simplified arc flash incident energy result.
/// </summary>
public record ArcFlashResult
{
    public string NodeId { get; init; } = "";
    public string NodeName { get; init; } = "";
    public double BoltedFaultCurrentKA { get; init; }
    public double ArcingCurrentKA { get; init; }
    public double IncidentEnergyCal { get; init; }
    public double ArcFlashBoundaryInches { get; init; }
    public int HazardCategory { get; init; }
    public string RequiredPPE { get; init; } = "";
}

/// <summary>
/// Short circuit analysis and arc flash calculations for the distribution hierarchy.
/// Validates NEC 110.9 interrupting rating adequacy and computes IEEE 1584 incident energy.
/// </summary>
public class ShortCircuitService
{
    // ── NFPA 70E Table 130.7(C)(15) PPE category thresholds (cal/cm²) ────────

    private static readonly (double MaxEnergy, int Category, string PPE)[] HazardCategories =
    {
        (1.2,  0, "No PPE required beyond standard work clothing"),
        (4.0,  1, "Arc-rated shirt/pants (min 4 cal/cm²), safety glasses, hearing protection"),
        (8.0,  2, "Arc-rated shirt/pants (min 8 cal/cm²), arc-rated face shield, balaclava, gloves"),
        (25.0, 3, "Arc flash suit (min 25 cal/cm²), arc-rated gloves, hard hat with face shield"),
        (40.0, 4, "Arc flash suit (min 40 cal/cm²), arc-rated gloves, hard hat with full face shield"),
    };

    /// <summary>
    /// Validates NEC 110.9 — equipment interrupting rating must be ≥ available fault current
    /// at every node in the distribution graph. Returns results for all distribution nodes.
    /// </summary>
    public List<ShortCircuitResult> ValidateAIC(List<DistributionNode> roots)
    {
        var results = new List<ShortCircuitResult>();
        foreach (var root in roots)
            ValidateAICRecursive(root, results);
        return results;
    }

    /// <summary>
    /// Returns only the nodes that fail the NEC 110.9 AIC check.
    /// </summary>
    public List<ShortCircuitResult> GetAICViolations(List<DistributionNode> roots)
    {
        return ValidateAIC(roots).Where(r => !r.IsAdequate).ToList();
    }

    /// <summary>
    /// Computes IEEE 1584-2018 simplified arc flash incident energy at a node.
    /// Uses the Lee method for systems ≥ 15 kV or as a simplified estimate:
    /// E = 4.184 × C_f × E_n × (t/0.2) × (610^x / D^x)
    /// 
    /// Simplified per IEEE 1584-2018 for systems 208V–15kV:
    ///   Arcing current (kA) ≈ Ibf^0.662 (for voltages ≤ 1kV)
    ///   Incident energy (cal/cm²) ≈ K1 × K2 × Ia^1.081 × t × (1/D^1.641)
    /// 
    /// This implementation uses the simplified Ralph Lee method suitable for
    /// preliminary analysis: E = V × Ibf × t / (D² × k_constant)
    /// </summary>
    public ArcFlashResult CalculateArcFlash(
        DistributionNode node,
        double workingDistanceInches = 18.0,
        double arcDurationSeconds = 0.5,
        double systemVoltageV = 480.0)
    {
        double boltedFaultKA = node.FaultCurrentKA;

        if (boltedFaultKA <= 0 || workingDistanceInches <= 0)
        {
            return new ArcFlashResult
            {
                NodeId = node.Id,
                NodeName = node.Name,
                BoltedFaultCurrentKA = boltedFaultKA,
            };
        }

        // Arcing current estimate for systems ≤ 1kV (IEEE 1584-2018 simplified)
        // log(Ia) = K + 0.662 × log(Ibf) + 0.0966V + 0.000526G + 0.5588V×log(Ibf) − 0.00304G×log(Ibf)
        // Simplified: Ia ≈ 0.85 × Ibf for 480V systems
        double arcingCurrentKA = systemVoltageV <= 1000
            ? 0.85 * boltedFaultKA
            : boltedFaultKA; // For > 1kV, arcing ≈ bolted

        // Convert working distance to cm
        double distanceCm = workingDistanceInches * 2.54;

        // Ralph Lee method: E = 5.12 × 10^5 × V × Iarc × t / D²
        // Where E is in J/cm², V in kV, Iarc in kA, t in seconds, D in mm
        // Convert to cal/cm² by dividing by 4.184
        double systemVoltageKV = systemVoltageV / 1000.0;
        double distanceMm = distanceCm * 10;

        // E (J/cm²) = 5.12e5 × V(kV) × Iarc(kA) × t(s) / D²(mm)
        double energyJoules = 5.12e5 * systemVoltageKV * arcingCurrentKA * arcDurationSeconds
                              / (distanceMm * distanceMm);

        // Convert J/cm² to cal/cm²
        double incidentEnergyCal = energyJoules / 4.184;

        // Arc flash boundary: distance where incident energy = 1.2 cal/cm² (onset of 2nd degree burn)
        // D_boundary = sqrt(5.12e5 × V × Iarc × t / (1.2 × 4.184)) in mm, then convert to inches
        double boundaryMm = Math.Sqrt(5.12e5 * systemVoltageKV * arcingCurrentKA * arcDurationSeconds
                                       / (1.2 * 4.184));
        double boundaryInches = boundaryMm / 25.4;

        // Determine hazard category
        int category = 4;
        string ppe = "DANGER: Incident energy exceeds 40 cal/cm². Do not work energized.";
        foreach (var (maxEnergy, cat, ppeDesc) in HazardCategories)
        {
            if (incidentEnergyCal <= maxEnergy)
            {
                category = cat;
                ppe = ppeDesc;
                break;
            }
        }

        return new ArcFlashResult
        {
            NodeId = node.Id,
            NodeName = node.Name,
            BoltedFaultCurrentKA = boltedFaultKA,
            ArcingCurrentKA = Math.Round(arcingCurrentKA, 2),
            IncidentEnergyCal = Math.Round(incidentEnergyCal, 2),
            ArcFlashBoundaryInches = Math.Round(boundaryInches, 1),
            HazardCategory = category,
            RequiredPPE = ppe,
        };
    }

    /// <summary>
    /// Computes arc flash results for all nodes in the distribution graph.
    /// </summary>
    public List<ArcFlashResult> CalculateArcFlashAll(
        List<DistributionNode> roots,
        double workingDistanceInches = 18.0,
        double arcDurationSeconds = 0.5,
        double systemVoltageV = 480.0)
    {
        var results = new List<ArcFlashResult>();
        foreach (var root in roots)
            CollectArcFlashRecursive(root, workingDistanceInches, arcDurationSeconds, systemVoltageV, results);
        return results;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void ValidateAICRecursive(DistributionNode node, List<ShortCircuitResult> results)
    {
        double equipmentAIC = GetEquipmentAIC(node);
        double available = node.FaultCurrentKA;
        bool adequate = available <= 0 || equipmentAIC >= available;
        double margin = available > 0 ? ((equipmentAIC - available) / available) * 100.0 : 100.0;

        results.Add(new ShortCircuitResult
        {
            NodeId = node.Id,
            NodeName = node.Name,
            NodeType = node.NodeType,
            AvailableFaultKA = available,
            EquipmentAICKA = equipmentAIC,
            IsAdequate = adequate,
            MarginPercent = Math.Round(margin, 1),
        });

        foreach (var child in node.Children)
            ValidateAICRecursive(child, results);
    }

    private static double GetEquipmentAIC(DistributionNode node)
    {
        return node.Component switch
        {
            PanelComponent panel => panel.AICRatingKA,
            TransformerComponent => 100.0, // Transformers typically withstand rated fault
            BusComponent => 65.0,          // Default bus withstand
            PowerSourceComponent => 200.0, // Source is inherently rated
            TransferSwitchComponent => 65.0,
            _ => 10.0,
        };
    }

    private void CollectArcFlashRecursive(
        DistributionNode node,
        double workingDistanceInches,
        double arcDurationSeconds,
        double systemVoltageV,
        List<ArcFlashResult> results)
    {
        // Determine voltage from component where possible
        double voltage = systemVoltageV;
        if (node.Component is TransformerComponent xfmr)
            voltage = xfmr.SecondaryVoltage;
        else if (node.Component is BusComponent bus)
            voltage = bus.Voltage;
        else if (node.Component is PowerSourceComponent ps)
            voltage = ps.Voltage;

        results.Add(CalculateArcFlash(node, workingDistanceInches, arcDurationSeconds, voltage));

        foreach (var child in node.Children)
            CollectArcFlashRecursive(child, workingDistanceInches, arcDurationSeconds, voltage, results);
    }
}
