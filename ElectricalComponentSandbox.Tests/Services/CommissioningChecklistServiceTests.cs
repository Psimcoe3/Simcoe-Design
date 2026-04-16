using ElectricalComponentSandbox.Services;
using System.Linq;
using Xunit;
using static ElectricalComponentSandbox.Services.CommissioningChecklistService;

namespace ElectricalComponentSandbox.Tests.Services;

public class CommissioningChecklistServiceTests
{
    [Theory]
    [InlineData(EquipmentType.Transformer)]
    [InlineData(EquipmentType.Switchgear)]
    [InlineData(EquipmentType.MotorControlCenter)]
    [InlineData(EquipmentType.PanelBoard)]
    [InlineData(EquipmentType.Generator)]
    [InlineData(EquipmentType.UPS)]
    [InlineData(EquipmentType.ATS)]
    [InlineData(EquipmentType.CableRun)]
    [InlineData(EquipmentType.GroundingSystem)]
    [InlineData(EquipmentType.ProtectiveRelay)]
    public void GenerateChecklist_AllEquipmentTypes_ProduceItems(EquipmentType type)
    {
        var result = CommissioningChecklistService.GenerateChecklist(type, "TAG-001");

        Assert.True(result.TotalItems > 0);
        Assert.Equal(type, result.Equipment);
        Assert.Equal("TAG-001", result.EquipmentTag);
    }

    [Fact]
    public void GenerateChecklist_Transformer_HasAllCategories()
    {
        var result = CommissioningChecklistService.GenerateChecklist(EquipmentType.Transformer);

        Assert.Contains(result.Items, i => i.Category == TestCategory.Visual);
        Assert.Contains(result.Items, i => i.Category == TestCategory.Mechanical);
        Assert.Contains(result.Items, i => i.Category == TestCategory.Electrical);
        Assert.Contains(result.Items, i => i.Category == TestCategory.Functional);
    }

    [Fact]
    public void GenerateChecklist_AllItemsHaveRequiredFields()
    {
        var result = CommissioningChecklistService.GenerateChecklist(EquipmentType.Switchgear);

        foreach (var item in result.Items)
        {
            Assert.False(string.IsNullOrEmpty(item.Id));
            Assert.False(string.IsNullOrEmpty(item.Description));
            Assert.False(string.IsNullOrEmpty(item.NETAReference));
            Assert.False(string.IsNullOrEmpty(item.AcceptanceCriteria));
        }
    }

    [Fact]
    public void GenerateChecklist_HasRequiredPriorityItems()
    {
        var result = CommissioningChecklistService.GenerateChecklist(EquipmentType.Generator);

        Assert.Contains(result.Items, i => i.Priority == Priority.Required);
    }

    [Fact]
    public void Checklist_Completion_CalculatesCorrectly()
    {
        var checklist = CommissioningChecklistService.GenerateChecklist(EquipmentType.PanelBoard);

        // All NotStarted → 0% completion
        Assert.Equal(0, checklist.CompletionPercent);
        Assert.False(checklist.IsComplete);

        // Mark all passed
        var allPassed = new Checklist
        {
            Equipment = EquipmentType.PanelBoard,
            Items = checklist.Items.Select(i => i with { Status = CheckStatus.Passed }).ToList(),
        };

        Assert.Equal(100.0, allPassed.CompletionPercent);
        Assert.True(allPassed.IsComplete);
        Assert.True(allPassed.AllPassed);
    }

    [Fact]
    public void Checklist_WithFailure_ReportsNotAllPassed()
    {
        var checklist = CommissioningChecklistService.GenerateChecklist(EquipmentType.CableRun);
        var items = checklist.Items.Select((item, i) =>
            i == 0 ? item with { Status = CheckStatus.Failed }
                    : item with { Status = CheckStatus.Passed }).ToList();

        var mixed = new Checklist
        {
            Equipment = EquipmentType.CableRun,
            Items = items,
        };

        Assert.True(mixed.IsComplete);
        Assert.False(mixed.AllPassed);
        Assert.Equal(1, mixed.FailedItems);
    }

    [Fact]
    public void GetOutstandingIssues_FailedItems_ListsAsFailures()
    {
        var checklist = CommissioningChecklistService.GenerateChecklist(EquipmentType.GroundingSystem);
        var items = checklist.Items.Select((item, i) =>
            i == 0 ? item with { Status = CheckStatus.Failed }
                    : item with { Status = CheckStatus.Passed }).ToList();

        var withFail = new Checklist { Equipment = EquipmentType.GroundingSystem, Items = items };
        var issues = CommissioningChecklistService.GetOutstandingIssues(withFail);

        Assert.Contains(issues, i => i.StartsWith("FAIL:"));
    }

    [Fact]
    public void GetOutstandingIssues_RequiredNotStarted_ListsAsIncomplete()
    {
        var checklist = CommissioningChecklistService.GenerateChecklist(EquipmentType.ProtectiveRelay);
        // All items are NotStarted by default
        var issues = CommissioningChecklistService.GetOutstandingIssues(checklist);

        Assert.Contains(issues, i => i.StartsWith("INCOMPLETE:"));
    }

    [Fact]
    public void GetOutstandingIssues_AllPassed_ReturnsEmpty()
    {
        var checklist = CommissioningChecklistService.GenerateChecklist(EquipmentType.ATS);
        var allPassed = new Checklist
        {
            Equipment = EquipmentType.ATS,
            Items = checklist.Items.Select(i => i with { Status = CheckStatus.Passed }).ToList(),
        };

        var issues = CommissioningChecklistService.GetOutstandingIssues(allPassed);

        Assert.Empty(issues);
    }

    [Fact]
    public void GeneratePlan_MultipleEquipment_AggregatesChecklists()
    {
        var equipment = new[]
        {
            (EquipmentType.Transformer, "TX-1"),
            (EquipmentType.Switchgear, "SWG-1"),
            (EquipmentType.PanelBoard, "PNL-1A"),
        };

        var plan = CommissioningChecklistService.GeneratePlan(equipment);

        Assert.Equal(3, plan.Checklists.Count);
        Assert.True(plan.TotalItems > 20);
        Assert.Equal(0, plan.OverallCompletionPercent);
        Assert.False(plan.AllPassed);
    }

    [Fact]
    public void GetRequiredInstruments_AlwaysIncludesCommonTools()
    {
        var instruments = CommissioningChecklistService.GetRequiredInstruments(EquipmentType.Transformer);

        Assert.Contains(instruments, i => i.Contains("Multimeter"));
        Assert.Contains(instruments, i => i.Contains("Torque"));
    }

    [Fact]
    public void GetRequiredInstruments_Transformer_IncludesSpecificTools()
    {
        var instruments = CommissioningChecklistService.GetRequiredInstruments(EquipmentType.Transformer);

        Assert.Contains(instruments, i => i.Contains("Megger"));
        Assert.Contains(instruments, i => i.Contains("Turns Ratio"));
    }

    [Fact]
    public void GetRequiredInstruments_Grounding_IncludesGroundTester()
    {
        var instruments = CommissioningChecklistService.GetRequiredInstruments(EquipmentType.GroundingSystem);

        Assert.Contains(instruments, i => i.Contains("Ground Resistance"));
    }

    [Fact]
    public void GetRequiredInstruments_Relay_IncludesRelayTestSet()
    {
        var instruments = CommissioningChecklistService.GetRequiredInstruments(EquipmentType.ProtectiveRelay);

        Assert.Contains(instruments, i => i.Contains("Relay Test Set"));
    }

    [Fact]
    public void Checklist_NotApplicable_CountsAsCompleted()
    {
        var checklist = CommissioningChecklistService.GenerateChecklist(EquipmentType.CableRun);
        var items = checklist.Items.Select((item, i) =>
            i == 0 ? item with { Status = CheckStatus.NotApplicable }
                    : item with { Status = CheckStatus.Passed }).ToList();

        var result = new Checklist { Equipment = EquipmentType.CableRun, Items = items };

        Assert.True(result.IsComplete);
        Assert.True(result.AllPassed);
    }
}
