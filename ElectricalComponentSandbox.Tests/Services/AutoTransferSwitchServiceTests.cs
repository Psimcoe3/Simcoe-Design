using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class AutoTransferSwitchServiceTests
{
    // ── Transfer Time ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AutoTransferSwitchService.SystemClass.Emergency, 10.0)]
    [InlineData(AutoTransferSwitchService.SystemClass.LegallyRequired, 60.0)]
    [InlineData(AutoTransferSwitchService.SystemClass.Optional, 120.0)]
    public void GetMaxTransferTime_PerSystemClass(
        AutoTransferSwitchService.SystemClass cls, double expected)
    {
        Assert.Equal(expected, AutoTransferSwitchService.GetMaxTransferTimeSec(cls));
    }

    // ── ATS Sizing ───────────────────────────────────────────────────────────

    [Fact]
    public void SizeAts_Emergency_SelectsAdequateFrame()
    {
        var result = AutoTransferSwitchService.SizeAts(
            150, AutoTransferSwitchService.SystemClass.Emergency);

        Assert.True(result.SelectedFrameAmps >= 150);
        Assert.Equal(150, result.ContinuousAmps);
        Assert.Equal(AutoTransferSwitchService.AtsClass.Class3, result.Class); // 10s → Class3
    }

    [Fact]
    public void SizeAts_WithMotorLoads_HigherWithstand()
    {
        var noMotor = AutoTransferSwitchService.SizeAts(
            200, AutoTransferSwitchService.SystemClass.Emergency, hasMotorLoads: false);
        var withMotor = AutoTransferSwitchService.SizeAts(
            200, AutoTransferSwitchService.SystemClass.Emergency, hasMotorLoads: true);

        Assert.True(withMotor.WithstandAmps > noMotor.WithstandAmps);
    }

    [Fact]
    public void SizeAts_MotorLoad_SoftLoadTransfer()
    {
        var result = AutoTransferSwitchService.SizeAts(
            100, AutoTransferSwitchService.SystemClass.Emergency, hasMotorLoads: true);

        Assert.Equal(AutoTransferSwitchService.TransferType.SoftLoad, result.RecommendedTransfer);
    }

    [Fact]
    public void SizeAts_ClosedTransition_WhenRequired()
    {
        var result = AutoTransferSwitchService.SizeAts(
            100, AutoTransferSwitchService.SystemClass.Emergency,
            requiresClosedTransition: true);

        Assert.Equal(AutoTransferSwitchService.TransferType.Closed, result.RecommendedTransfer);
        Assert.True(result.RequiresClosedTransition);
    }

    [Fact]
    public void SizeAts_NoMotor_OpenTransfer()
    {
        var result = AutoTransferSwitchService.SizeAts(
            100, AutoTransferSwitchService.SystemClass.Optional);

        Assert.Equal(AutoTransferSwitchService.TransferType.Open, result.RecommendedTransfer);
    }

    [Fact]
    public void SizeAts_LegallyRequired_LongerTransferTime()
    {
        var emergency = AutoTransferSwitchService.SizeAts(
            100, AutoTransferSwitchService.SystemClass.Emergency);
        var legal = AutoTransferSwitchService.SizeAts(
            100, AutoTransferSwitchService.SystemClass.LegallyRequired);

        Assert.True(legal.MaxTransferTimeSec > emergency.MaxTransferTimeSec);
    }

    [Fact]
    public void SizeAts_SmallLoad_SmallestFrame()
    {
        var result = AutoTransferSwitchService.SizeAts(
            20, AutoTransferSwitchService.SystemClass.Optional);

        Assert.Equal(30, result.SelectedFrameAmps);
    }

    // ── Withstand Rating ─────────────────────────────────────────────────────

    [Fact]
    public void SelectWithstandRating_SelectsNextStandard()
    {
        var result = AutoTransferSwitchService.SelectWithstandRating(20);

        Assert.True(result.SelectedWithstandKA >= 20);
        Assert.Equal(22, result.SelectedWithstandKA);
        Assert.True(result.IsAdequate);
    }

    [Fact]
    public void SelectWithstandRating_ExceedsMax_StillReturns()
    {
        var result = AutoTransferSwitchService.SelectWithstandRating(999);

        Assert.Equal(200, result.SelectedWithstandKA);
        Assert.False(result.IsAdequate);
    }

    // ── Exercise Interval ────────────────────────────────────────────────────

    [Fact]
    public void GetExerciseInterval_Emergency_Weekly()
    {
        Assert.Equal(7, AutoTransferSwitchService.GetExerciseIntervalDays(
            AutoTransferSwitchService.SystemClass.Emergency));
    }

    [Fact]
    public void GetExerciseInterval_Optional_Monthly()
    {
        Assert.Equal(30, AutoTransferSwitchService.GetExerciseIntervalDays(
            AutoTransferSwitchService.SystemClass.Optional));
    }
}
