using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Converts asset age, loading, environment, and latest test condition into a normalized health assessment.
/// </summary>
public static class AssetConditionService
{
    public enum AssetType
    {
        OverheadLine,
        UndergroundCable,
        Transformer,
        Switchgear,
        Breaker,
    }

    public enum EnvironmentSeverity
    {
        Mild,
        Moderate,
        Severe,
        Coastal,
    }

    public enum AssetCriticality
    {
        Standard,
        Important,
        Critical,
    }

    public enum ConditionBand
    {
        Good,
        Fair,
        Poor,
        Critical,
    }

    public record AssetProfile
    {
        public string Name { get; init; } = string.Empty;
        public AssetType AssetType { get; init; }
        public double AgeYears { get; init; }
        public double ExpectedLifeYears { get; init; } = 40;
        public double PeakLoadPerUnit { get; init; } = 0.6;
        public EnvironmentSeverity EnvironmentSeverity { get; init; } = EnvironmentSeverity.Moderate;
        public AssetCriticality Criticality { get; init; } = AssetCriticality.Standard;
        public bool HasRedundancy { get; init; }
        public ElectricalTestingService.TestVerdict? LatestTestVerdict { get; init; }
    }

    public record AssetConditionAssessment
    {
        public string AssetName { get; init; } = string.Empty;
        public double HealthIndex { get; init; }
        public double FailureProbability { get; init; }
        public double AgeScore { get; init; }
        public double LoadScore { get; init; }
        public double EnvironmentScore { get; init; }
        public double TestScore { get; init; }
        public double CriticalityMultiplier { get; init; }
        public ConditionBand ConditionBand { get; init; }
    }

    public static double CalculateAgeScore(double ageYears, double expectedLifeYears)
    {
        if (ageYears < 0 || expectedLifeYears <= 0)
            throw new ArgumentOutOfRangeException(nameof(ageYears), "Age must be non-negative and expected life must be positive.");

        double ageRatio = ageYears / expectedLifeYears;
        return Math.Round(Math.Clamp(ageRatio, 0, 1.5) / 1.5 * 100.0, 2);
    }

    public static double CalculateLoadScore(double peakLoadPerUnit)
    {
        if (peakLoadPerUnit < 0)
            throw new ArgumentOutOfRangeException(nameof(peakLoadPerUnit), "Load must be non-negative.");

        double normalized = Math.Clamp(peakLoadPerUnit, 0, 1.5) / 1.5;
        return Math.Round(normalized * 100.0, 2);
    }

    public static double GetEnvironmentScore(EnvironmentSeverity severity) => severity switch
    {
        EnvironmentSeverity.Mild => 20,
        EnvironmentSeverity.Moderate => 40,
        EnvironmentSeverity.Severe => 70,
        EnvironmentSeverity.Coastal => 85,
        _ => 40,
    };

    public static double GetTestScore(ElectricalTestingService.TestVerdict? verdict) => verdict switch
    {
        ElectricalTestingService.TestVerdict.Good => 15,
        ElectricalTestingService.TestVerdict.Investigate => 55,
        ElectricalTestingService.TestVerdict.Bad => 90,
        _ => 35,
    };

    public static double GetCriticalityMultiplier(AssetCriticality criticality, bool hasRedundancy)
    {
        double baseMultiplier = criticality switch
        {
            AssetCriticality.Important => 1.15,
            AssetCriticality.Critical => 1.35,
            _ => 1.0,
        };

        return hasRedundancy ? Math.Round(baseMultiplier * 0.9, 3) : baseMultiplier;
    }

    public static ConditionBand GetConditionBand(double healthIndex)
    {
        return healthIndex switch
        {
            < 35 => ConditionBand.Good,
            < 55 => ConditionBand.Fair,
            < 75 => ConditionBand.Poor,
            _ => ConditionBand.Critical,
        };
    }

    public static AssetConditionAssessment AssessAsset(AssetProfile asset)
    {
        double ageScore = CalculateAgeScore(asset.AgeYears, asset.ExpectedLifeYears);
        double loadScore = CalculateLoadScore(asset.PeakLoadPerUnit);
        double environmentScore = GetEnvironmentScore(asset.EnvironmentSeverity);
        double testScore = GetTestScore(asset.LatestTestVerdict);
        double criticalityMultiplier = GetCriticalityMultiplier(asset.Criticality, asset.HasRedundancy);

        double baseIndex = ageScore * 0.35 + loadScore * 0.2 + environmentScore * 0.15 + testScore * 0.3;
        double healthIndex = Math.Round(Math.Clamp(baseIndex * criticalityMultiplier, 0, 100), 2);
        double failureProbability = Math.Round(Math.Clamp(healthIndex / 100.0, 0, 1), 4);

        return new AssetConditionAssessment
        {
            AssetName = asset.Name,
            HealthIndex = healthIndex,
            FailureProbability = failureProbability,
            AgeScore = ageScore,
            LoadScore = loadScore,
            EnvironmentScore = environmentScore,
            TestScore = testScore,
            CriticalityMultiplier = criticalityMultiplier,
            ConditionBand = GetConditionBand(healthIndex),
        };
    }
}