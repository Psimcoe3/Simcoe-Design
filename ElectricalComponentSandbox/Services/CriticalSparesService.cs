using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Recommends critical spare stock levels from installed-base risk and replenishment lead time.
/// </summary>
public static class CriticalSparesService
{
    public record SparePartProfile
    {
        public string PartName { get; init; } = string.Empty;
        public AssetConditionService.AssetType AssetType { get; init; }
        public int InstalledBase { get; init; }
        public double AverageFailureProbability { get; init; }
        public int LeadTimeDays { get; init; }
        public double UnitCost { get; init; }
        public bool IsSinglePointOfFailure { get; init; }
    }

    public record SpareRecommendation
    {
        public string PartName { get; init; } = string.Empty;
        public double AnnualExpectedFailures { get; init; }
        public int MinimumStock { get; init; }
        public int TargetStock { get; init; }
        public double AnnualCarryingCost { get; init; }
        public bool IsCritical { get; init; }
    }

    public record CoverageAssessment
    {
        public string PartName { get; init; } = string.Empty;
        public int OnHandQuantity { get; init; }
        public int MinimumStock { get; init; }
        public int ShortageQuantity { get; init; }
        public bool HasAdequateCoverage { get; init; }
    }

    public static double CalculateAnnualExpectedFailures(int installedBase, double averageFailureProbability)
    {
        if (installedBase < 0 || averageFailureProbability < 0)
            throw new ArgumentOutOfRangeException(nameof(installedBase), "Installed base and failure probability must be non-negative.");

        return Math.Round(installedBase * averageFailureProbability, 4);
    }

    public static SpareRecommendation RecommendStock(SparePartProfile profile)
    {
        if (profile.LeadTimeDays < 0 || profile.UnitCost < 0)
            throw new ArgumentOutOfRangeException(nameof(profile), "Lead time and unit cost must be non-negative.");

        double annualFailures = CalculateAnnualExpectedFailures(profile.InstalledBase, profile.AverageFailureProbability);
        double leadTimeDemand = annualFailures * (profile.LeadTimeDays / 365.0);
        int minimumStock = (int)Math.Ceiling(leadTimeDemand);

        if (profile.IsSinglePointOfFailure)
            minimumStock = Math.Max(1, minimumStock + 1);

        int targetStock = Math.Max(minimumStock, (int)Math.Ceiling(annualFailures * 0.5));
        bool isCritical = profile.IsSinglePointOfFailure || profile.LeadTimeDays >= 120 || annualFailures >= 1.0;

        return new SpareRecommendation
        {
            PartName = profile.PartName,
            AnnualExpectedFailures = annualFailures,
            MinimumStock = minimumStock,
            TargetStock = targetStock,
            AnnualCarryingCost = Math.Round(targetStock * profile.UnitCost * 0.2, 2),
            IsCritical = isCritical,
        };
    }

    public static CoverageAssessment AssessCoverage(SpareRecommendation recommendation, int onHandQuantity)
    {
        if (onHandQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(onHandQuantity), "On-hand quantity must be non-negative.");

        int shortage = Math.Max(0, recommendation.MinimumStock - onHandQuantity);
        return new CoverageAssessment
        {
            PartName = recommendation.PartName,
            OnHandQuantity = onHandQuantity,
            MinimumStock = recommendation.MinimumStock,
            ShortageQuantity = shortage,
            HasAdequateCoverage = shortage == 0,
        };
    }

    public static List<SpareRecommendation> RankRecommendations(IEnumerable<SparePartProfile> profiles)
    {
        return (profiles ?? Array.Empty<SparePartProfile>())
            .Select(RecommendStock)
            .OrderByDescending(recommendation => recommendation.IsCritical)
            .ThenByDescending(recommendation => recommendation.MinimumStock)
            .ThenByDescending(recommendation => recommendation.AnnualExpectedFailures)
            .ToList();
    }
}