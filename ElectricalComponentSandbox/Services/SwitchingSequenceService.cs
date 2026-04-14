using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Generates ordered switching steps for fault isolation and feeder restoration.
/// </summary>
public static class SwitchingSequenceService
{
    public enum SwitchingOperationType
    {
        Open,
        Close,
    }

    public record SwitchingStep
    {
        public int OrderIndex { get; init; }
        public SwitchingOperationType Operation { get; init; }
        public string DeviceName { get; init; } = string.Empty;
        public bool IsRemoteControlled { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public record SwitchingSequencePlan
    {
        public string Purpose { get; init; } = string.Empty;
        public bool IsValid { get; init; }
        public bool CanBePerformedRemotely { get; init; }
        public string? Issue { get; init; }
        public List<SwitchingStep> Steps { get; init; } = new();
    }

    public static SwitchingSequencePlan CreateIsolationSequence(SectionalizingService.IsolationPlan isolationPlan)
    {
        if (!isolationPlan.IsolatesFault || string.IsNullOrWhiteSpace(isolationPlan.UpstreamSwitchName))
        {
            return new SwitchingSequencePlan
            {
                Purpose = $"Isolate {isolationPlan.FaultedSectionName}",
                Issue = isolationPlan.Issue ?? "Isolation plan is incomplete",
            };
        }

        var steps = new List<SwitchingStep>();
        int order = 1;
        steps.Add(new SwitchingStep
        {
            OrderIndex = order++,
            Operation = SwitchingOperationType.Open,
            DeviceName = isolationPlan.UpstreamSwitchName,
            IsRemoteControlled = isolationPlan.CanBePerformedRemotely,
            Reason = "Open the nearest upstream boundary to de-energize the faulted block",
        });

        if (!string.IsNullOrWhiteSpace(isolationPlan.DownstreamSwitchName))
        {
            steps.Add(new SwitchingStep
            {
                OrderIndex = order,
                Operation = SwitchingOperationType.Open,
                DeviceName = isolationPlan.DownstreamSwitchName,
                IsRemoteControlled = isolationPlan.CanBePerformedRemotely,
                Reason = "Open the downstream boundary to fully isolate the faulted block",
            });
        }

        return new SwitchingSequencePlan
        {
            Purpose = $"Isolate {isolationPlan.FaultedSectionName}",
            IsValid = true,
            CanBePerformedRemotely = isolationPlan.CanBePerformedRemotely,
            Steps = steps,
        };
    }

    public static SwitchingSequencePlan CreateRestorationSequence(
        SectionalizingService.IsolationPlan isolationPlan,
        FeederReconfigurationService.ReconfigurationPlan reconfigurationPlan,
        bool tieSwitchIsRemoteControlled = true,
        bool openPointIsRemoteControlled = true)
    {
        var isolationSequence = CreateIsolationSequence(isolationPlan);
        if (!isolationSequence.IsValid)
            return isolationSequence with { Purpose = $"Restore {isolationPlan.FaultedSectionName}" };

        if (!reconfigurationPlan.IsRadial)
        {
            return new SwitchingSequencePlan
            {
                Purpose = $"Restore {isolationPlan.FaultedSectionName}",
                Issue = reconfigurationPlan.Issue ?? "Restoration plan does not preserve a radial feeder",
            };
        }

        if (!reconfigurationPlan.PassesEmergencyRatings)
        {
            return new SwitchingSequencePlan
            {
                Purpose = $"Restore {isolationPlan.FaultedSectionName}",
                Issue = reconfigurationPlan.Issue ?? "Restoration plan exceeds emergency ratings",
            };
        }

        var steps = isolationSequence.Steps.ToList();
        int order = steps.Count + 1;

        if (!string.IsNullOrWhiteSpace(reconfigurationPlan.OpenPointName))
        {
            steps.Add(new SwitchingStep
            {
                OrderIndex = order++,
                Operation = SwitchingOperationType.Open,
                DeviceName = reconfigurationPlan.OpenPointName,
                IsRemoteControlled = openPointIsRemoteControlled,
                Reason = "Open the selected open point before backfeeding through the tie switch",
            });
        }

        if (!string.IsNullOrWhiteSpace(reconfigurationPlan.SwitchName))
        {
            steps.Add(new SwitchingStep
            {
                OrderIndex = order,
                Operation = SwitchingOperationType.Close,
                DeviceName = reconfigurationPlan.SwitchName,
                IsRemoteControlled = tieSwitchIsRemoteControlled,
                Reason = "Close the tie switch to restore load from the alternate source",
            });
        }

        bool remote = steps.All(step => step.IsRemoteControlled);
        bool valid = SequenceMaintainsRadialOperation(new SwitchingSequencePlan { Steps = steps, IsValid = true });

        return new SwitchingSequencePlan
        {
            Purpose = $"Restore {isolationPlan.FaultedSectionName}",
            IsValid = valid,
            CanBePerformedRemotely = remote,
            Issue = valid ? null : "Switching order does not preserve radial operation",
            Steps = steps,
        };
    }

    public static bool SequenceMaintainsRadialOperation(SwitchingSequencePlan plan)
    {
        if (plan.Steps.Count == 0)
            return false;

        int firstClose = plan.Steps
            .Where(step => step.Operation == SwitchingOperationType.Close)
            .Select(step => step.OrderIndex)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        int lastIsolationOpen = plan.Steps
            .Where(step => step.Operation == SwitchingOperationType.Open)
            .Select(step => step.OrderIndex)
            .DefaultIfEmpty(0)
            .Max();

        return lastIsolationOpen < firstClose || firstClose == int.MaxValue;
    }
}