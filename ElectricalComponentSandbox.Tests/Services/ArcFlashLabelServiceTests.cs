using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.ArcFlashLabelService;

namespace ElectricalComponentSandbox.Tests.Services;

public class ArcFlashLabelServiceTests
{
    // ── Hazard Category Determination ────────────────────────────────────────

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(1.2, 0)]
    [InlineData(1.3, 1)]
    [InlineData(4.0, 1)]
    [InlineData(4.1, 2)]
    [InlineData(8.0, 2)]
    [InlineData(8.1, 3)]
    [InlineData(25.0, 3)]
    [InlineData(25.1, 4)]
    [InlineData(40.0, 4)]
    [InlineData(50.0, 4)]
    public void DetermineHazardCategory_CorrectPerNFPA70E(double incidentEnergy, int expected)
    {
        Assert.Equal(expected, ArcFlashLabelService.DetermineHazardCategory(incidentEnergy));
    }

    [Fact]
    public void DetermineHazardCategory_Zero_Category0()
    {
        Assert.Equal(0, ArcFlashLabelService.DetermineHazardCategory(0));
    }

    // ── PPE Descriptions ─────────────────────────────────────────────────────

    [Fact]
    public void GetPPEDescription_Category0_NoSpecialPPE()
    {
        var ppe = ArcFlashLabelService.GetPPEDescription(0);
        Assert.Contains("No PPE required", ppe);
    }

    [Fact]
    public void GetPPEDescription_Category1_4CalMin()
    {
        var ppe = ArcFlashLabelService.GetPPEDescription(1);
        Assert.Contains("4 cal/cm²", ppe);
    }

    [Fact]
    public void GetPPEDescription_Category2_8CalMin()
    {
        var ppe = ArcFlashLabelService.GetPPEDescription(2);
        Assert.Contains("8 cal/cm²", ppe);
    }

    [Fact]
    public void GetPPEDescription_Category3_25CalMin()
    {
        var ppe = ArcFlashLabelService.GetPPEDescription(3);
        Assert.Contains("25 cal/cm²", ppe);
    }

    [Fact]
    public void GetPPEDescription_Category4_40CalMin()
    {
        var ppe = ArcFlashLabelService.GetPPEDescription(4);
        Assert.Contains("40 cal/cm²", ppe);
    }

    [Fact]
    public void GetPPEDescription_Invalid_ReturnsCategory4()
    {
        var ppe = ArcFlashLabelService.GetPPEDescription(99);
        Assert.Contains("40 cal/cm²", ppe);
    }

    // ── Shock Boundaries ─────────────────────────────────────────────────────

    [Fact]
    public void GetShockBoundaries_Below50V_NoBoundary()
    {
        var (limited, restricted) = ArcFlashLabelService.GetShockBoundaries(48);
        Assert.Equal(0, limited);
        Assert.Equal(0, restricted);
    }

    [Theory]
    [InlineData(120, 42, 12)]
    [InlineData(208, 42, 12)]
    [InlineData(480, 42, 12)]
    public void GetShockBoundaries_Under600V(double voltage, double expectedLimited, double expectedRestricted)
    {
        var (limited, restricted) = ArcFlashLabelService.GetShockBoundaries(voltage);
        Assert.Equal(expectedLimited, limited);
        Assert.Equal(expectedRestricted, restricted);
    }

    [Fact]
    public void GetShockBoundaries_MediumVoltage_LargerBoundary()
    {
        var (limited, restricted) = ArcFlashLabelService.GetShockBoundaries(4160);
        Assert.True(limited > 42, "Medium voltage should have larger approach boundary");
    }

    // ── Requires Label ───────────────────────────────────────────────────────

    [Fact]
    public void RequiresLabel_Above50V_True()
    {
        Assert.True(ArcFlashLabelService.RequiresLabel(120));
    }

    [Fact]
    public void RequiresLabel_Below50V_False()
    {
        Assert.False(ArcFlashLabelService.RequiresLabel(24));
    }

    [Fact]
    public void RequiresLabel_At50V_True()
    {
        Assert.True(ArcFlashLabelService.RequiresLabel(50));
    }

    [Fact]
    public void RequiresLabel_NotServiceEquipment_False()
    {
        Assert.False(ArcFlashLabelService.RequiresLabel(480, isServiceEquipment: false));
    }

    // ── Label Generation ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateLabel_BasicLabel()
    {
        var arcData = new ArcFlashResult
        {
            NodeId = "MDP",
            NodeName = "Main Distribution Panel",
            BoltedFaultCurrentKA = 22.0,
            ArcingCurrentKA = 18.5,
            IncidentEnergyCal = 6.5,
            ArcFlashBoundaryInches = 48,
            HazardCategory = 2,
            RequiredPPE = "Cat 2",
        };

        var label = ArcFlashLabelService.GenerateLabel(arcData, 480, 18, "Electrical Room 1");

        Assert.Equal("MDP", label.EquipmentId);
        Assert.Equal("Main Distribution Panel", label.EquipmentName);
        Assert.Equal(480, label.NominalVoltage);
        Assert.Equal(6.5, label.IncidentEnergyCal);
        Assert.Equal(18, label.WorkingDistanceInches);
        Assert.Equal(48, label.ArcFlashBoundaryInches);
        Assert.Equal(2, label.HazardCategory);
        Assert.Equal("Category 2", label.HazardRiskCategory);
        Assert.Equal("Electrical Room 1", label.Location);
        Assert.Equal(22.0, label.BoltedFaultCurrentKA);
        Assert.Contains("8 cal/cm²", label.RequiredPPE);
    }

    [Fact]
    public void GenerateLabel_ContainsAllRequiredLines()
    {
        var arcData = new ArcFlashResult
        {
            NodeId = "P1",
            NodeName = "Panel LP-1",
            BoltedFaultCurrentKA = 10.0,
            IncidentEnergyCal = 2.5,
            ArcFlashBoundaryInches = 24,
        };

        var label = ArcFlashLabelService.GenerateLabel(arcData, 208);

        // NEC 110.16(B) required fields
        Assert.Contains(label.LabelLines, l => l.Contains("Voltage"));
        Assert.Contains(label.LabelLines, l => l.Contains("Incident Energy"));
        Assert.Contains(label.LabelLines, l => l.Contains("Arc Flash Boundary"));
        Assert.Contains(label.LabelLines, l => l.Contains("PPE"));
        Assert.Contains(label.LabelLines, l => l.Contains("Date"));
    }

    [Fact]
    public void GenerateLabel_DangerHeader()
    {
        var arcData = new ArcFlashResult { IncidentEnergyCal = 5.0 };
        var label = ArcFlashLabelService.GenerateLabel(arcData, 480);
        Assert.Contains("DANGER", label.WarningHeader);
    }

    [Fact]
    public void GenerateLabel_CustomDate()
    {
        var arcData = new ArcFlashResult { IncidentEnergyCal = 3.0 };
        var label = ArcFlashLabelService.GenerateLabel(arcData, 480, dateOfStudy: "2024-01-15");
        Assert.Equal("2024-01-15", label.DateOfStudy);
    }

    [Fact]
    public void GenerateLabel_ShockBoundaries_480V()
    {
        var arcData = new ArcFlashResult { IncidentEnergyCal = 5.0 };
        var label = ArcFlashLabelService.GenerateLabel(arcData, 480);
        Assert.Equal(42, label.LimitedApproachBoundaryInches);
        Assert.Equal(12, label.RestrictedApproachBoundaryInches);
    }

    // ── Batch Label Generation ───────────────────────────────────────────────

    [Fact]
    public void GenerateLabelsFromStudy_MultipleNodes()
    {
        var results = new[]
        {
            new ArcFlashResult { NodeId = "MDP", NodeName = "Main", IncidentEnergyCal = 12.0, ArcFlashBoundaryInches = 60 },
            new ArcFlashResult { NodeId = "P1", NodeName = "Panel 1", IncidentEnergyCal = 3.5, ArcFlashBoundaryInches = 30 },
            new ArcFlashResult { NodeId = "P2", NodeName = "Panel 2", IncidentEnergyCal = 1.0, ArcFlashBoundaryInches = 18 },
        };

        var labels = ArcFlashLabelService.GenerateLabelsFromStudy(results, 480, dateOfStudy: "2024-06-01");

        Assert.Equal(3, labels.Count);
        Assert.Equal("MDP", labels[0].EquipmentId);
        Assert.Equal(3, labels[0].HazardCategory); // 12 cal → cat 3
        Assert.Equal(1, labels[1].HazardCategory); // 3.5 cal → cat 1
        Assert.Equal(0, labels[2].HazardCategory); // 1.0 cal → cat 0
    }

    [Fact]
    public void GenerateLabelsFromStudy_Empty_ReturnsEmpty()
    {
        var labels = ArcFlashLabelService.GenerateLabelsFromStudy(Array.Empty<ArcFlashResult>(), 480);
        Assert.Empty(labels);
    }

    // ── Real-World Panel Label ───────────────────────────────────────────────

    [Fact]
    public void RealWorld_480V_SwitchboardLabel()
    {
        var arcData = new ArcFlashResult
        {
            NodeId = "MSB-1",
            NodeName = "Main Switchboard MSB-1",
            BoltedFaultCurrentKA = 42.0,
            ArcingCurrentKA = 35.0,
            IncidentEnergyCal = 28.0,
            ArcFlashBoundaryInches = 96,
            HazardCategory = 3,
        };

        var label = ArcFlashLabelService.GenerateLabel(arcData, 480, 18, "Main Electrical Room, Building A");

        Assert.Equal(4, label.HazardCategory); // 28 cal → category 4
        Assert.Contains("40 cal/cm²", label.RequiredPPE);
        Assert.Equal("Main Electrical Room, Building A", label.Location);
        Assert.True(label.LabelLines.Count >= 8);
    }
}
