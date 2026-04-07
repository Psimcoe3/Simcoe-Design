using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services.Export;

namespace ElectricalComponentSandbox.Tests.Services;

public class XfdfExportServiceTests
{
    private readonly XfdfExportService _svc = new();

    [Fact]
    public void Export_ProducesValidXfdf()
    {
        var markups = new[]
        {
            new MarkupRecord
            {
                Id = "test-1",
                Type = MarkupType.Rectangle,
                Vertices = { new Point(10, 20), new Point(100, 80) },
                Metadata = { Label = "Test markup", Author = "TestUser" }
            }
        };

        var xfdf = _svc.Export(markups);

        Assert.Contains("<?xml", xfdf);
        Assert.Contains("<xfdf", xfdf);
        Assert.Contains("<annots>", xfdf);
        Assert.Contains("square", xfdf); // Rectangle → square in XFDF
        Assert.Contains("test-1", xfdf);
    }

    [Fact]
    public void Export_MapsTypesCorrectly()
    {
        var markups = new[]
        {
            new MarkupRecord { Type = MarkupType.Rectangle },
            new MarkupRecord { Type = MarkupType.Circle },
            new MarkupRecord { Type = MarkupType.Text },
            new MarkupRecord { Type = MarkupType.Polyline, Vertices = { new Point(0,0), new Point(10,10) } },
            new MarkupRecord { Type = MarkupType.Polygon, Vertices = { new Point(0,0), new Point(10,0), new Point(5,10) } },
            new MarkupRecord { Type = MarkupType.Dimension, Vertices = { new Point(0,0), new Point(100,0) } },
            new MarkupRecord { Type = MarkupType.Stamp },
        };

        var xfdf = _svc.Export(markups);

        Assert.Contains("square", xfdf);
        Assert.Contains("circle", xfdf);
        Assert.Contains("freetext", xfdf);
        Assert.Contains("ink", xfdf);
        Assert.Contains("polygon", xfdf);
        Assert.Contains("line", xfdf);
        Assert.Contains("stamp", xfdf);
    }

    [Fact]
    public void RoundTrip_PreservesBasicProperties()
    {
        const string rootReplyId = "reply-root";
        var original = new MarkupRecord
        {
            Id = "roundtrip-1",
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Resolved,
            StatusNote = "Reviewed in issue sync.",
            AssignedTo = "Field Team",
            Metadata = { Author = "Tester", Subject = "Electrical" },
            Appearance = { StrokeColor = "#0000FF", StrokeWidth = 3.0, Opacity = 0.8 },
            BoundingRect = new Rect(10, 20, 90, 60),
            Replies =
            {
                new MarkupReply
                {
                    Id = rootReplyId,
                    Author = "Reviewer",
                    Text = "Primary issue thread."
                },
                new MarkupReply
                {
                    ParentReplyId = rootReplyId,
                    Author = "Coordinator",
                    Kind = MarkupReplyKind.AssignmentAudit,
                    Text = "Need updated panel schedule before closeout."
                }
            }
        };

        var xfdf = _svc.Export(new[] { original });
        var imported = _svc.Import(xfdf);

        Assert.Single(imported);
        var result = imported[0];

        Assert.Equal("roundtrip-1", result.Id);
        Assert.Equal(MarkupType.Rectangle, result.Type);
        Assert.Equal(MarkupStatus.Resolved, result.Status);
        Assert.Equal("Reviewed in issue sync.", result.StatusNote);
        Assert.Equal("Tester", result.Metadata.Author);
        Assert.Equal("Field Team", result.AssignedTo);
        Assert.Equal("#0000FF", result.Appearance.StrokeColor);
        Assert.Equal(3.0, result.Appearance.StrokeWidth);
        Assert.Equal(0.8, result.Appearance.Opacity);
        Assert.Equal(2, result.Replies.Count);
        Assert.Equal(rootReplyId, result.Replies[0].Id);
        Assert.Null(result.Replies[0].ParentReplyId);
        Assert.Equal(rootReplyId, result.Replies[1].ParentReplyId);
        Assert.Equal("Coordinator", result.Replies[1].Author);
        Assert.True(result.Replies[1].IsAuditEntry);
        Assert.Equal(MarkupReplyKind.AssignmentAudit, result.Replies[1].Kind);
        Assert.Equal("Need updated panel schedule before closeout.", result.Replies[1].Text);
    }

    [Fact]
    public void RoundTrip_PreservesPolylineVertices()
    {
        var original = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(50, 25), new Point(100, 0) }
        };

        var xfdf = _svc.Export(new[] { original });
        var imported = _svc.Import(xfdf);

        Assert.Single(imported);
        Assert.Equal(3, imported[0].Vertices.Count);
        Assert.Equal(0, imported[0].Vertices[0].X);
        Assert.Equal(50, imported[0].Vertices[1].X);
        Assert.Equal(100, imported[0].Vertices[2].X);
    }

    [Fact]
    public void RoundTrip_PreservesLineStartEnd()
    {
        var original = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 20), new Point(110, 20) }
        };

        var xfdf = _svc.Export(new[] { original });
        var imported = _svc.Import(xfdf);

        Assert.Single(imported);
        Assert.Equal(2, imported[0].Vertices.Count);
        Assert.Equal(10, imported[0].Vertices[0].X);
        Assert.Equal(110, imported[0].Vertices[1].X);
    }

    [Fact]
    public void Import_EmptyXfdf_ReturnsEmptyList()
    {
        var xfdf = "<?xml version=\"1.0\"?><xfdf xmlns=\"http://ns.adobe.com/xfdf/\"><annots></annots></xfdf>";
        var result = _svc.Import(xfdf);
        Assert.Empty(result);
    }

    [Fact]
    public void PreviewImportMerge_ReportsConflictsWithoutMutatingExistingList()
    {
        var existing = new List<MarkupRecord>
        {
            new()
            {
                Id = "issue-1",
                Type = MarkupType.Rectangle,
                BoundingRect = new Rect(0, 0, 10, 10),
                Status = MarkupStatus.Open
            }
        };
        var importedMarkup = new MarkupRecord
        {
            Id = "issue-1",
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(5, 5, 20, 20),
            Status = MarkupStatus.Resolved,
            StatusNote = "Resolved in imported review."
        };

        var preview = _svc.PreviewImportMerge(_svc.Export(new[] { importedMarkup }), existing);

        Assert.Equal(1, preview.ImportedCount);
        Assert.Equal(1, preview.ConflictCount);
        Assert.True(preview.Conflicts[0].Kind.HasFlag(XfdfMergeConflictKind.GeometryMismatch));
        Assert.True(preview.Conflicts[0].Kind.HasFlag(XfdfMergeConflictKind.ReviewStateMismatch));
        Assert.Equal(1, preview.GeometryConflictCount);
        Assert.Equal(1, preview.ReviewStateConflictCount);
        Assert.Equal(MarkupStatus.Open, existing[0].Status);
        Assert.Null(existing[0].StatusNote);
        Assert.Equal(new Rect(0, 0, 10, 10), existing[0].BoundingRect);
    }

    [Fact]
    public void ImportAndMerge_PreferImported_UpdatesMarkupAndMergesReplies()
    {
        const string markupId = "issue-1";
        const string rootReplyId = "reply-root";
        var existing = new List<MarkupRecord>
        {
            new()
            {
                Id = markupId,
                Type = MarkupType.Rectangle,
                BoundingRect = new Rect(0, 0, 10, 10),
                Status = MarkupStatus.Open,
                AssignedTo = "Field Team",
                Replies =
                {
                    new MarkupReply
                    {
                        Id = rootReplyId,
                        Author = "Reviewer A",
                        Text = "Original note"
                    }
                }
            }
        };
        var importedMarkup = new MarkupRecord
        {
            Id = markupId,
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(5, 5, 20, 20),
            Status = MarkupStatus.Resolved,
            StatusNote = "Resolved in field.",
            AssignedTo = "Coordinator",
            Replies =
            {
                new MarkupReply
                {
                    Id = rootReplyId,
                    Author = "Reviewer A",
                    Text = "Original note"
                },
                new MarkupReply
                {
                    ParentReplyId = rootReplyId,
                    Author = "Reviewer B",
                    Kind = MarkupReplyKind.StatusAudit,
                    Text = "Status changed in review sync."
                }
            }
        };

        var mergeResult = _svc.ImportAndMerge(_svc.Export(new[] { importedMarkup }), existing, XfdfImportMergeMode.PreferImported);

        Assert.Equal(1, mergeResult.ImportedCount);
        Assert.Equal(0, mergeResult.AddedCount);
        Assert.Equal(1, mergeResult.UpdatedCount);
        Assert.Equal(1, mergeResult.ConflictCount);
        Assert.Equal(1, mergeResult.RepliesAddedCount);
        Assert.Equal(1, mergeResult.AuditRepliesAddedCount);
        Assert.Equal(1, mergeResult.StatusNotesAppliedCount);
        Assert.Contains(markupId, mergeResult.UpdatedMarkupIds);
        Assert.Contains("Reviewer B", mergeResult.ParticipantNames);
        Assert.True(mergeResult.Conflicts[0].Kind.HasFlag(XfdfMergeConflictKind.GeometryMismatch));
        Assert.True(mergeResult.Conflicts[0].Kind.HasFlag(XfdfMergeConflictKind.ReviewStateMismatch));
        Assert.Single(existing);
        Assert.Equal(MarkupStatus.Resolved, existing[0].Status);
        Assert.Equal("Resolved in field.", existing[0].StatusNote);
        Assert.Equal("Coordinator", existing[0].AssignedTo);
        Assert.Equal(new Rect(5, 5, 20, 20), existing[0].BoundingRect);
        Assert.Equal(2, existing[0].Replies.Count);
        Assert.Equal(rootReplyId, existing[0].Replies[1].ParentReplyId);
    }

    [Fact]
    public void ImportAndMerge_PreferExisting_PreservesExistingStateAndAppendsReplies()
    {
        const string markupId = "issue-1";
        const string rootReplyId = "reply-root";
        var existing = new List<MarkupRecord>
        {
            new()
            {
                Id = markupId,
                Type = MarkupType.Rectangle,
                BoundingRect = new Rect(0, 0, 10, 10),
                Status = MarkupStatus.Open,
                AssignedTo = "Field Team",
                Replies =
                {
                    new MarkupReply
                    {
                        Id = rootReplyId,
                        Author = "Reviewer A",
                        Text = "Original note"
                    }
                }
            }
        };
        var importedMarkup = new MarkupRecord
        {
            Id = markupId,
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(5, 5, 20, 20),
            Status = MarkupStatus.Resolved,
            StatusNote = "Resolved in field.",
            AssignedTo = "Coordinator",
            Replies =
            {
                new MarkupReply
                {
                    Id = rootReplyId,
                    Author = "Reviewer A",
                    Text = "Original note"
                },
                new MarkupReply
                {
                    ParentReplyId = rootReplyId,
                    Author = "Reviewer B",
                    Kind = MarkupReplyKind.AssignmentAudit,
                    Text = "Assigned to coordinator."
                }
            }
        };

        var mergeResult = _svc.ImportAndMerge(_svc.Export(new[] { importedMarkup }), existing, XfdfImportMergeMode.PreferExisting);

        Assert.Equal(1, mergeResult.UpdatedCount);
        Assert.Equal(1, mergeResult.RepliesAddedCount);
        Assert.Equal(1, mergeResult.AuditRepliesAddedCount);
        Assert.Equal(0, mergeResult.StatusNotesAppliedCount);
        Assert.Equal(MarkupStatus.Open, existing[0].Status);
        Assert.Null(existing[0].StatusNote);
        Assert.Equal("Field Team", existing[0].AssignedTo);
        Assert.Equal(new Rect(0, 0, 10, 10), existing[0].BoundingRect);
        Assert.Equal(2, existing[0].Replies.Count);
        Assert.Equal(rootReplyId, existing[0].Replies[1].ParentReplyId);
    }

    [Fact]
    public void ImportAndMerge_AddAsNew_DuplicatesMarkupWithFreshId()
    {
        var existing = new List<MarkupRecord>
        {
            new()
            {
                Id = "issue-1",
                Type = MarkupType.Rectangle,
                BoundingRect = new Rect(0, 0, 10, 10)
            }
        };
        var importedMarkup = new MarkupRecord
        {
            Id = "issue-1",
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(5, 5, 20, 20)
        };

        var mergeResult = _svc.ImportAndMerge(_svc.Export(new[] { importedMarkup }), existing, XfdfImportMergeMode.AddAsNew);

        Assert.Equal(1, mergeResult.AddedCount);
        Assert.Equal(0, mergeResult.UpdatedCount);
        Assert.Equal(1, mergeResult.DuplicatedCount);
        Assert.Single(mergeResult.DuplicatedMarkupIds);
        Assert.Equal(2, existing.Count);
        Assert.NotEqual("issue-1", existing[1].Id);
        Assert.Equal(new Rect(5, 5, 20, 20), existing[1].BoundingRect);
    }
}
