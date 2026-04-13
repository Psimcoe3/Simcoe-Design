using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Electrical cost estimation service.
///
/// Produces material + labor cost estimates from project components, circuits, and
/// conduit runs. Uses RS Means / industry-standard unit costs for:
///   - Wire (per foot by size)
///   - Conduit (per foot by trade size and material)
///   - Breakers (each by frame/type)
///   - Panels (each by bus amps)
///   - Devices (receptacles, switches, fixtures)
///   - Labor (per NEC standard man-hours)
///
/// All costs are in USD.
/// </summary>
public static class ElectricalCostEstimationService
{
    // ── Wire unit cost per foot ──────────────────────────────────────────────

    private static readonly Dictionary<string, double> CopperWireCostPerFoot = new()
    {
        ["14"] = 0.12, ["12"] = 0.16, ["10"] = 0.25, ["8"] = 0.45,
        ["6"] = 0.65, ["4"] = 1.05, ["3"] = 1.25, ["2"] = 1.55,
        ["1"] = 2.00, ["1/0"] = 2.55, ["2/0"] = 3.15, ["3/0"] = 3.85,
        ["4/0"] = 4.80, ["250"] = 5.75, ["300"] = 6.80, ["350"] = 7.90,
        ["400"] = 9.10, ["500"] = 11.50, ["600"] = 14.50, ["750"] = 18.50,
        ["1000"] = 24.00,
    };

    private static readonly Dictionary<string, double> AluminumWireCostPerFoot = new()
    {
        ["6"] = 0.35, ["4"] = 0.55, ["3"] = 0.65, ["2"] = 0.80,
        ["1"] = 1.00, ["1/0"] = 1.25, ["2/0"] = 1.55, ["3/0"] = 1.95,
        ["4/0"] = 2.45, ["250"] = 3.00, ["300"] = 3.55, ["350"] = 4.10,
        ["400"] = 4.75, ["500"] = 5.95, ["600"] = 7.50, ["750"] = 9.50,
        ["1000"] = 12.50,
    };

    // ── Conduit unit cost per foot ───────────────────────────────────────────

    private static readonly Dictionary<string, double> EmtConduitCostPerFoot = new()
    {
        ["1/2"] = 0.55, ["3/4"] = 0.75, ["1"] = 1.10, ["1-1/4"] = 1.60,
        ["1-1/2"] = 1.95, ["2"] = 2.55, ["2-1/2"] = 4.20, ["3"] = 5.50,
        ["3-1/2"] = 7.00, ["4"] = 8.50,
    };

    // ── Breaker unit costs ───────────────────────────────────────────────────

    private static readonly Dictionary<int, double> BreakerCostByAmps = new()
    {
        [15] = 12, [20] = 14, [25] = 35, [30] = 38, [40] = 55, [50] = 65,
        [60] = 75, [70] = 95, [80] = 105, [90] = 115, [100] = 135,
        [125] = 185, [150] = 220, [175] = 255, [200] = 290,
        [225] = 350, [250] = 390, [300] = 480, [400] = 850,
    };

    // ── Panel costs by bus amps ──────────────────────────────────────────────

    private static readonly Dictionary<int, double> PanelCostByBusAmps = new()
    {
        [100] = 350, [125] = 420, [150] = 490, [200] = 650,
        [225] = 800, [400] = 1800, [600] = 3200, [800] = 4500,
    };

    // ── Labor rates (hours) per unit ─────────────────────────────────────────

    private const double LaborRatePerHour = 85.0; // journeyman electrician

    /// <summary>
    /// Individual line item in the cost estimate.
    /// </summary>
    public record CostLineItem
    {
        public string Category { get; init; } = "";
        public string Description { get; init; } = "";
        public double Quantity { get; init; }
        public string Unit { get; init; } = "ea";
        public double UnitMaterialCost { get; init; }
        public double UnitLaborHours { get; init; }
        public double TotalMaterialCost => Math.Round(Quantity * UnitMaterialCost, 2);
        public double TotalLaborHours => Math.Round(Quantity * UnitLaborHours, 2);
        public double TotalLaborCost => Math.Round(TotalLaborHours * LaborRatePerHour, 2);
        public double TotalCost => TotalMaterialCost + TotalLaborCost;
    }

    /// <summary>
    /// Complete cost estimate for a project.
    /// </summary>
    public record CostEstimate
    {
        public string ProjectName { get; init; } = "";
        public List<CostLineItem> LineItems { get; init; } = new();
        public double TotalMaterialCost => LineItems.Sum(i => i.TotalMaterialCost);
        public double TotalLaborHours => LineItems.Sum(i => i.TotalLaborHours);
        public double TotalLaborCost => LineItems.Sum(i => i.TotalLaborCost);
        public double Subtotal => TotalMaterialCost + TotalLaborCost;
        public double OverheadAndProfit { get; init; }
        public double GrandTotal => Subtotal + OverheadAndProfit;
    }

    /// <summary>
    /// Input for cost estimation.
    /// </summary>
    public record CostEstimateInput
    {
        public string ProjectName { get; init; } = "";
        public List<Circuit> Circuits { get; init; } = new();
        public List<PanelSchedule> PanelSchedules { get; init; } = new();
        public List<WireRunInput> WireRuns { get; init; } = new();
        public List<ConduitRunInput> ConduitRuns { get; init; } = new();
        public double OverheadAndProfitPercent { get; init; } = 15.0;
    }

    /// <summary>Wire run for cost estimation.</summary>
    public record WireRunInput
    {
        public string WireSize { get; init; } = "12";
        public ConductorMaterial Material { get; init; } = ConductorMaterial.Copper;
        public double LengthFeet { get; init; }
        public int ConductorCount { get; init; } = 3;
        public string Description { get; init; } = "";
    }

    /// <summary>Conduit run for cost estimation.</summary>
    public record ConduitRunInput
    {
        public string TradeSize { get; init; } = "3/4";
        public double LengthFeet { get; init; }
        public string Description { get; init; } = "";
    }

    /// <summary>
    /// Generates a cost estimate from project inputs.
    /// </summary>
    public static CostEstimate Estimate(CostEstimateInput input)
    {
        var items = new List<CostLineItem>();

        // Panels
        foreach (var panel in input.PanelSchedules)
        {
            double panelCost = GetPanelCost(panel.BusAmps);
            items.Add(new CostLineItem
            {
                Category = "Panels",
                Description = $"Panel {panel.PanelName} ({panel.BusAmps}A bus)",
                Quantity = 1,
                Unit = "ea",
                UnitMaterialCost = panelCost,
                UnitLaborHours = GetPanelLaborHours(panel.BusAmps),
            });
        }

        // Breakers from circuits
        var breakerGroups = input.Circuits
            .Where(c => c.SlotType == CircuitSlotType.Circuit)
            .GroupBy(c => new { c.Breaker.TripAmps, c.Breaker.Poles })
            .ToList();

        foreach (var group in breakerGroups)
        {
            double brkCost = GetBreakerCost(group.Key.TripAmps) * group.Key.Poles;
            items.Add(new CostLineItem
            {
                Category = "Breakers",
                Description = $"{group.Key.TripAmps}A {group.Key.Poles}P breaker",
                Quantity = group.Count(),
                Unit = "ea",
                UnitMaterialCost = brkCost,
                UnitLaborHours = 0.25 * group.Key.Poles,
            });
        }

        // Wire runs
        foreach (var run in input.WireRuns)
        {
            double costPerFt = GetWireCostPerFoot(run.WireSize, run.Material);
            double totalFt = run.LengthFeet * run.ConductorCount;
            items.Add(new CostLineItem
            {
                Category = "Wire",
                Description = $"#{run.WireSize} {run.Material} ({run.ConductorCount}C) — {run.Description}",
                Quantity = totalFt,
                Unit = "ft",
                UnitMaterialCost = costPerFt,
                UnitLaborHours = GetWireLaborHoursPerFoot(run.WireSize),
            });
        }

        // Conduit runs
        foreach (var run in input.ConduitRuns)
        {
            double costPerFt = GetConduitCostPerFoot(run.TradeSize);
            items.Add(new CostLineItem
            {
                Category = "Conduit",
                Description = $"{run.TradeSize}\" EMT — {run.Description}",
                Quantity = run.LengthFeet,
                Unit = "ft",
                UnitMaterialCost = costPerFt,
                UnitLaborHours = GetConduitLaborHoursPerFoot(run.TradeSize),
            });
        }

        double subtotal = items.Sum(i => i.TotalCost);
        double overhead = subtotal * (input.OverheadAndProfitPercent / 100.0);

        return new CostEstimate
        {
            ProjectName = input.ProjectName,
            LineItems = items,
            OverheadAndProfit = Math.Round(overhead, 2),
        };
    }

    /// <summary>Gets wire cost per foot by size and material.</summary>
    public static double GetWireCostPerFoot(string wireSize, ConductorMaterial material)
    {
        var table = material == ConductorMaterial.Aluminum ? AluminumWireCostPerFoot : CopperWireCostPerFoot;
        return table.GetValueOrDefault(wireSize, 0.20);
    }

    /// <summary>Gets conduit cost per foot by trade size (EMT).</summary>
    public static double GetConduitCostPerFoot(string tradeSize)
    {
        return EmtConduitCostPerFoot.GetValueOrDefault(tradeSize, 1.10);
    }

    /// <summary>Gets breaker cost by trip amps.</summary>
    public static double GetBreakerCost(int tripAmps)
    {
        if (BreakerCostByAmps.TryGetValue(tripAmps, out double cost)) return cost;
        // Interpolate from nearest
        var sorted = BreakerCostByAmps.Keys.OrderBy(k => k).ToList();
        int closest = sorted.MinBy(k => Math.Abs(k - tripAmps));
        return BreakerCostByAmps[closest];
    }

    private static double GetPanelCost(int busAmps)
    {
        if (PanelCostByBusAmps.TryGetValue(busAmps, out double cost)) return cost;
        var sorted = PanelCostByBusAmps.Keys.OrderBy(k => k).ToList();
        int closest = sorted.MinBy(k => Math.Abs(k - busAmps));
        return PanelCostByBusAmps[closest];
    }

    private static double GetPanelLaborHours(int busAmps)
    {
        if (busAmps <= 100) return 4;
        if (busAmps <= 200) return 6;
        if (busAmps <= 400) return 10;
        return 14;
    }

    private static double GetWireLaborHoursPerFoot(string wireSize)
    {
        return wireSize switch
        {
            "14" or "12" or "10" => 0.008,
            "8" or "6" => 0.012,
            "4" or "3" or "2" or "1" => 0.018,
            _ => 0.025, // larger sizes
        };
    }

    private static double GetConduitLaborHoursPerFoot(string tradeSize)
    {
        return tradeSize switch
        {
            "1/2" => 0.035,
            "3/4" => 0.040,
            "1" => 0.050,
            "1-1/4" => 0.060,
            "1-1/2" => 0.070,
            "2" => 0.085,
            _ => 0.12,
        };
    }
}
