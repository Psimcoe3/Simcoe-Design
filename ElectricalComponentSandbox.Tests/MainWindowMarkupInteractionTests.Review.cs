using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    public void ReviewWorkflowCard_BindsAssignmentSummaryAndReplyThread()
    {
        const string rootReplyId = "reply-root";
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                AssignedTo = "Field Crew",
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
                        Text = "Need revised feeder routing.",
                        CreatedUtc = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc),
                        ModifiedUtc = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)
                    },
                    new MarkupReply
                    {
                        Id = "reply-child",
                        ParentReplyId = rootReplyId,
                        Author = "Reviewer B",
                        Text = "Confirmed on revised sheet E2.",
                        Kind = MarkupReplyKind.StatusAudit,
                        CreatedUtc = new DateTime(2026, 4, 7, 13, 0, 0, DateTimeKind.Utc),
                        ModifiedUtc = new DateTime(2026, 4, 7, 13, 0, 0, DateTimeKind.Utc)
                    }
                }
            },
            (window, viewModel, markup) =>
            {
                window.UpdateLayout();

                var reviewCard = FindRequired<Border>(window, "SelectedMarkupReviewCard");
                var assignmentSummary = FindRequired<TextBlock>(window, "SelectedMarkupAssignmentSummaryTextBlock");
                var replySummary = FindRequired<TextBlock>(window, "SelectedMarkupReplySummaryTextBlock");
                var addReplyButton = FindRequired<Button>(window, "AddMarkupReplyButton");
                var assignButton = FindRequired<Button>(window, "AssignSelectedMarkupButton");
                var claimButton = FindRequired<Button>(window, "ClaimSelectedMarkupButton");
                var replyList = FindRequired<ItemsControl>(window, "SelectedMarkupReplyList");
                var reviewCardBinding = reviewCard.GetBindingExpression(UIElement.VisibilityProperty);
                var assignmentSummaryBinding = assignmentSummary.GetBindingExpression(TextBlock.TextProperty);
                var replySummaryBinding = replySummary.GetBindingExpression(TextBlock.TextProperty);
                var replyListBinding = replyList.GetBindingExpression(ItemsControl.ItemsSourceProperty);

                return (
                    ReviewCardVisibilityPath: reviewCardBinding?.ParentBinding.Path.Path,
                    AssignmentSummaryPath: assignmentSummaryBinding?.ParentBinding.Path.Path,
                    ReplySummaryPath: replySummaryBinding?.ParentBinding.Path.Path,
                    ReplyItemsSourcePath: replyListBinding?.ParentBinding.Path.Path,
                    AddReplyEnabled: addReplyButton.IsEnabled,
                        AssignEnabled: assignButton.IsEnabled,
                        ClaimEnabled: claimButton.IsEnabled);
            });

        Assert.Equal("MarkupTool.SelectedMarkup", outcome.ReviewCardVisibilityPath);
        Assert.Equal("MarkupTool.SelectedMarkupAssignmentSummary", outcome.AssignmentSummaryPath);
        Assert.Equal("MarkupTool.SelectedMarkupReplySummary", outcome.ReplySummaryPath);
        Assert.Equal("MarkupTool.SelectedMarkupReplies", outcome.ReplyItemsSourcePath);
        Assert.True(outcome.AddReplyEnabled);
        Assert.True(outcome.AssignEnabled);
        Assert.True(outcome.ClaimEnabled);
    }

    [Fact]
    public void ReviewBucketSurface_BindsScopeBucketListAndActionSummary()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();

            var firstMarkup = new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Issue A",
                    Author = "Paul"
                }
            };
            firstMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(firstMarkup);

            var secondMarkup = new MarkupRecord
            {
                Type = MarkupType.Text,
                Status = MarkupStatus.Resolved,
                TextContent = "Done",
                Vertices = { new Point(12, 12) },
                Metadata = new MarkupMetadata
                {
                    Label = "Issue B",
                    Author = "Casey"
                }
            };
            secondMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(secondMarkup);

            var window = new MainWindow(viewModel);
            try
            {
                window.UpdateLayout();

                var scopeCombo = FindRequired<ComboBox>(window, "MarkupReviewScopeCombo");
                var issueGroupCombo = FindRequired<ComboBox>(window, "IssueGroupModeComboBox");
                var authorCombo = FindRequired<ComboBox>(window, "MarkupAuthorFilterCombo");
                var assigneeCombo = FindRequired<ComboBox>(window, "MarkupAssigneeFilterCombo");
                var issueGroupList = FindRequired<ListBox>(window, "IssueGroupListBox");
                var issueGroupSummary = FindRequired<TextBlock>(window, "SelectedIssueGroupSummaryTextBlock");
                var issueGroupActionSummary = FindRequired<TextBlock>(window, "SelectedIssueGroupActionSummaryTextBlock");
                var resolveBucketButton = FindRequired<Button>(window, "ResolveSelectedIssueGroupButton");
                var claimBucketButton = FindRequired<Button>(window, "ClaimSelectedIssueGroupButton");

                return (
                    ScopeItemsSourcePath: scopeCombo.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path,
                    ScopeSelectedItemPath: scopeCombo.GetBindingExpression(Selector.SelectedItemProperty)?.ParentBinding.Path.Path,
                    GroupModeItemsSourcePath: issueGroupCombo.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path,
                    GroupModeSelectedItemPath: issueGroupCombo.GetBindingExpression(Selector.SelectedItemProperty)?.ParentBinding.Path.Path,
                    AuthorItemsSourcePath: authorCombo.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path,
                    AuthorSelectedItemPath: authorCombo.GetBindingExpression(Selector.SelectedItemProperty)?.ParentBinding.Path.Path,
                    AssigneeItemsSourcePath: assigneeCombo.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path,
                    AssigneeSelectedItemPath: assigneeCombo.GetBindingExpression(Selector.SelectedItemProperty)?.ParentBinding.Path.Path,
                    GroupListItemsSourcePath: issueGroupList.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path,
                    GroupListSelectedItemPath: issueGroupList.GetBindingExpression(Selector.SelectedItemProperty)?.ParentBinding.Path.Path,
                    GroupSummaryPath: issueGroupSummary.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path.Path,
                    GroupActionSummaryPath: issueGroupActionSummary.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path.Path,
                    ResolveBucketEnabledPath: resolveBucketButton.GetBindingExpression(UIElement.IsEnabledProperty)?.ParentBinding.Path.Path,
                    ClaimBucketEnabledPath: claimBucketButton.GetBindingExpression(UIElement.IsEnabledProperty)?.ParentBinding.Path.Path);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("MarkupTool.ReviewScopeOptions", outcome.ScopeItemsSourcePath);
        Assert.Equal("MarkupTool.ReviewScope", outcome.ScopeSelectedItemPath);
        Assert.Equal("MarkupTool.IssueGroupModeOptions", outcome.GroupModeItemsSourcePath);
        Assert.Equal("MarkupTool.IssueGroupMode", outcome.GroupModeSelectedItemPath);
        Assert.Equal("MarkupTool.AuthorFilterOptions", outcome.AuthorItemsSourcePath);
        Assert.Equal("MarkupTool.AuthorFilter", outcome.AuthorSelectedItemPath);
        Assert.Equal("MarkupTool.AssigneeFilterOptions", outcome.AssigneeItemsSourcePath);
        Assert.Equal("MarkupTool.AssigneeFilter", outcome.AssigneeSelectedItemPath);
        Assert.Equal("MarkupTool.IssueGroups", outcome.GroupListItemsSourcePath);
        Assert.Equal("MarkupTool.SelectedIssueGroup", outcome.GroupListSelectedItemPath);
        Assert.Equal("MarkupTool.SelectedIssueGroupSummary", outcome.GroupSummaryPath);
        Assert.Equal("MarkupTool.SelectedIssueGroupActionSummary", outcome.GroupActionSummaryPath);
        Assert.Equal("MarkupTool.HasSelectedIssueGroup", outcome.ResolveBucketEnabledPath);
        Assert.Equal("MarkupTool.HasSelectedIssueGroup", outcome.ClaimBucketEnabledPath);
    }

    [Fact]
    public void ReviewWorkflowButtons_ResolveAndVoidApplyMatchingStatuses()
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
                window.UpdateLayout();

                var resolveButton = FindRequired<Button>(window, "ResolveSelectedMarkupButton");
                var voidButton = FindRequired<Button>(window, "VoidSelectedMarkupButton");

                resolveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var afterResolve = (Status: markup.Status, ReplyText: Assert.Single(markup.Replies).Text);

                viewModel.Undo();

                voidButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var afterVoid = (Status: markup.Status, ReplyText: Assert.Single(markup.Replies).Text);

                return (afterResolve, afterVoid);
            });

        Assert.Equal(MarkupStatus.Resolved, outcome.afterResolve.Status);
        Assert.Equal("Status changed: Open -> Resolved", outcome.afterResolve.ReplyText);
        Assert.Equal(MarkupStatus.Void, outcome.afterVoid.Status);
        Assert.Equal("Status changed: Open -> Void", outcome.afterVoid.ReplyText);
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
    public void ExecuteClaimSelectedMarkupForTesting_AssignsCurrentActorAndSupportsUndo()
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
                var changed = window.ExecuteClaimSelectedMarkupForTesting("Reviewer");
                var afterChange = (
                    AssignedTo: markup.AssignedTo,
                    ReplyCount: markup.Replies.Count,
                    VisibleReplyType: viewModel.MarkupTool.SelectedMarkupReplies[0].EntryTypeDisplayText);

                viewModel.Undo();
                var afterUndo = (
                    AssignedTo: markup.AssignedTo,
                    ReplyCount: markup.Replies.Count,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal("Reviewer", outcome.afterChange.AssignedTo);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
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
    public void ExecuteSetSelectedIssueGroupStatusForTesting_WithReviewerNote_AddsManualReplyAndAuditReply()
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
                viewModel.MarkupTool.SelectedIssueGroup = Assert.Single(viewModel.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

                var changed = window.ExecuteSetSelectedIssueGroupStatusForTesting(MarkupStatus.Resolved, "Reviewer", "Fixed in rev C.");
                var manualReply = Assert.Single(markup.Replies.Where(reply => !reply.IsAuditEntry));
                var auditReply = Assert.Single(markup.Replies.Where(reply => reply.Kind == MarkupReplyKind.StatusAudit));
                var afterChange = (
                    Status: markup.Status,
                    StatusNote: markup.StatusNote,
                    ReplyCount: markup.Replies.Count,
                    ManualReplyText: manualReply.Text,
                    AuditReplyText: auditReply.Text,
                    AuditParentReplyId: auditReply.ParentReplyId,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count,
                    VisibleManualReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count(reply => !reply.IsAuditEntry));

                viewModel.Undo();
                var afterUndo = (
                    Status: markup.Status,
                    StatusNote: markup.StatusNote,
                    ReplyCount: markup.Replies.Count,
                    VisibleReplyCount: viewModel.MarkupTool.SelectedMarkupReplies.Count);
                return (changed, afterChange, afterUndo);
            });

        Assert.True(outcome.changed);
        Assert.Equal(MarkupStatus.Resolved, outcome.afterChange.Status);
        Assert.Equal("Fixed in rev C.", outcome.afterChange.StatusNote);
        Assert.Equal(2, outcome.afterChange.ReplyCount);
        Assert.Equal("Fixed in rev C.", outcome.afterChange.ManualReplyText);
        Assert.Equal("Status changed: Open -> Resolved", outcome.afterChange.AuditReplyText);
        Assert.NotNull(outcome.afterChange.AuditParentReplyId);
        Assert.Equal(2, outcome.afterChange.VisibleReplyCount);
        Assert.Equal(1, outcome.afterChange.VisibleManualReplyCount);
        Assert.Equal(MarkupStatus.Open, outcome.afterUndo.Status);
        Assert.Null(outcome.afterUndo.StatusNote);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.VisibleReplyCount);
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
    public void ExecuteClaimSelectedIssueGroupForTesting_UpdatesOnlySelectedBucketAndSupportsUndo()
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

                var changed = window.ExecuteClaimSelectedIssueGroupForTesting("Reviewer");
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
        Assert.Equal("Reviewer", outcome.afterChange.BucketAssignee);
        Assert.Null(outcome.afterChange.OtherAssignee);
        Assert.Equal(1, outcome.afterChange.ReplyCount);
        Assert.Equal(0, outcome.afterChange.OtherReplyCount);
        Assert.Null(outcome.afterUndo.BucketAssignee);
        Assert.Null(outcome.afterUndo.OtherAssignee);
        Assert.Equal(0, outcome.afterUndo.ReplyCount);
        Assert.Equal(0, outcome.afterUndo.OtherReplyCount);
    }

    [Fact]
    public void ExecuteClaimVisibleMarkupsForTesting_UpdatesOnlyFilteredVisibleMarkupsAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();

            var visibleMarkup = new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Visible issue",
                    Author = "Paul"
                }
            };
            visibleMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(visibleMarkup);

            var hiddenMarkup = new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Approved,
                Vertices = { new Point(12, 12), new Point(20, 20) },
                Metadata = new MarkupMetadata
                {
                    Label = "Hidden issue",
                    Author = "Casey"
                }
            };
            hiddenMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(hiddenMarkup);

            viewModel.MarkupTool.StatusFilter = MarkupRecord.GetStatusDisplayText(MarkupStatus.Open);

            var window = new MainWindow(viewModel);
            try
            {
                var changed = window.ExecuteClaimVisibleMarkupsForTesting("Reviewer");
                var afterChange = (
                    VisibleAssignedTo: visibleMarkup.AssignedTo,
                    HiddenAssignedTo: hiddenMarkup.AssignedTo,
                    VisibleReplyCount: visibleMarkup.Replies.Count,
                    HiddenReplyCount: hiddenMarkup.Replies.Count);

                viewModel.Undo();
                var afterUndo = (
                    VisibleAssignedTo: visibleMarkup.AssignedTo,
                    HiddenAssignedTo: hiddenMarkup.AssignedTo,
                    VisibleReplyCount: visibleMarkup.Replies.Count,
                    HiddenReplyCount: hiddenMarkup.Replies.Count);
                return (changed, afterChange, afterUndo);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.changed);
        Assert.Equal("Reviewer", outcome.afterChange.VisibleAssignedTo);
        Assert.Null(outcome.afterChange.HiddenAssignedTo);
        Assert.Equal(1, outcome.afterChange.VisibleReplyCount);
        Assert.Equal(0, outcome.afterChange.HiddenReplyCount);
        Assert.Null(outcome.afterUndo.VisibleAssignedTo);
        Assert.Null(outcome.afterUndo.HiddenAssignedTo);
        Assert.Equal(0, outcome.afterUndo.VisibleReplyCount);
        Assert.Equal(0, outcome.afterUndo.HiddenReplyCount);
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

    [Fact]
    public void PublishMarkupReviewSnapshotForTesting_CapturesCurrentReviewSetAndBuildsComparisonSummary()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();

            var statusChangedMarkup = new MarkupRecord
            {
                Id = "snapshot-status",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Status change",
                    Author = "Paul"
                }
            };
            statusChangedMarkup.UpdateBoundingRect();

            var ownershipChangedMarkup = new MarkupRecord
            {
                Id = "snapshot-owner",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                AssignedTo = "Crew A",
                Vertices = { new Point(12, 0), new Point(22, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Ownership change",
                    Author = "Casey"
                }
            };
            ownershipChangedMarkup.UpdateBoundingRect();

            var unchangedMarkup = new MarkupRecord
            {
                Id = "snapshot-unchanged",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                AssignedTo = "Crew C",
                Vertices = { new Point(24, 0), new Point(34, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Unchanged",
                    Author = "Jordan"
                }
            };
            unchangedMarkup.UpdateBoundingRect();

            viewModel.Markups.Add(statusChangedMarkup);
            viewModel.Markups.Add(ownershipChangedMarkup);
            viewModel.Markups.Add(unchangedMarkup);

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("Coordination Set", "Coordinator");

                statusChangedMarkup.Status = MarkupStatus.Resolved;
                ownershipChangedMarkup.AssignedTo = "Crew B";

                var newMarkup = new MarkupRecord
                {
                    Id = "snapshot-new",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "New issue",
                    Vertices = { new Point(36, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Added later",
                        Author = "Taylor"
                    }
                };
                newMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(newMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var displayNames = window.GetMarkupReviewSnapshotDisplayNamesForTesting();
                var selected = window.SelectMarkupReviewSnapshotForTesting("Coordination Set");
                var summary = window.GetMarkupReviewSnapshotSummaryForTesting();
                var selectedSummary = window.GetSelectedMarkupReviewSnapshotSummaryForTesting();
                var comparison = window.GetSelectedMarkupReviewSnapshotComparisonForTesting();

                return (published, displayNames, selected, summary, selectedSummary, comparison);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.Single(outcome.displayNames);
        Assert.Equal("Coordination Set", outcome.displayNames[0]);
        Assert.True(outcome.selected);
        Assert.Contains("1 published review set", outcome.summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Coordination Set", outcome.selectedSummary, StringComparison.Ordinal);
        Assert.Contains("1 unchanged", outcome.comparison, StringComparison.Ordinal);
        Assert.Contains("1 status changed", outcome.comparison, StringComparison.Ordinal);
        Assert.Contains("1 ownership changed", outcome.comparison, StringComparison.Ordinal);
        Assert.Contains("1 new", outcome.comparison, StringComparison.Ordinal);
        Assert.Contains("0 missing", outcome.comparison, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkupReviewSnapshotSelection_IsRetainedAcrossSnapshotUiRefresh()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Id = "snapshot-retained",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Retained selection",
                    Author = "Paul"
                }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);

            var window = new MainWindow(viewModel);
            try
            {
                var firstPublished = window.PublishMarkupReviewSnapshotForTesting("Snapshot A", "Coordinator");

                markup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                var secondPublished = window.PublishMarkupReviewSnapshotForTesting("Snapshot B", "Coordinator");
                var selected = window.SelectMarkupReviewSnapshotForTesting("Snapshot A");

                markup.AssignedTo = "Field Crew";
                viewModel.MarkupTool.RefreshReviewContext();

                var displayNames = window.GetMarkupReviewSnapshotDisplayNamesForTesting();
                var selectedSummary = window.GetSelectedMarkupReviewSnapshotSummaryForTesting();

                return (firstPublished, secondPublished, selected, displayNames, selectedSummary);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.firstPublished);
        Assert.True(outcome.secondPublished);
        Assert.True(outcome.selected);
        Assert.Equal(new[] { "Snapshot B", "Snapshot A" }, outcome.displayNames);
        Assert.Contains("Snapshot A", outcome.selectedSummary, StringComparison.Ordinal);
    }
}