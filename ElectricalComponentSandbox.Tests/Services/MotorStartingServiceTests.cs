using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MotorStartingServiceTests
{
    private static MotorStartingService.MotorParameters MakeMotor(
        double hp = 50, double voltage = 480, double fla = 65, double lra = 0) =>
        new()
        {
            RatedHP = hp,
            RatedVoltage = voltage,
            FullLoadAmps = fla,
            LockedRotorAmps = lra,
        };

    // ── Inrush Multipliers ───────────────────────────────────────────────────

    [Fact]
    public void LrMultiplier_SmallMotor_Higher()
    {
        double small = MotorStartingService.GetTypicalLrMultiplier(
            MotorStartingService.MotorType.SquirrelCage, 3);
        double large = MotorStartingService.GetTypicalLrMultiplier(
            MotorStartingService.MotorType.SquirrelCage, 100);
        Assert.True(small > large);
    }

    [Fact]
    public void LrMultiplier_WoundRotor_Lower()
    {
        double wr = MotorStartingService.GetTypicalLrMultiplier(
            MotorStartingService.MotorType.WoundRotor, 50);
        double sc = MotorStartingService.GetTypicalLrMultiplier(
            MotorStartingService.MotorType.SquirrelCage, 50);
        Assert.True(wr < sc);
    }

    // ── Starting Method Factors ──────────────────────────────────────────────

    [Fact]
    public void StartingFactor_DOL_Is1()
    {
        Assert.Equal(1.0, MotorStartingService.GetStartingMethodFactor(
            MotorStartingService.StartingMethod.AcrossTheLine));
    }

    [Fact]
    public void StartingFactor_VFD_Lowest()
    {
        double vfd = MotorStartingService.GetStartingMethodFactor(
            MotorStartingService.StartingMethod.VariableFrequencyDrive);
        double dol = MotorStartingService.GetStartingMethodFactor(
            MotorStartingService.StartingMethod.AcrossTheLine);
        Assert.True(vfd < dol);
    }

    [Fact]
    public void StartingTorque_VFD_Highest()
    {
        double vfd = MotorStartingService.GetStartingTorquePercent(
            MotorStartingService.StartingMethod.VariableFrequencyDrive);
        double dol = MotorStartingService.GetStartingTorquePercent(
            MotorStartingService.StartingMethod.AcrossTheLine);
        Assert.True(vfd >= dol);
    }

    // ── Voltage Dip ──────────────────────────────────────────────────────────

    [Fact]
    public void VoltageDip_DOL_HighFault_Small()
    {
        var motor = MakeMotor(50, 480, 65, 390); // 6× FLA
        var result = MotorStartingService.CalculateVoltageDip(motor, 50000);
        Assert.True(result.VoltageDipPercent < 5);
        Assert.True(result.IsAcceptable);
    }

    [Fact]
    public void VoltageDip_DOL_LowFault_Large()
    {
        var motor = MakeMotor(200, 480, 240, 1440); // 6× FLA
        var result = MotorStartingService.CalculateVoltageDip(motor, 3000);
        Assert.True(result.VoltageDipPercent > 15);
        Assert.False(result.IsAcceptable);
    }

    [Fact]
    public void VoltageDip_ReducedMethod_Lower()
    {
        var motor = MakeMotor(100, 480, 124, 744);
        var dol = MotorStartingService.CalculateVoltageDip(motor, 5000,
            MotorStartingService.StartingMethod.AcrossTheLine);
        var ss = MotorStartingService.CalculateVoltageDip(motor, 5000,
            MotorStartingService.StartingMethod.SoftStarter);
        Assert.True(ss.VoltageDipPercent < dol.VoltageDipPercent);
    }

    [Fact]
    public void VoltageDip_UsesDefaultLR_WhenZero()
    {
        var motor = MakeMotor(50, 480, 65, 0); // LRA=0 → uses calculated
        var result = MotorStartingService.CalculateVoltageDip(motor, 10000);
        Assert.True(result.InrushAmps > 0);
    }

    [Fact]
    public void VoltageDip_VoltageDropped()
    {
        var motor = MakeMotor(50, 480, 65, 390);
        var result = MotorStartingService.CalculateVoltageDip(motor, 10000);
        Assert.True(result.VoltageDuringStarting < 480);
        Assert.True(result.VoltageDuringStarting > 0);
    }

    // ── Method Comparison ────────────────────────────────────────────────────

    [Fact]
    public void Compare_Returns5Methods()
    {
        var motor = MakeMotor(50, 480, 65, 390);
        var results = MotorStartingService.CompareStartingMethods(motor, 10000);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Compare_SortedByVoltageDip()
    {
        var motor = MakeMotor(50, 480, 65, 390);
        var results = MotorStartingService.CompareStartingMethods(motor, 10000);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i].VoltageDipPercent >= results[i - 1].VoltageDipPercent);
    }

    [Fact]
    public void Compare_VFD_AlwaysLowestDip()
    {
        var motor = MakeMotor(100, 480, 124, 744);
        var results = MotorStartingService.CompareStartingMethods(motor, 5000);
        Assert.Equal(MotorStartingService.StartingMethod.VariableFrequencyDrive,
            results[0].Method);
    }

    // ── Recommendation ───────────────────────────────────────────────────────

    [Fact]
    public void Recommend_HighFault_PrefersDOL()
    {
        var motor = MakeMotor(10, 480, 14, 84);
        var rec = MotorStartingService.RecommendStartingMethod(motor, 100000);
        Assert.NotNull(rec);
        Assert.Equal(MotorStartingService.StartingMethod.AcrossTheLine, rec!.Method);
    }

    [Fact]
    public void Recommend_LowFault_EscalatesMethod()
    {
        var motor = MakeMotor(200, 480, 240, 1440);
        var rec = MotorStartingService.RecommendStartingMethod(motor, 3000);
        Assert.NotNull(rec);
        Assert.NotEqual(MotorStartingService.StartingMethod.AcrossTheLine, rec!.Method);
    }

    [Fact]
    public void Recommend_VeryLowFault_MayReturnNull()
    {
        var motor = MakeMotor(500, 480, 600, 3600);
        // Very low fault current — even VFD might not be enough? Let's verify
        var rec = MotorStartingService.RecommendStartingMethod(motor, 100, maxDipPercent: 1);
        // With extreme constraints, may not find acceptable method
        // Just verify it handles gracefully
        Assert.True(rec == null || rec.MeetsVoltageDipLimit);
    }
}
