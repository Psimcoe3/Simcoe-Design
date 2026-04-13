using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalSafetyServiceTests
{
    // ── Approach Boundaries ──────────────────────────────────────────────────

    [Fact]
    public void GetApproachBoundaries_120V_StandardDistances()
    {
        var result = ElectricalSafetyService.GetApproachBoundaries(120);

        Assert.Equal(3.5, result.LimitedApproachFt);
        Assert.Equal(1.0, result.RestrictedApproachFt);
        Assert.True(result.ProhibitedApproachFt > 0);
    }

    [Fact]
    public void GetApproachBoundaries_480V_SameLimited()
    {
        var result = ElectricalSafetyService.GetApproachBoundaries(480);

        Assert.Equal(3.5, result.LimitedApproachFt);
    }

    [Fact]
    public void GetApproachBoundaries_13800V_LargerDistances()
    {
        var lv = ElectricalSafetyService.GetApproachBoundaries(480);
        var mv = ElectricalSafetyService.GetApproachBoundaries(13800);

        Assert.True(mv.LimitedApproachFt > lv.LimitedApproachFt);
        Assert.True(mv.RestrictedApproachFt > lv.RestrictedApproachFt);
        Assert.True(mv.ArcFlashBoundaryFt > lv.ArcFlashBoundaryFt);
    }

    [Fact]
    public void GetApproachBoundaries_Below50V_AllZero()
    {
        var result = ElectricalSafetyService.GetApproachBoundaries(24);

        Assert.Equal(0, result.LimitedApproachFt);
        Assert.Equal(0, result.RestrictedApproachFt);
    }

    // ── PPE Category ─────────────────────────────────────────────────────────

    [Fact]
    public void DeterminePpe_LowFault_LowCategory()
    {
        var result = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Panelboard,
            5, 0.03, 18);

        // Low fault + fast clearing → low energy
        Assert.True(result.IncidentEnergyCalCm2 < 8);
    }

    [Fact]
    public void DeterminePpe_HighFault_HigherCategory()
    {
        var low = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Switchgear600V,
            10, 0.1, 18);
        var high = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Switchgear600V,
            50, 0.5, 18);

        Assert.True(high.IncidentEnergyCalCm2 > low.IncidentEnergyCalCm2);
        Assert.True(high.Category >= low.Category);
    }

    [Fact]
    public void DeterminePpe_LongerClearing_MoreEnergy()
    {
        var fast = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Mcc600V,
            20, 0.05, 18);
        var slow = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Mcc600V,
            20, 0.5, 18);

        Assert.True(slow.IncidentEnergyCalCm2 > fast.IncidentEnergyCalCm2);
    }

    [Fact]
    public void DeterminePpe_EwpRequired_AboveThreshold()
    {
        var result = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Switchgear600V,
            25, 0.5, 18);

        Assert.True(result.EnergizedWorkPermitRequired);
    }

    [Fact]
    public void DeterminePpe_ArcFlashBoundary_Positive()
    {
        var result = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Mcc600V,
            20, 0.2, 18);

        Assert.True(result.ArcFlashBoundaryFt > 0);
    }

    [Fact]
    public void DeterminePpe_MVSwitchgear_HigherEnergy()
    {
        var lv = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Switchgear600V,
            20, 0.2, 18);
        var mv = ElectricalSafetyService.DeterminePpe(
            ElectricalSafetyService.EquipmentClass.Switchgear15kV,
            20, 0.2, 36);

        // MV has higher Cf but greater working distance
        Assert.True(mv.IncidentEnergyCalCm2 > 0);
    }

    // ── Label Generation ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateLabel_LowEnergy_CautionWarning()
    {
        var label = ElectricalSafetyService.GenerateLabel(480, 0.8, 24);

        Assert.Equal(ElectricalSafetyService.PpeCategory.Category0, label.RequiredPpe);
        Assert.Contains("CAUTION", label.HazardWarning);
    }

    [Fact]
    public void GenerateLabel_HighEnergy_WarningText()
    {
        var label = ElectricalSafetyService.GenerateLabel(480, 12.0, 48);

        Assert.Equal(ElectricalSafetyService.PpeCategory.Category3, label.RequiredPpe);
        Assert.Contains("WARNING", label.HazardWarning);
    }

    [Fact]
    public void GenerateLabel_Above40_DangerDoNotWork()
    {
        var label = ElectricalSafetyService.GenerateLabel(13800, 55.0, 120);

        Assert.Contains("DANGER", label.HazardWarning);
        Assert.Contains("DO NOT WORK ENERGIZED", label.HazardWarning);
    }

    [Fact]
    public void GenerateLabel_CategoryMapsCorrectly()
    {
        Assert.Equal(ElectricalSafetyService.PpeCategory.Category1,
            ElectricalSafetyService.GenerateLabel(480, 3.0, 36).RequiredPpe);
        Assert.Equal(ElectricalSafetyService.PpeCategory.Category2,
            ElectricalSafetyService.GenerateLabel(480, 6.0, 48).RequiredPpe);
        Assert.Equal(ElectricalSafetyService.PpeCategory.Category4,
            ElectricalSafetyService.GenerateLabel(480, 35.0, 96).RequiredPpe);
    }
}
