using ElectricalComponentSandbox.Services;
using Xunit;
using static ElectricalComponentSandbox.Services.HazardousAreaClassificationService;

namespace ElectricalComponentSandbox.Tests.Services;

public class HazardousAreaClassificationServiceTests
{
    [Theory]
    [InlineData(TemperatureClass.T1, 450)]
    [InlineData(TemperatureClass.T4, 135)]
    [InlineData(TemperatureClass.T6, 85)]
    public void GetMaxSurfaceTemp_ReturnsCorrectValue(TemperatureClass tc, double expected)
    {
        Assert.Equal(expected, HazardousAreaClassificationService.GetMaxSurfaceTemp(tc));
    }

    [Fact]
    public void DivisionToZone_ClassI_Division1_ReturnsZone1()
    {
        var result = HazardousAreaClassificationService.DivisionToZone(HazardClass.ClassI, Division.Division1);
        Assert.Equal(Zone.Zone1, result);
    }

    [Fact]
    public void DivisionToZone_ClassI_Division2_ReturnsZone2()
    {
        var result = HazardousAreaClassificationService.DivisionToZone(HazardClass.ClassI, Division.Division2);
        Assert.Equal(Zone.Zone2, result);
    }

    [Fact]
    public void DivisionToZone_ClassII_Division1_ReturnsZone21()
    {
        var result = HazardousAreaClassificationService.DivisionToZone(HazardClass.ClassII, Division.Division1);
        Assert.Equal(Zone.Zone21, result);
    }

    [Fact]
    public void DivisionToZone_Unclassified_ReturnsUnclassified()
    {
        var result = HazardousAreaClassificationService.DivisionToZone(HazardClass.ClassI, Division.Unclassified);
        Assert.Equal(Zone.Unclassified, result);
    }

    [Fact]
    public void GetEquipmentRequirement_ClassI_Division1_ReturnsExplosionproof()
    {
        var result = HazardousAreaClassificationService.GetEquipmentRequirement(
            HazardClass.ClassI, Division.Division1, MaterialGroup.GroupD, TemperatureClass.T3);

        Assert.Equal(ProtectionMethod.Explosionproof, result.Method);
        Assert.Contains("501.10(A)", result.NecArticle);
        Assert.True(result.IntrinsicSafetyPermitted);
    }

    [Fact]
    public void GetEquipmentRequirement_ClassI_Division2_ReturnsNonIncendive()
    {
        var result = HazardousAreaClassificationService.GetEquipmentRequirement(
            HazardClass.ClassI, Division.Division2, MaterialGroup.GroupD, TemperatureClass.T3);

        Assert.Equal(ProtectionMethod.NonIncendive, result.Method);
        Assert.Contains("501.10(B)", result.NecArticle);
    }

    [Fact]
    public void GetEquipmentRequirement_ClassII_Division1_ReturnsDustIgnitionproof()
    {
        var result = HazardousAreaClassificationService.GetEquipmentRequirement(
            HazardClass.ClassII, Division.Division1, MaterialGroup.GroupG, TemperatureClass.T4);

        Assert.Equal(ProtectionMethod.DustIgnitionproof, result.Method);
        Assert.Contains("502.10(A)", result.NecArticle);
    }

    [Fact]
    public void GetEquipmentRequirement_ClassIII_Division1_ReturnsDustTight()
    {
        var result = HazardousAreaClassificationService.GetEquipmentRequirement(
            HazardClass.ClassIII, Division.Division1, MaterialGroup.GroupG, TemperatureClass.T4);

        Assert.Equal(ProtectionMethod.DustTight, result.Method);
    }

    [Fact]
    public void GetEquipmentRequirement_Unclassified_ReturnsGeneralPurpose()
    {
        var result = HazardousAreaClassificationService.GetEquipmentRequirement(
            HazardClass.ClassI, Division.Unclassified, MaterialGroup.GroupD, TemperatureClass.T3);

        Assert.Equal(ProtectionMethod.GeneralPurpose, result.Method);
    }

    [Fact]
    public void IsTempClassAdequate_BelowAIT_ReturnsTrue()
    {
        // T3 max = 200°C, AIT = 365°C (gasoline) → adequate
        Assert.True(HazardousAreaClassificationService.IsTempClassAdequate(TemperatureClass.T3, 365));
    }

    [Fact]
    public void IsTempClassAdequate_AtOrAboveAIT_ReturnsFalse()
    {
        // T1 max = 450°C, AIT = 300°C → NOT adequate
        Assert.False(HazardousAreaClassificationService.IsTempClassAdequate(TemperatureClass.T1, 300));
    }

    [Fact]
    public void RecommendTempClass_GasolineAIT_ReturnsT3OrLower()
    {
        // Gasoline AIT ≈ 280°C → T2B (max 260°C) is the highest safe class below 280°C
        var result = HazardousAreaClassificationService.RecommendTempClass(280);
        double maxTemp = HazardousAreaClassificationService.GetMaxSurfaceTemp(result);
        Assert.True(maxTemp < 280);
    }

    [Fact]
    public void RecommendTempClass_LowAIT_ReturnsT6()
    {
        // Very low AIT (90°C) → T6 (85°C) is the only option
        var result = HazardousAreaClassificationService.RecommendTempClass(90);
        Assert.Equal(TemperatureClass.T6, result);
    }

    [Fact]
    public void ClassifyArea_Division1_IncludesNormalOperationWarning()
    {
        var result = HazardousAreaClassificationService.ClassifyArea(
            HazardClass.ClassI, Division.Division1, MaterialGroup.GroupD, TemperatureClass.T3);

        Assert.Contains(result.Warnings, w => w.Contains("Division 1"));
        Assert.Equal(Zone.Zone1, result.Area.Zone);
    }

    [Fact]
    public void ClassifyArea_GroupA_IncludesHeightenedProtectionWarning()
    {
        var result = HazardousAreaClassificationService.ClassifyArea(
            HazardClass.ClassI, Division.Division1, MaterialGroup.GroupA, TemperatureClass.T2);

        Assert.Contains(result.Warnings, w => w.Contains("Group A/B"));
    }

    [Fact]
    public void ClassifyArea_TClassExceedsAIT_WarnsAboutTClass()
    {
        var result = HazardousAreaClassificationService.ClassifyArea(
            HazardClass.ClassI, Division.Division2, MaterialGroup.GroupD,
            TemperatureClass.T1, autoIgnitionTempC: 300);

        Assert.Contains(result.Warnings, w => w.Contains("T-class"));
    }

    [Fact]
    public void ClassifyArea_Description_ContainsMaterialInfo()
    {
        var result = HazardousAreaClassificationService.ClassifyArea(
            HazardClass.ClassII, Division.Division2, MaterialGroup.GroupG, TemperatureClass.T4);

        Assert.Contains("dust", result.Area.Description, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GroupG", result.Area.Description);
    }
}
