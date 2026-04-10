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

            var missingMarkup = new MarkupRecord
            {
                Id = "snapshot-missing",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.InProgress,
                AssignedTo = "Crew D",
                Vertices = { new Point(36, 0), new Point(46, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Missing later",
                    Author = "Morgan"
                }
            };
            missingMarkup.UpdateBoundingRect();

            viewModel.Markups.Add(statusChangedMarkup);
            viewModel.Markups.Add(ownershipChangedMarkup);
            viewModel.Markups.Add(unchangedMarkup);
            viewModel.Markups.Add(missingMarkup);

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("Coordination Set", "Coordinator");

                statusChangedMarkup.Status = MarkupStatus.Resolved;
                ownershipChangedMarkup.AssignedTo = "Crew B";
                viewModel.Markups.Remove(missingMarkup);

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
                var details = window.GetSelectedMarkupReviewSnapshotDetailsForTesting();
                var diffTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var addedLaterSheetContext = window.GetMarkupReviewSnapshotDiffSheetContextForTesting("Added later");
                var selectedNewIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Added later");
                var selectedMarkupIdAfterNewIssue = viewModel.MarkupTool.SelectedMarkup?.Id;
                var selectedMissingIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Missing later");
                var selectedMarkupIdAfterMissingIssue = viewModel.MarkupTool.SelectedMarkup?.Id;
                var selectedSheetDisplayName = viewModel.SelectedSheet?.DisplayName;

                return (published, displayNames, selected, summary, selectedSummary, comparison, details, diffTitles, addedLaterSheetContext, selectedNewIssue, selectedMarkupIdAfterNewIssue, selectedMissingIssue, selectedMarkupIdAfterMissingIssue, selectedSheetDisplayName);
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
        Assert.Contains("1 missing", outcome.comparison, StringComparison.Ordinal);
        Assert.Contains("Changed issues:", outcome.details, StringComparison.Ordinal);
        Assert.Contains("Status change [status Open -> Resolved]", outcome.details, StringComparison.Ordinal);
        Assert.Contains("Ownership change [owner Crew A -> Crew B]", outcome.details, StringComparison.Ordinal);
        Assert.Contains("New issues:", outcome.details, StringComparison.Ordinal);
        Assert.Contains("Added later", outcome.details, StringComparison.Ordinal);
        Assert.Contains("Missing issues:", outcome.details, StringComparison.Ordinal);
        Assert.Contains("Missing later", outcome.details, StringComparison.Ordinal);
        Assert.Equal(4, outcome.diffTitles.Count);
        Assert.Contains("Status change", outcome.diffTitles, StringComparer.Ordinal);
        Assert.Contains("Ownership change", outcome.diffTitles, StringComparer.Ordinal);
        Assert.Contains("Added later", outcome.diffTitles, StringComparer.Ordinal);
        Assert.Contains("Missing later", outcome.diffTitles, StringComparer.Ordinal);
        Assert.Equal($"Sheet: {outcome.selectedSheetDisplayName}", outcome.addedLaterSheetContext);
        Assert.True(outcome.selectedNewIssue);
        Assert.Equal("snapshot-new", outcome.selectedMarkupIdAfterNewIssue);
        Assert.True(outcome.selectedMissingIssue);
        Assert.Null(outcome.selectedMarkupIdAfterMissingIssue);
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

    [Fact]
    public void DeleteSelectedMarkupReviewSnapshotForTesting_RemovesSnapshotAndRetainsRemainingSelection()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Id = "snapshot-delete",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Delete candidate",
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
                var selected = window.SelectMarkupReviewSnapshotForTesting("Snapshot B");
                var deleted = window.DeleteSelectedMarkupReviewSnapshotForTesting();
                var displayNames = window.GetMarkupReviewSnapshotDisplayNamesForTesting();
                var selectedSummary = window.GetSelectedMarkupReviewSnapshotSummaryForTesting();

                return (firstPublished, secondPublished, selected, deleted, displayNames, selectedSummary);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.firstPublished);
        Assert.True(outcome.secondPublished);
        Assert.True(outcome.selected);
        Assert.True(outcome.deleted);
        Assert.Single(outcome.displayNames);
        Assert.Equal("Snapshot A", outcome.displayNames[0]);
        Assert.Contains("Snapshot A", outcome.selectedSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void RenameSelectedMarkupReviewSnapshotForTesting_UpdatesDisplayNameAndRetainsSelection()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Id = "snapshot-rename",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Rename candidate",
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
                var renamed = window.RenameSelectedMarkupReviewSnapshotForTesting("Coordination Baseline");
                var displayNames = window.GetMarkupReviewSnapshotDisplayNamesForTesting();
                var selectedSummary = window.GetSelectedMarkupReviewSnapshotSummaryForTesting();

                return (firstPublished, secondPublished, selected, renamed, displayNames, selectedSummary);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.firstPublished);
        Assert.True(outcome.secondPublished);
        Assert.True(outcome.selected);
        Assert.True(outcome.renamed);
        Assert.Equal(new[] { "Snapshot B", "Coordination Baseline" }, outcome.displayNames);
        Assert.Contains("Coordination Baseline", outcome.selectedSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectMarkupReviewSnapshotDiffEntryForTesting_MissingIssue_SwitchesToRecordedSheetContext()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var liveMarkup = new MarkupRecord
            {
                Id = "snapshot-live",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Live issue",
                    Author = "Paul"
                }
            };
            liveMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(liveMarkup);

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var missingMarkup = new MarkupRecord
            {
                Id = "snapshot-missing-sheet",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(12, 0), new Point(22, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Missing on lighting",
                    Author = "Casey"
                }
            };
            missingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(missingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                viewModel.Markups.Remove(missingMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                viewModel.MarkupTool.SelectedMarkup = viewModel.Markups.FirstOrDefault(markup => string.Equals(markup.Id, liveMarkup.Id, StringComparison.Ordinal));

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var selectedMissingIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Missing on lighting");
                var selectedSheetDisplayName = viewModel.SelectedSheet?.DisplayName;
                var selectedMarkupId = viewModel.MarkupTool.SelectedMarkup?.Id;

                return (published, selectedSnapshot, selectedMissingIssue, selectedSheetDisplayName, selectedMarkupId, secondSheet.DisplayName);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.True(outcome.selectedMissingIssue);
        Assert.Equal(outcome.DisplayName, outcome.selectedSheetDisplayName);
        Assert.Null(outcome.selectedMarkupId);
    }

    [Fact]
    public void SelectMarkupReviewSnapshotDiffEntryForTesting_MissingIssue_UsesRecordedSheetIdAfterSheetRename()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var liveMarkup = new MarkupRecord
            {
                Id = "snapshot-live-rename",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Live issue",
                    Author = "Paul"
                }
            };
            liveMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(liveMarkup);

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var missingMarkup = new MarkupRecord
            {
                Id = "snapshot-missing-renamed-sheet",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(12, 0), new Point(22, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Missing on renamed lighting",
                    Author = "Casey"
                }
            };
            missingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(missingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                secondSheet.Name = "Renamed Lighting Plan";
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.Markups.Remove(missingMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                viewModel.MarkupTool.SelectedMarkup = viewModel.Markups.FirstOrDefault(markup => string.Equals(markup.Id, liveMarkup.Id, StringComparison.Ordinal));

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var sheetContext = window.GetMarkupReviewSnapshotDiffSheetContextForTesting("Missing on renamed lighting");
                var selectedMissingIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Missing on renamed lighting");
                var selectedSheetDisplayName = viewModel.SelectedSheet?.DisplayName;
                var selectedMarkupId = viewModel.MarkupTool.SelectedMarkup?.Id;

                return (published, selectedSnapshot, sheetContext, selectedMissingIssue, selectedSheetDisplayName, selectedMarkupId, secondSheet.DisplayName);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal($"Sheet: {outcome.DisplayName}", outcome.sheetContext);
        Assert.True(outcome.selectedMissingIssue);
        Assert.Equal(outcome.DisplayName, outcome.selectedSheetDisplayName);
        Assert.Null(outcome.selectedMarkupId);
    }

    [Fact]
    public void SelectMarkupReviewSnapshotDiffEntryForTesting_MissingIssue_WithDeletedSheet_ShowsUnavailableHintAndClearsSelection()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var liveMarkup = new MarkupRecord
            {
                Id = "snapshot-live-deleted-sheet",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Live issue",
                    Author = "Paul"
                }
            };
            liveMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(liveMarkup);

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var missingMarkup = new MarkupRecord
            {
                Id = "snapshot-missing-deleted-sheet",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(12, 0), new Point(22, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Missing on deleted lighting",
                    Author = "Casey"
                }
            };
            missingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(missingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                viewModel.SelectSheet(firstSheet);
                viewModel.MarkupTool.SelectedMarkup = viewModel.Markups.FirstOrDefault(markup => string.Equals(markup.Id, liveMarkup.Id, StringComparison.Ordinal));
                var deletedSheetDisplayName = secondSheet.DisplayName;
                var deleted = viewModel.DeleteSheet(secondSheet);
                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var diffHeaders = window.GetMarkupReviewSnapshotDiffHeaderTextsForTesting();
                var headerBadges = window.GetMarkupReviewSnapshotDiffHeaderBadgeTextsForTesting();
                var headerPriorities = window.GetMarkupReviewSnapshotDiffHeaderPriorityKeysForTesting();
                var revealHint = window.GetMarkupReviewSnapshotDiffRevealHintForTesting("Missing on deleted lighting");
                var sheetContext = window.GetMarkupReviewSnapshotDiffSheetContextForTesting("Missing on deleted lighting");
                var selectedMissingIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Missing on deleted lighting");
                var selectedSheetDisplayName = viewModel.SelectedSheet?.DisplayName;
                var selectedMarkupId = viewModel.MarkupTool.SelectedMarkup?.Id;

                return (published, deleted, selectedSnapshot, diffHeaders, headerBadges, headerPriorities, revealHint, sheetContext, selectedMissingIssue, selectedSheetDisplayName, selectedMarkupId, firstSheet.DisplayName, deletedSheetDisplayName);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.deleted);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal(new[] { $"Snapshot-only sheet: {outcome.deletedSheetDisplayName} (1 issue: 1 missing)" }, outcome.diffHeaders);
        Assert.Equal(new[] { "1 issue | 1 missing" }, outcome.headerBadges);
        Assert.Equal(new[] { "missing" }, outcome.headerPriorities);
        Assert.Equal("Recorded sheet no longer exists in the current project.", outcome.revealHint);
        Assert.Equal($"Snapshot sheet: {outcome.deletedSheetDisplayName}", outcome.sheetContext);
        Assert.True(outcome.selectedMissingIssue);
        Assert.Equal(outcome.DisplayName, outcome.selectedSheetDisplayName);
        Assert.Null(outcome.selectedMarkupId);
    }

    [Fact]
    public void GetMarkupReviewSnapshotDiffTitlesForTesting_AllSheetsSnapshot_AreOrderedBySheetContext()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var lightingMarkup = new MarkupRecord
            {
                Id = "snapshot-sort-lighting",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha lighting change",
                    Author = "Casey"
                }
            };
            lightingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(lightingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                lightingMarkup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                var powerNewMarkup = new MarkupRecord
                {
                    Id = "snapshot-sort-power-new",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "New power issue",
                    Vertices = { new Point(12, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Zulu power new",
                        Author = "Paul"
                    }
                };
                powerNewMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(powerNewMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var diffTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();

                return (published, selectedSnapshot, diffTitles);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal(new[] { "Zulu power new", "Alpha lighting change" }, outcome.diffTitles);
    }

    [Fact]
    public void GetMarkupReviewSnapshotDiffHeaderTextsForTesting_AllSheetsSnapshot_EmitsVisibleSheetGroupHeaders()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var lightingMarkup = new MarkupRecord
            {
                Id = "snapshot-header-lighting",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha lighting change",
                    Author = "Casey"
                }
            };
            lightingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(lightingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                lightingMarkup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                var powerNewMarkup = new MarkupRecord
                {
                    Id = "snapshot-header-power-new",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "New power issue",
                    Vertices = { new Point(12, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Zulu power new",
                        Author = "Paul"
                    }
                };
                powerNewMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(powerNewMarkup);

                var secondPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-header-power-new-2",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "Another power issue",
                    Vertices = { new Point(18, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Beta power new",
                        Author = "Jordan"
                    }
                };
                secondPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(secondPowerMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var diffHeaders = window.GetMarkupReviewSnapshotDiffHeaderTextsForTesting();
                var headerBadges = window.GetMarkupReviewSnapshotDiffHeaderBadgeTextsForTesting();
                var headerPriorities = window.GetMarkupReviewSnapshotDiffHeaderPriorityKeysForTesting();
                var displayedSheetContext = window.GetMarkupReviewSnapshotDiffDisplayedSheetContextForTesting("Zulu power new");
                var underlyingSheetContext = window.GetMarkupReviewSnapshotDiffSheetContextForTesting("Zulu power new");

                return (
                    published,
                    selectedSnapshot,
                    diffHeaders,
                    headerBadges,
                    headerPriorities,
                    displayedSheetContext,
                    underlyingSheetContext,
                    firstSheetDisplayName: firstSheet.DisplayName,
                    secondSheetDisplayName: secondSheet.DisplayName);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal(
            new[]
            {
                $"Sheet: {outcome.firstSheetDisplayName} (2 issues: 2 new)",
                $"Sheet: {outcome.secondSheetDisplayName} (1 issue: 1 changed)"
            },
            outcome.diffHeaders);
        Assert.Equal(new[] { "2 issues | 2 new", "1 issue | 1 changed" }, outcome.headerBadges);
        Assert.Equal(new[] { "new", "changed" }, outcome.headerPriorities);
        Assert.Equal(string.Empty, outcome.displayedSheetContext);
        Assert.Equal($"Sheet: {outcome.firstSheetDisplayName}", outcome.underlyingSheetContext);
    }

    [Fact]
    public void ToggleMarkupReviewSnapshotDiffHeaderForTesting_HidesAndRestoresSheetGroupIssues()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var lightingMarkup = new MarkupRecord
            {
                Id = "snapshot-collapse-lighting",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha lighting change",
                    Author = "Casey"
                }
            };
            lightingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(lightingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                lightingMarkup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                var zuluPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-collapse-power-new-zulu",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "New power issue",
                    Vertices = { new Point(12, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Zulu power new",
                        Author = "Paul"
                    }
                };
                zuluPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(zuluPowerMarkup);

                var betaPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-collapse-power-new-beta",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "Another power issue",
                    Vertices = { new Point(18, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Beta power new",
                        Author = "Jordan"
                    }
                };
                betaPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(betaPowerMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var initialTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var initialToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var initialToggleToolTips = window.GetMarkupReviewSnapshotDiffHeaderToggleToolTipsForTesting();
                var collapsed = window.ToggleMarkupReviewSnapshotDiffHeaderForTesting($"Sheet: {firstSheet.DisplayName}");
                var collapsedTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var collapsedToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var collapsedToggleToolTips = window.GetMarkupReviewSnapshotDiffHeaderToggleToolTipsForTesting();
                var selectHiddenIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Zulu power new");
                var expanded = window.ToggleMarkupReviewSnapshotDiffHeaderForTesting($"Sheet: {firstSheet.DisplayName}");
                var expandedTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var expandedToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var expandedToggleToolTips = window.GetMarkupReviewSnapshotDiffHeaderToggleToolTipsForTesting();
                var selectRestoredIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Zulu power new");

                return (
                    published,
                    selectedSnapshot,
                    initialTitles,
                    initialToggles,
                    initialToggleToolTips,
                    collapsed,
                    collapsedTitles,
                    collapsedToggles,
                    collapsedToggleToolTips,
                    selectHiddenIssue,
                    expanded,
                    expandedTitles,
                    expandedToggles,
                    expandedToggleToolTips,
                    selectRestoredIssue);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal(new[] { "Beta power new", "Zulu power new", "Alpha lighting change" }, outcome.initialTitles);
        Assert.Equal(new[] { "[-]", "[-]" }, outcome.initialToggles);
        Assert.Equal(new[] { "Collapse this sheet and hide 2 issues.", "Collapse this sheet and hide 1 issue." }, outcome.initialToggleToolTips);
        Assert.True(outcome.collapsed);
        Assert.Equal(new[] { "Alpha lighting change" }, outcome.collapsedTitles);
        Assert.Equal(new[] { "[+]", "[-]" }, outcome.collapsedToggles);
        Assert.Equal(new[] { "Expand this sheet and show 2 issues.", "Collapse this sheet and hide 1 issue." }, outcome.collapsedToggleToolTips);
        Assert.False(outcome.selectHiddenIssue);
        Assert.True(outcome.expanded);
        Assert.Equal(new[] { "Beta power new", "Zulu power new", "Alpha lighting change" }, outcome.expandedTitles);
        Assert.Equal(new[] { "[-]", "[-]" }, outcome.expandedToggles);
        Assert.Equal(new[] { "Collapse this sheet and hide 2 issues.", "Collapse this sheet and hide 1 issue." }, outcome.expandedToggleToolTips);
        Assert.True(outcome.selectRestoredIssue);
    }

    [Fact]
    public void CollapseAllMarkupReviewSnapshotDiffGroupsForTesting_HidesAllGroupsUntilExpanded()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var lightingMarkup = new MarkupRecord
            {
                Id = "snapshot-collapse-all-lighting",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha lighting change",
                    Author = "Casey"
                }
            };
            lightingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(lightingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                lightingMarkup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                var powerMarkup = new MarkupRecord
                {
                    Id = "snapshot-collapse-all-power",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "New power issue",
                    Vertices = { new Point(12, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Zulu power new",
                        Author = "Paul"
                    }
                };
                powerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(powerMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var initialTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var initialToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var initialExpandAllText = window.GetExpandAllMarkupReviewSnapshotDiffGroupsTextForTesting();
                var initialExpandAllToolTip = window.GetExpandAllMarkupReviewSnapshotDiffGroupsToolTipForTesting();
                var initialCollapseAllText = window.GetCollapseAllMarkupReviewSnapshotDiffGroupsTextForTesting();
                var initialCollapseAllToolTip = window.GetCollapseAllMarkupReviewSnapshotDiffGroupsToolTipForTesting();
                var collapsed = window.CollapseAllMarkupReviewSnapshotDiffGroupsForTesting();
                var collapsedTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var collapsedToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var collapsedExpandAllText = window.GetExpandAllMarkupReviewSnapshotDiffGroupsTextForTesting();
                var collapsedExpandAllToolTip = window.GetExpandAllMarkupReviewSnapshotDiffGroupsToolTipForTesting();
                var collapsedCollapseAllText = window.GetCollapseAllMarkupReviewSnapshotDiffGroupsTextForTesting();
                var collapsedCollapseAllToolTip = window.GetCollapseAllMarkupReviewSnapshotDiffGroupsToolTipForTesting();
                var collapsedCollapseOthersTexts = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersTextsForTesting();
                var collapsedCollapseOthersToolTips = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersToolTipsForTesting();
                var selectHiddenIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Zulu power new");
                var expanded = window.ExpandAllMarkupReviewSnapshotDiffGroupsForTesting();
                var expandedTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var expandedToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var expandedExpandAllText = window.GetExpandAllMarkupReviewSnapshotDiffGroupsTextForTesting();
                var expandedExpandAllToolTip = window.GetExpandAllMarkupReviewSnapshotDiffGroupsToolTipForTesting();
                var expandedCollapseAllText = window.GetCollapseAllMarkupReviewSnapshotDiffGroupsTextForTesting();
                var expandedCollapseAllToolTip = window.GetCollapseAllMarkupReviewSnapshotDiffGroupsToolTipForTesting();
                var selectRestoredIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Zulu power new");

                return (
                    published,
                    selectedSnapshot,
                    initialTitles,
                    initialToggles,
                    initialExpandAllText,
                    initialExpandAllToolTip,
                    initialCollapseAllText,
                    initialCollapseAllToolTip,
                    collapsed,
                    collapsedTitles,
                    collapsedToggles,
                    collapsedExpandAllText,
                    collapsedExpandAllToolTip,
                    collapsedCollapseAllText,
                    collapsedCollapseAllToolTip,
                    collapsedCollapseOthersTexts,
                    collapsedCollapseOthersToolTips,
                    selectHiddenIssue,
                    expanded,
                    expandedTitles,
                    expandedToggles,
                    expandedExpandAllText,
                    expandedExpandAllToolTip,
                    expandedCollapseAllText,
                    expandedCollapseAllToolTip,
                    selectRestoredIssue);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal(new[] { "Zulu power new", "Alpha lighting change" }, outcome.initialTitles);
        Assert.Equal(new[] { "[-]", "[-]" }, outcome.initialToggles);
        Assert.Equal("Expand All (0 hidden)", outcome.initialExpandAllText);
        Assert.Equal("All sheet groups are already expanded.", outcome.initialExpandAllToolTip);
        Assert.Equal("Collapse All (2 visible)", outcome.initialCollapseAllText);
        Assert.Equal("Collapse 2 visible sheet groups.", outcome.initialCollapseAllToolTip);
        Assert.True(outcome.collapsed);
        Assert.Empty(outcome.collapsedTitles);
        Assert.Equal(new[] { "[+]", "[+]" }, outcome.collapsedToggles);
        Assert.Equal("Expand All (2 hidden)", outcome.collapsedExpandAllText);
        Assert.Equal("Expand 2 hidden sheet groups.", outcome.collapsedExpandAllToolTip);
        Assert.Equal("Collapse All (0 visible)", outcome.collapsedCollapseAllText);
        Assert.Equal("All sheet groups are already collapsed.", outcome.collapsedCollapseAllToolTip);
        Assert.Equal(new[] { "Show Only This Sheet", "Show Only This Sheet" }, outcome.collapsedCollapseOthersTexts);
        Assert.Equal(new[] { "Show only this sheet's issues.", "Show only this sheet's issues." }, outcome.collapsedCollapseOthersToolTips);
        Assert.False(outcome.selectHiddenIssue);
        Assert.True(outcome.expanded);
        Assert.Equal(new[] { "Zulu power new", "Alpha lighting change" }, outcome.expandedTitles);
        Assert.Equal(new[] { "[-]", "[-]" }, outcome.expandedToggles);
        Assert.Equal("Expand All (0 hidden)", outcome.expandedExpandAllText);
        Assert.Equal("All sheet groups are already expanded.", outcome.expandedExpandAllToolTip);
        Assert.Equal("Collapse All (2 visible)", outcome.expandedCollapseAllText);
        Assert.Equal("Collapse 2 visible sheet groups.", outcome.expandedCollapseAllToolTip);
        Assert.True(outcome.selectRestoredIssue);
    }

    [Fact]
    public void MarkupReviewSnapshotDiffGroupCollapseState_IsRetainedPerSnapshotSelection()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var lightingMarkup = new MarkupRecord
            {
                Id = "snapshot-persist-lighting",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha lighting change",
                    Author = "Casey"
                }
            };
            lightingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(lightingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var publishedA = window.PublishMarkupReviewSnapshotForTesting("Snapshot A", "Coordinator");

                lightingMarkup.Status = MarkupStatus.Resolved;
                viewModel.SelectSheet(firstSheet);

                var betaPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-persist-power-beta",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "Power issue",
                    Vertices = { new Point(12, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Beta power new",
                        Author = "Paul"
                    }
                };
                betaPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(betaPowerMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var publishedB = window.PublishMarkupReviewSnapshotForTesting("Snapshot B", "Coordinator");

                var zuluPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-persist-power-zulu",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "Late power issue",
                    Vertices = { new Point(18, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Zulu power new",
                        Author = "Jordan"
                    }
                };
                zuluPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(zuluPowerMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedA = window.SelectMarkupReviewSnapshotForTesting("Snapshot A");
                var initialATitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var initialAToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var collapsedA = window.ToggleMarkupReviewSnapshotDiffHeaderForTesting($"Sheet: {firstSheet.DisplayName}");
                var collapsedATitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var collapsedAToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();

                var selectedB = window.SelectMarkupReviewSnapshotForTesting("Snapshot B");
                var initialBTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var initialBToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var collapsedB = window.ToggleMarkupReviewSnapshotDiffHeaderForTesting($"Sheet: {firstSheet.DisplayName}");
                var collapsedBTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var collapsedBToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();

                var reselectedA = window.SelectMarkupReviewSnapshotForTesting("Snapshot A");
                var restoredATitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var restoredAToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var hiddenASelection = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Beta power new");

                var reselectedB = window.SelectMarkupReviewSnapshotForTesting("Snapshot B");
                var restoredBTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var restoredBToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var hiddenBSelection = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Zulu power new");

                return (
                    publishedA,
                    publishedB,
                    selectedA,
                    initialATitles,
                    initialAToggles,
                    collapsedA,
                    collapsedATitles,
                    collapsedAToggles,
                    selectedB,
                    initialBTitles,
                    initialBToggles,
                    collapsedB,
                    collapsedBTitles,
                    collapsedBToggles,
                    reselectedA,
                    restoredATitles,
                    restoredAToggles,
                    hiddenASelection,
                    reselectedB,
                    restoredBTitles,
                    restoredBToggles,
                    hiddenBSelection);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.publishedA);
        Assert.True(outcome.publishedB);
        Assert.True(outcome.selectedA);
        Assert.Equal(new[] { "Beta power new", "Zulu power new", "Alpha lighting change" }, outcome.initialATitles);
        Assert.Equal(new[] { "[-]", "[-]" }, outcome.initialAToggles);
        Assert.True(outcome.collapsedA);
        Assert.Equal(new[] { "Alpha lighting change" }, outcome.collapsedATitles);
        Assert.Equal(new[] { "[+]", "[-]" }, outcome.collapsedAToggles);
        Assert.True(outcome.selectedB);
        Assert.Equal(new[] { "Zulu power new" }, outcome.initialBTitles);
        Assert.Equal(new[] { "[-]" }, outcome.initialBToggles);
        Assert.True(outcome.collapsedB);
        Assert.Empty(outcome.collapsedBTitles);
        Assert.Equal(new[] { "[+]" }, outcome.collapsedBToggles);
        Assert.True(outcome.reselectedA);
        Assert.Equal(new[] { "Alpha lighting change" }, outcome.restoredATitles);
        Assert.Equal(new[] { "[+]", "[-]" }, outcome.restoredAToggles);
        Assert.False(outcome.hiddenASelection);
        Assert.True(outcome.reselectedB);
        Assert.Empty(outcome.restoredBTitles);
        Assert.Equal(new[] { "[+]" }, outcome.restoredBToggles);
        Assert.False(outcome.hiddenBSelection);
    }

    [Fact]
    public void CollapseOtherMarkupReviewSnapshotDiffGroupsForTesting_IsolatesSelectedSheetGroup()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var lightingMarkup = new MarkupRecord
            {
                Id = "snapshot-collapse-others-lighting",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha lighting change",
                    Author = "Casey"
                }
            };
            lightingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(lightingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                lightingMarkup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                var zuluPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-collapse-others-power-zulu",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "New power issue",
                    Vertices = { new Point(12, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Zulu power new",
                        Author = "Paul"
                    }
                };
                zuluPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(zuluPowerMarkup);

                var betaPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-collapse-others-power-beta",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "Another power issue",
                    Vertices = { new Point(18, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Beta power new",
                        Author = "Jordan"
                    }
                };
                betaPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(betaPowerMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var initialTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var initialToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var initialCollapseOthersTexts = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersTextsForTesting();
                var initialCollapseOthersEnabled = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersEnabledStatesForTesting();
                var initialCollapseOthersVisible = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersVisibleStatesForTesting();
                var initialCollapseOthersPresentation = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersPresentationKeysForTesting();
                var initialCollapseOthersToolTips = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersToolTipsForTesting();
                var collapsedOthers = window.CollapseOtherMarkupReviewSnapshotDiffGroupsForTesting($"Sheet: {secondSheet.DisplayName}");
                var isolatedTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var isolatedToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var isolatedCollapseOthersTexts = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersTextsForTesting();
                var isolatedCollapseOthersEnabled = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersEnabledStatesForTesting();
                var isolatedCollapseOthersVisible = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersVisibleStatesForTesting();
                var isolatedCollapseOthersPresentation = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersPresentationKeysForTesting();
                var isolatedCollapseOthersToolTips = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersToolTipsForTesting();
                var isolatedAgain = window.CollapseOtherMarkupReviewSnapshotDiffGroupsForTesting($"Sheet: {secondSheet.DisplayName}");
                var hiddenPowerSelection = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Zulu power new");
                var visibleLightingSelection = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Alpha lighting change");
                var expanded = window.ExpandAllMarkupReviewSnapshotDiffGroupsForTesting();
                var expandedTitles = window.GetMarkupReviewSnapshotDiffTitlesForTesting();
                var expandedToggles = window.GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting();
                var expandedCollapseOthersTexts = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersTextsForTesting();
                var expandedCollapseOthersEnabled = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersEnabledStatesForTesting();
                var expandedCollapseOthersVisible = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersVisibleStatesForTesting();
                var expandedCollapseOthersPresentation = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersPresentationKeysForTesting();
                var expandedCollapseOthersToolTips = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersToolTipsForTesting();

                return (
                    published,
                    selectedSnapshot,
                    initialTitles,
                    initialToggles,
                    initialCollapseOthersTexts,
                    initialCollapseOthersEnabled,
                    initialCollapseOthersVisible,
                    initialCollapseOthersPresentation,
                    initialCollapseOthersToolTips,
                    collapsedOthers,
                    isolatedTitles,
                    isolatedToggles,
                    isolatedCollapseOthersTexts,
                    isolatedCollapseOthersEnabled,
                    isolatedCollapseOthersVisible,
                    isolatedCollapseOthersPresentation,
                    isolatedCollapseOthersToolTips,
                    isolatedAgain,
                    hiddenPowerSelection,
                    visibleLightingSelection,
                    expanded,
                    expandedTitles,
                    expandedToggles,
                    expandedCollapseOthersTexts,
                    expandedCollapseOthersEnabled,
                    expandedCollapseOthersVisible,
                    expandedCollapseOthersPresentation,
                    expandedCollapseOthersToolTips);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal(new[] { "Beta power new", "Zulu power new", "Alpha lighting change" }, outcome.initialTitles);
        Assert.Equal(new[] { "[-]", "[-]" }, outcome.initialToggles);
        Assert.Equal(new[] { "Collapse Others", "Collapse Others" }, outcome.initialCollapseOthersTexts);
        Assert.Equal(new[] { true, true }, outcome.initialCollapseOthersEnabled);
        Assert.Equal(new[] { true, true }, outcome.initialCollapseOthersVisible);
        Assert.Equal(new[] { "action", "action" }, outcome.initialCollapseOthersPresentation);
        Assert.Equal(new[] { "Collapse 1 other visible sheet group.", "Collapse 1 other visible sheet group." }, outcome.initialCollapseOthersToolTips);
        Assert.True(outcome.collapsedOthers);
        Assert.Equal(new[] { "Alpha lighting change" }, outcome.isolatedTitles);
        Assert.Equal(new[] { "[+]", "[-]" }, outcome.isolatedToggles);
        Assert.Equal(new[] { "Show Only This Sheet", "Isolated" }, outcome.isolatedCollapseOthersTexts);
        Assert.Equal(new[] { true, false }, outcome.isolatedCollapseOthersEnabled);
        Assert.Equal(new[] { true, true }, outcome.isolatedCollapseOthersVisible);
        Assert.Equal(new[] { "action", "status" }, outcome.isolatedCollapseOthersPresentation);
        Assert.Equal(new[] { "Show this sheet and collapse 1 other visible sheet group.", "This sheet is already the only visible group." }, outcome.isolatedCollapseOthersToolTips);
        Assert.False(outcome.isolatedAgain);
        Assert.False(outcome.hiddenPowerSelection);
        Assert.True(outcome.visibleLightingSelection);
        Assert.True(outcome.expanded);
        Assert.Equal(new[] { "Beta power new", "Zulu power new", "Alpha lighting change" }, outcome.expandedTitles);
        Assert.Equal(new[] { "[-]", "[-]" }, outcome.expandedToggles);
        Assert.Equal(new[] { "Collapse Others", "Collapse Others" }, outcome.expandedCollapseOthersTexts);
        Assert.Equal(new[] { true, true }, outcome.expandedCollapseOthersEnabled);
        Assert.Equal(new[] { true, true }, outcome.expandedCollapseOthersVisible);
        Assert.Equal(new[] { "action", "action" }, outcome.expandedCollapseOthersPresentation);
        Assert.Equal(new[] { "Collapse 1 other visible sheet group.", "Collapse 1 other visible sheet group." }, outcome.expandedCollapseOthersToolTips);
    }

    [Fact]
    public void GetMarkupReviewSnapshotDiffHeaderCollapseOthersVisibleStatesForTesting_SingleGroupSnapshot_HidesIsolateButton()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var powerMarkup = new MarkupRecord
            {
                Id = "snapshot-single-group-power",
                Type = MarkupType.Text,
                Status = MarkupStatus.Open,
                TextContent = "Power issue",
                Vertices = { new Point(12, 6) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha power issue",
                    Author = "Paul"
                }
            };
            powerMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(powerMarkup);
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("Current Sheet Snapshot", "Coordinator");

                powerMarkup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("Current Sheet Snapshot");
                var headerTexts = window.GetMarkupReviewSnapshotDiffHeaderTextsForTesting();
                var collapseOthersTexts = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersTextsForTesting();
                var collapseOthersEnabled = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersEnabledStatesForTesting();
                var collapseOthersVisible = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersVisibleStatesForTesting();
                var collapseOthersPresentation = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersPresentationKeysForTesting();
                var collapseOthersToolTips = window.GetMarkupReviewSnapshotDiffHeaderCollapseOthersToolTipsForTesting();

                return (
                    published,
                    selectedSnapshot,
                    headerTexts,
                    collapseOthersTexts,
                    collapseOthersEnabled,
                    collapseOthersVisible,
                    collapseOthersPresentation,
                    collapseOthersToolTips);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.Equal(new[] { "Sheet: E101 - Power Plan (1 issue: 1 changed)" }, outcome.headerTexts);
        Assert.Equal(new[] { string.Empty }, outcome.collapseOthersTexts);
        Assert.Equal(new[] { false }, outcome.collapseOthersEnabled);
        Assert.Equal(new[] { false }, outcome.collapseOthersVisible);
        Assert.Equal(new[] { "hidden" }, outcome.collapseOthersPresentation);
        Assert.Equal(new[] { string.Empty }, outcome.collapseOthersToolTips);
    }

    [Fact]
    public void MarkupReviewSnapshotDiffSelection_IsRetainedWhenVisibleAndClearedWhenCollapsed()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var firstSheet = viewModel.SelectedSheet ?? throw new InvalidOperationException("Expected a default sheet.");
            firstSheet.Number = "E101";
            firstSheet.Name = "Power Plan";

            var secondSheet = viewModel.AddSheet("Lighting Plan");
            secondSheet.Number = "E201";

            var lightingMarkup = new MarkupRecord
            {
                Id = "snapshot-selection-lighting",
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Alpha lighting change",
                    Author = "Casey"
                }
            };
            lightingMarkup.UpdateBoundingRect();
            viewModel.Markups.Add(lightingMarkup);

            viewModel.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
            viewModel.MarkupTool.RefreshReviewContext();

            var window = new MainWindow(viewModel);
            try
            {
                var published = window.PublishMarkupReviewSnapshotForTesting("All Sheets Snapshot", "Coordinator");

                lightingMarkup.Status = MarkupStatus.Resolved;
                viewModel.MarkupTool.RefreshReviewContext();

                viewModel.SelectSheet(firstSheet);
                var betaPowerMarkup = new MarkupRecord
                {
                    Id = "snapshot-selection-power-beta",
                    Type = MarkupType.Text,
                    Status = MarkupStatus.Open,
                    TextContent = "Power issue",
                    Vertices = { new Point(12, 6) },
                    Metadata = new MarkupMetadata
                    {
                        Label = "Beta power new",
                        Author = "Paul"
                    }
                };
                betaPowerMarkup.UpdateBoundingRect();
                viewModel.Markups.Add(betaPowerMarkup);
                viewModel.MarkupTool.RefreshReviewContext();

                var selectedSnapshot = window.SelectMarkupReviewSnapshotForTesting("All Sheets Snapshot");
                var selectedLightingIssue = window.SelectMarkupReviewSnapshotDiffEntryForTesting("Alpha lighting change");
                var initialSelectedTitle = window.GetSelectedMarkupReviewSnapshotDiffTitleForTesting();
                var collapsedPower = window.ToggleMarkupReviewSnapshotDiffHeaderForTesting($"Sheet: {firstSheet.DisplayName}");
                var retainedSelectedTitle = window.GetSelectedMarkupReviewSnapshotDiffTitleForTesting();
                var collapsedLighting = window.ToggleMarkupReviewSnapshotDiffHeaderForTesting($"Sheet: {secondSheet.DisplayName}");
                var clearedSelectedTitle = window.GetSelectedMarkupReviewSnapshotDiffTitleForTesting();

                return (
                    published,
                    selectedSnapshot,
                    selectedLightingIssue,
                    initialSelectedTitle,
                    collapsedPower,
                    retainedSelectedTitle,
                    collapsedLighting,
                    clearedSelectedTitle);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.published);
        Assert.True(outcome.selectedSnapshot);
        Assert.True(outcome.selectedLightingIssue);
        Assert.Equal("Alpha lighting change", outcome.initialSelectedTitle);
        Assert.True(outcome.collapsedPower);
        Assert.Equal("Alpha lighting change", outcome.retainedSelectedTitle);
        Assert.True(outcome.collapsedLighting);
        Assert.Equal(string.Empty, outcome.clearedSelectedTitle);
    }
}