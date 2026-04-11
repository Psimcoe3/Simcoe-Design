using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC-based electrical calculations for construction layout:
/// voltage drop, load analysis, wire sizing, and fault current.
/// Reference: NFPA 70 (NEC) 2023 Edition.
/// </summary>
public class ElectricalCalculationService
{
    // ── Wire resistance per 1000 ft (NEC Chapter 9, Table 8) ─────────────────
    // Uncoated copper, DC resistance at 75°C (ohms per 1000 ft)

    private static readonly Dictionary<string, double> CopperResistancePer1000Ft = new()
    {
        ["14"]  = 3.14,
        ["12"]  = 1.98,
        ["10"]  = 1.24,
        ["8"]   = 0.778,
        ["6"]   = 0.491,
        ["4"]   = 0.308,
        ["3"]   = 0.245,
        ["2"]   = 0.194,
        ["1"]   = 0.154,
        ["1/0"] = 0.122,
        ["2/0"] = 0.0967,
        ["3/0"] = 0.0766,
        ["4/0"] = 0.0608,
        ["250"] = 0.0515,
        ["300"] = 0.0429,
        ["350"] = 0.0367,
        ["400"] = 0.0321,
        ["500"] = 0.0258,
    };

    private static readonly Dictionary<string, double> AluminumResistancePer1000Ft = new()
    {
        ["12"]  = 3.25,
        ["10"]  = 2.04,
        ["8"]   = 1.28,
        ["6"]   = 0.808,
        ["4"]   = 0.508,
        ["3"]   = 0.403,
        ["2"]   = 0.319,
        ["1"]   = 0.253,
        ["1/0"] = 0.201,
        ["2/0"] = 0.159,
        ["3/0"] = 0.126,
        ["4/0"] = 0.100,
        ["250"] = 0.0847,
        ["300"] = 0.0707,
        ["350"] = 0.0605,
        ["400"] = 0.0529,
        ["500"] = 0.0424,
    };

    // ── Wire ampacity (NEC Table 310.16, 75°C column) ────────────────────────

    private static readonly Dictionary<string, int> CopperAmpacity75C = new()
    {
        ["14"]  = 20,
        ["12"]  = 25,
        ["10"]  = 35,
        ["8"]   = 50,
        ["6"]   = 65,
        ["4"]   = 85,
        ["3"]   = 100,
        ["2"]   = 115,
        ["1"]   = 130,
        ["1/0"] = 150,
        ["2/0"] = 175,
        ["3/0"] = 200,
        ["4/0"] = 230,
        ["250"] = 255,
        ["300"] = 285,
        ["350"] = 310,
        ["400"] = 335,
        ["500"] = 380,
    };

    private static readonly Dictionary<string, int> AluminumAmpacity75C = new()
    {
        ["12"]  = 20,
        ["10"]  = 30,
        ["8"]   = 40,
        ["6"]   = 50,
        ["4"]   = 65,
        ["3"]   = 75,
        ["2"]   = 90,
        ["1"]   = 100,
        ["1/0"] = 120,
        ["2/0"] = 135,
        ["3/0"] = 155,
        ["4/0"] = 180,
        ["250"] = 205,
        ["300"] = 230,
        ["350"] = 250,
        ["400"] = 270,
        ["500"] = 310,
    };

    // ── Voltage Drop ─────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates voltage drop for a circuit per NEC 210.19(A) Informational Note No. 4.
    /// Uses simplified single-phase formula: Vd = 2 × I × R × L / 1000
    /// For three-phase: Vd = √3 × I × R × L / 1000
    /// </summary>
    public VoltageDropResult CalculateVoltageDrop(Circuit circuit)
    {
        if (circuit.Voltage <= 0 || circuit.WireLengthFeet <= 0)
            return VoltageDropResult.Invalid;

        double current = circuit.DemandLoadVA / circuit.Voltage;
        if (circuit.Poles > 1)
            current = circuit.DemandLoadVA / (circuit.Voltage * Math.Sqrt(3));

        double resistancePer1000 = GetResistancePer1000Ft(
            circuit.Wire.Size, circuit.Wire.Material);

        if (resistancePer1000 <= 0)
            return VoltageDropResult.Invalid;

        double multiplier = circuit.Poles >= 3 ? Math.Sqrt(3) : 2.0;
        double voltageDrop = multiplier * current * resistancePer1000 * circuit.WireLengthFeet / 1000.0;
        double percentDrop = (voltageDrop / circuit.Voltage) * 100.0;

        return new VoltageDropResult
        {
            IsValid = true,
            VoltageDropVolts = voltageDrop,
            VoltageDropPercent = percentDrop,
            CurrentAmps = current,
            VoltageAtLoad = circuit.Voltage - voltageDrop,
            ExceedsNecRecommendation = percentDrop > 3.0,
            ExceedsTotalRecommendation = percentDrop > 5.0
        };
    }

    // ── Wire Sizing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Recommends minimum wire size for a circuit based on ampacity and voltage drop.
    /// Checks both NEC Table 310.16 ampacity and the 3% branch / 5% total VD recommendation.
    /// </summary>
    public WireSizeRecommendation RecommendWireSize(Circuit circuit, double maxVoltageDropPercent = 3.0)
    {
        double current = circuit.DemandLoadVA / circuit.Voltage;
        if (circuit.Poles > 1)
            current = circuit.DemandLoadVA / (circuit.Voltage * Math.Sqrt(3));

        var sizes = GetSizeList();
        var ampacityTable = circuit.Wire.Material == ConductorMaterial.Copper
            ? CopperAmpacity75C
            : AluminumAmpacity75C;

        string? ampacitySize = null;
        string? voltageDropSize = null;

        // Find minimum size for ampacity
        foreach (var size in sizes)
        {
            if (ampacityTable.TryGetValue(size, out int ampacity) && ampacity >= current)
            {
                ampacitySize = size;
                break;
            }
        }

        // Find minimum size for voltage drop
        if (circuit.WireLengthFeet > 0 && maxVoltageDropPercent > 0)
        {
            foreach (var size in sizes)
            {
                var testCircuit = new Circuit
                {
                    Voltage = circuit.Voltage,
                    ConnectedLoadVA = circuit.ConnectedLoadVA,
                    DemandFactor = circuit.DemandFactor,
                    Poles = circuit.Poles,
                    WireLengthFeet = circuit.WireLengthFeet,
                    Wire = new WireSpec { Size = size, Material = circuit.Wire.Material }
                };
                var vd = CalculateVoltageDrop(testCircuit);
                if (vd.IsValid && vd.VoltageDropPercent <= maxVoltageDropPercent)
                {
                    voltageDropSize = size;
                    break;
                }
            }
        }

        // Take the larger of the two
        int ampIdx = ampacitySize != null ? Array.IndexOf(sizes, ampacitySize) : sizes.Length - 1;
        int vdIdx = voltageDropSize != null ? Array.IndexOf(sizes, voltageDropSize) : sizes.Length - 1;
        int finalIdx = Math.Max(ampIdx, vdIdx);

        return new WireSizeRecommendation
        {
            RecommendedSize = sizes[finalIdx],
            AmpacityGoverning = ampIdx >= vdIdx,
            VoltageDropGoverning = vdIdx > ampIdx,
            CurrentAmps = current,
            MinSizeForAmpacity = ampacitySize ?? sizes[^1],
            MinSizeForVoltageDrop = voltageDropSize
        };
    }

    // ── Load Analysis ────────────────────────────────────────────────────────

    /// <summary>
    /// Analyzes total panel load and returns a summary.
    /// When <paramref name="distributionSystem"/> is provided it takes precedence over
    /// the legacy <see cref="PanelSchedule.VoltageConfig"/> enum.
    /// </summary>
    public PanelLoadSummary AnalyzePanelLoad(PanelSchedule schedule, DistributionSystemType? distributionSystem = null)
    {
        var activeCircuits = schedule.Circuits
            .Where(c => c.SlotType == CircuitSlotType.Circuit && c.IsPowerCircuit)
            .ToList();

        var (phA, phB, phC) = schedule.PhaseDemandVA;
        double maxPhase = Math.Max(phA, Math.Max(phB, phC));

        double lineVoltage;
        bool isThreePhase;

        if (distributionSystem != null)
        {
            lineVoltage = distributionSystem.LineVoltage;
            isThreePhase = distributionSystem.IsThreePhase;
        }
        else
        {
            lineVoltage = schedule.VoltageConfig switch
            {
                PanelVoltageConfig.V120_240_1Ph => 240,
                PanelVoltageConfig.V120_208_3Ph => 208,
                PanelVoltageConfig.V277_480_3Ph => 480,
                PanelVoltageConfig.V240_3Ph => 240,
                _ => 208
            };
            isThreePhase = schedule.VoltageConfig != PanelVoltageConfig.V120_240_1Ph;
        }

        double totalCurrent;
        if (isThreePhase)
            totalCurrent = schedule.TotalDemandVA / (lineVoltage * Math.Sqrt(3));
        else
            totalCurrent = schedule.TotalDemandVA / lineVoltage;

        double busUtilization = (totalCurrent / schedule.BusAmps) * 100.0;

        int spareSlots = schedule.Circuits.Count(c => c.SlotType == CircuitSlotType.Spare);
        int spaceSlots = schedule.Circuits.Count(c => c.SlotType == CircuitSlotType.Space);
        int usedPoles  = activeCircuits.Sum(c => c.Breaker.Poles);
        int totalSlots = schedule.IsMainLugsOnly
            ? schedule.BusAmps / 20
            : schedule.MainBreakerAmps / 20 * 2;
        int availableSpaces = Math.Max(0, totalSlots - usedPoles - spareSlots - spaceSlots);

        // Per-classification demand totals (active circuits only)
        var classificationTotals = activeCircuits
            .GroupBy(c => c.LoadClassification)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.DemandLoadVA));

        return new PanelLoadSummary
        {
            TotalConnectedVA = schedule.TotalConnectedVA,
            TotalDemandVA = schedule.TotalDemandVA,
            PhaseALoadVA = phA,
            PhaseBLoadVA = phB,
            PhaseCLoadVA = phC,
            MaxPhaseImbalanceVA = maxPhase - Math.Min(phA, Math.Min(phB, phC)),
            TotalCurrentAmps = totalCurrent,
            BusUtilizationPercent = busUtilization,
            IsOverloaded = totalCurrent > schedule.BusAmps,
            CircuitCount = activeCircuits.Count,
            SpareSlots = spareSlots,
            SpaceSlots = spaceSlots,
            AvailableSpaces = availableSpaces,
            ClassificationTotals = classificationTotals
        };
    }

    // ── Demand Load (NEC 220) ──────────────────────────────────────────────

    /// <summary>
    /// Calculates the total NEC 220 demand load for a collection of circuits by
    /// grouping them by <see cref="LoadClassification"/> and applying the matching
    /// <see cref="DemandSchedule"/> tiers. Circuits whose classification has no
    /// matching schedule pass through at 100%.
    /// Returns a <see cref="DemandLoadResult"/> with per-classification and total values.
    /// </summary>
    public static DemandLoadResult CalculateDemandLoad(
        IEnumerable<Circuit> circuits,
        IEnumerable<DemandSchedule> demandSchedules)
    {
        var scheduleMap = demandSchedules.ToDictionary(s => s.Classification);

        var groups = circuits
            .Where(c => c.SlotType == CircuitSlotType.Circuit && c.IsPowerCircuit)
            .GroupBy(c => c.LoadClassification);

        var details = new Dictionary<LoadClassification, DemandClassificationDetail>();
        double totalConnected = 0;
        double totalDemand = 0;

        foreach (var group in groups)
        {
            double connected = group.Sum(c => c.ConnectedLoadVA);
            double demand;

            if (scheduleMap.TryGetValue(group.Key, out var schedule))
                demand = schedule.Apply(connected);
            else
                demand = connected; // no schedule → 100%

            details[group.Key] = new DemandClassificationDetail
            {
                Classification = group.Key,
                ConnectedVA = connected,
                DemandVA = demand,
                Factor = connected > 0 ? demand / connected : 1.0
            };

            totalConnected += connected;
            totalDemand += demand;
        }

        return new DemandLoadResult
        {
            TotalConnectedVA = totalConnected,
            TotalDemandVA = totalDemand,
            OverallFactor = totalConnected > 0 ? totalDemand / totalConnected : 1.0,
            ClassificationDetails = details
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private double GetResistancePer1000Ft(string wireSize, ConductorMaterial material)
    {
        var table = material == ConductorMaterial.Copper
            ? CopperResistancePer1000Ft
            : AluminumResistancePer1000Ft;
        return table.TryGetValue(wireSize, out var r) ? r : 0;
    }

    private static string[] GetSizeList() => new[]
    {
        "14", "12", "10", "8", "6", "4", "3", "2", "1",
        "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500"
    };
}

// ── Result types ─────────────────────────────────────────────────────────────

public class VoltageDropResult
{
    public bool IsValid { get; init; }
    public double VoltageDropVolts { get; init; }
    public double VoltageDropPercent { get; init; }
    public double CurrentAmps { get; init; }
    public double VoltageAtLoad { get; init; }

    /// <summary>Exceeds NEC 210.19 recommended 3% for branch circuits</summary>
    public bool ExceedsNecRecommendation { get; init; }

    /// <summary>Exceeds NEC recommended 5% total (feeder + branch)</summary>
    public bool ExceedsTotalRecommendation { get; init; }

    public static VoltageDropResult Invalid => new() { IsValid = false };
}

public class WireSizeRecommendation
{
    public string RecommendedSize { get; init; } = "12";
    public bool AmpacityGoverning { get; init; }
    public bool VoltageDropGoverning { get; init; }
    public double CurrentAmps { get; init; }
    public string MinSizeForAmpacity { get; init; } = "12";
    public string? MinSizeForVoltageDrop { get; init; }
}

public class PanelLoadSummary
{
    public double TotalConnectedVA { get; init; }
    public double TotalDemandVA { get; init; }
    public double PhaseALoadVA { get; init; }
    public double PhaseBLoadVA { get; init; }
    public double PhaseCLoadVA { get; init; }
    public double MaxPhaseImbalanceVA { get; init; }
    public double TotalCurrentAmps { get; init; }
    public double BusUtilizationPercent { get; init; }
    public bool IsOverloaded { get; init; }

    /// <summary>Number of active (non-Spare, non-Space) circuits.</summary>
    public int CircuitCount { get; init; }

    /// <summary>Number of slots explicitly typed as Spare.</summary>
    public int SpareSlots { get; init; }

    /// <summary>Number of slots explicitly typed as Space.</summary>
    public int SpaceSlots { get; init; }

    /// <summary>Unallocated one-pole slots remaining in the panel after active + spare + space.</summary>
    public int AvailableSpaces { get; init; }

    /// <summary>Demand load in VA keyed by LoadClassification for active circuits.</summary>
    public Dictionary<LoadClassification, double> ClassificationTotals { get; init; } = new();
}

public class DemandLoadResult
{
    public double TotalConnectedVA { get; init; }
    public double TotalDemandVA { get; init; }

    /// <summary>Overall effective demand factor (TotalDemandVA / TotalConnectedVA).</summary>
    public double OverallFactor { get; init; }

    /// <summary>Per-classification breakdown of connected vs. demand load.</summary>
    public Dictionary<LoadClassification, DemandClassificationDetail> ClassificationDetails { get; init; } = new();
}

public class DemandClassificationDetail
{
    public LoadClassification Classification { get; init; }
    public double ConnectedVA { get; init; }
    public double DemandVA { get; init; }

    /// <summary>Effective demand factor for this classification (DemandVA / ConnectedVA).</summary>
    public double Factor { get; init; }
}
