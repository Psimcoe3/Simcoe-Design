using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class IslandingDetectionServiceTests
{
    [Fact]
    public void GetDefaultThresholds_60Hz_ReturnsExpectedWindow()
    {
        var result = IslandingDetectionService.GetDefaultThresholds();

        Assert.Equal(57.0, result.MinFrequencyHz);
        Assert.Equal(61.8, result.MaxFrequencyHz);
    }

    [Fact]
    public void GetDefaultThresholds_50Hz_ReturnsLowerWindow()
    {
        var result = IslandingDetectionService.GetDefaultThresholds(50);

        Assert.True(result.MaxFrequencyHz < 60);
    }

    [Fact]
    public void CalculateRocof_LargerFrequencyStep_IncreasesRocof()
    {
        double low = IslandingDetectionService.CalculateRocof(60, 59.9, 0.2);
        double high = IslandingDetectionService.CalculateRocof(60, 59.7, 0.2);

        Assert.True(high > low);
    }

    [Fact]
    public void AssessIslanding_WithinWindows_NoTrip()
    {
        var result = IslandingDetectionService.AssessIslanding(60.0, 1.0, 0.1, 2);

        Assert.False(result.TripRequired);
    }

    [Fact]
    public void AssessIslanding_UnderFrequency_Trips()
    {
        var result = IslandingDetectionService.AssessIslanding(56.8, 1.0, 0.1, 2);

        Assert.True(result.FrequencyTrip);
        Assert.True(result.TripRequired);
    }

    [Fact]
    public void AssessIslanding_OverVoltage_Trips()
    {
        var result = IslandingDetectionService.AssessIslanding(60.0, 1.12, 0.1, 2);

        Assert.True(result.VoltageTrip);
    }

    [Fact]
    public void AssessIslanding_HighRocof_Trips()
    {
        var result = IslandingDetectionService.AssessIslanding(60.0, 1.0, 0.8, 2);

        Assert.True(result.RocofTrip);
    }

    [Fact]
    public void AssessIslanding_HighVectorShift_Trips()
    {
        var result = IslandingDetectionService.AssessIslanding(60.0, 1.0, 0.1, 15);

        Assert.True(result.VectorShiftTrip);
    }

    [Fact]
    public void AssessIslanding_ReasonReflectsFirstTriggeredElement()
    {
        var result = IslandingDetectionService.AssessIslanding(56.8, 1.12, 0.8, 15);

        Assert.Contains("Frequency", result.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanReconnect_StableWindowAndTime_ReturnsTrue()
    {
        bool result = IslandingDetectionService.CanReconnect(60.0, 1.0, 300);

        Assert.True(result);
    }

    [Fact]
    public void CanReconnect_NotStableLongEnough_ReturnsFalse()
    {
        bool result = IslandingDetectionService.CanReconnect(60.0, 1.0, 120);

        Assert.False(result);
    }

    [Fact]
    public void CanReconnect_OutsideWindow_ReturnsFalse()
    {
        bool result = IslandingDetectionService.CanReconnect(56.8, 1.0, 600);

        Assert.False(result);
    }
}