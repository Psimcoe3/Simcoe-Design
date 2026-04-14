using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class CrewDispatchServiceTests
{
    [Fact]
    public void CanCrewPerformTask_RequiresMatchingSkill()
    {
        var crew = new CrewDispatchService.Crew
        {
            Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.Switching },
        };
        var task = new CrewDispatchService.OutageTask
        {
            RequiredSkill = CrewDispatchService.CrewSkill.OverheadRepair,
        };

        Assert.False(CrewDispatchService.CanCrewPerformTask(crew, task));
    }

    [Fact]
    public void CanCrewPerformTask_RespectsAfterHoursRequirement()
    {
        var crew = new CrewDispatchService.Crew
        {
            Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.Switching },
            SupportsAfterHours = false,
        };
        var task = new CrewDispatchService.OutageTask
        {
            RequiredSkill = CrewDispatchService.CrewSkill.Switching,
            RequiresAfterHours = true,
        };

        Assert.False(CrewDispatchService.CanCrewPerformTask(crew, task));
    }

    [Fact]
    public void RankTasks_SortsByPriorityCustomersAndDuration()
    {
        var ranked = CrewDispatchService.RankTasks(new[]
        {
            new CrewDispatchService.OutageTask { Name = "B", Priority = CrewDispatchService.TaskPriority.High, CustomersRestored = 50, TravelMinutes = 20, WorkMinutes = 40 },
            new CrewDispatchService.OutageTask { Name = "A", Priority = CrewDispatchService.TaskPriority.Critical, CustomersRestored = 10, TravelMinutes = 10, WorkMinutes = 10 },
            new CrewDispatchService.OutageTask { Name = "C", Priority = CrewDispatchService.TaskPriority.High, CustomersRestored = 100, TravelMinutes = 15, WorkMinutes = 20 },
        });

        Assert.Equal(new[] { "A", "C", "B" }, ranked.Select(task => task.Name).ToArray());
    }

    [Fact]
    public void CreateDispatchPlan_AssignsTaskToEarliestCapableCrew()
    {
        var plan = CrewDispatchService.CreateDispatchPlan(
            new[]
            {
                new CrewDispatchService.Crew { Name = "Crew A", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.Switching }, AvailableFromMinutes = 15 },
                new CrewDispatchService.Crew { Name = "Crew B", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.Switching }, AvailableFromMinutes = 0 },
            },
            new[]
            {
                new CrewDispatchService.OutageTask { Name = "Switch", RequiredSkill = CrewDispatchService.CrewSkill.Switching, TravelMinutes = 10, WorkMinutes = 15, CustomersRestored = 100 },
            });

        Assert.Single(plan.Assignments);
        Assert.Equal("Crew B", plan.Assignments[0].CrewName);
        Assert.Equal(25, plan.Assignments[0].FinishMinute);
    }

    [Fact]
    public void CreateDispatchPlan_UsesCrewReadyTimeForSequentialTasks()
    {
        var plan = CrewDispatchService.CreateDispatchPlan(
            new[]
            {
                new CrewDispatchService.Crew { Name = "Crew A", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.OverheadRepair } },
            },
            new[]
            {
                new CrewDispatchService.OutageTask { Name = "Repair 1", RequiredSkill = CrewDispatchService.CrewSkill.OverheadRepair, Priority = CrewDispatchService.TaskPriority.Critical, TravelMinutes = 20, WorkMinutes = 40, CustomersRestored = 200 },
                new CrewDispatchService.OutageTask { Name = "Repair 2", RequiredSkill = CrewDispatchService.CrewSkill.OverheadRepair, Priority = CrewDispatchService.TaskPriority.High, TravelMinutes = 10, WorkMinutes = 30, CustomersRestored = 150 },
            });

        Assert.Equal(60, plan.Assignments[0].FinishMinute);
        Assert.Equal(60, plan.Assignments[1].StartMinute);
        Assert.Equal(100, plan.Assignments[1].FinishMinute);
    }

    [Fact]
    public void CreateDispatchPlan_ProductiveCrewFinishesSooner()
    {
        var plan = CrewDispatchService.CreateDispatchPlan(
            new[]
            {
                new CrewDispatchService.Crew { Name = "Standard", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.UndergroundRepair }, ProductivityFactor = 1.0 },
                new CrewDispatchService.Crew { Name = "Fast", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.UndergroundRepair }, ProductivityFactor = 2.0 },
            },
            new[]
            {
                new CrewDispatchService.OutageTask { Name = "Cable Splice", RequiredSkill = CrewDispatchService.CrewSkill.UndergroundRepair, TravelMinutes = 15, WorkMinutes = 120, CustomersRestored = 80 },
            });

        Assert.Equal("Fast", plan.Assignments[0].CrewName);
        Assert.Equal(75, plan.Assignments[0].FinishMinute);
    }

    [Fact]
    public void CreateDispatchPlan_LeavesUnqualifiedTasksUnassigned()
    {
        var plan = CrewDispatchService.CreateDispatchPlan(
            new[]
            {
                new CrewDispatchService.Crew { Name = "Switch Crew", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.Switching } },
            },
            new[]
            {
                new CrewDispatchService.OutageTask { Name = "Tree Work", RequiredSkill = CrewDispatchService.CrewSkill.Vegetation, TravelMinutes = 10, WorkMinutes = 60, CustomersRestored = 40 },
            });

        Assert.Empty(plan.Assignments);
        Assert.Equal(new[] { "Tree Work" }, plan.UnassignedTasks);
        Assert.NotNull(plan.Issue);
    }

    [Fact]
    public void CreateDispatchPlan_SumsRestoredCustomersAndClearTime()
    {
        var plan = CrewDispatchService.CreateDispatchPlan(
            new[]
            {
                new CrewDispatchService.Crew { Name = "Switch Crew", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.Switching } },
                new CrewDispatchService.Crew { Name = "Line Crew", Skills = new List<CrewDispatchService.CrewSkill> { CrewDispatchService.CrewSkill.OverheadRepair } },
            },
            new[]
            {
                new CrewDispatchService.OutageTask { Name = "Open Tie", RequiredSkill = CrewDispatchService.CrewSkill.Switching, Priority = CrewDispatchService.TaskPriority.Critical, TravelMinutes = 10, WorkMinutes = 10, CustomersRestored = 300 },
                new CrewDispatchService.OutageTask { Name = "Pole Repair", RequiredSkill = CrewDispatchService.CrewSkill.OverheadRepair, Priority = CrewDispatchService.TaskPriority.High, TravelMinutes = 30, WorkMinutes = 90, CustomersRestored = 120 },
            });

        Assert.Equal(420, plan.RestoredCustomers);
        Assert.Equal(120, plan.EstimatedClearTimeMinutes);
    }
}