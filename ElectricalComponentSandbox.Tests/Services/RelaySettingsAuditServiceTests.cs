using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class RelaySettingsAuditServiceTests
{
    private static ProtectiveRelayService.RelaySettings CreateSettings() => new()
    {
        Id = "RLY-1",
        Function = ProtectiveRelayService.RelayFunction.Function51,
        Curve = ProtectiveRelayService.CurveType.VeryInverse,
        CtRatio = 200,
        PickupAmps = 300,
        TimeDial = 0.7,
        InstantaneousAmps = 1800,
    };

    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(100, 105, 5)]
    [InlineData(100, 120, 20)]
    [InlineData(0, 50, 100)]
    public void CalculatePercentDifference_ReturnsExpectedResult(double target, double actual, double expected)
    {
        Assert.Equal(expected, RelaySettingsAuditService.CalculatePercentDifference(target, actual), 2);
    }

    [Theory]
    [InlineData(2, 5, RelaySettingsAuditService.SettingSeverity.Info)]
    [InlineData(8, 5, RelaySettingsAuditService.SettingSeverity.Warning)]
    [InlineData(12, 5, RelaySettingsAuditService.SettingSeverity.Critical)]
    public void ClassifySeverity_UsesToleranceBands(double difference, double tolerance, RelaySettingsAuditService.SettingSeverity expected)
    {
        Assert.Equal(expected, RelaySettingsAuditService.ClassifySeverity(difference, tolerance));
    }

    [Fact]
    public void AuditSettings_ExactMatchHasNoVariances()
    {
        var target = CreateSettings();
        var result = RelaySettingsAuditService.AuditSettings(target, target);

        Assert.True(result.MatchesStudy);
        Assert.Empty(result.Variances);
    }

    [Fact]
    public void AuditSettings_PickupOutsideToleranceCreatesWarning()
    {
        var target = CreateSettings();
        var actual = CreateSettings() with { PickupAmps = 321 };
        var result = RelaySettingsAuditService.AuditSettings(target, actual);

        var variance = Assert.Single(result.Variances);
        Assert.Equal("PickupAmps", variance.FieldName);
        Assert.Equal(RelaySettingsAuditService.SettingSeverity.Warning, variance.Severity);
    }

    [Fact]
    public void AuditSettings_LargeInstantaneousMismatchIsCritical()
    {
        var target = CreateSettings();
        var actual = CreateSettings() with { InstantaneousAmps = 2500 };
        var result = RelaySettingsAuditService.AuditSettings(target, actual);

        Assert.Contains(result.Variances, variance => variance.FieldName == "InstantaneousAmps" && variance.Severity == RelaySettingsAuditService.SettingSeverity.Critical);
        Assert.Equal(1, result.CriticalCount);
    }

    [Fact]
    public void AuditSettings_CurveMismatchIsCritical()
    {
        var target = CreateSettings();
        var actual = CreateSettings() with { Curve = ProtectiveRelayService.CurveType.ExtremelyInverse };
        var result = RelaySettingsAuditService.AuditSettings(target, actual);

        Assert.Contains(result.Variances, variance => variance.FieldName == "Curve" && variance.Severity == RelaySettingsAuditService.SettingSeverity.Critical);
    }

    [Fact]
    public void AuditSettings_CtMismatchCreatesVariance()
    {
        var target = CreateSettings();
        var actual = CreateSettings() with { CtRatio = 150 };
        var result = RelaySettingsAuditService.AuditSettings(target, actual);

        Assert.Contains(result.Variances, variance => variance.FieldName == "CtRatio");
    }

    [Fact]
    public void AuditSettings_CustomToleranceSuppressesSmallPickupDrift()
    {
        var target = CreateSettings();
        var actual = CreateSettings() with { PickupAmps = 312 };
        var result = RelaySettingsAuditService.AuditSettings(target, actual, new RelaySettingsAuditService.AuditTolerance { PickupPercentTolerance = 5.0 });

        Assert.True(result.MatchesStudy);
        Assert.DoesNotContain(result.Variances, variance => variance.FieldName == "PickupAmps");
    }

    [Fact]
    public void AuditSettings_TimeDialDifferenceUsesAbsoluteTolerance()
    {
        var target = CreateSettings();
        var actual = CreateSettings() with { TimeDial = 0.95 };
        var result = RelaySettingsAuditService.AuditSettings(target, actual, new RelaySettingsAuditService.AuditTolerance { TimeDialTolerance = 0.1 });

        Assert.Contains(result.Variances, variance => variance.FieldName == "TimeDial" && variance.Severity == RelaySettingsAuditService.SettingSeverity.Critical);
    }

    [Fact]
    public void AuditPortfolio_AuditsMultiplePairs()
    {
        var target = CreateSettings();
        var results = RelaySettingsAuditService.AuditPortfolio(new[]
        {
            (target, target),
            (target, target with { PickupAmps = 330 }),
        });

        Assert.Equal(2, results.Count);
        Assert.Single(results.Where(result => !result.MatchesStudy));
    }
}