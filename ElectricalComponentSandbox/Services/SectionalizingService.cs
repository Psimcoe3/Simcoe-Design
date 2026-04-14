using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Builds fault isolation plans for simple radial feeders using boundary switch locations.
/// </summary>
public static class SectionalizingService
{
    public record FeederSection
    {
        public string Name { get; init; } = string.Empty;
        public int SequenceOrder { get; init; }
        public double ConnectedLoadAmps { get; init; }
    }

    public record BoundarySwitch
    {
        public string Name { get; init; } = string.Empty;
        public int BoundaryAfterSequenceOrder { get; init; }
        public bool IsNormallyClosed { get; init; } = true;
        public bool IsRemoteControlled { get; init; } = true;
    }

    public record IsolationPlan
    {
        public string FaultedSectionName { get; init; } = string.Empty;
        public string? UpstreamSwitchName { get; init; }
        public string? DownstreamSwitchName { get; init; }
        public bool IsolatesFault { get; init; }
        public bool CanBePerformedRemotely { get; init; }
        public double UnservedLoadAmps { get; init; }
        public List<string> IsolatedSections { get; init; } = new();
        public List<string> EnergizedSections { get; init; } = new();
        public string? Issue { get; init; }
    }

    public static BoundarySwitch? FindNearestUpstreamSwitch(IEnumerable<BoundarySwitch> switches, int faultedSectionOrder)
    {
        return (switches ?? Array.Empty<BoundarySwitch>())
            .Where(boundary => boundary.IsNormallyClosed && boundary.BoundaryAfterSequenceOrder < faultedSectionOrder)
            .OrderByDescending(boundary => boundary.BoundaryAfterSequenceOrder)
            .FirstOrDefault();
    }

    public static BoundarySwitch? FindNearestDownstreamSwitch(IEnumerable<BoundarySwitch> switches, int faultedSectionOrder)
    {
        return (switches ?? Array.Empty<BoundarySwitch>())
            .Where(boundary => boundary.IsNormallyClosed && boundary.BoundaryAfterSequenceOrder >= faultedSectionOrder)
            .OrderBy(boundary => boundary.BoundaryAfterSequenceOrder)
            .FirstOrDefault();
    }

    public static IsolationPlan CreateIsolationPlan(
        IEnumerable<FeederSection> sections,
        IEnumerable<BoundarySwitch> switches,
        string faultedSectionName)
    {
        var orderedSections = (sections ?? Array.Empty<FeederSection>())
            .OrderBy(section => section.SequenceOrder)
            .ToList();

        var faultedSection = orderedSections
            .FirstOrDefault(section => string.Equals(section.Name, faultedSectionName, StringComparison.OrdinalIgnoreCase));

        if (faultedSection is null)
        {
            return new IsolationPlan
            {
                FaultedSectionName = faultedSectionName,
                Issue = "Faulted section was not found",
            };
        }

        var upstreamSwitch = FindNearestUpstreamSwitch(switches, faultedSection.SequenceOrder);
        var downstreamSwitch = FindNearestDownstreamSwitch(switches, faultedSection.SequenceOrder);
        int feederEnd = orderedSections.Count == 0 ? 0 : orderedSections.Max(section => section.SequenceOrder);

        if (upstreamSwitch is null)
        {
            return new IsolationPlan
            {
                FaultedSectionName = faultedSection.Name,
                DownstreamSwitchName = downstreamSwitch?.Name,
                Issue = "No upstream isolation switch is available",
            };
        }

        if (downstreamSwitch is null && faultedSection.SequenceOrder < feederEnd)
        {
            return new IsolationPlan
            {
                FaultedSectionName = faultedSection.Name,
                UpstreamSwitchName = upstreamSwitch.Name,
                Issue = "No downstream isolation switch is available",
            };
        }

        int isolatedEnd = downstreamSwitch?.BoundaryAfterSequenceOrder ?? feederEnd;
        var isolatedSections = orderedSections
            .Where(section => section.SequenceOrder > upstreamSwitch.BoundaryAfterSequenceOrder
                && section.SequenceOrder <= isolatedEnd)
            .Select(section => section.Name)
            .ToList();
        var energizedSections = orderedSections
            .Where(section => !isolatedSections.Contains(section.Name, StringComparer.OrdinalIgnoreCase))
            .Select(section => section.Name)
            .ToList();

        bool remote = upstreamSwitch.IsRemoteControlled && (downstreamSwitch?.IsRemoteControlled ?? true);
        double unservedLoad = orderedSections
            .Where(section => isolatedSections.Contains(section.Name, StringComparer.OrdinalIgnoreCase))
            .Sum(section => section.ConnectedLoadAmps);

        return new IsolationPlan
        {
            FaultedSectionName = faultedSection.Name,
            UpstreamSwitchName = upstreamSwitch.Name,
            DownstreamSwitchName = downstreamSwitch?.Name,
            IsolatesFault = isolatedSections.Contains(faultedSection.Name, StringComparer.OrdinalIgnoreCase),
            CanBePerformedRemotely = remote,
            UnservedLoadAmps = Math.Round(unservedLoad, 2),
            IsolatedSections = isolatedSections,
            EnergizedSections = energizedSections,
        };
    }
}