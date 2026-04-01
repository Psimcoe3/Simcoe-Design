using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class RevisionHistoryServiceTests
{
    [Fact]
    public void GetNextRevisionNumber_WithNoRevisions_Returns1()
    {
        var service = new RevisionHistoryService();

        var next = service.GetNextRevisionNumber([]);

        Assert.Equal("1", next);
    }

    [Fact]
    public void GetNextRevisionNumber_WithNumericRevisions_ReturnsNextNumber()
    {
        var service = new RevisionHistoryService();
        var revisions = new[]
        {
            new RevisionEntry { RevisionNumber = "2" },
            new RevisionEntry { RevisionNumber = "5" },
            new RevisionEntry { RevisionNumber = "3" }
        };

        var next = service.GetNextRevisionNumber(revisions);

        Assert.Equal("6", next);
    }

    [Fact]
    public void GetNextRevisionNumber_WithAlphabeticRevisions_ReturnsNextLetter()
    {
        var service = new RevisionHistoryService();
        var revisions = new[]
        {
            new RevisionEntry { RevisionNumber = "A" },
            new RevisionEntry { RevisionNumber = "C" }
        };

        var next = service.GetNextRevisionNumber(revisions);

        Assert.Equal("D", next);
    }

    [Fact]
    public void AddRevision_InsertsNewestFirstByDefault()
    {
        var service = new RevisionHistoryService();
        var sheet = DrawingSheet.CreateDefault(1);
        var first = new RevisionEntry { RevisionNumber = "1", Description = "Initial issue" };
        var second = new RevisionEntry { RevisionNumber = "2", Description = "Issued for review" };

        service.AddRevision(sheet, first);
        service.AddRevision(sheet, second);

        Assert.Equal(2, sheet.RevisionEntries.Count);
        Assert.Equal("2", sheet.RevisionEntries[0].RevisionNumber);
        Assert.Equal("1", sheet.RevisionEntries[1].RevisionNumber);
    }

    [Fact]
    public void UpdateRevision_UpdatesMatchingEntry()
    {
        var service = new RevisionHistoryService();
        var sheet = DrawingSheet.CreateDefault(1);
        var revision = new RevisionEntry
        {
            RevisionNumber = "A",
            Date = "2026-03-31",
            Description = "Initial issue",
            Author = "Paul"
        };
        service.AddRevision(sheet, revision);

        var changed = service.UpdateRevision(sheet, revision.Id, "B", "2026-04-01", "Issued for permit", "Reviewer");

        Assert.True(changed);
        Assert.Equal("B", revision.RevisionNumber);
        Assert.Equal("2026-04-01", revision.Date);
        Assert.Equal("Issued for permit", revision.Description);
        Assert.Equal("Reviewer", revision.Author);
    }
}