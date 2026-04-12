using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class MotorLoadServiceTests
{
    // ── NEC Table 430.248  Single-Phase FLA ──────────────────────────────────

    [Theory]
    [InlineData(0.5,  115,  9.8)]
    [InlineData(1.0,  115, 16.0)]
    [InlineData(1.5,  115, 20.0)]
    [InlineData(3.0,  115, 34.0)]
    [InlineData(5.0,  115, 56.0)]
    [InlineData(10.0, 115,100.0)]
    [InlineData(0.5,  230,  4.9)]
    [InlineData(1.0,  230,  8.0)]
    [InlineData(5.0,  230, 28.0)]
    [InlineData(10.0, 230, 50.0)]
    [InlineData(1.0,  200,  9.2)]
    [InlineData(5.0,  200, 32.2)]
    public void GetFLA_SinglePhase_ReturnsTableValue(double hp, double voltage, double expected)
    {
        double fla = MotorLoadService.GetFLA(hp, voltage, 1);
        Assert.Equal(expected, fla);
    }

    // ── NEC Table 430.250  Three-Phase FLA ──────────────────────────────────

    [Theory]
    [InlineData(1.0,  460,   1.6)]
    [InlineData(5.0,  460,   6.6)]
    [InlineData(10.0, 460,  12.0)]
    [InlineData(25.0, 460,  29.0)]
    [InlineData(50.0, 460,  55.0)]
    [InlineData(100.0,460, 105.0)]
    [InlineData(200.0,460, 198.0)]
    [InlineData(10.0, 208,  26.9)]
    [InlineData(10.0, 230,  24.0)]
    [InlineData(10.0, 200,  28.0)]
    [InlineData(10.0, 575,   9.6)]
    [InlineData(50.0, 208, 124.8)]
    [InlineData(75.0, 230, 162.0)]
    public void GetFLA_ThreePhase_ReturnsTableValue(double hp, double voltage, double expected)
    {
        double fla = MotorLoadService.GetFLA(hp, voltage, 3);
        Assert.Equal(expected, fla);
    }

    [Fact]
    public void GetFLA_ClosestHP_MatchesNearest()
    {
        // 0.33 HP in 1-phase table: closest is 1/3 HP = 0.333...
        double fla = MotorLoadService.GetFLA(1.0 / 3, 115, 1);
        Assert.Equal(7.2, fla);
    }

    [Fact]
    public void GetFLA_QuarterHP_SinglePhase115()
    {
        double fla = MotorLoadService.GetFLA(0.25, 115, 1);
        Assert.Equal(5.8, fla);
    }

    [Fact]
    public void GetFLA_SixthHP_SinglePhase115()
    {
        double fla = MotorLoadService.GetFLA(1.0 / 6, 115, 1);
        Assert.Equal(4.4, fla);
    }

    // ── Branch Circuit Sizing (NEC 430.22 / 430.52 / 430.32) ────────────────

    [Fact]
    public void SizeBranchCircuit_10HP460V3Phase_CorrectValues()
    {
        var motor = new MotorLoad { Id = "M1", HP = 10, Voltage = 460, Phase = 3 };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        Assert.Equal(12.0, result.FLA);
        // 125% of 12 = 15
        Assert.Equal(15.0, result.MinWireAmpacity);
        // Wire size for 15A at 75°C Cu
        Assert.NotEqual("---", result.RecommendedWireSize);
    }

    [Fact]
    public void SizeBranchCircuit_OCPD_DualElement_175Percent()
    {
        var motor = new MotorLoad { Id = "M2", HP = 10, Voltage = 460, Phase = 3 };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        // FLA=12, 175% = 21 → next standard = 25
        Assert.Equal(25, result.MaxOCPDAmpsDualElement);
    }

    [Fact]
    public void SizeBranchCircuit_OCPD_InverseTime_250Percent()
    {
        var motor = new MotorLoad { Id = "M3", HP = 10, Voltage = 460, Phase = 3 };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        // FLA=12, 250% = 30 → next standard = 30
        Assert.Equal(30, result.MaxOCPDAmpsInverse);
    }

    [Fact]
    public void SizeBranchCircuit_Overload_SF115_125Percent()
    {
        var motor = new MotorLoad
        {
            Id = "M4", HP = 10, Voltage = 460, Phase = 3,
            ServiceFactor = 1.15
        };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        // FLA=12, 125% = 15
        Assert.Equal(15.0, result.OverloadTripAmps);
    }

    [Fact]
    public void SizeBranchCircuit_Overload_SF100_115Percent()
    {
        var motor = new MotorLoad
        {
            Id = "M5", HP = 10, Voltage = 460, Phase = 3,
            ServiceFactor = 1.0
        };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        // FLA=12, 115% = 13.8
        Assert.Equal(13.8, result.OverloadTripAmps);
    }

    [Fact]
    public void SizeBranchCircuit_NameplateAmps_UsedForOverload()
    {
        var motor = new MotorLoad
        {
            Id = "M6", HP = 10, Voltage = 460, Phase = 3,
            NameplateAmps = 11.0, ServiceFactor = 1.15
        };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        // Overload uses nameplate (11A) not table FLA (12A), 125% = 13.75 → rounded 13.8
        Assert.Equal(13.8, result.OverloadTripAmps);
        // But wire and OCPD still based on table FLA (12A)
        Assert.Equal(12.0, result.FLA);
    }

    [Fact]
    public void SizeBranchCircuit_50HP460V_LargerValues()
    {
        var motor = new MotorLoad { Id = "M7", HP = 50, Voltage = 460, Phase = 3 };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        Assert.Equal(55.0, result.FLA);
        Assert.Equal(68.8, result.MinWireAmpacity);
        // 175% of 55 = 96.25 → next standard = 100
        Assert.Equal(100, result.MaxOCPDAmpsDualElement);
        // 250% of 55 = 137.5 → next standard = 150
        Assert.Equal(150, result.MaxOCPDAmpsInverse);
    }

    [Fact]
    public void SizeBranchCircuit_SinglePhase_Works()
    {
        var motor = new MotorLoad { Id = "M8", HP = 5, Voltage = 230, Phase = 1 };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        Assert.Equal(28.0, result.FLA);
        Assert.Equal(35.0, result.MinWireAmpacity);
    }

    // ── Feeder Sizing (NEC 430.24) ──────────────────────────────────────────

    [Fact]
    public void SizeMotorFeeder_TwoMotors_125PercentLargestPlusRest()
    {
        var motors = new[]
        {
            new MotorLoad { Id = "M1", HP = 10, Voltage = 460, Phase = 3 },
            new MotorLoad { Id = "M2", HP = 5,  Voltage = 460, Phase = 3 },
        };
        var result = MotorLoadService.SizeMotorFeeder(motors);

        // FLA: 12 + 6.6 = 18.6, largest = 12
        Assert.Equal(18.6, result.TotalFLA);
        Assert.Equal(12.0, result.LargestMotorFLA);
        // MinAmpacity = 18.6 + 0.25*12 = 21.6
        Assert.Equal(21.6, result.MinFeederAmpacity);
        Assert.Equal(2, result.MotorCount);
    }

    [Fact]
    public void SizeMotorFeeder_ThreeMotors_Correct()
    {
        var motors = new[]
        {
            new MotorLoad { Id = "M1", HP = 50, Voltage = 460, Phase = 3 },
            new MotorLoad { Id = "M2", HP = 25, Voltage = 460, Phase = 3 },
            new MotorLoad { Id = "M3", HP = 10, Voltage = 460, Phase = 3 },
        };
        var result = MotorLoadService.SizeMotorFeeder(motors);

        // FLAs: 55 + 29 + 12 = 96, largest = 55
        Assert.Equal(96.0, result.TotalFLA);
        Assert.Equal(55.0, result.LargestMotorFLA);
        // MinAmpacity = 96 + 0.25*55 = 109.75
        Assert.Equal(109.8, result.MinFeederAmpacity);
        Assert.Equal(3, result.MotorCount);
    }

    [Fact]
    public void SizeMotorFeeder_SingleMotor_125Percent()
    {
        var motors = new[] { new MotorLoad { Id = "M1", HP = 10, Voltage = 460, Phase = 3 } };
        var result = MotorLoadService.SizeMotorFeeder(motors);

        // FLA = 12, largest = 12, MinAmpacity = 12 + 3 = 15
        Assert.Equal(12.0, result.TotalFLA);
        Assert.Equal(15.0, result.MinFeederAmpacity);
    }

    [Fact]
    public void SizeMotorFeeder_Empty_ReturnsDefault()
    {
        var result = MotorLoadService.SizeMotorFeeder(Array.Empty<MotorLoad>());
        Assert.Equal(0, result.MotorCount);
        Assert.Equal(0, result.MinFeederAmpacity);
    }

    [Fact]
    public void SizeMotorFeeder_MixedPhase_Works()
    {
        var motors = new[]
        {
            new MotorLoad { Id = "M1", HP = 10, Voltage = 230, Phase = 1 },
            new MotorLoad { Id = "M2", HP = 10, Voltage = 460, Phase = 3 },
        };
        var result = MotorLoadService.SizeMotorFeeder(motors);

        // 1-phase 10HP/230V = 50A, 3-phase 10HP/460V = 12A
        Assert.Equal(62.0, result.TotalFLA);
        Assert.Equal(50.0, result.LargestMotorFLA);
    }

    [Fact]
    public void SizeMotorFeeder_RecommendedWireSize_NotEmpty()
    {
        var motors = new[]
        {
            new MotorLoad { Id = "M1", HP = 50, Voltage = 460, Phase = 3 },
            new MotorLoad { Id = "M2", HP = 25, Voltage = 460, Phase = 3 },
        };
        var result = MotorLoadService.SizeMotorFeeder(motors);
        Assert.NotEqual("---", result.RecommendedWireSize);
    }

    // ── 200HP Large Motor ───────────────────────────────────────────────────

    [Fact]
    public void SizeBranchCircuit_200HP3Phase460V_Correct()
    {
        var motor = new MotorLoad { Id = "MX", HP = 200, Voltage = 460, Phase = 3 };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        Assert.Equal(198.0, result.FLA);
        // 125% = 247.5
        Assert.Equal(247.5, result.MinWireAmpacity);
        // Dual-element: 175% = 346.5 → ceil = 347 → next standard = 350
        Assert.Equal(350, result.MaxOCPDAmpsDualElement);
        // Inverse-time: 250% = 495 → next standard = 500
        Assert.Equal(500, result.MaxOCPDAmpsInverse);
    }

    // ── 0.5HP small motors ──────────────────────────────────────────────────

    [Fact]
    public void SizeBranchCircuit_HalfHP3Phase460V()
    {
        var motor = new MotorLoad { Id = "MS", HP = 0.5, Voltage = 460, Phase = 3 };
        var result = MotorLoadService.SizeBranchCircuit(motor);

        Assert.Equal(0.9, result.FLA);
    }
}
