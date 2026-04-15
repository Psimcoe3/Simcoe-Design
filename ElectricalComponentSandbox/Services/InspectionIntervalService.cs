using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Recommends inspection cadence and method from asset condition and criticality.
/// </summary>
public static class InspectionIntervalService
{
    public enum InspectionMethod
    {
        Visual,
        Infrared,
        DiagnosticTesting,
        DetailedOffline,
    }

    public record InspectionRecommendation
    {
        public string AssetName { get; init; } = string.Empty;
        public int IntervalMonths { get; init; }
        public InspectionMethod PrimaryMethod { get; init; }
        public bool RequiresInfrared { get; init; }
        public bool RequiresOfflineTesting { get; init; }
        public string Rationale { get; init; } = string.Empty;
    }

    public static int GetBaseIntervalMonths(AssetConditionService.AssetType assetType) => assetType switch
    {
        AssetConditionService.AssetType.Transformer => 12,
        AssetConditionService.AssetType.Switchgear => 12,
        AssetConditionService.AssetType.Breaker => 18,
        AssetConditionService.AssetType.UndergroundCable => 24,
        _ => 24,
    };

    public static InspectionMethod GetPrimaryMethod(AssetConditionService.AssetType assetType, AssetConditionService.ConditionBand band)
    {
        if (band == AssetConditionService.ConditionBand.Critical)
            return InspectionMethod.DetailedOffline;

        return assetType switch
        {
            AssetConditionService.AssetType.Transformer => InspectionMethod.DiagnosticTesting,
            AssetConditionService.AssetType.Switchgear => InspectionMethod.Infrared,
            AssetConditionService.AssetType.Breaker => InspectionMethod.DiagnosticTesting,
            _ => InspectionMethod.Visual,
        };
    }

    public static int AdjustIntervalMonths(
        int baseIntervalMonths,
        AssetConditionService.AssetConditionAssessment assessment,
        AssetConditionService.AssetCriticality criticality)
    {
        double multiplier = assessment.ConditionBand switch
        {
            AssetConditionService.ConditionBand.Good => 1.25,
            AssetConditionService.ConditionBand.Fair => 1.0,
            AssetConditionService.ConditionBand.Poor => 0.5,
            _ => 0.25,
        };

        multiplier *= criticality switch
        {
            AssetConditionService.AssetCriticality.Important => 0.85,
            AssetConditionService.AssetCriticality.Critical => 0.7,
            _ => 1.0,
        };

        return Math.Max(1, (int)Math.Round(baseIntervalMonths * multiplier, MidpointRounding.AwayFromZero));
    }

    public static InspectionRecommendation RecommendInspection(AssetConditionService.AssetProfile asset)
    {
        var assessment = AssetConditionService.AssessAsset(asset);
        int baseInterval = GetBaseIntervalMonths(asset.AssetType);
        int adjustedInterval = AdjustIntervalMonths(baseInterval, assessment, asset.Criticality);
        InspectionMethod method = GetPrimaryMethod(asset.AssetType, assessment.ConditionBand);

        bool requiresInfrared = asset.AssetType is AssetConditionService.AssetType.Transformer or AssetConditionService.AssetType.Switchgear or AssetConditionService.AssetType.Breaker;
        bool requiresOfflineTesting = method == InspectionMethod.DetailedOffline || assessment.TestScore >= 55;

        string rationale = assessment.ConditionBand switch
        {
            AssetConditionService.ConditionBand.Good => "Asset condition is healthy; use routine monitoring cadence.",
            AssetConditionService.ConditionBand.Fair => "Asset condition is moderate; keep standard inspection cadence.",
            AssetConditionService.ConditionBand.Poor => "Asset condition is degraded; tighten interval and expand diagnostic checks.",
            _ => "Asset condition is critical; immediate detailed inspection and offline testing are warranted.",
        };

        return new InspectionRecommendation
        {
            AssetName = asset.Name,
            IntervalMonths = adjustedInterval,
            PrimaryMethod = method,
            RequiresInfrared = requiresInfrared,
            RequiresOfflineTesting = requiresOfflineTesting,
            Rationale = rationale,
        };
    }
}