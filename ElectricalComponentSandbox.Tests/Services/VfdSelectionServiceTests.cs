using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class VfdSelectionServiceTests
{
    private static VfdSelectionService.MotorSpec Pump() => new()
    {
        RatedHP = 25,
        RatedVoltage = 480,
        RatedFLA = 34,
        Duty = VfdSelectionService.MotorDuty.VariableTorque,
    };

    private static VfdSelectionService.MotorSpec Conveyor() => new()
    {
        RatedHP = 25,
        RatedVoltage = 480,
        RatedFLA = 34,
        Duty = VfdSelectionService.MotorDuty.ConstantTorque,
    };

    // ── VFD Sizing ───────────────────────────────────────────────────────────

    [Fact]
    public void SizeVfd_VariableTorque_ExactMatch()
    {
        var result = VfdSelectionService.SizeVfd(Pump());

        Assert.Equal(25, result.SelectedHP);
        Assert.Equal(34, result.OutputAmps);
    }

    [Fact]
    public void SizeVfd_ConstantTorque_OversizedFrame()
    {
        var result = VfdSelectionService.SizeVfd(Conveyor());

        // 25 × 1.15 = 28.75 → next standard = 30 HP
        Assert.True(result.SelectedHP >= 28.75);
        Assert.Equal(30, result.SelectedHP);
    }

    [Fact]
    public void SizeVfd_NoFLA_CalculatesFromHP()
    {
        var motor = new VfdSelectionService.MotorSpec
        {
            RatedHP = 10,
            RatedVoltage = 480,
            RatedFLA = 0,  // not provided
        };
        var result = VfdSelectionService.SizeVfd(motor);

        Assert.True(result.OutputAmps > 0);
    }

    [Fact]
    public void SizeVfd_InputAmps_GreaterThanOutput()
    {
        var result = VfdSelectionService.SizeVfd(Pump());

        Assert.True(result.InputAmps > result.OutputAmps);
    }

    [Theory]
    [InlineData(VfdSelectionService.VfdType.SixPulse, 30.0)]
    [InlineData(VfdSelectionService.VfdType.TwelvePulse, 12.0)]
    [InlineData(VfdSelectionService.VfdType.EighteenPulse, 5.0)]
    [InlineData(VfdSelectionService.VfdType.ActiveFrontEnd, 3.0)]
    public void SizeVfd_THDi_ByType(VfdSelectionService.VfdType type, double expectedTHDi)
    {
        var result = VfdSelectionService.SizeVfd(Pump(), type);

        Assert.Equal(expectedTHDi, result.EstimatedTHDiPercent);
    }

    // ── Harmonic Assessment ──────────────────────────────────────────────────

    [Fact]
    public void AssessHarmonics_6Pulse_HighTHDi()
    {
        var result = VfdSelectionService.AssessHarmonics(
            VfdSelectionService.VfdType.SixPulse, 50, 500);

        Assert.Equal(30, result.EstimatedITHDPercent);
    }

    [Fact]
    public void AssessHarmonics_AFE_MeetsIeee519()
    {
        var result = VfdSelectionService.AssessHarmonics(
            VfdSelectionService.VfdType.ActiveFrontEnd, 50, 500);

        Assert.True(result.MeetsIeee519);
    }

    [Fact]
    public void AssessHarmonics_LargeVfdOnSmallTransformer_HighVTHD()
    {
        var small = VfdSelectionService.AssessHarmonics(
            VfdSelectionService.VfdType.SixPulse, 50, 500);
        var large = VfdSelectionService.AssessHarmonics(
            VfdSelectionService.VfdType.SixPulse, 200, 500);

        Assert.True(large.EstimatedVTHDPercent > small.EstimatedVTHDPercent);
    }

    // ── Cable Derating ───────────────────────────────────────────────────────

    [Fact]
    public void DerateCable_ReducesAmpacity()
    {
        var result = VfdSelectionService.DerateCable(34, 50);

        Assert.True(result.DeratedAmps < result.StandardAmps);
        Assert.Equal(0.95, result.DeratingFactor);
    }

    [Fact]
    public void DerateCable_HigherFreq_ShorterCableLength()
    {
        var low = VfdSelectionService.DerateCable(34, 50, 2.0);
        var high = VfdSelectionService.DerateCable(34, 50, 12.0);

        Assert.True(high.MaxCableLengthFeet < low.MaxCableLengthFeet);
    }

    // ── Braking Resistor ─────────────────────────────────────────────────────

    [Fact]
    public void SizeBrakingResistor_CorrectPeakPower()
    {
        var result = VfdSelectionService.SizeBrakingResistor(25, 680);

        // 25 HP × 0.746 = 18.65 kW
        Assert.Equal(18.65, result.PeakBrakingKW, 2);
    }

    [Fact]
    public void SizeBrakingResistor_DutyCycle_AffectsContinuous()
    {
        var low = VfdSelectionService.SizeBrakingResistor(25, 680, 5);
        var high = VfdSelectionService.SizeBrakingResistor(25, 680, 20);

        Assert.True(high.ContinuousBrakingKW > low.ContinuousBrakingKW);
    }

    [Fact]
    public void SizeBrakingResistor_ResistorOhms_Positive()
    {
        var result = VfdSelectionService.SizeBrakingResistor(50, 680);

        Assert.True(result.ResistorOhms > 0);
        Assert.True(result.ResistorWatts > 0);
    }
}
