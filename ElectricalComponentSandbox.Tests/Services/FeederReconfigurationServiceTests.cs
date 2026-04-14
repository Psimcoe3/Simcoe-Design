using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class FeederReconfigurationServiceTests
{
    [Fact]
    public void CalculateTransferCapacity_IsLimitedBySmallestConstraint()
    {
        double result = FeederReconfigurationService.CalculateTransferCapacity(180, 150, 160);

        Assert.Equal(150, result);
    }

    [Fact]
    public void EvaluateOption_ComputesUnservedLoad_WhenTieIsLimited()
    {
        var option = new FeederReconfigurationService.TieSwitchOption
        {
            SwitchName = "Tie-1",
            TieCapacityAmps = 120,
            SourceHeadroomAmps = 200,
        };

        var result = FeederReconfigurationService.EvaluateOption(160, option);

        Assert.Equal(120, result.RestoredLoadAmps);
        Assert.Equal(40, result.UnservedLoadAmps);
    }

    [Fact]
    public void EvaluateOption_PreservesOpenPointName()
    {
        var option = new FeederReconfigurationService.TieSwitchOption
        {
            SwitchName = "Tie-2",
            OpenPointName = "NO-Section-7",
            TieCapacityAmps = 160,
            SourceHeadroomAmps = 180,
        };

        var result = FeederReconfigurationService.EvaluateOption(120, option);

        Assert.Equal("NO-Section-7", result.OpenPointName);
    }

    [Fact]
    public void EvaluateOption_NonRadialSequence_FlagsIssue()
    {
        var option = new FeederReconfigurationService.TieSwitchOption
        {
            SwitchName = "Tie-3",
            TieCapacityAmps = 180,
            SourceHeadroomAmps = 200,
            MaintainsRadialConfiguration = false,
        };

        var result = FeederReconfigurationService.EvaluateOption(100, option);

        Assert.False(result.IsRadial);
        Assert.Contains("radial", result.Issue);
    }

    [Fact]
    public void EvaluateOption_SectionOverload_FailsEmergencyRatingCheck()
    {
        var option = new FeederReconfigurationService.TieSwitchOption
        {
            SwitchName = "Tie-4",
            TieCapacityAmps = 180,
            SourceHeadroomAmps = 180,
            AffectedSections =
            {
                new FeederReconfigurationService.SectionTransferImpact
                {
                    SectionName = "North Main",
                    ExistingLoadAmps = 190,
                    AddedLoadAmpsAtFullTransfer = 70,
                    EmergencyRatingAmps = 240,
                },
            },
        };

        var result = FeederReconfigurationService.EvaluateOption(140, option);

        Assert.False(result.PassesEmergencyRatings);
        Assert.Contains("North Main", result.Issue);
    }

    [Fact]
    public void EvaluateOption_HealthyTransfer_Passes()
    {
        var option = new FeederReconfigurationService.TieSwitchOption
        {
            SwitchName = "Tie-5",
            TieCapacityAmps = 180,
            SourceHeadroomAmps = 200,
            AffectedSections =
            {
                new FeederReconfigurationService.SectionTransferImpact
                {
                    SectionName = "East Main",
                    ExistingLoadAmps = 110,
                    AddedLoadAmpsAtFullTransfer = 40,
                    EmergencyRatingAmps = 190,
                },
                new FeederReconfigurationService.SectionTransferImpact
                {
                    SectionName = "West Main",
                    ExistingLoadAmps = 100,
                    AddedLoadAmpsAtFullTransfer = 30,
                    EmergencyRatingAmps = 180,
                },
            },
        };

        var result = FeederReconfigurationService.EvaluateOption(120, option);

        Assert.True(result.IsRadial);
        Assert.True(result.PassesEmergencyRatings);
        Assert.All(result.Sections, section => Assert.False(section.IsOverloaded));
    }

    [Fact]
    public void SelectBestOption_PrefersFullyRestoredRadialPlan()
    {
        var result = FeederReconfigurationService.SelectBestOption(120, new[]
        {
            new FeederReconfigurationService.TieSwitchOption
            {
                SwitchName = "Limited",
                TieCapacityAmps = 90,
                SourceHeadroomAmps = 200,
            },
            new FeederReconfigurationService.TieSwitchOption
            {
                SwitchName = "Full",
                TieCapacityAmps = 150,
                SourceHeadroomAmps = 180,
            },
        });

        Assert.Equal("Full", result.SwitchName);
        Assert.Equal(120, result.RestoredLoadAmps);
    }

    [Fact]
    public void SelectBestOption_PrefersRadialPlanOverNonRadialAlternative()
    {
        var result = FeederReconfigurationService.SelectBestOption(100, new[]
        {
            new FeederReconfigurationService.TieSwitchOption
            {
                SwitchName = "Looped",
                TieCapacityAmps = 150,
                SourceHeadroomAmps = 150,
                MaintainsRadialConfiguration = false,
            },
            new FeederReconfigurationService.TieSwitchOption
            {
                SwitchName = "Radial",
                TieCapacityAmps = 130,
                SourceHeadroomAmps = 130,
                MaintainsRadialConfiguration = true,
            },
        });

        Assert.Equal("Radial", result.SwitchName);
        Assert.True(result.IsRadial);
    }

    [Fact]
    public void SelectBestOption_WithoutOptions_ReturnsIssue()
    {
        var result = FeederReconfigurationService.SelectBestOption(100, new FeederReconfigurationService.TieSwitchOption[0]);

        Assert.Equal("No tie-switch options were provided", result.Issue);
    }
}