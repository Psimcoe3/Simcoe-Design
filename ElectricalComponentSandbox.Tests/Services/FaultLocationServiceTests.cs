using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class FaultLocationServiceTests
{
    [Fact]
    public void EvaluateCandidate_MatchingPattern_GetsPositiveScore()
    {
        var result = FaultLocationService.EvaluateCandidate(
            new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = true },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I2", BoundaryAfterSequenceOrder = 2, IsLatched = false },
            });

        Assert.True(result.Score > 0);
        Assert.Equal(2, result.MatchingIndicators);
    }

    [Fact]
    public void EvaluateCandidate_ConflictingPattern_GetsNegativeScore()
    {
        var result = FaultLocationService.EvaluateCandidate(
            new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = false },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I2", BoundaryAfterSequenceOrder = 2, IsLatched = true },
            });

        Assert.True(result.Score < 0);
        Assert.Equal(2, result.ConflictingIndicators);
    }

    [Fact]
    public void LocateFault_LatchedUpstreamAndClearDownstream_ReturnsMiddleSection()
    {
        var result = FaultLocationService.LocateFault(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3 },
            },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = true },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I2", BoundaryAfterSequenceOrder = 2, IsLatched = false },
            });

        Assert.Equal("S2", result.MostLikelySectionName);
    }

    [Fact]
    public void LocateFault_AllIndicatorsLatched_PointsToTailSection()
    {
        var result = FaultLocationService.LocateFault(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3 },
            },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = true },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I2", BoundaryAfterSequenceOrder = 2, IsLatched = true },
            });

        Assert.Equal("S3", result.MostLikelySectionName);
    }

    [Fact]
    public void LocateFault_NoLatchedIndicators_PointsToFirstSection()
    {
        var result = FaultLocationService.LocateFault(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
            },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = false },
            });

        Assert.Equal("S1", result.MostLikelySectionName);
    }

    [Fact]
    public void LocateFault_WeightedObservationCanBreakTie()
    {
        var result = FaultLocationService.LocateFault(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3 },
            },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = true, ConfidenceWeight = 2.0 },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I2", BoundaryAfterSequenceOrder = 2, IsLatched = true, ConfidenceWeight = 0.5 },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I3", BoundaryAfterSequenceOrder = 2, IsLatched = false, ConfidenceWeight = 2.0 },
            });

        Assert.Equal("S2", result.MostLikelySectionName);
    }

    [Fact]
    public void LocateFault_CandidatesAreSortedByScore()
    {
        var result = FaultLocationService.LocateFault(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3 },
            },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = true },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I2", BoundaryAfterSequenceOrder = 2, IsLatched = false },
            });

        Assert.Equal(result.MostLikelySectionName, result.Candidates.First().SectionName);
    }

    [Fact]
    public void LocateFault_WithoutSections_ReturnsIssue()
    {
        var result = FaultLocationService.LocateFault(System.Array.Empty<SectionalizingService.FeederSection>(), System.Array.Empty<FaultLocationService.IndicatorObservation>());

        Assert.Equal("No feeder sections were provided", result.Issue);
    }

    [Fact]
    public void LocateFault_PerfectPattern_HasHighConfidence()
    {
        var result = FaultLocationService.LocateFault(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3 },
            },
            new[]
            {
                new FaultLocationService.IndicatorObservation { IndicatorName = "I1", BoundaryAfterSequenceOrder = 1, IsLatched = true },
                new FaultLocationService.IndicatorObservation { IndicatorName = "I2", BoundaryAfterSequenceOrder = 2, IsLatched = false },
            });

        Assert.True(result.ConfidencePercent >= 75);
    }
}