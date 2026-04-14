using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Evaluates tie-switch restoration options while preserving radial operation and emergency ratings.
/// </summary>
public static class FeederReconfigurationService
{
    public record SectionTransferImpact
    {
        public string SectionName { get; init; } = string.Empty;
        public double ExistingLoadAmps { get; init; }
        public double AddedLoadAmpsAtFullTransfer { get; init; }
        public double EmergencyRatingAmps { get; init; }
    }

    public record TieSwitchOption
    {
        public string SwitchName { get; init; } = string.Empty;
        public string? OpenPointName { get; init; }
        public double TieCapacityAmps { get; init; }
        public double SourceHeadroomAmps { get; init; }
        public bool MaintainsRadialConfiguration { get; init; } = true;
        public List<SectionTransferImpact> AffectedSections { get; init; } = new();
    }

    public record SectionReconfigurationAssessment
    {
        public string SectionName { get; init; } = string.Empty;
        public double PostTransferLoadAmps { get; init; }
        public double LoadingPercent { get; init; }
        public bool IsOverloaded { get; init; }
    }

    public record ReconfigurationPlan
    {
        public string? SwitchName { get; init; }
        public string? OpenPointName { get; init; }
        public double RestoredLoadAmps { get; init; }
        public double UnservedLoadAmps { get; init; }
        public double TransferUtilizationPercent { get; init; }
        public bool IsRadial { get; init; }
        public bool PassesEmergencyRatings { get; init; }
        public string? Issue { get; init; }
        public List<SectionReconfigurationAssessment> Sections { get; init; } = new();
    }

    public static double CalculateTransferCapacity(double loadToRestoreAmps, double tieCapacityAmps, double sourceHeadroomAmps)
    {
        if (loadToRestoreAmps < 0 || tieCapacityAmps < 0 || sourceHeadroomAmps < 0)
            throw new ArgumentOutOfRangeException(nameof(loadToRestoreAmps), "Transfer inputs must be non-negative.");

        return Math.Round(Math.Min(loadToRestoreAmps, Math.Min(tieCapacityAmps, sourceHeadroomAmps)), 2);
    }

    public static ReconfigurationPlan EvaluateOption(double loadToRestoreAmps, TieSwitchOption option)
    {
        double restoredLoad = CalculateTransferCapacity(loadToRestoreAmps, option.TieCapacityAmps, option.SourceHeadroomAmps);
        double allocationFactor = loadToRestoreAmps <= 0 ? 0 : restoredLoad / loadToRestoreAmps;

        var sections = option.AffectedSections
            .Select(section =>
            {
                double postLoad = section.ExistingLoadAmps + section.AddedLoadAmpsAtFullTransfer * allocationFactor;
                double loadingPercent = section.EmergencyRatingAmps <= 0 ? 0 : postLoad / section.EmergencyRatingAmps * 100.0;
                return new SectionReconfigurationAssessment
                {
                    SectionName = section.SectionName,
                    PostTransferLoadAmps = Math.Round(postLoad, 2),
                    LoadingPercent = Math.Round(loadingPercent, 1),
                    IsOverloaded = section.EmergencyRatingAmps > 0 && postLoad > section.EmergencyRatingAmps,
                };
            })
            .ToList();

        var overloadedSection = sections
            .Where(section => section.IsOverloaded)
            .OrderByDescending(section => section.LoadingPercent)
            .FirstOrDefault();

        string? issue = !option.MaintainsRadialConfiguration
            ? "Selected switching sequence does not preserve a radial feeder"
            : overloadedSection is not null
                ? $"Section {overloadedSection.SectionName} exceeds emergency rating at {overloadedSection.LoadingPercent}%"
                : restoredLoad < loadToRestoreAmps
                    ? "Transfer capacity is insufficient to restore the full disconnected load"
                    : null;

        return new ReconfigurationPlan
        {
            SwitchName = option.SwitchName,
            OpenPointName = option.OpenPointName,
            RestoredLoadAmps = restoredLoad,
            UnservedLoadAmps = Math.Round(Math.Max(0, loadToRestoreAmps - restoredLoad), 2),
            TransferUtilizationPercent = option.TieCapacityAmps <= 0 ? 0 : Math.Round(restoredLoad / option.TieCapacityAmps * 100.0, 1),
            IsRadial = option.MaintainsRadialConfiguration,
            PassesEmergencyRatings = overloadedSection is null,
            Issue = issue,
            Sections = sections,
        };
    }

    public static ReconfigurationPlan SelectBestOption(double loadToRestoreAmps, IEnumerable<TieSwitchOption> options)
    {
        var plans = (options ?? Array.Empty<TieSwitchOption>())
            .Select(option => EvaluateOption(loadToRestoreAmps, option))
            .ToList();

        var preferred = plans
            .OrderByDescending(plan => plan.IsRadial && plan.PassesEmergencyRatings)
            .ThenByDescending(plan => plan.RestoredLoadAmps)
            .ThenBy(plan => plan.Sections.Count(section => section.IsOverloaded))
            .ThenBy(plan => plan.Sections.Count == 0 ? 0 : plan.Sections.Max(section => section.LoadingPercent))
            .FirstOrDefault();

        return preferred ?? new ReconfigurationPlan
        {
            Issue = "No tie-switch options were provided",
        };
    }
}