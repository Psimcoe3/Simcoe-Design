using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalRoomClearanceServiceTests
{
    // ── Depth Requirements ───────────────────────────────────────────────────

    [Theory]
    [InlineData(120,  ElectricalRoomClearanceService.ClearanceCondition.Condition1, 3.0)]
    [InlineData(120,  ElectricalRoomClearanceService.ClearanceCondition.Condition2, 3.0)]
    [InlineData(120,  ElectricalRoomClearanceService.ClearanceCondition.Condition3, 3.0)]
    [InlineData(208,  ElectricalRoomClearanceService.ClearanceCondition.Condition1, 3.0)]
    [InlineData(480,  ElectricalRoomClearanceService.ClearanceCondition.Condition2, 3.5)]
    [InlineData(480,  ElectricalRoomClearanceService.ClearanceCondition.Condition3, 4.0)]
    [InlineData(4160, ElectricalRoomClearanceService.ClearanceCondition.Condition1, 4.0)]
    [InlineData(4160, ElectricalRoomClearanceService.ClearanceCondition.Condition3, 6.0)]
    public void RequiredDepth_PerNEC(double voltage, ElectricalRoomClearanceService.ClearanceCondition cond, double expected)
    {
        double depth = ElectricalRoomClearanceService.GetRequiredDepth(voltage, cond);
        Assert.Equal(expected, depth);
    }

    // ── Width Requirements ───────────────────────────────────────────────────

    [Fact]
    public void RequiredWidth_Minimum30Inches()
    {
        double width = ElectricalRoomClearanceService.GetRequiredWidth(20);
        Assert.Equal(30, width);
    }

    [Fact]
    public void RequiredWidth_WideEquipment()
    {
        double width = ElectricalRoomClearanceService.GetRequiredWidth(48);
        Assert.Equal(48, width);
    }

    // ── Height Requirements ──────────────────────────────────────────────────

    [Fact]
    public void RequiredHeight_Minimum6Pt5()
    {
        double height = ElectricalRoomClearanceService.GetRequiredHeight(60);
        Assert.Equal(6.5, height);
    }

    [Fact]
    public void RequiredHeight_TallEquipment()
    {
        double height = ElectricalRoomClearanceService.GetRequiredHeight(84);
        Assert.Equal(7.0, height);
    }

    // ── Full Clearance Check ─────────────────────────────────────────────────

    [Fact]
    public void CheckClearance_AllCompliant()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P1", Name = "Panel-1",
            NominalVoltage = 208,
            Condition = ElectricalRoomClearanceService.ClearanceCondition.Condition1,
            EquipmentWidthInches = 20, EquipmentHeightInches = 60,
            ProvidedClearanceDepthFeet = 4.0,
            ProvidedClearanceWidthInches = 36,
            ProvidedClearanceHeightFeet = 8.0,
        };
        var result = ElectricalRoomClearanceService.CheckClearance(equip);
        Assert.True(result.FullyCompliant);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void CheckClearance_DepthViolation()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P2", Name = "Panel-2",
            NominalVoltage = 480,
            Condition = ElectricalRoomClearanceService.ClearanceCondition.Condition3,
            EquipmentWidthInches = 20, EquipmentHeightInches = 60,
            ProvidedClearanceDepthFeet = 3.0, // Required: 4.0
            ProvidedClearanceWidthInches = 36,
            ProvidedClearanceHeightFeet = 8.0,
        };
        var result = ElectricalRoomClearanceService.CheckClearance(equip);
        Assert.False(result.DepthCompliant);
        Assert.False(result.FullyCompliant);
        Assert.Contains(result.Violations, v => v.Contains("110.26(A)(1)"));
    }

    [Fact]
    public void CheckClearance_WidthViolation()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P3", Name = "Panel-3",
            NominalVoltage = 208,
            Condition = ElectricalRoomClearanceService.ClearanceCondition.Condition1,
            EquipmentWidthInches = 20, EquipmentHeightInches = 60,
            ProvidedClearanceDepthFeet = 4.0,
            ProvidedClearanceWidthInches = 24, // < 30" minimum
            ProvidedClearanceHeightFeet = 8.0,
        };
        var result = ElectricalRoomClearanceService.CheckClearance(equip);
        Assert.False(result.WidthCompliant);
        Assert.Contains(result.Violations, v => v.Contains("110.26(A)(2)"));
    }

    [Fact]
    public void CheckClearance_HeightViolation()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P4", Name = "Panel-4",
            NominalVoltage = 208,
            Condition = ElectricalRoomClearanceService.ClearanceCondition.Condition1,
            EquipmentWidthInches = 20, EquipmentHeightInches = 60,
            ProvidedClearanceDepthFeet = 4.0,
            ProvidedClearanceWidthInches = 36,
            ProvidedClearanceHeightFeet = 6.0, // < 6.5' minimum
        };
        var result = ElectricalRoomClearanceService.CheckClearance(equip);
        Assert.False(result.HeightCompliant);
        Assert.Contains(result.Violations, v => v.Contains("110.26(A)(3)"));
    }

    [Fact]
    public void CheckClearance_MultipleViolations()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P5", Name = "Panel-5",
            NominalVoltage = 480,
            Condition = ElectricalRoomClearanceService.ClearanceCondition.Condition3,
            EquipmentWidthInches = 20, EquipmentHeightInches = 60,
            ProvidedClearanceDepthFeet = 2.0,
            ProvidedClearanceWidthInches = 24,
            ProvidedClearanceHeightFeet = 5.0,
        };
        var result = ElectricalRoomClearanceService.CheckClearance(equip);
        Assert.Equal(3, result.Violations.Count);
    }

    // ── Dedicated Space ──────────────────────────────────────────────────────

    [Fact]
    public void DedicatedSpace_Compliant()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P1", EquipmentWidthInches = 20, EquipmentDepthInches = 6, EquipmentHeightInches = 72,
        };
        var result = ElectricalRoomClearanceService.CheckDedicatedSpace(equip, 24, 12, 15);
        Assert.True(result.Compliant);
    }

    [Fact]
    public void DedicatedSpace_HeightViolation()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P1", EquipmentWidthInches = 20, EquipmentDepthInches = 6, EquipmentHeightInches = 72,
        };
        // 72" = 6ft, need 6+6=12ft dedicated height
        var result = ElectricalRoomClearanceService.CheckDedicatedSpace(equip, 24, 12, 8);
        Assert.False(result.Compliant);
        Assert.Contains(result.Violations, v => v.Contains("110.26(E)"));
    }

    [Fact]
    public void DedicatedSpace_WidthViolation()
    {
        var equip = new ElectricalRoomClearanceService.EquipmentClearance
        {
            Id = "P1", EquipmentWidthInches = 36, EquipmentDepthInches = 6, EquipmentHeightInches = 72,
        };
        var result = ElectricalRoomClearanceService.CheckDedicatedSpace(equip, 24, 12, 15);
        Assert.False(result.Compliant);
    }

    // ── Batch Check ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckAll_MultipleEquipment()
    {
        var equipment = new[]
        {
            new ElectricalRoomClearanceService.EquipmentClearance
            {
                Id = "P1", Name = "Panel-1", NominalVoltage = 208,
                ProvidedClearanceDepthFeet = 4, ProvidedClearanceWidthInches = 36,
                ProvidedClearanceHeightFeet = 8,
            },
            new ElectricalRoomClearanceService.EquipmentClearance
            {
                Id = "P2", Name = "Panel-2", NominalVoltage = 480,
                Condition = ElectricalRoomClearanceService.ClearanceCondition.Condition3,
                ProvidedClearanceDepthFeet = 2, ProvidedClearanceWidthInches = 36,
                ProvidedClearanceHeightFeet = 8,
            },
        };
        var results = ElectricalRoomClearanceService.CheckAll(equipment);
        Assert.Equal(2, results.Count);
        Assert.True(results[0].FullyCompliant);
        Assert.False(results[1].FullyCompliant);
    }

    // ── High Voltage ─────────────────────────────────────────────────────────

    [Fact]
    public void RequiredDepth_MediumVoltage()
    {
        double depth = ElectricalRoomClearanceService.GetRequiredDepth(
            15000, ElectricalRoomClearanceService.ClearanceCondition.Condition2);
        Assert.Equal(6.0, depth);
    }

    [Fact]
    public void RequiredDepth_VeryHighVoltage_UsesMaxRow()
    {
        double depth = ElectricalRoomClearanceService.GetRequiredDepth(
            100000, ElectricalRoomClearanceService.ClearanceCondition.Condition3);
        Assert.Equal(12.0, depth);
    }
}
