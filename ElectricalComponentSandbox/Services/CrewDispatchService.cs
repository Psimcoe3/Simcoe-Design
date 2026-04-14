using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Assigns outage restoration tasks to capable crews based on availability, travel, and priority.
/// </summary>
public static class CrewDispatchService
{
    public enum CrewSkill
    {
        Switching,
        OverheadRepair,
        UndergroundRepair,
        Vegetation,
    }

    public enum TaskPriority
    {
        Critical = 1,
        High = 2,
        Normal = 3,
        Low = 4,
    }

    public record Crew
    {
        public string Name { get; init; } = string.Empty;
        public List<CrewSkill> Skills { get; init; } = new();
        public double AvailableFromMinutes { get; init; }
        public double ProductivityFactor { get; init; } = 1.0;
        public bool SupportsAfterHours { get; init; } = true;
    }

    public record OutageTask
    {
        public string Name { get; init; } = string.Empty;
        public CrewSkill RequiredSkill { get; init; }
        public TaskPriority Priority { get; init; } = TaskPriority.Normal;
        public int CustomersRestored { get; init; }
        public double TravelMinutes { get; init; }
        public double WorkMinutes { get; init; }
        public bool RequiresAfterHours { get; init; }
    }

    public record CrewAssignment
    {
        public string CrewName { get; init; } = string.Empty;
        public string TaskName { get; init; } = string.Empty;
        public double StartMinute { get; init; }
        public double TravelMinutes { get; init; }
        public double WorkMinutes { get; init; }
        public double FinishMinute { get; init; }
        public int CustomersRestored { get; init; }
    }

    public record DispatchPlan
    {
        public List<CrewAssignment> Assignments { get; init; } = new();
        public List<string> UnassignedTasks { get; init; } = new();
        public int RestoredCustomers { get; init; }
        public double EstimatedClearTimeMinutes { get; init; }
        public string? Issue { get; init; }
    }

    public static bool CanCrewPerformTask(Crew crew, OutageTask task)
    {
        if (!crew.Skills.Contains(task.RequiredSkill))
            return false;

        if (task.RequiresAfterHours && !crew.SupportsAfterHours)
            return false;

        return crew.ProductivityFactor > 0;
    }

    public static List<OutageTask> RankTasks(IEnumerable<OutageTask> tasks)
    {
        return (tasks ?? Array.Empty<OutageTask>())
            .OrderBy(task => (int)task.Priority)
            .ThenByDescending(task => task.CustomersRestored)
            .ThenBy(task => task.TravelMinutes + task.WorkMinutes)
            .ToList();
    }

    public static DispatchPlan CreateDispatchPlan(IEnumerable<Crew> crews, IEnumerable<OutageTask> tasks)
    {
        var rankedTasks = RankTasks(tasks);
        var assignments = new List<CrewAssignment>();
        var unassigned = new List<string>();
        var crewReadyTimes = (crews ?? Array.Empty<Crew>()).ToDictionary(crew => crew.Name, crew => crew.AvailableFromMinutes, StringComparer.OrdinalIgnoreCase);
        var crewCatalog = (crews ?? Array.Empty<Crew>()).ToDictionary(crew => crew.Name, crew => crew, StringComparer.OrdinalIgnoreCase);

        foreach (var task in rankedTasks)
        {
            var candidate = crewCatalog.Values
                .Where(crew => CanCrewPerformTask(crew, task))
                .Select(crew =>
                {
                    double ready = crewReadyTimes[crew.Name];
                    double workMinutes = task.WorkMinutes / crew.ProductivityFactor;
                    double finish = ready + task.TravelMinutes + workMinutes;
                    return new { Crew = crew, Ready = ready, WorkMinutes = workMinutes, Finish = finish };
                })
                .OrderBy(option => option.Finish)
                .ThenBy(option => option.Ready)
                .FirstOrDefault();

            if (candidate is null)
            {
                unassigned.Add(task.Name);
                continue;
            }

            crewReadyTimes[candidate.Crew.Name] = candidate.Finish;
            assignments.Add(new CrewAssignment
            {
                CrewName = candidate.Crew.Name,
                TaskName = task.Name,
                StartMinute = Math.Round(candidate.Ready, 2),
                TravelMinutes = Math.Round(task.TravelMinutes, 2),
                WorkMinutes = Math.Round(candidate.WorkMinutes, 2),
                FinishMinute = Math.Round(candidate.Finish, 2),
                CustomersRestored = task.CustomersRestored,
            });
        }

        double clearTime = assignments.Count == 0 ? 0 : assignments.Max(assignment => assignment.FinishMinute);
        return new DispatchPlan
        {
            Assignments = assignments,
            UnassignedTasks = unassigned,
            RestoredCustomers = assignments.Sum(assignment => assignment.CustomersRestored),
            EstimatedClearTimeMinutes = Math.Round(clearTime, 2),
            Issue = unassigned.Count == 0 ? null : "Some outage tasks could not be assigned to an available qualified crew",
        };
    }
}