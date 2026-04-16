using ElectricalComponentSandbox.Services;
using Xunit;
using static ElectricalComponentSandbox.Services.SeismicQualificationService;

namespace ElectricalComponentSandbox.Tests.Services;

public class SeismicQualificationServiceTests
{
    [Theory]
    [InlineData(0.05, QualificationLevel.Low)]
    [InlineData(0.3, QualificationLevel.Moderate)]
    [InlineData(0.8, QualificationLevel.High)]
    public void DetermineLevel_ReturnsCorrectLevel(double sds, QualificationLevel expected)
    {
        Assert.Equal(expected, SeismicQualificationService.DetermineLevel(sds));
    }

    [Theory]
    [InlineData(ImportanceCategory.Standard, 1.0)]
    [InlineData(ImportanceCategory.Essential, 1.5)]
    public void GetIp_ReturnsCorrectFactor(ImportanceCategory cat, double expected)
    {
        Assert.Equal(expected, SeismicQualificationService.GetIp(cat));
    }

    [Fact]
    public void GetAp_TransformerReturnsRigid()
    {
        Assert.Equal(1.0, SeismicQualificationService.GetAp(EquipmentType.Transformer));
    }

    [Fact]
    public void GetAp_SwitchgearReturnsFlexible()
    {
        Assert.Equal(2.5, SeismicQualificationService.GetAp(EquipmentType.Switchgear));
    }

    [Fact]
    public void CalculateSeismicForce_GroundLevel_ProducesMinimumZOverH()
    {
        var input = new SeismicInput
        {
            Sds = 1.0,
            Equipment = EquipmentType.PanelBoard,
            EquipmentWeightLbs = 500,
            MountingHeightFeet = 0,
            BuildingHeightFeet = 40,
            Importance = ImportanceCategory.Standard,
        };

        var result = SeismicQualificationService.CalculateSeismicForce(input);

        // z/h = 0, so Fp = 0.4 × 2.5 × 1.0 × 500 / (2.5/1.0) × (1+0) = 200
        Assert.True(result.DesignForce > 0);
        Assert.True(result.DesignForce >= result.FpMin);
    }

    [Fact]
    public void CalculateSeismicForce_RoofLevel_HigherThanGround()
    {
        var ground = new SeismicInput
        {
            Sds = 1.0, Equipment = EquipmentType.PanelBoard,
            EquipmentWeightLbs = 500, MountingHeightFeet = 0, BuildingHeightFeet = 40,
        };
        var roof = new SeismicInput
        {
            Sds = 1.0, Equipment = EquipmentType.PanelBoard,
            EquipmentWeightLbs = 500, MountingHeightFeet = 40, BuildingHeightFeet = 40,
        };

        var groundResult = SeismicQualificationService.CalculateSeismicForce(ground);
        var roofResult = SeismicQualificationService.CalculateSeismicForce(roof);

        Assert.True(roofResult.DesignForce >= groundResult.DesignForce);
    }

    [Fact]
    public void CalculateSeismicForce_EssentialEquipment_HigherForce()
    {
        var standard = new SeismicInput
        {
            Sds = 0.8, Equipment = EquipmentType.Switchgear,
            EquipmentWeightLbs = 2000, MountingHeightFeet = 0, BuildingHeightFeet = 40,
            Importance = ImportanceCategory.Standard,
        };
        var essential = new SeismicInput
        {
            Sds = 0.8, Equipment = EquipmentType.Switchgear,
            EquipmentWeightLbs = 2000, MountingHeightFeet = 0, BuildingHeightFeet = 40,
            Importance = ImportanceCategory.Essential,
        };

        var sResult = SeismicQualificationService.CalculateSeismicForce(standard);
        var eResult = SeismicQualificationService.CalculateSeismicForce(essential);

        Assert.True(eResult.DesignForce > sResult.DesignForce);
    }

    [Fact]
    public void CalculateSeismicForce_BoundedByFpMinAndFpMax()
    {
        var input = new SeismicInput
        {
            Sds = 0.5, Equipment = EquipmentType.CableTray,
            EquipmentWeightLbs = 300, MountingHeightFeet = 20, BuildingHeightFeet = 40,
        };

        var result = SeismicQualificationService.CalculateSeismicForce(input);

        Assert.True(result.DesignForce >= result.FpMin);
        Assert.True(result.DesignForce <= result.FpMax);
    }

    [Fact]
    public void CalculateAnchorage_DistributesShearEvenly()
    {
        var forces = new SeismicForceResult { DesignForce = 1200 };

        var result = SeismicQualificationService.CalculateAnchorage(forces, 2000, 6.0, 4, 2.5);

        Assert.Equal(300.0, result.ShearPerAnchorLbs);
        Assert.Equal(4, result.NumberOfAnchors);
    }

    [Fact]
    public void CalculateAnchorage_HeavyEquipment_GravityRelievesTension()
    {
        var forces = new SeismicForceResult { DesignForce = 500 };

        // Very heavy equipment → gravity relief should dominate
        var result = SeismicQualificationService.CalculateAnchorage(forces, 10000, 4.0, 4, 3.0);

        Assert.Equal(0, result.TensionPerAnchorLbs);
    }

    [Fact]
    public void CalculateAnchorage_SelectsAnchorDiameter()
    {
        var forces = new SeismicForceResult { DesignForce = 2000 };

        var result = SeismicQualificationService.CalculateAnchorage(forces, 500, 7.0, 4, 2.0);

        Assert.False(string.IsNullOrEmpty(result.MinAnchorDiameter));
    }

    [Fact]
    public void Qualify_HighSds_RequiresShakeTableTest()
    {
        var input = new SeismicInput
        {
            Sds = 1.2, Equipment = EquipmentType.Switchgear,
            EquipmentWeightLbs = 3000, MountingHeightFeet = 0, BuildingHeightFeet = 40,
            NumberOfAnchors = 4,
        };

        var result = SeismicQualificationService.Qualify(input);

        Assert.Equal(QualificationLevel.High, result.Level);
        Assert.Contains(result.Requirements, r => r.Contains("shake table"));
    }

    [Fact]
    public void Qualify_BatteryRack_RequiresSeismicRestraint()
    {
        var input = new SeismicInput
        {
            Sds = 0.5, Equipment = EquipmentType.BatteryRack,
            EquipmentWeightLbs = 1500, MountingHeightFeet = 0, BuildingHeightFeet = 40,
            NumberOfAnchors = 4,
        };

        var result = SeismicQualificationService.Qualify(input);

        Assert.Contains(result.Requirements, r => r.Contains("Battery rack"));
    }

    [Fact]
    public void Qualify_Generator_RequiresSnubbers()
    {
        var input = new SeismicInput
        {
            Sds = 0.6, Equipment = EquipmentType.Generator,
            EquipmentWeightLbs = 5000, MountingHeightFeet = 0, BuildingHeightFeet = 40,
            NumberOfAnchors = 6,
        };

        var result = SeismicQualificationService.Qualify(input);

        Assert.Contains(result.Requirements, r => r.Contains("snubber"));
    }

    [Fact]
    public void Qualify_EssentialImportance_NotesOperationalRequirement()
    {
        var input = new SeismicInput
        {
            Sds = 0.4, Equipment = EquipmentType.UPS,
            EquipmentWeightLbs = 800, MountingHeightFeet = 0, BuildingHeightFeet = 40,
            Importance = ImportanceCategory.Essential,
            NumberOfAnchors = 4,
        };

        var result = SeismicQualificationService.Qualify(input);

        Assert.Contains(result.Requirements, r => r.Contains("Ip = 1.5"));
    }
}
