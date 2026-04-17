using ElectricalComponentSandbox.Services;
using Xunit;
using static ElectricalComponentSandbox.Services.SymmetricalComponentsService;

namespace ElectricalComponentSandbox.Tests.Services;

public class SymmetricalComponentsServiceTests
{
    [Fact]
    public void CalculateBaseCurrent_StandardFormula()
    {
        // 10 MVA, 13.8 kV → I = 10000 / (√3 × 13.8) ≈ 418.4 A
        double result = SymmetricalComponentsService.CalculateBaseCurrent(10000, 13.8);
        Assert.True(result > 415 && result < 425);
    }

    [Fact]
    public void CalculateBaseCurrent_ZeroVoltage_ReturnsZero()
    {
        Assert.Equal(0, SymmetricalComponentsService.CalculateBaseCurrent(1000, 0));
    }

    [Fact]
    public void CalculateThreePhaseFault_SimpleImpedance()
    {
        // Z1 = 0 + j0.1 PU → I_3P = 1/0.1 = 10 PU
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.3 },
        };
        double result = SymmetricalComponentsService.CalculateThreePhaseFault(z);
        Assert.Equal(10.0, result, 2);
    }

    [Fact]
    public void CalculateSLGFault_HigherThan3P_WhenZ0IsSmall()
    {
        // Z1 = Z2 = j0.1, Z0 = j0.05 → I_SLG = 3 / |0.1+0.1+0.05| = 3/0.25 = 12 PU
        // 3P = 1/0.1 = 10 PU → SLG > 3P
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.05 },
        };
        double slg = SymmetricalComponentsService.CalculateSLGFault(z);
        double threePh = SymmetricalComponentsService.CalculateThreePhaseFault(z);
        Assert.True(slg > threePh);
        Assert.Equal(12.0, slg, 1);
    }

    [Fact]
    public void CalculateSLGFault_LowerThan3P_WhenZ0IsLarge()
    {
        // Z1 = Z2 = j0.1, Z0 = j0.5 → I_SLG = 3 / 0.7 ≈ 4.29 PU
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.5 },
        };
        double slg = SymmetricalComponentsService.CalculateSLGFault(z);
        double threePh = SymmetricalComponentsService.CalculateThreePhaseFault(z);
        Assert.True(slg < threePh);
    }

    [Fact]
    public void CalculateLLFault_IsAbout87PercentOf3P()
    {
        // Z1 = Z2 = j0.1 → I_LL = √3 / |0.1+0.1| = √3/0.2 ≈ 8.66
        // I_3P = 10 → ratio ≈ 0.866
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.3 },
        };
        double ll = SymmetricalComponentsService.CalculateLLFault(z);
        double threePh = SymmetricalComponentsService.CalculateThreePhaseFault(z);
        double ratio = ll / threePh;
        Assert.True(ratio > 0.85 && ratio < 0.88);
    }

    [Fact]
    public void CalculateDLGFault_GreaterThanLL()
    {
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.2 },
        };
        double dlg = SymmetricalComponentsService.CalculateDLGFault(z);
        double ll = SymmetricalComponentsService.CalculateLLFault(z);
        Assert.True(dlg > ll);
    }

    [Fact]
    public void CalculateFaultCurrent_ReturnsAmps()
    {
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.3 },
        };
        double result = SymmetricalComponentsService.CalculateFaultCurrent(
            FaultType.ThreePhase, z, 10000, 4.16);
        Assert.True(result > 10000); // 10 PU × base current
    }

    [Fact]
    public void RunFaultStudy_ProducesAllFourTypes()
    {
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0.01, X = 0.1 },
            Z2 = new Complex { R = 0.01, X = 0.1 },
            Z0 = new Complex { R = 0.02, X = 0.25 },
        };

        var result = SymmetricalComponentsService.RunFaultStudy(z, 10000, 4.16);

        Assert.Equal(4, result.Faults.Count);
        Assert.True(result.BaseCurrentAmps > 0);
        Assert.True(result.MaxFaultCurrentAmps > 0);
    }

    [Fact]
    public void RunFaultStudy_IdentifiesWorstCase()
    {
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.05 },
        };

        var result = SymmetricalComponentsService.RunFaultStudy(z, 10000, 4.16);

        // For this sequence-impedance mix, the implemented DLG model produces the largest current.
        Assert.Equal(FaultType.DoubleLineToGround, result.WorstCase);
    }

    [Fact]
    public void RunFaultStudy_MultiplierVsThreePhase_LLIsAbout0_87()
    {
        var z = new SequenceImpedance
        {
            Z1 = new Complex { R = 0, X = 0.1 },
            Z2 = new Complex { R = 0, X = 0.1 },
            Z0 = new Complex { R = 0, X = 0.3 },
        };

        var result = SymmetricalComponentsService.RunFaultStudy(z, 1000, 0.48);

        var llFault = result.Faults.Find(f => f.Type == FaultType.LineToLine);
        Assert.NotNull(llFault);
        Assert.True(llFault!.MultiplierVsThreePhase > 0.85 && llFault.MultiplierVsThreePhase < 0.88);
    }

    [Fact]
    public void EstimateSequenceImpedance_DefaultRatios_Z2EqualsZ1()
    {
        var z1 = new Complex { R = 0.01, X = 0.1 };
        var result = SymmetricalComponentsService.EstimateSequenceImpedance(z1);

        Assert.Equal(z1.R, result.Z2.R);
        Assert.Equal(z1.X, result.Z2.X);
        Assert.Equal(z1.R, result.Z0.R);
    }

    [Fact]
    public void EstimateSequenceImpedance_CustomRatios()
    {
        var z1 = new Complex { R = 0, X = 0.1 };
        var result = SymmetricalComponentsService.EstimateSequenceImpedance(z1, z2ToZ1Ratio: 1.0, z0ToZ1Ratio: 3.0);

        Assert.Equal(0.1, result.Z2.X, 4);
        Assert.Equal(0.3, result.Z0.X, 4);
    }

    [Fact]
    public void Complex_Magnitude_PythagoreanTheorem()
    {
        var c = new Complex { R = 3, X = 4 };
        Assert.Equal(5.0, c.Magnitude, 4);
    }

    [Fact]
    public void Complex_Addition_ComponentWise()
    {
        var a = new Complex { R = 1, X = 2 };
        var b = new Complex { R = 3, X = 4 };
        var result = a + b;
        Assert.Equal(4, result.R);
        Assert.Equal(6, result.X);
    }

    [Fact]
    public void Complex_Parallel_TwoEqualImpedances()
    {
        var z = new Complex { R = 0, X = 0.2 };
        var par = Complex.Parallel(z, z);
        // Two equal in parallel = half
        Assert.Equal(0.1, par.X, 4);
    }
}
