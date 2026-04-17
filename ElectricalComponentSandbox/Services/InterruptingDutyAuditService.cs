using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Ranks interrupting-duty exposure from short-circuit AIC results.
/// </summary>
public static class InterruptingDutyAuditService
{
    public enum DutySeverity
    {
        Low,
        Moderate,
        High,
        Critical,
    }

    public record DutyExposure
    {
        public string NodeId { get; init; } = string.Empty;
        public string NodeName { get; init; } = string.Empty;
        public double AvailableFaultKA { get; init; }
        public double EquipmentAICKA { get; init; }
        public double MarginPercent { get; init; }
        public DutySeverity Severity { get; init; }
        public string RecommendedAction { get; init; } = string.Empty;
    }

    public record DutyAuditSummary
    {
        public int TotalNodeCount { get; init; }
        public int ViolationCount { get; init; }
        public int CriticalCount { get; init; }
        public DutyExposure HighestExposure { get; init; } = new();
        public List<DutyExposure> Exposures { get; init; } = new();
    }

    public static DutySeverity ClassifySeverity(ShortCircuitResult result, double lowMarginThresholdPercent = 10.0)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsAdequate && result.MarginPercent <= -25.0)
            return DutySeverity.Critical;

        if (!result.IsAdequate)
            return DutySeverity.High;

        if (result.MarginPercent < lowMarginThresholdPercent)
            return DutySeverity.Moderate;

        return DutySeverity.Low;
    }

    public static DutyExposure CreateExposure(ShortCircuitResult result, double lowMarginThresholdPercent = 10.0)
    {
        ArgumentNullException.ThrowIfNull(result);

        var severity = ClassifySeverity(result, lowMarginThresholdPercent);
        return new DutyExposure
        {
            NodeId = result.NodeId,
            NodeName = result.NodeName,
            AvailableFaultKA = result.AvailableFaultKA,
            EquipmentAICKA = result.EquipmentAICKA,
            MarginPercent = result.MarginPercent,
            Severity = severity,
            RecommendedAction = GetRecommendedAction(severity),
        };
    }

    public static DutyAuditSummary AuditResults(IEnumerable<ShortCircuitResult> results, double lowMarginThresholdPercent = 10.0)
    {
        var exposures = (results ?? Array.Empty<ShortCircuitResult>())
            .Select(result => CreateExposure(result, lowMarginThresholdPercent))
            .OrderByDescending(exposure => exposure.Severity)
            .ThenBy(exposure => exposure.MarginPercent)
            .ToList();

        if (exposures.Count == 0)
            return new DutyAuditSummary();

        return new DutyAuditSummary
        {
            TotalNodeCount = exposures.Count,
            ViolationCount = exposures.Count(exposure => exposure.Severity is DutySeverity.High or DutySeverity.Critical),
            CriticalCount = exposures.Count(exposure => exposure.Severity == DutySeverity.Critical),
            HighestExposure = exposures[0],
            Exposures = exposures,
        };
    }

    public static DutyAuditSummary AuditDistribution(List<DistributionNode> roots, ShortCircuitService shortCircuitService, double lowMarginThresholdPercent = 10.0)
    {
        ArgumentNullException.ThrowIfNull(shortCircuitService);
        return AuditResults(shortCircuitService.ValidateAIC(roots ?? new List<DistributionNode>()), lowMarginThresholdPercent);
    }

    private static string GetRecommendedAction(DutySeverity severity) => severity switch
    {
        DutySeverity.Critical => "Replace or current-limit equipment before energization.",
        DutySeverity.High => "Upgrade interrupting rating or reduce available fault current.",
        DutySeverity.Moderate => "Review low AIC margin during next study revision.",
        _ => "No immediate interrupting-duty action required.",
    };
}