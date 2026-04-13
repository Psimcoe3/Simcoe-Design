using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Panel phase balancing service.
/// Analyzes panel schedules for phase imbalance and recommends circuit-to-phase
/// reassignment to minimize neutral current and per-phase load deviation.
///
/// NEC 220.61 and general best practice target ≤10% phase imbalance.
/// Excessive imbalance causes:
/// - Increased neutral current (thermal risk in 3-phase 4-wire systems)
/// - Uneven voltage regulation
/// - Reduced transformer capacity utilization
/// </summary>
public static class PanelPhaseBalancingService
{
    /// <summary>Maximum recommended phase imbalance percentage.</summary>
    public const double MaxRecommendedImbalancePercent = 10.0;

    /// <summary>
    /// A proposed swap: move a circuit from one phase to another.
    /// </summary>
    public record PhaseSwapRecommendation
    {
        public string CircuitId { get; init; } = "";
        public string CircuitDescription { get; init; } = "";
        public string CurrentPhase { get; init; } = "";
        public string ProposedPhase { get; init; } = "";
        public double LoadVA { get; init; }
        public double ImbalanceBeforeVA { get; init; }
        public double ImbalanceAfterVA { get; init; }
    }

    /// <summary>
    /// Overall balance analysis result.
    /// </summary>
    public record PhaseBalanceAnalysis
    {
        public double PhaseALoadVA { get; init; }
        public double PhaseBLoadVA { get; init; }
        public double PhaseCLoadVA { get; init; }
        public double AverageLoadVA { get; init; }
        public double MaxImbalanceVA { get; init; }
        public double ImbalancePercent { get; init; }
        public bool IsAcceptable { get; init; }
        public double EstimatedNeutralCurrentAmps { get; init; }
        public List<PhaseSwapRecommendation> Recommendations { get; init; } = new();
        public double OptimizedImbalancePercent { get; init; }
        public double OptimizedPhaseAVA { get; init; }
        public double OptimizedPhaseBVA { get; init; }
        public double OptimizedPhaseCVA { get; init; }
    }

    /// <summary>
    /// Analyzes the panel schedule for phase imbalance and generates swap recommendations.
    /// </summary>
    public static PhaseBalanceAnalysis Analyze(PanelSchedule schedule)
    {
        var (a, b, c) = GetPhaseLoads(schedule);
        double avg = (a + b + c) / 3.0;
        double maxImbalance = Math.Max(Math.Abs(a - avg), Math.Max(Math.Abs(b - avg), Math.Abs(c - avg)));
        double imbalancePercent = avg > 0 ? (maxImbalance / avg) * 100 : 0;
        double neutralAmps = CalculateNeutralCurrent(a, b, c, schedule);

        // Generate swap recommendations
        var recommendations = GenerateSwapRecommendations(schedule, a, b, c);

        // Calculate optimized state
        double optA = a, optB = b, optC = c;
        foreach (var swap in recommendations)
        {
            ApplySwap(ref optA, ref optB, ref optC, swap);
        }
        double optAvg = (optA + optB + optC) / 3.0;
        double optMax = Math.Max(Math.Abs(optA - optAvg), Math.Max(Math.Abs(optB - optAvg), Math.Abs(optC - optAvg)));
        double optImbalance = optAvg > 0 ? (optMax / optAvg) * 100 : 0;

        return new PhaseBalanceAnalysis
        {
            PhaseALoadVA = Math.Round(a, 1),
            PhaseBLoadVA = Math.Round(b, 1),
            PhaseCLoadVA = Math.Round(c, 1),
            AverageLoadVA = Math.Round(avg, 1),
            MaxImbalanceVA = Math.Round(maxImbalance, 1),
            ImbalancePercent = Math.Round(imbalancePercent, 1),
            IsAcceptable = imbalancePercent <= MaxRecommendedImbalancePercent,
            EstimatedNeutralCurrentAmps = Math.Round(neutralAmps, 1),
            Recommendations = recommendations,
            OptimizedImbalancePercent = Math.Round(optImbalance, 1),
            OptimizedPhaseAVA = Math.Round(optA, 1),
            OptimizedPhaseBVA = Math.Round(optB, 1),
            OptimizedPhaseCVA = Math.Round(optC, 1),
        };
    }

    /// <summary>
    /// Applies recommended swaps to the schedule directly, returning the updated schedule.
    /// </summary>
    public static PanelSchedule ApplyRecommendations(PanelSchedule schedule, IEnumerable<PhaseSwapRecommendation> swaps)
    {
        foreach (var swap in swaps)
        {
            var circuit = schedule.Circuits.FirstOrDefault(c => c.Id == swap.CircuitId);
            if (circuit != null && circuit.Poles == 1)
            {
                circuit.Phase = swap.ProposedPhase;
            }
        }
        return schedule;
    }

    /// <summary>
    /// Calculates the imbalance percentage for given phase loads.
    /// </summary>
    public static double CalculateImbalancePercent(double phaseA, double phaseB, double phaseC)
    {
        double avg = (phaseA + phaseB + phaseC) / 3.0;
        if (avg <= 0) return 0;
        double maxDev = Math.Max(Math.Abs(phaseA - avg),
            Math.Max(Math.Abs(phaseB - avg), Math.Abs(phaseC - avg)));
        return (maxDev / avg) * 100;
    }

    private static (double A, double B, double C) GetPhaseLoads(PanelSchedule schedule)
    {
        double a = 0, b = 0, c = 0;
        foreach (var circuit in schedule.Circuits)
        {
            if (circuit.SlotType != CircuitSlotType.Circuit) continue;
            double loadPerPole = circuit.DemandLoadVA / circuit.Poles;
            foreach (var ch in circuit.Phase)
            {
                switch (ch)
                {
                    case 'A': a += loadPerPole; break;
                    case 'B': b += loadPerPole; break;
                    case 'C': c += loadPerPole; break;
                }
            }
        }
        return (a, b, c);
    }

    private static double CalculateNeutralCurrent(double phaseA, double phaseB, double phaseC, PanelSchedule schedule)
    {
        // For 3-phase 4-wire, neutral current = vector sum of unbalanced phase currents
        // Simplified: I_N = sqrt(I_A² + I_B² + I_C² - I_A*I_B - I_B*I_C - I_A*I_C)
        double voltage = schedule.VoltageConfig switch
        {
            PanelVoltageConfig.V120_208_3Ph => 120,
            PanelVoltageConfig.V277_480_3Ph => 277,
            _ => 120,
        };

        double iA = voltage > 0 ? phaseA / voltage : 0;
        double iB = voltage > 0 ? phaseB / voltage : 0;
        double iC = voltage > 0 ? phaseC / voltage : 0;

        // 120° phase angle neutral current formula
        double neutral = Math.Sqrt(iA * iA + iB * iB + iC * iC - iA * iB - iB * iC - iA * iC);
        return neutral;
    }

    private static List<PhaseSwapRecommendation> GenerateSwapRecommendations(
        PanelSchedule schedule, double currentA, double currentB, double currentC)
    {
        var recommendations = new List<PhaseSwapRecommendation>();

        // Only swap single-pole circuits
        var movableCircuits = schedule.Circuits
            .Where(c => c.SlotType == CircuitSlotType.Circuit && c.Poles == 1)
            .OrderByDescending(c => c.DemandLoadVA)
            .ToList();

        double a = currentA, b = currentB, c = currentC;

        // Greedy swap: iteratively move circuits from heaviest phase to lightest
        for (int iterations = 0; iterations < movableCircuits.Count; iterations++)
        {
            double avg = (a + b + c) / 3.0;
            double maxDev = Math.Max(Math.Abs(a - avg), Math.Max(Math.Abs(b - avg), Math.Abs(c - avg)));
            double currentImbalance = avg > 0 ? (maxDev / avg) * 100 : 0;

            if (currentImbalance <= MaxRecommendedImbalancePercent)
                break;

            // Find heaviest and lightest phases
            string heaviestPhase = GetHeaviestPhase(a, b, c);
            string lightestPhase = GetLightestPhase(a, b, c);

            double heaviestLoad = GetPhaseLoad(heaviestPhase, a, b, c);
            double lightestLoad = GetPhaseLoad(lightestPhase, a, b, c);
            double targetSwapVA = (heaviestLoad - lightestLoad) / 2.0;

            // Find best circuit to swap (closest to target without over-correcting)
            var bestCircuit = movableCircuits
                .Where(ci => ci.Phase == heaviestPhase)
                .OrderBy(ci => Math.Abs(ci.DemandLoadVA - targetSwapVA))
                .FirstOrDefault();

            if (bestCircuit == null) break;

            double loadVA = bestCircuit.DemandLoadVA;
            double beforeImbalance = maxDev;

            // Simulate swap
            double newA = a, newB = b, newC = c;
            AdjustPhase(ref newA, ref newB, ref newC, heaviestPhase, -loadVA);
            AdjustPhase(ref newA, ref newB, ref newC, lightestPhase, loadVA);

            double newAvg = (newA + newB + newC) / 3.0;
            double newMaxDev = Math.Max(Math.Abs(newA - newAvg),
                Math.Max(Math.Abs(newB - newAvg), Math.Abs(newC - newAvg)));

            // Only accept if it improves balance
            if (newMaxDev >= maxDev) break;

            recommendations.Add(new PhaseSwapRecommendation
            {
                CircuitId = bestCircuit.Id,
                CircuitDescription = bestCircuit.Description,
                CurrentPhase = heaviestPhase,
                ProposedPhase = lightestPhase,
                LoadVA = loadVA,
                ImbalanceBeforeVA = Math.Round(beforeImbalance, 1),
                ImbalanceAfterVA = Math.Round(newMaxDev, 1),
            });

            a = newA; b = newB; c = newC;
            movableCircuits.Remove(bestCircuit);
        }

        return recommendations;
    }

    private static string GetHeaviestPhase(double a, double b, double c)
    {
        if (a >= b && a >= c) return "A";
        if (b >= a && b >= c) return "B";
        return "C";
    }

    private static string GetLightestPhase(double a, double b, double c)
    {
        if (a <= b && a <= c) return "A";
        if (b <= a && b <= c) return "B";
        return "C";
    }

    private static double GetPhaseLoad(string phase, double a, double b, double c) =>
        phase switch { "A" => a, "B" => b, "C" => c, _ => 0 };

    private static void AdjustPhase(ref double a, ref double b, ref double c, string phase, double delta)
    {
        switch (phase)
        {
            case "A": a += delta; break;
            case "B": b += delta; break;
            case "C": c += delta; break;
        }
    }

    private static void ApplySwap(ref double a, ref double b, ref double c, PhaseSwapRecommendation swap)
    {
        AdjustPhase(ref a, ref b, ref c, swap.CurrentPhase, -swap.LoadVA);
        AdjustPhase(ref a, ref b, ref c, swap.ProposedPhase, swap.LoadVA);
    }
}
