using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class BusBracingServiceTests
{
    [Theory]
    [InlineData(BusBracingService.BusMaterial.Copper, 32000.0)]
    [InlineData(BusBracingService.BusMaterial.Aluminum, 21000.0)]
    public void GetYieldStrength_ReturnsMaterialValue(BusBracingService.BusMaterial material, double expected)
    {
        Assert.Equal(expected, BusBracingService.GetYieldStrength(material));
    }

    [Fact]
    public void GetAllowableStress_Is67PercentOfYield()
    {
        double yield = BusBracingService.GetYieldStrength(BusBracingService.BusMaterial.Copper);
        double allowable = BusBracingService.GetAllowableStress(BusBracingService.BusMaterial.Copper);
        Assert.Equal(0.67 * yield, allowable, 1);
    }

    [Fact]
    public void CalculateSectionModulus_RectangularBar()
    {
        var bus = new BusBracingService.BusBarSpec
        {
            Shape = BusBracingService.BusShape.Rectangular,
            WidthInches = 4.0,
            ThicknessInches = 0.25,
        };
        // S = 4 × 0.25² / 6 = 0.0417
        double result = BusBracingService.CalculateSectionModulus(bus);
        Assert.True(result > 0.04 && result < 0.05);
    }

    [Fact]
    public void CalculatePeakForce_IncreasesWithFaultCurrent()
    {
        var lowFault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 20 };
        var highFault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 65 };

        double lowForce = BusBracingService.CalculatePeakForce(lowFault, 8.0);
        double highForce = BusBracingService.CalculatePeakForce(highFault, 8.0);

        Assert.True(highForce > lowForce);
        // Force ∝ I², so high/low ≈ (65/20)² ≈ 10.6
        Assert.True(highForce / lowForce > 9);
    }

    [Fact]
    public void CalculatePeakForce_CloserSpacing_HigherForce()
    {
        var fault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 40 };

        double wideForce = BusBracingService.CalculatePeakForce(fault, 12.0);
        double closeForce = BusBracingService.CalculatePeakForce(fault, 4.0);

        Assert.True(closeForce > wideForce);
    }

    [Fact]
    public void CalculateBracingForce_LowFault_IsAdequate()
    {
        var bus = new BusBracingService.BusBarSpec
        {
            Material = BusBracingService.BusMaterial.Copper,
            Shape = BusBracingService.BusShape.Rectangular,
            WidthInches = 4.0,
            ThicknessInches = 0.25,
            PhaseSeparationInches = 8.0,
            SupportSpanInches = 24.0,
        };
        var fault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 10 };

        var result = BusBracingService.CalculateBracingForce(bus, fault);

        Assert.True(result.IsAdequate);
        Assert.True(result.StressRatio < 1.0);
    }

    [Fact]
    public void CalculateBracingForce_VeryHighFault_Fails()
    {
        var bus = new BusBracingService.BusBarSpec
        {
            Material = BusBracingService.BusMaterial.Aluminum,
            Shape = BusBracingService.BusShape.Rectangular,
            WidthInches = 2.0,
            ThicknessInches = 0.125,
            PhaseSeparationInches = 4.0,
            SupportSpanInches = 36.0,
        };
        var fault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 65 };

        var result = BusBracingService.CalculateBracingForce(bus, fault);

        Assert.False(result.IsAdequate);
        Assert.True(result.StressRatio > 1.0);
    }

    [Fact]
    public void CalculateInsulatorRequirement_Safety2Point5x()
    {
        var forces = new BusBracingService.BracingForceResult
        {
            TotalForceOnSpanLbs = 400,
            StressRatio = 0.5,
            IsAdequate = true,
        };
        var bus = new BusBracingService.BusBarSpec { SupportSpanInches = 24 };

        var result = BusBracingService.CalculateInsulatorRequirement(forces, bus);

        Assert.Equal(1000, result.MinCantileverStrengthLbs);
    }

    [Fact]
    public void CalculateInsulatorRequirement_OverStressed_ReducesMaxSpan()
    {
        var forces = new BusBracingService.BracingForceResult
        {
            TotalForceOnSpanLbs = 1000,
            StressRatio = 4.0,
            IsAdequate = false,
        };
        var bus = new BusBracingService.BusBarSpec { SupportSpanInches = 36 };

        var result = BusBracingService.CalculateInsulatorRequirement(forces, bus);

        Assert.True(result.MaxSupportSpanInches < 36);
        // span ∝ 1/√(ratio), so 36/√4 = 18
        Assert.True(result.MaxSupportSpanInches <= 18.5);
    }

    [Fact]
    public void Assess_AdequateBus_MeetsBracing()
    {
        var bus = new BusBracingService.BusBarSpec
        {
            Material = BusBracingService.BusMaterial.Copper,
            Shape = BusBracingService.BusShape.Rectangular,
            WidthInches = 4.0,
            ThicknessInches = 0.25,
            PhaseSeparationInches = 8.0,
            SupportSpanInches = 18.0,
        };
        var fault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 15 };

        var result = BusBracingService.Assess(bus, fault);

        Assert.True(result.MeetsBracingRequirement);
        Assert.True(result.WithstandRatingKA >= 15);
    }

    [Fact]
    public void Assess_InadequateBus_RecommendsReducedSpan()
    {
        var bus = new BusBracingService.BusBarSpec
        {
            Material = BusBracingService.BusMaterial.Aluminum,
            Shape = BusBracingService.BusShape.Rectangular,
            WidthInches = 2.0,
            ThicknessInches = 0.125,
            PhaseSeparationInches = 4.0,
            SupportSpanInches = 36.0,
        };
        var fault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 65 };

        var result = BusBracingService.Assess(bus, fault);

        Assert.False(result.MeetsBracingRequirement);
        Assert.Contains(result.Recommendations, r => r.Contains("reduce support span") || r.Contains("stress ratio"));
    }

    [Fact]
    public void Assess_TightSpacing_WarnsAboutClearance()
    {
        var bus = new BusBracingService.BusBarSpec
        {
            Material = BusBracingService.BusMaterial.Copper,
            Shape = BusBracingService.BusShape.Rectangular,
            WidthInches = 4.0,
            ThicknessInches = 0.25,
            PhaseSeparationInches = 3.0,
            SupportSpanInches = 12.0,
        };
        var fault = new BusBracingService.FaultCondition { SymmetricalFaultKA = 10 };

        var result = BusBracingService.Assess(bus, fault);

        Assert.Contains(result.Recommendations, r => r.Contains("clearance") || r.Contains("creepage"));
    }
}
