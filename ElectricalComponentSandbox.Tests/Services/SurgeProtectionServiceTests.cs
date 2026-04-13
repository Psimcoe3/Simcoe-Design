using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SurgeProtectionServiceTests
{
    // ── MCOV ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(480)]   // 480Y/277 → L-G 277 × 1.15 ≈ 319
    [InlineData(208)]   // 208Y/120 → L-G 120 × 1.15 ≈ 138
    public void Mcov_AtLeast115PercentLG(double voltage)
    {
        double mcov = SurgeProtectionService.GetMinimumMcov(voltage);
        double vLG = voltage / System.Math.Sqrt(3);
        Assert.True(mcov >= vLG * 1.15);
    }

    [Fact]
    public void Mcov_SinglePhase240()
    {
        double mcov = SurgeProtectionService.GetMinimumMcovSinglePhase(240);
        // 240/2 = 120 × 1.15 = 138
        Assert.True(mcov >= 138);
    }

    // ── Surge Rating ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SurgeProtectionService.SpdType.Type1, SurgeProtectionService.ExposureLevel.High, 200)]
    [InlineData(SurgeProtectionService.SpdType.Type2, SurgeProtectionService.ExposureLevel.Medium, 50)]
    [InlineData(SurgeProtectionService.SpdType.Type3, SurgeProtectionService.ExposureLevel.Low, 5)]
    public void SurgeRating_MinKA(
        SurgeProtectionService.SpdType type,
        SurgeProtectionService.ExposureLevel exposure,
        double expectedMin)
    {
        var (minKA, _) = SurgeProtectionService.GetSurgeRating(type, exposure);
        Assert.Equal(expectedMin, minKA);
    }

    [Fact]
    public void SurgeRating_RecommendedExceedsMin()
    {
        var (minKA, recKA) = SurgeProtectionService.GetSurgeRating(
            SurgeProtectionService.SpdType.Type2,
            SurgeProtectionService.ExposureLevel.Medium);
        Assert.True(recKA > minKA);
    }

    // ── VPR ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Vpr_Type3LowerThanType1()
    {
        double vpr1 = SurgeProtectionService.GetTypicalVpr(480, SurgeProtectionService.SpdType.Type1);
        double vpr3 = SurgeProtectionService.GetTypicalVpr(480, SurgeProtectionService.SpdType.Type3);
        Assert.True(vpr3 < vpr1);
    }

    [Fact]
    public void Vpr_480V_Positive()
    {
        double vpr = SurgeProtectionService.GetTypicalVpr(480, SurgeProtectionService.SpdType.Type2);
        Assert.True(vpr > 0);
    }

    // ── Protection Modes ─────────────────────────────────────────────────────

    [Fact]
    public void Modes_3PhaseWithNeutral()
    {
        string modes = SurgeProtectionService.GetProtectionModes(true, true);
        Assert.Contains("L-N", modes);
        Assert.Contains("L-G", modes);
        Assert.Contains("L-L", modes);
        Assert.Contains("N-G", modes);
    }

    [Fact]
    public void Modes_3PhaseNoNeutral()
    {
        string modes = SurgeProtectionService.GetProtectionModes(true, false);
        Assert.Contains("L-L", modes);
        Assert.DoesNotContain("L-N", modes);
    }

    // ── Full Specification ───────────────────────────────────────────────────

    [Fact]
    public void Specify_Type1_480V()
    {
        var spec = SurgeProtectionService.SpecifyDevice(
            SurgeProtectionService.SpdType.Type1, 480,
            threePhase: true, hasNeutral: true,
            exposure: SurgeProtectionService.ExposureLevel.High);
        Assert.Equal(SurgeProtectionService.SpdType.Type1, spec.Type);
        Assert.True(spec.McovVolts > 0);
        Assert.True(spec.MinSurgeRatingKA >= 200);
        Assert.True(spec.NecRequired);
        Assert.Contains("230.67", spec.Notes);
    }

    [Fact]
    public void Specify_Type2_208V()
    {
        var spec = SurgeProtectionService.SpecifyDevice(
            SurgeProtectionService.SpdType.Type2, 208,
            exposure: SurgeProtectionService.ExposureLevel.Medium);
        Assert.True(spec.NecRequired);
        Assert.Contains("242.24", spec.Notes);
    }

    [Fact]
    public void Specify_Type3_NotRequired()
    {
        var spec = SurgeProtectionService.SpecifyDevice(
            SurgeProtectionService.SpdType.Type3, 208);
        Assert.False(spec.NecRequired);
    }

    // ── Coordination ─────────────────────────────────────────────────────────

    [Fact]
    public void Coordination_FullCascade_MeetsNec()
    {
        var devices = new List<SurgeProtectionService.SpdSpecification>
        {
            SurgeProtectionService.SpecifyDevice(SurgeProtectionService.SpdType.Type1, 480),
            SurgeProtectionService.SpecifyDevice(SurgeProtectionService.SpdType.Type2, 480),
            SurgeProtectionService.SpecifyDevice(SurgeProtectionService.SpdType.Type3, 480),
        };
        var result = SurgeProtectionService.EvaluateCoordination(devices);
        Assert.True(result.MeetsNec242);
        Assert.True(result.HasServiceEntrance);
        Assert.True(result.HasDistribution);
        Assert.True(result.HasBranch);
    }

    [Fact]
    public void Coordination_NoSpd_NotCompliant()
    {
        var result = SurgeProtectionService.EvaluateCoordination(
            new List<SurgeProtectionService.SpdSpecification>());
        Assert.False(result.MeetsNec242);
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public void Coordination_Type1Only_RecommendsType2()
    {
        var devices = new List<SurgeProtectionService.SpdSpecification>
        {
            SurgeProtectionService.SpecifyDevice(SurgeProtectionService.SpdType.Type1, 480),
        };
        var result = SurgeProtectionService.EvaluateCoordination(devices);
        Assert.True(result.MeetsNec242);
        Assert.Contains(result.Recommendations, r => r.Contains("Type 2"));
    }
}
