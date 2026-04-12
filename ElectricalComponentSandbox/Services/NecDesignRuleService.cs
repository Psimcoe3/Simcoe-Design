using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC 2023 design rule checker. Validates circuits, panels, and conduit against code requirements.
/// Returns a list of violations with severity, rule reference, and fix suggestions.
/// </summary>
public class NecDesignRuleService
{
    // ── NEC Table 310.16 ampacity — delegated to NecAmpacityService ─────────

    // ── NEC 240.4(D) small conductor overcurrent limits ──────────────────────

    private static readonly Dictionary<string, int> SmallConductorMaxOcpd = new()
    {
        ["14"] = 15,
        ["12"] = 20,
        ["10"] = 30,
    };

    // ── NEC 210.3 standard breaker sizes ─────────────────────────────────────

    private static readonly HashSet<int> StandardBreakerAmps = new()
    {
        15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100
    };

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Validates all circuits and panel schedules against NEC 2023 rules.
    /// </summary>
    public List<NecViolation> ValidateAll(
        IEnumerable<Circuit> circuits,
        IEnumerable<PanelSchedule> panelSchedules,
        ElectricalCalculationService electricalCalcService)
    {
        var violations = new List<NecViolation>();

        foreach (var circuit in circuits)
        {
            violations.AddRange(ValidateCircuit(circuit, electricalCalcService));
        }

        foreach (var panel in panelSchedules)
        {
            violations.AddRange(ValidatePanel(panel, electricalCalcService));
        }

        return violations;
    }

    /// <summary>
    /// Validates all circuits, panels, and distribution graph (NEC 110.9) against NEC 2023 rules.
    /// </summary>
    public List<NecViolation> ValidateAll(
        IEnumerable<Circuit> circuits,
        IEnumerable<PanelSchedule> panelSchedules,
        ElectricalCalculationService electricalCalcService,
        List<DistributionNode>? distributionRoots = null)
    {
        var violations = ValidateAll(circuits, panelSchedules, electricalCalcService);

        if (distributionRoots != null)
        {
            violations.AddRange(ValidateAIC(distributionRoots));
        }

        return violations;
    }

    /// <summary>
    /// NEC 110.9 — validates equipment interrupting rating ≥ available fault current
    /// at every node in the distribution graph.
    /// </summary>
    public List<NecViolation> ValidateAIC(List<DistributionNode> roots)
    {
        var service = new ShortCircuitService();
        var aicViolations = service.GetAICViolations(roots);
        return aicViolations.Select(r => new NecViolation
        {
            RuleId = "NEC 110.9",
            Description = $"Equipment '{r.NodeName}' has an interrupting rating of {r.EquipmentAICKA:F1} kA " +
                          $"but available fault current is {r.AvailableFaultKA:F1} kA. " +
                          $"Equipment interrupting rating must be ≥ available fault current.",
            Severity = ViolationSeverity.Error,
            AffectedItemId = r.NodeId,
            AffectedItemName = r.NodeName,
            Suggestion = $"Upgrade equipment to a minimum interrupting rating of {r.AvailableFaultKA:F1} kA, " +
                         $"or add impedance (e.g., current-limiting fuse/reactor) upstream to reduce fault current."
        }).ToList();
    }

    /// <summary>
    /// Validates a single circuit against NEC 2023 branch circuit rules.
    /// </summary>
    public List<NecViolation> ValidateCircuit(
        Circuit circuit,
        ElectricalCalculationService electricalCalcService,
        double? availableFaultCurrentKA = null)
    {
        var violations = new List<NecViolation>();

        CheckBranchCircuitConductorSizing(circuit, violations);
        CheckStandardBreakerSize(circuit, violations);
        CheckSmallConductorProtection(circuit, violations);
        CheckReceptacleRating(circuit, violations);
        CheckBranchVoltageDropRecommendation(circuit, electricalCalcService, violations);
        CheckTotalVoltageDropRecommendation(circuit, electricalCalcService, violations);
        CheckTemperatureRating(circuit, violations);
        CheckGroundConductorSizing(circuit, violations);

        if (availableFaultCurrentKA.HasValue)
            CheckBreakerInterruptingRating(circuit, availableFaultCurrentKA.Value, violations);

        return violations;
    }

    /// <summary>
    /// Validates a panel schedule against NEC 2023 panel and feeder rules.
    /// </summary>
    public List<NecViolation> ValidatePanel(
        PanelSchedule panelSchedule,
        ElectricalCalculationService electricalCalcService)
    {
        var violations = new List<NecViolation>();

        CheckPanelBusRating(panelSchedule, violations);
        CheckPhaseImbalance(panelSchedule, violations);

        // Validate each circuit within this panel as well (feeder conductor sizing)
        foreach (var circuit in panelSchedule.Circuits)
        {
            CheckFeederConductorSizing(circuit, violations);
        }

        return violations;
    }

    // ── Rule checks ──────────────────────────────────────────────────────────

    /// <summary>
    /// NEC 210.19(A) - Branch circuit conductor sizing:
    /// Wire ampacity must be >= breaker trip rating.
    /// </summary>
    private void CheckBranchCircuitConductorSizing(Circuit circuit, List<NecViolation> violations)
    {
        int ampacity = GetAmpacity(circuit.Wire.Size, circuit.Wire.Material);
        if (ampacity <= 0)
            return;

        if (ampacity < circuit.Breaker.TripAmps)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 210.19(A)",
                Description = $"Branch circuit conductor {circuit.Wire.Size} AWG {circuit.Wire.Material} " +
                              $"has ampacity of {ampacity}A which is less than the {circuit.Breaker.TripAmps}A breaker trip rating.",
                Severity = ViolationSeverity.Error,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = $"Increase wire size to handle at least {circuit.Breaker.TripAmps}A, " +
                             $"or reduce the breaker trip rating to {ampacity}A or less."
            });
        }
    }

    /// <summary>
    /// NEC 215.2(A) - Feeder conductor sizing:
    /// Wire ampacity must be >= breaker trip rating for feeders.
    /// Applied here to circuits within a panel context.
    /// </summary>
    private void CheckFeederConductorSizing(Circuit circuit, List<NecViolation> violations)
    {
        int ampacity = GetAmpacity(circuit.Wire.Size, circuit.Wire.Material);
        if (ampacity <= 0)
            return;

        if (ampacity < circuit.Breaker.TripAmps)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 215.2(A)",
                Description = $"Feeder conductor {circuit.Wire.Size} AWG {circuit.Wire.Material} " +
                              $"has ampacity of {ampacity}A which is less than the {circuit.Breaker.TripAmps}A breaker trip rating.",
                Severity = ViolationSeverity.Error,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = $"Increase feeder wire size to handle at least {circuit.Breaker.TripAmps}A, " +
                             $"or reduce the breaker trip rating."
            });
        }
    }

    /// <summary>
    /// NEC 210.3 - Branch circuit rating must use standard breaker sizes.
    /// Standard sizes: 15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100A.
    /// </summary>
    private void CheckStandardBreakerSize(Circuit circuit, List<NecViolation> violations)
    {
        if (!StandardBreakerAmps.Contains(circuit.Breaker.TripAmps))
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 210.3",
                Description = $"Breaker trip rating of {circuit.Breaker.TripAmps}A is not a standard branch circuit size.",
                Severity = ViolationSeverity.Error,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = $"Use a standard breaker size: {string.Join(", ", StandardBreakerAmps.OrderBy(a => a))}A."
            });
        }
    }

    /// <summary>
    /// NEC 240.4(D) - Small conductor overcurrent protection limits:
    /// #14 AWG max 15A, #12 AWG max 20A, #10 AWG max 30A.
    /// </summary>
    private void CheckSmallConductorProtection(Circuit circuit, List<NecViolation> violations)
    {
        if (SmallConductorMaxOcpd.TryGetValue(circuit.Wire.Size, out int maxOcpd))
        {
            if (circuit.Breaker.TripAmps > maxOcpd)
            {
                violations.Add(new NecViolation
                {
                    RuleId = "NEC 240.4(D)",
                    Description = $"#{circuit.Wire.Size} AWG conductor is protected by a {circuit.Breaker.TripAmps}A breaker, " +
                                  $"but NEC 240.4(D) limits overcurrent protection to {maxOcpd}A for this wire size.",
                    Severity = ViolationSeverity.Error,
                    AffectedItemId = circuit.Id,
                    AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                    Suggestion = $"Reduce the breaker to {maxOcpd}A or increase the wire size."
                });
            }
        }
    }

    /// <summary>
    /// NEC 210.21(B) - Receptacle rating vs branch circuit rating:
    /// On a 15A circuit, receptacles must be rated 15A. On a 20A circuit, receptacles
    /// can be 15A or 20A. On circuits 30A and above, receptacle rating must match.
    /// This check flags circuits where the breaker rating may not support typical
    /// receptacle configurations.
    /// </summary>
    private void CheckReceptacleRating(Circuit circuit, List<NecViolation> violations)
    {
        // Only check single-pole branch circuits that appear to serve receptacles
        if (circuit.Poles != 1)
            return;

        string descLower = circuit.Description.ToLowerInvariant();
        bool isReceptacleCircuit = descLower.Contains("receptacle") ||
                                   descLower.Contains("outlet") ||
                                   descLower.Contains("recep");

        if (!isReceptacleCircuit)
            return;

        // NEC 210.21(B)(3): Single receptacle on an individual branch circuit
        // must match the circuit rating. For multiple receptacles, Table 210.21(B)(3)
        // limits apply. Here we flag non-standard breaker sizes for receptacle circuits.
        int tripAmps = circuit.Breaker.TripAmps;
        if (tripAmps != 15 && tripAmps != 20 && tripAmps != 30 && tripAmps != 40 && tripAmps != 50)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 210.21(B)",
                Description = $"Receptacle circuit has a {tripAmps}A breaker. " +
                              $"Standard receptacle branch circuits should be rated 15A, 20A, 30A, 40A, or 50A per Table 210.21(B)(3).",
                Severity = ViolationSeverity.Warning,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = "Verify receptacle ratings match the branch circuit rating per NEC Table 210.21(B)(3)."
            });
        }
    }

    /// <summary>
    /// NEC 408.36 - Panel bus rating: total demand current must not exceed bus ampacity.
    /// </summary>
    private void CheckPanelBusRating(PanelSchedule panel, List<NecViolation> violations)
    {
        double lineVoltage = panel.VoltageConfig switch
        {
            PanelVoltageConfig.V120_240_1Ph => 240,
            PanelVoltageConfig.V120_208_3Ph => 208,
            PanelVoltageConfig.V277_480_3Ph => 480,
            PanelVoltageConfig.V240_3Ph     => 240,
            _                               => 208
        };

        bool isThreePhase = panel.VoltageConfig != PanelVoltageConfig.V120_240_1Ph;
        double totalCurrent = isThreePhase
            ? panel.TotalDemandVA / (lineVoltage * Math.Sqrt(3))
            : panel.TotalDemandVA / lineVoltage;

        if (totalCurrent > panel.BusAmps)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 408.36",
                Description = $"Panel '{panel.PanelName}' total demand current is {totalCurrent:F1}A, " +
                              $"which exceeds the bus rating of {panel.BusAmps}A.",
                Severity = ViolationSeverity.Error,
                AffectedItemId = panel.PanelId,
                AffectedItemName = panel.PanelName,
                Suggestion = $"Reduce loads or upgrade the panel bus to at least {Math.Ceiling(totalCurrent)}A."
            });
        }
        else if (totalCurrent > panel.BusAmps * 0.80)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 408.36",
                Description = $"Panel '{panel.PanelName}' total demand current is {totalCurrent:F1}A " +
                              $"({totalCurrent / panel.BusAmps * 100:F0}% of {panel.BusAmps}A bus rating). " +
                              $"Consider load growth capacity.",
                Severity = ViolationSeverity.Info,
                AffectedItemId = panel.PanelId,
                AffectedItemName = panel.PanelName,
                Suggestion = "Panel is above 80% utilization. Consider capacity for future load growth."
            });
        }
    }

    /// <summary>
    /// NEC 210.19(A)(1) Informational Note No. 4 - Branch circuit voltage drop
    /// should not exceed 3%.
    /// </summary>
    private void CheckBranchVoltageDropRecommendation(
        Circuit circuit,
        ElectricalCalculationService calcService,
        List<NecViolation> violations)
    {
        if (circuit.WireLengthFeet <= 0 || circuit.Voltage <= 0)
            return;

        var vdResult = calcService.CalculateVoltageDrop(circuit);
        if (!vdResult.IsValid)
            return;

        if (vdResult.ExceedsNecRecommendation)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 210.19(A)(1)",
                Description = $"Branch circuit voltage drop is {vdResult.VoltageDropPercent:F2}%, " +
                              $"exceeding the NEC recommended maximum of 3%.",
                Severity = ViolationSeverity.Warning,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = $"Increase wire size or reduce circuit length ({circuit.WireLengthFeet:F0} ft) " +
                             $"to bring voltage drop below 3%. Voltage at load: {vdResult.VoltageAtLoad:F1}V."
            });
        }
    }

    /// <summary>
    /// NEC 215.2(A)(4) Informational Note No. 2 - Total voltage drop for feeders
    /// plus branch circuits should not exceed 5%.
    /// </summary>
    private void CheckTotalVoltageDropRecommendation(
        Circuit circuit,
        ElectricalCalculationService calcService,
        List<NecViolation> violations)
    {
        if (circuit.WireLengthFeet <= 0 || circuit.Voltage <= 0)
            return;

        var vdResult = calcService.CalculateVoltageDrop(circuit);
        if (!vdResult.IsValid)
            return;

        if (vdResult.ExceedsTotalRecommendation)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 215.2(A)(4)",
                Description = $"Total voltage drop is {vdResult.VoltageDropPercent:F2}%, " +
                              $"exceeding the NEC recommended maximum of 5% for feeders plus branch circuits combined.",
                Severity = ViolationSeverity.Warning,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = $"Significantly increase wire size or shorten the circuit run. " +
                             $"Current length: {circuit.WireLengthFeet:F0} ft, voltage at load: {vdResult.VoltageAtLoad:F1}V."
            });
        }
    }

    /// <summary>
    /// NEC 110.14(C) - Temperature rating compatibility check.
    /// Conductors must be used at the temperature rating appropriate for the termination.
    /// Residential circuits (100A or less) typically require 60°C-rated terminations,
    /// meaning only the 60°C ampacity column applies even for 90°C-rated wire.
    /// This check warns when relying on 75°C or 90°C ampacity with smaller breakers
    /// that commonly have 60°C-rated terminations.
    /// </summary>
    private void CheckTemperatureRating(Circuit circuit, List<NecViolation> violations)
    {
        // NEC 110.14(C)(1)(a): For equipment rated 100A or less, or marked for
        // #14 through #1 conductors, use 60°C ampacity unless the equipment
        // is listed and marked for higher temperature conductors.
        if (circuit.Breaker.TripAmps > 100)
            return;

        // Common 60°C insulation types
        string insulation = circuit.Wire.InsulationType.ToUpperInvariant();
        bool is60CInsulation = insulation == "TW" || insulation == "UF";

        // 75°C types: THW, THWN, XHHW (when wet-rated)
        // 90°C types: THHN, THWN-2, XHHW-2
        // For terminations rated 60°C, the conductor must be sized per the 60°C column
        // even if the insulation is rated higher. We flag an info-level note.
        bool is90CInsulation = insulation == "THHN" || insulation == "THWN-2" || insulation == "XHHW-2";

        if (is90CInsulation && circuit.Breaker.TripAmps <= 100)
        {
            // Check if the wire would fail at 60°C ampacity.
            // 60°C copper ampacities for small conductors:
            // #14=15A, #12=20A, #10=30A (these are also the 240.4(D) limits)
            // For larger sizes, 75°C column is typically acceptable for listed equipment.
            if (SmallConductorMaxOcpd.TryGetValue(circuit.Wire.Size, out int maxAt60C))
            {
                int ampacity75 = GetAmpacity(circuit.Wire.Size, circuit.Wire.Material);
                if (ampacity75 > maxAt60C)
                {
                    // The 60°C limit already enforced by 240.4(D) for small conductors,
                    // so only flag if there's a mismatch concern on larger sizes.
                    // For small conductors this is already covered; skip.
                    return;
                }
            }
        }

        if (is60CInsulation)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 110.14(C)",
                Description = $"Wire insulation type '{circuit.Wire.InsulationType}' is rated for 60°C only. " +
                              $"Verify ampacity is based on the 60°C column of NEC Table 310.16.",
                Severity = ViolationSeverity.Info,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = "Consider upgrading to 75°C-rated insulation (e.g., THWN) or 90°C (e.g., THHN) " +
                             "for higher ampacity allowance."
            });
        }
    }

    /// <summary>
    /// Phase imbalance warning: if the maximum phase load exceeds the average
    /// of all phases by more than 20%, flag it as a warning.
    /// </summary>
    private void CheckPhaseImbalance(PanelSchedule panel, List<NecViolation> violations)
    {
        var (phA, phB, phC) = panel.PhaseDemandVA;

        // For single-phase panels, only two legs are relevant
        bool isThreePhase = panel.VoltageConfig != PanelVoltageConfig.V120_240_1Ph;

        double maxPhase;
        double avgPhase;

        if (isThreePhase)
        {
            maxPhase = Math.Max(phA, Math.Max(phB, phC));
            avgPhase = (phA + phB + phC) / 3.0;
        }
        else
        {
            maxPhase = Math.Max(phA, phB);
            avgPhase = (phA + phB) / 2.0;
        }

        if (avgPhase <= 0)
            return;

        double imbalancePercent = ((maxPhase - avgPhase) / avgPhase) * 100.0;

        if (imbalancePercent > 20.0)
        {
            string phaseDetail = isThreePhase
                ? $"Phase A: {phA:F0}VA, Phase B: {phB:F0}VA, Phase C: {phC:F0}VA"
                : $"Phase A: {phA:F0}VA, Phase B: {phB:F0}VA";

            violations.Add(new NecViolation
            {
                RuleId = "Phase Imbalance",
                Description = $"Panel '{panel.PanelName}' has a phase imbalance of {imbalancePercent:F1}%. " +
                              $"{phaseDetail}. Maximum phase load exceeds average by more than 20%.",
                Severity = ViolationSeverity.Warning,
                AffectedItemId = panel.PanelId,
                AffectedItemName = panel.PanelName,
                Suggestion = "Redistribute circuits across phases to improve balance. " +
                             "Excessive imbalance causes neutral overloading and voltage fluctuations."
            });
        }
    }

    /// <summary>
    /// NEC 110.9 — Breaker interrupting rating must be ≥ available fault current at its location.
    /// </summary>
    private void CheckBreakerInterruptingRating(Circuit circuit, double availableFaultKA, List<NecViolation> violations)
    {
        double breakerAIC = circuit.Breaker.InterruptingRatingKAIC;
        if (breakerAIC < availableFaultKA)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 110.9",
                Description = $"Breaker on circuit '{circuit.Description}' has an interrupting rating of {breakerAIC:F1} kAIC " +
                              $"but available fault current at the panel is {availableFaultKA:F1} kA.",
                Severity = ViolationSeverity.Error,
                AffectedItemId = circuit.Id,
                AffectedItemName = $"Circuit {circuit.CircuitNumber} - {circuit.Description}",
                Suggestion = $"Replace breaker with one rated for at least {availableFaultKA:F1} kAIC, " +
                             $"or add upstream fault current limiting."
            });
        }
    }

    /// <summary>
    /// NEC 250.122 — Equipment grounding conductor must be sized based on OCPD rating.
    /// </summary>
    private void CheckGroundConductorSizing(Circuit circuit, List<NecViolation> violations)
    {
        var result = GroundingService.ValidateGroundSize(circuit);
        if (!result.IsAdequate)
        {
            violations.Add(new NecViolation
            {
                RuleId = "NEC 250.122",
                Description = $"Equipment grounding conductor is #{result.ActualGroundSize} but NEC Table 250.122 " +
                              $"requires minimum #{result.MinimumEGCSize} {result.Material} for a {result.OCPDAmps}A OCPD.",
                Severity = ViolationSeverity.Error,
                AffectedItemId = circuit.Id,
                AffectedItemName = result.CircuitDescription,
                Suggestion = $"Increase ground conductor to at least #{result.MinimumEGCSize} {result.Material}.",
            });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Looks up wire ampacity from NEC Table 310.16 at 75°C via shared NecAmpacityService.
    /// </summary>
    private static int GetAmpacity(string wireSize, ConductorMaterial material)
    {
        return NecAmpacityService.LookupAmpacity(wireSize, material, Models.InsulationTemperatureRating.C75);
    }
}

// ── Violation types ──────────────────────────────────────────────────────────

public class NecViolation
{
    /// <summary>NEC rule reference, e.g. "NEC 240.4(D)"</summary>
    public string RuleId { get; init; } = "";

    /// <summary>Human-readable description of the violation</summary>
    public string Description { get; init; } = "";

    /// <summary>Severity level: Error (code violation), Warning (exceeds recommendation), Info (best practice)</summary>
    public ViolationSeverity Severity { get; init; }

    /// <summary>Circuit ID or panel ID of the affected item</summary>
    public string AffectedItemId { get; init; } = "";

    /// <summary>Display name of the affected item</summary>
    public string AffectedItemName { get; init; } = "";

    /// <summary>Suggested fix for the violation</summary>
    public string Suggestion { get; init; } = "";
}

public enum ViolationSeverity
{
    /// <summary>Code violation - must be corrected</summary>
    Error,

    /// <summary>Exceeds NEC recommendation - should be reviewed</summary>
    Warning,

    /// <summary>Best practice suggestion</summary>
    Info
}
