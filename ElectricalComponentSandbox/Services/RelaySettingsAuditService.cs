using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Compares field relay settings against protection-study targets with configurable tolerances.
/// </summary>
public static class RelaySettingsAuditService
{
    public enum SettingSeverity
    {
        Info,
        Warning,
        Critical,
    }

    public record AuditTolerance
    {
        public double PickupPercentTolerance { get; init; } = 5.0;
        public double TimeDialTolerance { get; init; } = 0.1;
        public double InstantaneousPercentTolerance { get; init; } = 10.0;
    }

    public record SettingVariance
    {
        public string FieldName { get; init; } = string.Empty;
        public double TargetValue { get; init; }
        public double ActualValue { get; init; }
        public double PercentDifference { get; init; }
        public SettingSeverity Severity { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public record RelayAuditResult
    {
        public string RelayId { get; init; } = string.Empty;
        public bool MatchesStudy { get; init; }
        public int WarningCount { get; init; }
        public int CriticalCount { get; init; }
        public List<SettingVariance> Variances { get; init; } = new();
    }

    public static double CalculatePercentDifference(double targetValue, double actualValue)
    {
        if (targetValue == 0)
            return actualValue == 0 ? 0 : 100.0;

        return Math.Round(Math.Abs(actualValue - targetValue) / Math.Abs(targetValue) * 100.0, 2);
    }

    public static SettingSeverity ClassifySeverity(double percentDifference, double tolerancePercent)
    {
        if (percentDifference <= tolerancePercent)
            return SettingSeverity.Info;

        if (percentDifference <= tolerancePercent * 2)
            return SettingSeverity.Warning;

        return SettingSeverity.Critical;
    }

    public static RelayAuditResult AuditSettings(
        ProtectiveRelayService.RelaySettings target,
        ProtectiveRelayService.RelaySettings actual,
        AuditTolerance? tolerance = null)
    {
        tolerance ??= new AuditTolerance();
        var variances = CollectVariances(target, actual, tolerance);

        return new RelayAuditResult
        {
            RelayId = string.IsNullOrWhiteSpace(actual.Id) ? target.Id : actual.Id,
            MatchesStudy = variances.All(variance => variance.Severity == SettingSeverity.Info),
            WarningCount = variances.Count(variance => variance.Severity == SettingSeverity.Warning),
            CriticalCount = variances.Count(variance => variance.Severity == SettingSeverity.Critical),
            Variances = variances,
        };
    }

    public static List<RelayAuditResult> AuditPortfolio(
        IEnumerable<(ProtectiveRelayService.RelaySettings Target, ProtectiveRelayService.RelaySettings Actual)> settings,
        AuditTolerance? tolerance = null)
    {
        return (settings ?? Array.Empty<(ProtectiveRelayService.RelaySettings, ProtectiveRelayService.RelaySettings)>())
            .Select(pair => AuditSettings(pair.Target, pair.Actual, tolerance))
            .ToList();
    }

    private static List<SettingVariance> CollectVariances(
        ProtectiveRelayService.RelaySettings target,
        ProtectiveRelayService.RelaySettings actual,
        AuditTolerance tolerance)
    {
        var variances = new List<SettingVariance>();
        AddPickupVariance(variances, target, actual, tolerance);
        AddTimeDialVariance(variances, target, actual, tolerance);
        AddInstantaneousVariance(variances, target, actual, tolerance);
        AddCurveVariance(variances, target, actual);
        AddCtVariance(variances, target, actual);
        return variances;
    }

    private static void AddPickupVariance(
        List<SettingVariance> variances,
        ProtectiveRelayService.RelaySettings target,
        ProtectiveRelayService.RelaySettings actual,
        AuditTolerance tolerance)
    {
        AddVariance(
            variances,
            "PickupAmps",
            target.PickupAmps,
            actual.PickupAmps,
            tolerance.PickupPercentTolerance,
            "% pickup mismatch");
    }

    private static void AddTimeDialVariance(
        List<SettingVariance> variances,
        ProtectiveRelayService.RelaySettings target,
        ProtectiveRelayService.RelaySettings actual,
        AuditTolerance tolerance)
    {
        AddAbsoluteVariance(
            variances,
            "TimeDial",
            target.TimeDial,
            actual.TimeDial,
            tolerance.TimeDialTolerance,
            percentScale: 100,
            messageSuffix: "time dial mismatch");
    }

    private static void AddInstantaneousVariance(
        List<SettingVariance> variances,
        ProtectiveRelayService.RelaySettings target,
        ProtectiveRelayService.RelaySettings actual,
        AuditTolerance tolerance)
    {
        if (target.InstantaneousAmps <= 0 && actual.InstantaneousAmps <= 0)
            return;

        AddVariance(
            variances,
            "InstantaneousAmps",
            target.InstantaneousAmps,
            actual.InstantaneousAmps,
            tolerance.InstantaneousPercentTolerance,
            "% instantaneous mismatch");
    }

    private static void AddCurveVariance(
        List<SettingVariance> variances,
        ProtectiveRelayService.RelaySettings target,
        ProtectiveRelayService.RelaySettings actual)
    {
        if (target.Curve == actual.Curve)
            return;

        variances.Add(new SettingVariance
        {
            FieldName = "Curve",
            TargetValue = (double)target.Curve,
            ActualValue = (double)actual.Curve,
            PercentDifference = 100,
            Severity = SettingSeverity.Critical,
            Message = $"Curve mismatch: study {target.Curve}, field {actual.Curve}",
        });
    }

    private static void AddCtVariance(
        List<SettingVariance> variances,
        ProtectiveRelayService.RelaySettings target,
        ProtectiveRelayService.RelaySettings actual)
    {
        if (Math.Abs(target.CtRatio - actual.CtRatio) <= 0.001)
            return;

        double percentDifference = CalculatePercentDifference(target.CtRatio, actual.CtRatio);
        if (percentDifference <= 2.0)
            return;

        variances.Add(new SettingVariance
        {
            FieldName = "CtRatio",
            TargetValue = target.CtRatio,
            ActualValue = actual.CtRatio,
            PercentDifference = percentDifference,
            Severity = ClassifySeverity(percentDifference, 2.0),
            Message = $"CT ratio mismatch: study {target.CtRatio}, field {actual.CtRatio}",
        });
    }

    private static void AddVariance(
        List<SettingVariance> variances,
        string fieldName,
        double targetValue,
        double actualValue,
        double tolerancePercent,
        string messageSuffix)
    {
        double percentDifference = CalculatePercentDifference(targetValue, actualValue);
        if (percentDifference <= tolerancePercent)
            return;

        variances.Add(new SettingVariance
        {
            FieldName = fieldName,
            TargetValue = targetValue,
            ActualValue = actualValue,
            PercentDifference = percentDifference,
            Severity = ClassifySeverity(percentDifference, tolerancePercent),
            Message = $"{fieldName} {messageSuffix}: study {targetValue}, field {actualValue}",
        });
    }

    private static void AddAbsoluteVariance(
        List<SettingVariance> variances,
        string fieldName,
        double targetValue,
        double actualValue,
        double toleranceAbsolute,
        double percentScale,
        string messageSuffix)
    {
        double absoluteDifference = Math.Abs(actualValue - targetValue);
        if (absoluteDifference <= toleranceAbsolute)
            return;

        double percentDifference = Math.Round(absoluteDifference / Math.Max(toleranceAbsolute, 0.0001) * percentScale, 2);
        variances.Add(new SettingVariance
        {
            FieldName = fieldName,
            TargetValue = targetValue,
            ActualValue = actualValue,
            PercentDifference = percentDifference,
            Severity = ClassifySeverity(absoluteDifference, toleranceAbsolute),
            Message = $"{fieldName} {messageSuffix}: study {targetValue}, field {actualValue}",
        });
    }
}