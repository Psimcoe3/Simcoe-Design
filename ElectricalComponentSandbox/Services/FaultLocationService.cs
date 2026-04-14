using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Estimates the most likely faulted section from latched and clear fault indicator observations.
/// </summary>
public static class FaultLocationService
{
    public record IndicatorObservation
    {
        public string IndicatorName { get; init; } = string.Empty;
        public int BoundaryAfterSequenceOrder { get; init; }
        public bool IsLatched { get; init; }
        public double ConfidenceWeight { get; init; } = 1.0;
    }

    public record CandidateAssessment
    {
        public string SectionName { get; init; } = string.Empty;
        public double Score { get; init; }
        public int MatchingIndicators { get; init; }
        public int ConflictingIndicators { get; init; }
    }

    public record FaultLocationResult
    {
        public string? MostLikelySectionName { get; init; }
        public double ConfidencePercent { get; init; }
        public List<CandidateAssessment> Candidates { get; init; } = new();
        public string? Issue { get; init; }
    }

    public static CandidateAssessment EvaluateCandidate(
        SectionalizingService.FeederSection section,
        IEnumerable<IndicatorObservation> observations)
    {
        int matches = 0;
        int conflicts = 0;
        double score = 0;

        foreach (var observation in observations ?? Array.Empty<IndicatorObservation>())
        {
            bool expectedLatched = observation.BoundaryAfterSequenceOrder < section.SequenceOrder;
            if (observation.IsLatched == expectedLatched)
            {
                matches++;
                score += observation.ConfidenceWeight;
            }
            else
            {
                conflicts++;
                score -= observation.ConfidenceWeight;
            }
        }

        return new CandidateAssessment
        {
            SectionName = section.Name,
            Score = Math.Round(score, 2),
            MatchingIndicators = matches,
            ConflictingIndicators = conflicts,
        };
    }

    public static FaultLocationResult LocateFault(
        IEnumerable<SectionalizingService.FeederSection> sections,
        IEnumerable<IndicatorObservation> observations)
    {
        var sectionList = (sections ?? Array.Empty<SectionalizingService.FeederSection>())
            .OrderBy(section => section.SequenceOrder)
            .ToList();
        var observationList = (observations ?? Array.Empty<IndicatorObservation>()).ToList();

        if (sectionList.Count == 0)
        {
            return new FaultLocationResult
            {
                Issue = "No feeder sections were provided",
            };
        }

        var candidates = sectionList
            .Select(section => EvaluateCandidate(section, observationList))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.ConflictingIndicators)
            .ThenBy(candidate => candidate.SectionName)
            .ToList();

        var best = candidates.First();
        double totalWeight = observationList.Sum(observation => observation.ConfidenceWeight);
        double confidence = totalWeight <= 0
            ? 0
            : Math.Round((best.Score + totalWeight) / (2 * totalWeight) * 100.0, 1);

        return new FaultLocationResult
        {
            MostLikelySectionName = best.SectionName,
            ConfidencePercent = Math.Max(0, Math.Min(100, confidence)),
            Candidates = candidates,
        };
    }
}