using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Equipment grounding conductor and grounding electrode conductor sizing per NEC 2023.
/// NEC Table 250.122: EGC size based on upstream OCPD rating.
/// NEC Table 250.66: Grounding electrode conductor based on largest service conductor.
/// </summary>
public static class GroundingService
{
    // ── NEC Table 250.122 — Equipment Grounding Conductor (EGC) ──────────────
    // OCPD rating → minimum EGC size (AWG/kcmil) for Copper and Aluminum

    private static readonly (int MaxOCPD, string CopperSize, string AluminumSize)[] Table250_122 =
    {
        (15,    "14",   "12"),
        (20,    "12",   "10"),
        (30,    "10",   "8"),
        (40,    "10",   "8"),
        (60,    "10",   "8"),
        (100,   "8",    "6"),
        (200,   "6",    "4"),
        (300,   "4",    "2"),
        (400,   "3",    "1"),
        (500,   "2",    "1/0"),
        (600,   "1",    "2/0"),
        (800,   "1/0",  "3/0"),
        (1000,  "2/0",  "4/0"),
        (1200,  "3/0",  "250"),
        (1600,  "4/0",  "350"),
        (2000,  "250",  "400"),
        (2500,  "350",  "600"),
        (3000,  "400",  "600"),
        (4000,  "500",  "750"),
        (5000,  "700",  "1000"),
        (6000,  "800",  "1200"),
    };

    // ── NEC Table 250.66 — Grounding Electrode Conductor (GEC) ──────────────
    // Largest ungrounded service conductor (Cu) → minimum GEC size (Cu / Al)

    private static readonly (string MaxServiceConductorCu, string CopperGEC, string AluminumGEC)[] Table250_66 =
    {
        ("2",     "8",    "6"),
        ("1",     "6",    "4"),
        ("1/0",   "6",    "4"),
        ("2/0",   "4",    "2"),
        ("3/0",   "2",    "1/0"),
        ("4/0",   "2",    "1/0"),
        ("250",   "2",    "1/0"),
        ("300",   "2",    "1/0"),
        ("350",   "1/0",  "3/0"),
        ("400",   "1/0",  "3/0"),
        ("500",   "1/0",  "3/0"),
        ("600",   "2/0",  "4/0"),
        ("700",   "2/0",  "4/0"),
        ("750",   "2/0",  "4/0"),
        ("800",   "2/0",  "4/0"),
        ("900",   "3/0",  "250"),
        ("1000",  "3/0",  "250"),
        ("1250",  "3/0",  "250"),
    };

    // Wire size ordering for comparison (smallest to largest)
    private static readonly string[] SizeOrder =
    {
        "14", "12", "10", "8", "6", "4", "3", "2", "1",
        "1/0", "2/0", "3/0", "4/0",
        "250", "300", "350", "400", "500", "600", "700", "750", "800", "900", "1000", "1200"
    };

    /// <summary>
    /// Returns the minimum equipment grounding conductor size per NEC Table 250.122
    /// based on the upstream overcurrent protection device (OCPD) rating.
    /// </summary>
    public static string GetMinEGCSize(int ocpdAmps, ConductorMaterial material = ConductorMaterial.Copper)
    {
        foreach (var (maxOcpd, cuSize, alSize) in Table250_122)
        {
            if (ocpdAmps <= maxOcpd)
                return material == ConductorMaterial.Copper ? cuSize : alSize;
        }

        // Above table maximum, return largest listed size
        var last = Table250_122[^1];
        return material == ConductorMaterial.Copper ? last.CopperSize : last.AluminumSize;
    }

    /// <summary>
    /// Returns the minimum grounding electrode conductor size per NEC Table 250.66
    /// based on the largest ungrounded service-entrance conductor.
    /// </summary>
    public static string GetMinGECSize(string largestServiceConductor, ConductorMaterial material = ConductorMaterial.Copper)
    {
        int svcIndex = SizeIndex(largestServiceConductor);

        // If the service conductor size is not in our standard ordering,
        // try to parse it as a kcmil number. If it exceeds the last table entry,
        // return the largest GEC size.
        if (svcIndex < 0)
        {
            if (int.TryParse(largestServiceConductor, out int kcmil))
            {
                var lastTableEntry = Table250_66[^1];
                int lastIndex = SizeIndex(lastTableEntry.MaxServiceConductorCu);
                if (lastIndex < 0 || kcmil > 1250) // 1250 is the last entry in table
                {
                    return material == ConductorMaterial.Copper ? lastTableEntry.CopperGEC : lastTableEntry.AluminumGEC;
                }
            }
            // Unknown size format, fall through to table scan
        }

        foreach (var (maxSvc, cuGEC, alGEC) in Table250_66)
        {
            int tableIndex = SizeIndex(maxSvc);
            if (svcIndex <= tableIndex)
                return material == ConductorMaterial.Copper ? cuGEC : alGEC;
        }

        // Above table maximum
        var last = Table250_66[^1];
        return material == ConductorMaterial.Copper ? last.CopperGEC : last.AluminumGEC;
    }

    /// <summary>
    /// Validates that a circuit's ground wire size meets the NEC 250.122 minimum
    /// for its upstream OCPD (breaker trip amps).
    /// Returns null if adequate, or a violation description if undersized.
    /// </summary>
    public static GroundingValidationResult ValidateGroundSize(Circuit circuit)
    {
        string minSize = GetMinEGCSize(circuit.Breaker.TripAmps, circuit.Wire.Material);
        string actual = circuit.Wire.GroundSize;

        bool adequate = SizeIndex(actual) >= SizeIndex(minSize);

        return new GroundingValidationResult
        {
            CircuitId = circuit.Id,
            CircuitDescription = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
            OCPDAmps = circuit.Breaker.TripAmps,
            Material = circuit.Wire.Material,
            MinimumEGCSize = minSize,
            ActualGroundSize = actual,
            IsAdequate = adequate,
        };
    }

    /// <summary>
    /// Validates ground sizing for all circuits.
    /// </summary>
    public static List<GroundingValidationResult> ValidateAll(IEnumerable<Circuit> circuits)
    {
        return circuits.Select(ValidateGroundSize).ToList();
    }

    /// <summary>
    /// Returns 0-based index in the size ordering. Returns -1 if not found.
    /// </summary>
    internal static int SizeIndex(string wireSize)
    {
        for (int i = 0; i < SizeOrder.Length; i++)
        {
            if (string.Equals(SizeOrder[i], wireSize, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}

/// <summary>
/// Result of an EGC sizing validation for a circuit.
/// </summary>
public record GroundingValidationResult
{
    public string CircuitId { get; init; } = "";
    public string CircuitDescription { get; init; } = "";
    public int OCPDAmps { get; init; }
    public ConductorMaterial Material { get; init; }
    public string MinimumEGCSize { get; init; } = "";
    public string ActualGroundSize { get; init; } = "";
    public bool IsAdequate { get; init; }
}
