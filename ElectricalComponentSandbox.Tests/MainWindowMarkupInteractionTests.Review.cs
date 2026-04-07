using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public partial class MainWindowMarkupInteractionTests
{
    [Fact]
    public void ExecuteAddMarkupReplyCommandForTesting_AddsReplyAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                }
            },
            (window, viewModel, markup) =>
            {
                var added = window.ExecuteAddMarkupReplyCommandForTesting("Need revised feeder routing.", "Reviewer");
                var afterAddReplyText = markup.Replies[0].Text;
                var afterAddParentReplyId = markup.Replies[0].ParentReplyId;
                var afterAddReplyIsAuditEntry = markup.Replies[0].IsAuditEntry;
                var afterAddVisibleReplyCount = viewModel.MarkupTool.SelectedMarkupReplies.Count;
                var afterAddVisibleReplyDepth = viewModel.MarkupTool.SelectedMarkupReplies[0].ThreadDepth;
                var afterAddVisibleReplyIsAuditEntry = viewModel.MarkupTool.SelectedMarkupReplies[0].IsAuditEntry;
                var afterAddVisibleReplyType = viewModel.MarkupTool.SelectedMarkupReplies[0].EntryTypeDisplayText;

                viewModel.Undo();
                var afterUndoReplyCount = markup.Replies.Count;
                var afterUndoVisibleReplyCount = viewModel.MarkupTool.SelectedMarkupReplies.Count;
                return (added, afterAddReplyText, afterAddParentReplyId, afterAddReplyIsAuditEntry, afterAddVisibleReplyCount, afterAddVisibleReplyDepth, afterAddVisibleReplyIsAuditEntry, afterAddVisibleReplyType, afterUndoReplyCount, afterUndoVisibleReplyCount);
            });

        Assert.True(outcome.added);
        Assert.Equal("Need revised feeder routing.", outcome.afterAddReplyText);
        Assert.Null(outcome.afterAddParentReplyId);
        Assert.False(outcome.afterAddReplyIsAuditEntry);
        Assert.Equal(1, outcome.afterAddVisibleReplyCount);
        Assert.Equal(0, outcome.afterAddVisibleReplyDepth);
        Assert.False(outcome.afterAddVisibleReplyIsAuditEntry);
        Assert.Equal("Reply", outcome.afterAddVisibleReplyType);
        Assert.Equal(0, outcome.afterUndoReplyCount);
        Assert.Equal(0, outcome.afterUndoVisibleReplyCount);
    }

    [Fact]
    public void ExecuteAddMarkupReplyCommandForTesting_WithExistingReply_ThreadsUnderLatestReply()
    {
        const string rootReplyId = "reply-root";
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                },
                Replies =
                {
                    new MarkupReply
                    {
                        Id = rootReplyId,
                        Author = "Reviewer A",
                        Text = "Original note",
                        CreatedUtc = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc),
                        ModifiedUtc = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)
                    }
                }
            },
            (window, viewModel, markup) =>
            {
                var added = window.ExecuteAddMarkupReplyCommandForTesting("Follow-up response", "Reviewer B");
                var addedReply = markup.Replies.Single(reply => reply.Text == "Follow-up response");
                return (
                    added,
                    AddedReplyParentId: addedReply.ParentReplyId,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count,
                    RootDepth: viewModel.MarkupTool.SelectedMarkupReplies[0].ThreadDepth,
                    ChildDepth: viewModel.MarkupTool.SelectedMarkupReplies[1].ThreadDepth,
                    ChildParentId: viewModel.MarkupTool.SelectedMarkupReplies[1].ParentReplyId);
            });

        Assert.True(outcome.added);
        Assert.Equal(rootReplyId, outcome.AddedReplyParentId);
        Assert.Equal(2, outcome.VisibleReplyCount);
        Assert.Equal(0, outcome.RootDepth);
        Assert.Equal(1, outcome.ChildDepth);
        Assert.Equal(rootReplyId, outcome.ChildParentId);
    }

    [Fact]
    public void ExecuteAddMarkupReplyCommandForTesting_WithoutSelection_ReturnsFalse()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow(viewModel);
            try
            {
                var added = window.ExecuteAddMarkupReplyCommandForTesting("Need revised feeder routing.", "Reviewer");
                return (added, VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(outcome.added);
        Assert.Equal(0, outcome.VisibleReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteAddMarkupReplyCommandForTesting_BlankReply_ReturnsFalseAndLeavesRepliesUnchanged()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                }
            },
            (window, viewModel, markup) =>
            {
                var added = window.ExecuteAddMarkupReplyCommandForTesting("   ", "Reviewer");
                return (added, ReplyCount: markup.Replies.Count, VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            });

        Assert.False(outcome.added);
        Assert.Equal(0, outcome.ReplyCount);
        Assert.Equal(0, outcome.VisibleReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteSetSelectedMarkupStatusForTesting_AddsAuditReplyAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Open,
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                }
            },
            (window, viewModel, markup) =>
            {
                var changed = window.ExecuteSetSelectedMarkupStatusForTesting(MarkupStatus.Approved, "Reviewer");
                var afterChange = (
                    Status: markup.Status,
                    StatusNote: markup.StatusNote,
                    ReplyCount: markup.Replies.Count,
                    AuditParentReplyId: markup.Replies[0].ParentReplyId,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count,
                    VisibleReplyIsAuditEntry: viewModel.MarkupTool.SelectedMarkupReplies[0].IsAuditEntry,
                    VisibleReplyType: viewModel.MarkupTool.SelectedMarkupReplies[0].EntryTypeDisplayText,
                    VisibleReplyDepth: viewModel.MarkupTool.SelectedMarkupReplies[0].ThreadDepth);

                viewModel.Undo();
                var afterUndo = (
                    Status: markup.Status,
                    StatusNote: markup.StatusNote,
                    ReplyCount: markup.Replies.Count,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal(MarkupStatus.Approved, outcome.afterChange.Status);
        Assert.Equal("Status changed: Open -> Approved", outcome.afterChange.StatusNote);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
        Assert.Null(outcome.afterChange.AuditParentReplyId);
        Assert.Equal(1, outcome.afterChange.VisibleReplyCount);
        Assert.True(outcome.afterChange.VisibleReplyIsAuditEntry);
        Assert.Equal("Status", outcome.afterChange.VisibleReplyType);
        Assert.Equal(0, outcome.afterChange.VisibleReplyDepth);
        Assert.Equal(MarkupStatus.Open, outcome.afterUndo.Status);
        Assert.Null(outcome.afterUndo.StatusNote);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.VisibleReplyCount);
    }

    [Fact]
    public void ExecuteSetSelectedMarkupStatusForTesting_WithExistingReply_ThreadsAuditUnderLatestReply()
    {
        const string rootReplyId = "reply-root";
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Open,
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                },
                Replies =
                {
                    new MarkupReply
                    {
                        Id = rootReplyId,
                        Author = "Reviewer A",
                        Text = "Original note",
                        CreatedUtc = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc),
                        ModifiedUtc = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)
                    }
                }
            },
            (window, viewModel, markup) =>
            {
                var changed = window.ExecuteSetSelectedMarkupStatusForTesting(MarkupStatus.Resolved, "Reviewer B");
                var auditReply = markup.Replies.Single(reply => reply.Kind == MarkupReplyKind.StatusAudit);
                return (
                    changed,
                    AuditParentId: auditReply.ParentReplyId,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count,
                    ChildDepth: viewModel.MarkupTool.SelectedMarkupReplies[1].ThreadDepth,
                    ChildParentId: viewModel.MarkupTool.SelectedMarkupReplies[1].ParentReplyId);
            });

        Assert.True(outcome.changed);
        Assert.Equal(rootReplyId, outcome.AuditParentId);
        Assert.Equal(2, outcome.VisibleReplyCount);
        Assert.Equal(1, outcome.ChildDepth);
        Assert.Equal(rootReplyId, outcome.ChildParentId);
    }

    [Fact]
    public void ExecuteSetSelectedMarkupStatusForTesting_WithoutSelection_ReturnsFalse()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow(viewModel);
            try
            {
                var changed = window.ExecuteSetSelectedMarkupStatusForTesting(MarkupStatus.Approved, "Reviewer");
                return (changed, CanUndo: viewModel.UndoRedo.CanUndo);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(outcome.changed);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteSetSelectedMarkupStatusForTesting_SameStatus_ReturnsFalseAndLeavesAuditTrailUnchanged()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Approved,
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                }
            },
            (window, viewModel, markup) =>
            {
                var changed = window.ExecuteSetSelectedMarkupStatusForTesting(MarkupStatus.Approved, "Reviewer");
                return (changed, Status: markup.Status, StatusNote: markup.StatusNote, ReplyCount: markup.Replies.Count, VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            });

        Assert.False(outcome.changed);
        Assert.Equal(MarkupStatus.Approved, outcome.Status);
        Assert.Null(outcome.StatusNote);
        Assert.Equal(0, outcome.ReplyCount);
        Assert.Equal(0, outcome.VisibleReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteAssignSelectedMarkupForTesting_AddsAssignmentAuditAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                }
            },
            (window, viewModel, markup) =>
            {
                var changed = window.ExecuteAssignSelectedMarkupForTesting("Field Crew", "Coordinator");
                var afterChange = (
                    AssignedTo: markup.AssignedTo,
                    ReplyCount: markup.Replies.Count,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count,
                    VisibleReplyIsAuditEntry: viewModel.MarkupTool.SelectedMarkupReplies[0].IsAuditEntry,
                    VisibleReplyType: viewModel.MarkupTool.SelectedMarkupReplies[0].EntryTypeDisplayText);

                viewModel.Undo();
                var afterUndo = (
                    AssignedTo: markup.AssignedTo,
                    ReplyCount: markup.Replies.Count,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal("Field Crew", outcome.afterChange.AssignedTo);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
        Assert.Equal(1, outcome.afterChange.VisibleReplyCount);
        Assert.True(outcome.afterChange.VisibleReplyIsAuditEntry);
        Assert.Equal("Assignment", outcome.afterChange.VisibleReplyType);
        Assert.Null(outcome.afterUndo.AssignedTo);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.VisibleReplyCount);
    }

    [Fact]
    public void ExecuteAssignSelectedMarkupForTesting_WithoutSelection_ReturnsFalse()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow(viewModel);
            try
            {
                var changed = window.ExecuteAssignSelectedMarkupForTesting("Field Crew", "Coordinator");
                return (changed, CanUndo: viewModel.UndoRedo.CanUndo);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(outcome.changed);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteAssignSelectedMarkupForTesting_SameAssignee_ReturnsFalseAndLeavesAuditTrailUnchanged()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                AssignedTo = "Field Crew",
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                }
            },
            (window, viewModel, markup) =>
            {
                var changed = window.ExecuteAssignSelectedMarkupForTesting("Field Crew", "Coordinator");
                return (changed, AssignedTo: markup.AssignedTo, ReplyCount: markup.Replies.Count, VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            });

        Assert.False(outcome.changed);
        Assert.Equal("Field Crew", outcome.AssignedTo);
        Assert.Equal(0, outcome.ReplyCount);
        Assert.Equal(0, outcome.VisibleReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteSetSelectedIssueGroupStatusForTesting_UpdatesOnlySelectedBucketAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Open,
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                var otherMarkup = new MarkupRecord
                {
                    Type = MarkupType.Rectangle,
                    Vertices = { new Point(12, 12), new Point(20, 20) },
                    Status = MarkupStatus.Open,
                    Metadata = new MarkupMetadata
                    {
                        Label = "Bucket B",
                        Author = "Casey"
                    }
                };
                otherMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(otherMarkup);
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = Assert.Single(viewModel.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

                var changed = window.ExecuteSetSelectedIssueGroupStatusForTesting(MarkupStatus.Resolved, "Reviewer");
                var afterChange = (
                    BucketStatus: markup.Status,
                    OtherStatus: otherMarkup.Status,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);

                viewModel.Undo();
                var afterUndo = (
                    BucketStatus: markup.Status,
                    OtherStatus: otherMarkup.Status,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterChange.BucketStatus);
        Assert.Equal(MarkupStatus.Open, outcome.afterChange.OtherStatus);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
        Assert.Equal(0, outcome.afterChange.OtherReplyCount);
        Assert.Equal(MarkupStatus.Open, outcome.afterUndo.BucketStatus);
        Assert.Equal(MarkupStatus.Open, outcome.afterUndo.OtherStatus);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.OtherReplyCount);
    }

    [Fact]
    public void ExecuteSetSelectedIssueGroupStatusForTesting_WithoutSelectedBucket_ReturnsFalse()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Open,
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = null;

                var changed = window.ExecuteSetSelectedIssueGroupStatusForTesting(MarkupStatus.Resolved, "Reviewer");
                return (changed, Status: markup.Status, ReplyCount: markup.Replies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            });

        Assert.False(outcome.changed);
        Assert.Equal(MarkupStatus.Open, outcome.Status);
        Assert.Equal(0, outcome.ReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteSetSelectedIssueGroupStatusForTesting_NoEligibleMarkupInBucket_ReturnsFalseAndLeavesAuditTrailUnchanged()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Resolved,
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = Assert.Single(viewModel.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

                var changed = window.ExecuteSetSelectedIssueGroupStatusForTesting(MarkupStatus.Resolved, "Reviewer");
                return (changed, Status: markup.Status, ReplyCount: markup.Replies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            });

        Assert.False(outcome.changed);
        Assert.Equal(MarkupStatus.Resolved, outcome.Status);
        Assert.Equal(0, outcome.ReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteAssignSelectedIssueGroupForTesting_UpdatesOnlySelectedBucketAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                var otherMarkup = new MarkupRecord
                {
                    Type = MarkupType.Rectangle,
                    Vertices = { new Point(12, 12), new Point(20, 20) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Bucket B",
                        Author = "Casey"
                    }
                };
                otherMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(otherMarkup);
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = Assert.Single(viewModel.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

                var changed = window.ExecuteAssignSelectedIssueGroupForTesting("Field Crew", "Reviewer");
                var afterChange = (
                    BucketAssignee: markup.AssignedTo,
                    OtherAssignee: otherMarkup.AssignedTo,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);

                viewModel.Undo();
                var afterUndo = (
                    BucketAssignee: markup.AssignedTo,
                    OtherAssignee: otherMarkup.AssignedTo,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal("Field Crew", outcome.afterChange.BucketAssignee);
        Assert.Null(outcome.afterChange.OtherAssignee);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
        Assert.Equal(0, outcome.afterChange.OtherReplyCount);
        Assert.Null(outcome.afterUndo.BucketAssignee);
        Assert.Null(outcome.afterUndo.OtherAssignee);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.OtherReplyCount);
    }

    [Fact]
    public void ExecuteAssignSelectedIssueGroupForTesting_WithoutSelectedBucket_ReturnsFalse()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = null;

                var changed = window.ExecuteAssignSelectedIssueGroupForTesting("Field Crew", "Reviewer");
                return (changed, AssignedTo: markup.AssignedTo, ReplyCount: markup.Replies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            });

        Assert.False(outcome.changed);
        Assert.Null(outcome.AssignedTo);
        Assert.Equal(0, outcome.ReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteAssignSelectedIssueGroupForTesting_NoEligibleMarkupInBucket_ReturnsFalseAndLeavesAuditTrailUnchanged()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                AssignedTo = "Field Crew",
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = Assert.Single(viewModel.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

                var changed = window.ExecuteAssignSelectedIssueGroupForTesting("Field Crew", "Reviewer");
                return (changed, AssignedTo: markup.AssignedTo, ReplyCount: markup.Replies.Count, CanUndo: viewModel.UndoRedo.CanUndo);
            });

        Assert.False(outcome.changed);
        Assert.Equal("Field Crew", outcome.AssignedTo);
        Assert.Equal(0, outcome.ReplyCount);
        Assert.False(outcome.CanUndo);
    }

    [Fact]
    public void ExecuteApproveSelectedIssueGroupForTesting_UpdatesOnlySelectedBucketAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Resolved,
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                var otherMarkup = new MarkupRecord
                {
                    Type = MarkupType.Rectangle,
                    Vertices = { new Point(12, 12), new Point(20, 20) },
                    Status = MarkupStatus.Resolved,
                    Metadata = new MarkupMetadata
                    {
                        Label = "Bucket B",
                        Author = "Casey"
                    }
                };
                otherMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(otherMarkup);
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = Assert.Single(viewModel.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

                var changed = window.ExecuteApproveSelectedIssueGroupForTesting("Reviewer");
                var afterChange = (
                    BucketStatus: markup.Status,
                    OtherStatus: otherMarkup.Status,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);

                viewModel.Undo();
                var afterUndo = (
                    BucketStatus: markup.Status,
                    OtherStatus: otherMarkup.Status,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal(MarkupStatus.Approved, outcome.afterChange.BucketStatus);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterChange.OtherStatus);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
        Assert.Equal(0, outcome.afterChange.OtherReplyCount);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterUndo.BucketStatus);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterUndo.OtherStatus);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.OtherReplyCount);
    }

    [Fact]
    public void ExecuteRejectSelectedIssueGroupForTesting_UpdatesOnlySelectedBucketAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Resolved,
                Metadata = new MarkupMetadata
                {
                    Label = "Bucket A",
                    Author = "Paul"
                }
            },
            (window, viewModel, markup) =>
            {
                var otherMarkup = new MarkupRecord
                {
                    Type = MarkupType.Rectangle,
                    Vertices = { new Point(12, 12), new Point(20, 20) },
                    Status = MarkupStatus.Resolved,
                    Metadata = new MarkupMetadata
                    {
                        Label = "Bucket B",
                        Author = "Casey"
                    }
                };
                otherMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(otherMarkup);
                viewModel.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
                viewModel.MarkupTool.SelectedIssueGroup = Assert.Single(viewModel.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

                var changed = window.ExecuteRejectSelectedIssueGroupForTesting("Reviewer");
                var afterChange = (
                    BucketStatus: markup.Status,
                    OtherStatus: otherMarkup.Status,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);

                viewModel.Undo();
                var afterUndo = (
                    BucketStatus: markup.Status,
                    OtherStatus: otherMarkup.Status,
                    ReplyCount: markup.Replies.Count,
                    OtherReplyCount: otherMarkup.Replies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal(MarkupStatus.Rejected, outcome.afterChange.BucketStatus);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterChange.OtherStatus);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
        Assert.Equal(0, outcome.afterChange.OtherReplyCount);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterUndo.BucketStatus);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterUndo.OtherStatus);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.OtherReplyCount);
    }
}