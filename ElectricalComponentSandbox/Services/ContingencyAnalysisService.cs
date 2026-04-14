using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Performs N-1 screening of distribution contingencies using transferred load and emergency ratings.
/// </summary>
public static class ContingencyAnalysisService
{
    public record FeederSection
    {
        public string Name { get; init; } = string.Empty;
        public double NormalLoadAmps { get; init; }
        public double EmergencyRatingAmps { get; init; }
    }

    public record SectionTransfer
    {
        public string SectionName { get; init; } = string.Empty;
        public double AddedLoadAmps { get; init; }
    }

    public record ContingencyScenario
    {
        public string Name { get; init; } = string.Empty;
        public string OutagedElementName { get; init; } = string.Empty;
        public double ReceivingSourceNormalLoadAmps { get; init; }
        public double ReceivingSourceEmergencyRatingAmps { get; init; }
        public double TransferredLoadAmps { get; init; }
        public bool CanRestoreLoad { get; init; } = true;
        public bool SwitchingRequired { get; init; } = true;
        public List<FeederSection> Sections { get; init; } = new();
        public List<SectionTransfer> Transfers { get; init; } = new();
    }

    public record SectionAssessment
    {
        public string SectionName { get; init; } = string.Empty;
        public double PostContingencyLoadAmps { get; init; }
        public double LoadingPercent { get; init; }
        public bool IsOverloaded { get; init; }
    }

    public record ContingencyAssessment
    {
        public string ScenarioName { get; init; } = string.Empty;
        public string OutagedElementName { get; init; } = string.Empty;
        public bool PassesNMinusOne { get; init; }
        public bool RequiresSwitching { get; init; }
        public bool SourceOverloaded { get; init; }
        public double SourcePostContingencyLoadAmps { get; init; }
        public double SourceLoadingPercent { get; init; }
        public string? LimitingConstraint { get; init; }
        public List<SectionAssessment> Sections { get; init; } = new();
    }

    public record ContingencyPortfolioSummary
    {
        public int TotalScenarios { get; init; }
        public int PassingScenarios { get; init; }
        public int FailingScenarios { get; init; }
        public double WorstSourceLoadingPercent { get; init; }
        public string? WorstScenarioName { get; init; }
        public List<ContingencyAssessment> Assessments { get; init; } = new();
    }

    public static double CalculatePostContingencyLoad(double normalLoadAmps, double addedLoadAmps)
    {
        if (normalLoadAmps < 0 || addedLoadAmps < 0)
            throw new ArgumentOutOfRangeException(nameof(normalLoadAmps), "Loads must be non-negative.");

        return Math.Round(normalLoadAmps + addedLoadAmps, 2);
    }

    public static SectionAssessment EvaluateSection(FeederSection section, double addedLoadAmps)
    {
        if (section.EmergencyRatingAmps <= 0)
            throw new ArgumentException("Emergency rating must be positive.");

        double postLoad = CalculatePostContingencyLoad(section.NormalLoadAmps, addedLoadAmps);
        double loadingPercent = postLoad / section.EmergencyRatingAmps * 100.0;

        return new SectionAssessment
        {
            SectionName = section.Name,
            PostContingencyLoadAmps = postLoad,
            LoadingPercent = Math.Round(loadingPercent, 1),
            IsOverloaded = postLoad > section.EmergencyRatingAmps,
        };
    }

    public static ContingencyAssessment AnalyzeScenario(ContingencyScenario scenario)
    {
        if (!scenario.CanRestoreLoad)
        {
            return new ContingencyAssessment
            {
                ScenarioName = scenario.Name,
                OutagedElementName = scenario.OutagedElementName,
                PassesNMinusOne = false,
                RequiresSwitching = scenario.SwitchingRequired,
                LimitingConstraint = "No restoration path available",
            };
        }

        var transferLookup = scenario.Transfers
            .GroupBy(t => t.SectionName)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.AddedLoadAmps));

        var sectionAssessments = scenario.Sections
            .Select(section => EvaluateSection(section, transferLookup.GetValueOrDefault(section.Name, 0)))
            .ToList();

        double sourcePostLoad = CalculatePostContingencyLoad(
            scenario.ReceivingSourceNormalLoadAmps,
            scenario.TransferredLoadAmps);
        double sourceLoadingPercent = scenario.ReceivingSourceEmergencyRatingAmps <= 0
            ? 0
            : sourcePostLoad / scenario.ReceivingSourceEmergencyRatingAmps * 100.0;
        bool sourceOverloaded = scenario.ReceivingSourceEmergencyRatingAmps > 0
            && sourcePostLoad > scenario.ReceivingSourceEmergencyRatingAmps;

        var worstSection = sectionAssessments
            .Where(section => section.IsOverloaded)
            .OrderByDescending(section => section.LoadingPercent)
            .FirstOrDefault();

        string? limitingConstraint = sourceOverloaded
            ? $"Source loading exceeds emergency rating at {Math.Round(sourceLoadingPercent, 1)}%"
            : worstSection is not null
                ? $"Section {worstSection.SectionName} exceeds emergency rating at {worstSection.LoadingPercent}%"
                : null;

        return new ContingencyAssessment
        {
            ScenarioName = scenario.Name,
            OutagedElementName = scenario.OutagedElementName,
            PassesNMinusOne = !sourceOverloaded && worstSection is null,
            RequiresSwitching = scenario.SwitchingRequired,
            SourceOverloaded = sourceOverloaded,
            SourcePostContingencyLoadAmps = sourcePostLoad,
            SourceLoadingPercent = Math.Round(sourceLoadingPercent, 1),
            LimitingConstraint = limitingConstraint,
            Sections = sectionAssessments,
        };
    }

    public static ContingencyPortfolioSummary AnalyzePortfolio(IEnumerable<ContingencyScenario> scenarios)
    {
        var assessments = (scenarios ?? Array.Empty<ContingencyScenario>())
            .Select(AnalyzeScenario)
            .ToList();

        var worstScenario = assessments
            .OrderByDescending(assessment => assessment.SourceLoadingPercent)
            .FirstOrDefault();

        return new ContingencyPortfolioSummary
        {
            TotalScenarios = assessments.Count,
            PassingScenarios = assessments.Count(assessment => assessment.PassesNMinusOne),
            FailingScenarios = assessments.Count(assessment => !assessment.PassesNMinusOne),
            WorstSourceLoadingPercent = worstScenario?.SourceLoadingPercent ?? 0,
            WorstScenarioName = worstScenario?.ScenarioName,
            Assessments = assessments,
        };
    }
}