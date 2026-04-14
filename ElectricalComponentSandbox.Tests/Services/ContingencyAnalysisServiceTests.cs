using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ContingencyAnalysisServiceTests
{
    [Fact]
    public void CalculatePostContingencyLoad_AddsTransferredLoad()
    {
        double result = ContingencyAnalysisService.CalculatePostContingencyLoad(180, 55);

        Assert.Equal(235, result);
    }

    [Fact]
    public void EvaluateSection_WithinEmergencyRating_Passes()
    {
        var section = new ContingencyAnalysisService.FeederSection
        {
            Name = "Main-1",
            NormalLoadAmps = 220,
            EmergencyRatingAmps = 300,
        };

        var result = ContingencyAnalysisService.EvaluateSection(section, 40);

        Assert.False(result.IsOverloaded);
        Assert.Equal(86.7, result.LoadingPercent, 1);
    }

    [Fact]
    public void EvaluateSection_AboveEmergencyRating_FlagsOverload()
    {
        var section = new ContingencyAnalysisService.FeederSection
        {
            Name = "Tie-2",
            NormalLoadAmps = 180,
            EmergencyRatingAmps = 250,
        };

        var result = ContingencyAnalysisService.EvaluateSection(section, 90);

        Assert.True(result.IsOverloaded);
        Assert.True(result.LoadingPercent > 100);
    }

    [Fact]
    public void AnalyzeScenario_NoRestorationPath_Fails()
    {
        var scenario = new ContingencyAnalysisService.ContingencyScenario
        {
            Name = "Substation Loss",
            OutagedElementName = "Source A",
            CanRestoreLoad = false,
        };

        var result = ContingencyAnalysisService.AnalyzeScenario(scenario);

        Assert.False(result.PassesNMinusOne);
        Assert.Equal("No restoration path available", result.LimitingConstraint);
    }

    [Fact]
    public void AnalyzeScenario_SourceOverload_Fails()
    {
        var scenario = new ContingencyAnalysisService.ContingencyScenario
        {
            Name = "Transformer Outage",
            OutagedElementName = "XFMR-1",
            ReceivingSourceNormalLoadAmps = 420,
            ReceivingSourceEmergencyRatingAmps = 500,
            TransferredLoadAmps = 140,
        };

        var result = ContingencyAnalysisService.AnalyzeScenario(scenario);

        Assert.False(result.PassesNMinusOne);
        Assert.True(result.SourceOverloaded);
        Assert.Contains("Source loading", result.LimitingConstraint);
    }

    [Fact]
    public void AnalyzeScenario_SectionOverload_Fails()
    {
        var scenario = new ContingencyAnalysisService.ContingencyScenario
        {
            Name = "Feeder Section Outage",
            OutagedElementName = "Section B",
            ReceivingSourceNormalLoadAmps = 300,
            ReceivingSourceEmergencyRatingAmps = 500,
            TransferredLoadAmps = 120,
            Sections =
            {
                new ContingencyAnalysisService.FeederSection { Name = "North Tie", NormalLoadAmps = 190, EmergencyRatingAmps = 240 },
                new ContingencyAnalysisService.FeederSection { Name = "South Tie", NormalLoadAmps = 140, EmergencyRatingAmps = 260 },
            },
            Transfers =
            {
                new ContingencyAnalysisService.SectionTransfer { SectionName = "North Tie", AddedLoadAmps = 70 },
                new ContingencyAnalysisService.SectionTransfer { SectionName = "South Tie", AddedLoadAmps = 50 },
            },
        };

        var result = ContingencyAnalysisService.AnalyzeScenario(scenario);

        Assert.False(result.PassesNMinusOne);
        Assert.Contains("North Tie", result.LimitingConstraint);
    }

    [Fact]
    public void AnalyzeScenario_HealthyTransfer_Passes()
    {
        var scenario = new ContingencyAnalysisService.ContingencyScenario
        {
            Name = "Radial Feeder Outage",
            OutagedElementName = "Feeder 3",
            ReceivingSourceNormalLoadAmps = 260,
            ReceivingSourceEmergencyRatingAmps = 450,
            TransferredLoadAmps = 90,
            Sections =
            {
                new ContingencyAnalysisService.FeederSection { Name = "Tie East", NormalLoadAmps = 120, EmergencyRatingAmps = 220 },
                new ContingencyAnalysisService.FeederSection { Name = "Tie West", NormalLoadAmps = 100, EmergencyRatingAmps = 210 },
            },
            Transfers =
            {
                new ContingencyAnalysisService.SectionTransfer { SectionName = "Tie East", AddedLoadAmps = 40 },
                new ContingencyAnalysisService.SectionTransfer { SectionName = "Tie West", AddedLoadAmps = 25 },
            },
        };

        var result = ContingencyAnalysisService.AnalyzeScenario(scenario);

        Assert.True(result.PassesNMinusOne);
        Assert.False(result.SourceOverloaded);
        Assert.All(result.Sections, section => Assert.False(section.IsOverloaded));
    }

    [Fact]
    public void AnalyzeScenario_PreservesSwitchingRequirement()
    {
        var scenario = new ContingencyAnalysisService.ContingencyScenario
        {
            Name = "Manual Transfer",
            OutagedElementName = "Breaker 5",
            ReceivingSourceNormalLoadAmps = 200,
            ReceivingSourceEmergencyRatingAmps = 400,
            TransferredLoadAmps = 80,
            SwitchingRequired = true,
        };

        var result = ContingencyAnalysisService.AnalyzeScenario(scenario);

        Assert.True(result.RequiresSwitching);
    }

    [Fact]
    public void AnalyzePortfolio_CountsPassingAndFailingScenarios()
    {
        var scenarios = new[]
        {
            new ContingencyAnalysisService.ContingencyScenario
            {
                Name = "Pass",
                OutagedElementName = "Line 1",
                ReceivingSourceNormalLoadAmps = 200,
                ReceivingSourceEmergencyRatingAmps = 400,
                TransferredLoadAmps = 80,
            },
            new ContingencyAnalysisService.ContingencyScenario
            {
                Name = "Fail",
                OutagedElementName = "Line 2",
                ReceivingSourceNormalLoadAmps = 350,
                ReceivingSourceEmergencyRatingAmps = 400,
                TransferredLoadAmps = 90,
            },
        };

        var result = ContingencyAnalysisService.AnalyzePortfolio(scenarios);

        Assert.Equal(2, result.TotalScenarios);
        Assert.Equal(1, result.PassingScenarios);
        Assert.Equal(1, result.FailingScenarios);
    }

    [Fact]
    public void AnalyzePortfolio_TracksWorstScenario()
    {
        var scenarios = new[]
        {
            new ContingencyAnalysisService.ContingencyScenario
            {
                Name = "Mild",
                OutagedElementName = "Line 1",
                ReceivingSourceNormalLoadAmps = 220,
                ReceivingSourceEmergencyRatingAmps = 400,
                TransferredLoadAmps = 60,
            },
            new ContingencyAnalysisService.ContingencyScenario
            {
                Name = "Severe",
                OutagedElementName = "Line 2",
                ReceivingSourceNormalLoadAmps = 330,
                ReceivingSourceEmergencyRatingAmps = 400,
                TransferredLoadAmps = 70,
            },
        };

        var result = ContingencyAnalysisService.AnalyzePortfolio(scenarios);

        Assert.Equal("Severe", result.WorstScenarioName);
        Assert.True(result.WorstSourceLoadingPercent >= 100);
    }
}