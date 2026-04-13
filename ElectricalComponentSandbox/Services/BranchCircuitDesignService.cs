using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Automated branch circuit designer.
///
/// Given a load description (VA, voltage, phase, distance), this service
/// auto-selects the complete branch circuit design: breaker, wire, conduit,
/// and ground — all per NEC requirements.
///
/// Combines: NecAmpacityService, ConduitFillService, FeederVoltageDropService,
/// GroundingService, ElectricalCalculationService
/// </summary>
public class BranchCircuitDesignService
{
    private readonly NecAmpacityService _ampacity;
    private readonly ConduitFillService _conduitFill;

    public BranchCircuitDesignService(
        NecAmpacityService ampacity,
        ConduitFillService conduitFill)
    {
        _ampacity = ampacity;
        _conduitFill = conduitFill;
    }

    /// <summary>
    /// Standard NEC OCPD sizes per 240.6(A).
    /// </summary>
    private static readonly int[] StandardOcpdAmps =
    [
        15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100,
        110, 125, 150, 175, 200, 225, 250, 300, 350, 400,
        450, 500, 600, 700, 800, 1000, 1200, 1600, 2000, 2500, 3000,
    ];

    /// <summary>
    /// Wire ampacity table (NEC 310.16, 75°C column, copper).
    /// </summary>
    private static readonly (string Size, double Amps75C)[] CopperAmpacity75C =
    [
        ("14", 20), ("12", 25), ("10", 35), ("8", 50),
        ("6", 65), ("4", 85), ("3", 100), ("2", 115),
        ("1", 130), ("1/0", 150), ("2/0", 175), ("3/0", 200),
        ("4/0", 230), ("250", 255), ("300", 285), ("350", 310),
        ("400", 335), ("500", 380),
    ];

    /// <summary>
    /// Wire ampacity table (NEC 310.16, 75°C column, aluminum).
    /// </summary>
    private static readonly (string Size, double Amps75C)[] AluminumAmpacity75C =
    [
        ("12", 20), ("10", 30), ("8", 40),
        ("6", 50), ("4", 65), ("3", 75), ("2", 90),
        ("1", 100), ("1/0", 120), ("2/0", 135), ("3/0", 155),
        ("4/0", 180), ("250", 205), ("300", 230), ("350", 250),
        ("400", 270), ("500", 310),
    ];

    /// <summary>Input for branch circuit auto-design.</summary>
    public record BranchCircuitInput
    {
        /// <summary>Connected load in VA.</summary>
        public double LoadVA { get; init; }

        /// <summary>System voltage (e.g. 120, 208, 277, 480).</summary>
        public double Voltage { get; init; } = 120;

        /// <summary>Number of poles (1, 2, or 3).</summary>
        public int Poles { get; init; } = 1;

        /// <summary>One-way wire run length in feet.</summary>
        public double LengthFeet { get; init; } = 100;

        /// <summary>Conductor material preference.</summary>
        public ConductorMaterial Material { get; init; } = ConductorMaterial.Copper;

        /// <summary>Conduit material preference.</summary>
        public ConduitMaterialType ConduitMaterial { get; init; } = ConduitMaterialType.EMT;

        /// <summary>Power factor (0-1). Default 1.0 for resistive loads.</summary>
        public double PowerFactor { get; init; } = 1.0;

        /// <summary>Max allowable voltage drop percent (NEC recommends 3% branch).</summary>
        public double MaxVoltageDropPercent { get; init; } = 3.0;

        /// <summary>Load classification.</summary>
        public LoadClassification Classification { get; init; } = LoadClassification.Power;

        /// <summary>Continuous load flag (NEC 210.20: 125% for continuous).</summary>
        public bool IsContinuous { get; init; }

        /// <summary>Description for the designed circuit.</summary>
        public string Description { get; init; } = "";
    }

    /// <summary>Auto-designed branch circuit result.</summary>
    public record BranchCircuitDesign
    {
        public double LoadAmps { get; init; }
        public double DesignAmps { get; init; }
        public int OcpdAmps { get; init; }
        public string WireSize { get; init; } = "";
        public string GroundSize { get; init; } = "";
        public string ConduitSize { get; init; } = "";
        public double VoltageDropPercent { get; init; }
        public double ConduitFillPercent { get; init; }
        public bool VoltageDropOk { get; init; }
        public bool ConduitFillOk { get; init; }
        public ConductorMaterial Material { get; init; }
        public ConduitMaterialType ConduitMaterial { get; init; }
        public int ConductorCount { get; init; }
        public List<string> Warnings { get; init; } = new();
        public List<string> NecReferences { get; init; } = new();
        public string Description { get; init; } = "";
        public bool IsValid { get; init; }
    }

    /// <summary>
    /// Designs a complete branch circuit for the given load.
    /// </summary>
    public BranchCircuitDesign Design(BranchCircuitInput input)
    {
        var warnings = new List<string>();
        var necRefs = new List<string>();

        // 1. Calculate load amps
        double divisor = input.Poles switch
        {
            1 => input.Voltage,
            2 => input.Voltage, // line-to-line for 2P
            3 => input.Voltage * 1.732,
            _ => input.Voltage,
        };
        double loadAmps = divisor > 0 ? input.LoadVA / divisor : 0;

        // 2. Apply continuous load factor (NEC 210.20)
        double designAmps = input.IsContinuous ? loadAmps * 1.25 : loadAmps;
        necRefs.Add("NEC 210.20 (continuous load 125%)");

        // 3. Select OCPD
        int ocpdAmps = SelectOcpd(designAmps);
        necRefs.Add("NEC 240.6(A) (standard OCPD sizes)");

        // NEC 210.3: minimum 15A branch circuit
        if (ocpdAmps < 15)
        {
            ocpdAmps = 15;
            warnings.Add("OCPD increased to minimum 15A per NEC 210.3");
        }

        // 4. Select wire size — must carry OCPD amps (NEC 240.4)
        string wireSize = SelectWireSize(ocpdAmps, input.Material);
        necRefs.Add("NEC 310.16 (conductor ampacity at 75°C)");
        necRefs.Add("NEC 240.4 (conductor protection)");

        // 5. Check voltage drop and upsize wire if needed
        double vdPercent = CalculateVoltageDropPercent(
            wireSize, input.Material, input.LengthFeet, loadAmps, input.Voltage, input.Poles);

        if (vdPercent > input.MaxVoltageDropPercent)
        {
            // Try upsizing wire for VD
            string? upsized = UpsizeForVoltageDrop(
                wireSize, input.Material, input.LengthFeet, loadAmps,
                input.Voltage, input.Poles, input.MaxVoltageDropPercent);

            if (upsized != null)
            {
                wireSize = upsized;
                vdPercent = CalculateVoltageDropPercent(
                    wireSize, input.Material, input.LengthFeet, loadAmps, input.Voltage, input.Poles);
                warnings.Add($"Wire upsized to #{wireSize} for voltage drop compliance");
            }
            else
            {
                warnings.Add($"Voltage drop {vdPercent:F1}% exceeds {input.MaxVoltageDropPercent}% — no standard wire size sufficient at this distance");
            }
        }
        necRefs.Add("NEC 210.19(A) Info Note 4 (3% branch VD)");

        // 6. Ground conductor (NEC 250.122)
        string groundSize = GroundingService.GetMinEGCSize(ocpdAmps, input.Material);
        necRefs.Add("NEC 250.122 (EGC sizing)");

        // 7. Count conductors and size conduit
        int conductorCount = GetConductorCount(input.Poles);
        var allWires = new List<string>();
        // Hot conductors
        int hotCount = input.Poles;
        for (int i = 0; i < hotCount; i++) allWires.Add(wireSize);
        // Neutral (included for 1P and 3P-4W)
        if (input.Poles <= 2) allWires.Add(wireSize);
        // Ground
        allWires.Add(groundSize);

        string? conduitSize = _conduitFill.RecommendConduitSize(input.ConduitMaterial, allWires);
        double fillPercent = 0;
        bool fillOk = true;

        if (conduitSize != null)
        {
            var fillResult = _conduitFill.CalculateFill(conduitSize, input.ConduitMaterial, allWires);
            fillPercent = fillResult.FillPercent;
            fillOk = !fillResult.ExceedsCode;
        }
        else
        {
            conduitSize = ">4\"";
            fillOk = false;
            warnings.Add("No standard conduit size accommodates these conductors");
        }
        necRefs.Add("NEC Ch.9 Table 1 (conduit fill)");

        // 8. Validate NEC 210.21(B) — receptacle rating vs circuit rating
        if (input.Classification == LoadClassification.Power && ocpdAmps > 50)
        {
            warnings.Add("Receptacle circuits > 50A require specific outlet ratings per NEC 210.21(B)");
        }

        bool isValid = vdPercent <= input.MaxVoltageDropPercent && fillOk;

        return new BranchCircuitDesign
        {
            LoadAmps = Math.Round(loadAmps, 2),
            DesignAmps = Math.Round(designAmps, 2),
            OcpdAmps = ocpdAmps,
            WireSize = wireSize,
            GroundSize = groundSize,
            ConduitSize = conduitSize,
            VoltageDropPercent = Math.Round(vdPercent, 2),
            ConduitFillPercent = Math.Round(fillPercent, 2),
            VoltageDropOk = vdPercent <= input.MaxVoltageDropPercent,
            ConduitFillOk = fillOk,
            Material = input.Material,
            ConduitMaterial = input.ConduitMaterial,
            ConductorCount = allWires.Count,
            Warnings = warnings,
            NecReferences = necRefs,
            Description = input.Description,
            IsValid = isValid,
        };
    }

    /// <summary>
    /// Designs multiple branch circuits from a list of loads and returns
    /// aggregate summary.
    /// </summary>
    public List<BranchCircuitDesign> DesignAll(IReadOnlyList<BranchCircuitInput> inputs) =>
        inputs.Select(Design).ToList();

    // ── Internals ────────────────────────────────────────────────────────────

    internal static int SelectOcpd(double amps)
    {
        foreach (var size in StandardOcpdAmps)
        {
            if (size >= amps) return size;
        }
        return StandardOcpdAmps[^1];
    }

    internal static string SelectWireSize(double requiredAmps, ConductorMaterial material)
    {
        var table = material == ConductorMaterial.Aluminum ? AluminumAmpacity75C : CopperAmpacity75C;

        foreach (var (size, amps) in table)
        {
            if (amps >= requiredAmps) return size;
        }
        return table[^1].Size; // largest available
    }

    internal static double CalculateVoltageDropPercent(
        string wireSize, ConductorMaterial material,
        double lengthFeet, double amps, double voltage, int poles)
    {
        double resistance = GetResistancePerKFt(wireSize, material);
        double factor = poles == 3 ? 1.732 : 2.0;
        double vd = factor * resistance * amps * lengthFeet / 1000.0;
        return voltage > 0 ? (vd / voltage) * 100.0 : 0;
    }

    private static string? UpsizeForVoltageDrop(
        string currentSize, ConductorMaterial material,
        double lengthFeet, double amps, double voltage, int poles,
        double maxVdPercent)
    {
        var table = material == ConductorMaterial.Aluminum ? AluminumAmpacity75C : CopperAmpacity75C;
        bool pastCurrent = false;

        foreach (var (size, _) in table)
        {
            if (size == currentSize) { pastCurrent = true; continue; }
            if (!pastCurrent) continue;

            double vd = CalculateVoltageDropPercent(size, material, lengthFeet, amps, voltage, poles);
            if (vd <= maxVdPercent) return size;
        }
        return null;
    }

    private static int GetConductorCount(int poles) => poles switch
    {
        1 => 3,  // 1 hot + 1 neutral + 1 ground
        2 => 4,  // 2 hot + 1 neutral + 1 ground
        3 => 5,  // 3 hot + 1 neutral + 1 ground
        _ => 3,
    };

    /// <summary>
    /// DC resistance per 1000 ft (NEC Chapter 9, Table 8 — uncoated copper/aluminum).
    /// </summary>
    private static readonly Dictionary<string, double> CopperResistance = new()
    {
        ["14"] = 3.14, ["12"] = 1.98, ["10"] = 1.24, ["8"] = 0.778,
        ["6"] = 0.491, ["4"] = 0.308, ["3"] = 0.245, ["2"] = 0.194,
        ["1"] = 0.154, ["1/0"] = 0.122, ["2/0"] = 0.0967, ["3/0"] = 0.0766,
        ["4/0"] = 0.0608, ["250"] = 0.0515, ["300"] = 0.0429, ["350"] = 0.0367,
        ["400"] = 0.0321, ["500"] = 0.0258,
    };

    private static readonly Dictionary<string, double> AluminumResistance = new()
    {
        ["12"] = 3.25, ["10"] = 2.04, ["8"] = 1.28,
        ["6"] = 0.808, ["4"] = 0.508, ["3"] = 0.403, ["2"] = 0.319,
        ["1"] = 0.253, ["1/0"] = 0.201, ["2/0"] = 0.159, ["3/0"] = 0.126,
        ["4/0"] = 0.100, ["250"] = 0.0847, ["300"] = 0.0707, ["350"] = 0.0605,
        ["400"] = 0.0529, ["500"] = 0.0424,
    };

    private static double GetResistancePerKFt(string wireSize, ConductorMaterial material)
    {
        var table = material == ConductorMaterial.Aluminum ? AluminumResistance : CopperResistance;
        return table.TryGetValue(wireSize, out var r) ? r : 999;
    }
}
