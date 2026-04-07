using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests.ViewModels;

public partial class MainViewModelTests
{
    [Fact]
    public void MarkupTool_SelectedMarkup_ExposesReplyThreadDetails()
    {
        var vm = new MainViewModel();
        const string rootReplyId = "reply-root";
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Metadata = new MarkupMetadata
            {
                Label = "Panel note",
                Author = "Author"
            },
            Replies =
            {
                new MarkupReply
                {
                    Id = rootReplyId,
                    Author = "Reviewer A",
                    Text = "Need revised clearance dimensions.",
                    Kind = MarkupReplyKind.Manual,
                    CreatedUtc = new DateTime(2026, 3, 28, 18, 0, 0, DateTimeKind.Utc),
                    ModifiedUtc = new DateTime(2026, 3, 28, 18, 0, 0, DateTimeKind.Utc)
                },
                new MarkupReply
                {
                    Id = "reply-child",
                    ParentReplyId = rootReplyId,
                    Author = "Reviewer B",
                    Text = "Confirmed on sheet E2.",
                    Kind = MarkupReplyKind.StatusAudit,
                    CreatedUtc = new DateTime(2026, 3, 29, 18, 0, 0, DateTimeKind.Utc),
                    ModifiedUtc = new DateTime(2026, 3, 29, 18, 0, 0, DateTimeKind.Utc)
                }
            }
        };

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasSelectedMarkupReplies);
        Assert.Equal(2, vm.MarkupTool.SelectedMarkupReplyCount);
        Assert.Equal(2, vm.MarkupTool.SelectedMarkupReplies.Count);
        Assert.Equal(1, vm.MarkupTool.SelectedMarkupManualReplyCount);
        Assert.Equal(1, vm.MarkupTool.SelectedMarkupAuditReplyCount);
        Assert.Equal(1, vm.MarkupTool.SelectedMarkupStatusAuditReplyCount);
        Assert.Equal(0, vm.MarkupTool.SelectedMarkupAssignmentAuditReplyCount);
        Assert.Equal("Reviewer A", vm.MarkupTool.SelectedMarkupReplies[0].Author);
        Assert.True(vm.MarkupTool.SelectedMarkupReplies[0].IsThreadRoot);
        Assert.Equal(0, vm.MarkupTool.SelectedMarkupReplies[0].ThreadDepth);
        Assert.False(vm.MarkupTool.SelectedMarkupReplies[0].IsAuditEntry);
        Assert.Equal(MarkupReplyKind.Manual, vm.MarkupTool.SelectedMarkupReplies[0].Kind);
        Assert.Equal("Reply", vm.MarkupTool.SelectedMarkupReplies[0].EntryTypeDisplayText);
        Assert.Equal("Need revised clearance dimensions.", vm.MarkupTool.SelectedMarkupReplies[0].Text);
        Assert.True(vm.MarkupTool.SelectedMarkupReplies[1].IsAuditEntry);
        Assert.False(vm.MarkupTool.SelectedMarkupReplies[1].IsThreadRoot);
        Assert.Equal(rootReplyId, vm.MarkupTool.SelectedMarkupReplies[1].ParentReplyId);
        Assert.Equal(1, vm.MarkupTool.SelectedMarkupReplies[1].ThreadDepth);
        Assert.Equal(MarkupReplyKind.StatusAudit, vm.MarkupTool.SelectedMarkupReplies[1].Kind);
        Assert.Equal("Status", vm.MarkupTool.SelectedMarkupReplies[1].EntryTypeDisplayText);
        Assert.Contains("1 replies, 1 status updates", vm.MarkupTool.SelectedMarkupReplySummary);
        Assert.Contains("Latest status update by Reviewer B", vm.MarkupTool.SelectedMarkupReplySummary);
        Assert.Equal("Reply text participates in Markups search filtering.", vm.MarkupTool.SelectedMarkupReplySearchSummary);
    }

    [Fact]
    public void MarkupTool_LabelSearch_MatchesReplyText()
    {
        var vm = new MainViewModel();
        var matchingMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Metadata = new MarkupMetadata { Label = "Issue A" },
            Replies =
            {
                new MarkupReply
                {
                    Author = "Reviewer",
                    Text = "Fire alarm homerun rerouted to avoid beam pocket."
                }
            }
        };
        var otherMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Metadata = new MarkupMetadata { Label = "Issue B" }
        };

        vm.Markups.Add(matchingMarkup);
        vm.Markups.Add(otherMarkup);

        vm.MarkupTool.LabelSearch = "beam pocket";

        Assert.Single(vm.MarkupTool.FilteredMarkups);
        Assert.Same(matchingMarkup, vm.MarkupTool.FilteredMarkups[0]);
    }

    [Fact]
    public void TryApplySelectedMarkupStatus_AddsStatusAuditReply()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Metadata = new MarkupMetadata { Label = "Issue A" }
        };

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        var changed = vm.TryApplySelectedMarkupStatus(MarkupStatus.Resolved, "Reviewer");

        Assert.True(changed);
        Assert.Equal(MarkupStatus.Resolved, markup.Status);
        Assert.Equal("Status changed: Open -> Resolved", markup.StatusNote);
        Assert.Single(markup.Replies);
        Assert.Equal("Reviewer", markup.Replies[0].Author);
        Assert.True(markup.Replies[0].IsAuditEntry);
        Assert.Equal(MarkupReplyKind.StatusAudit, markup.Replies[0].Kind);
        Assert.Equal("Status changed: Open -> Resolved", markup.Replies[0].Text);
        Assert.Single(vm.MarkupTool.SelectedMarkupReplies);
        Assert.True(vm.MarkupTool.SelectedMarkupReplies[0].IsAuditEntry);
        Assert.Equal("Status", vm.MarkupTool.SelectedMarkupReplies[0].EntryTypeDisplayText);
    }

    [Fact]
    public void ApplyFilteredMarkupStatus_AddsAuditRepliesToVisibleMarkupsOnly()
    {
        var vm = new MainViewModel();
        var visibleMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Metadata = new MarkupMetadata { Label = "Visible" }
        };
        var filteredOutMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Metadata = new MarkupMetadata { Label = "Filtered Out" }
        };

        vm.Markups.Add(visibleMarkup);
        vm.Markups.Add(filteredOutMarkup);
        vm.MarkupTool.LabelSearch = "Visible";

        var changedCount = vm.ApplyFilteredMarkupStatus(MarkupStatus.Void, "Coordinator");

        Assert.Equal(1, changedCount);
        Assert.Equal(MarkupStatus.Void, visibleMarkup.Status);
        Assert.Single(visibleMarkup.Replies);
        Assert.True(visibleMarkup.Replies[0].IsAuditEntry);
        Assert.Equal(MarkupReplyKind.StatusAudit, visibleMarkup.Replies[0].Kind);
        Assert.Equal("Status changed: Open -> Void", visibleMarkup.Replies[0].Text);
        Assert.Equal(MarkupStatus.Open, filteredOutMarkup.Status);
        Assert.Empty(filteredOutMarkup.Replies);
    }

    [Fact]
    public void ApplySelectedIssueGroupStatus_UpdatesOnlySelectedBucket()
    {
        var vm = new MainViewModel();
        var bucketMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Metadata = new MarkupMetadata { Label = "Bucket A", Author = "Paul" }
        };
        var otherMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Metadata = new MarkupMetadata { Label = "Bucket B", Author = "Casey" }
        };

        vm.Markups.Add(bucketMarkup);
        vm.Markups.Add(otherMarkup);
        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
        vm.MarkupTool.SelectedIssueGroup = Assert.Single(vm.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

        var changedCount = vm.ApplySelectedIssueGroupStatus(MarkupStatus.Resolved, "Coordinator");

        Assert.Equal(1, changedCount);
        Assert.Equal(MarkupStatus.Resolved, bucketMarkup.Status);
        Assert.Single(bucketMarkup.Replies);
        Assert.Equal(MarkupReplyKind.StatusAudit, bucketMarkup.Replies[0].Kind);
        Assert.Equal(MarkupStatus.Open, otherMarkup.Status);
        Assert.Empty(otherMarkup.Replies);
    }

    [Fact]
    public void TryAssignSelectedMarkup_AddsAssignmentAuditReply()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Metadata = new MarkupMetadata { Label = "Issue A" }
        };

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        var changed = vm.TryAssignSelectedMarkup("Field Crew", "Coordinator");

        Assert.True(changed);
        Assert.Equal("Field Crew", markup.AssignedTo);
        Assert.Single(markup.Replies);
        Assert.True(markup.Replies[0].IsAuditEntry);
        Assert.Equal(MarkupReplyKind.AssignmentAudit, markup.Replies[0].Kind);
        Assert.Equal("Assignment changed: (unassigned) -> Field Crew", markup.Replies[0].Text);
        Assert.Equal("Field Crew", vm.MarkupTool.SelectedMarkupAssignedTo);
        Assert.True(vm.MarkupTool.SelectedMarkupReplies[0].IsAuditEntry);
        Assert.Equal("Assignment", vm.MarkupTool.SelectedMarkupReplies[0].EntryTypeDisplayText);
    }

    [Fact]
    public void ApplyFilteredMarkupAssignment_UpdatesOnlyVisibleMarkups()
    {
        var vm = new MainViewModel();
        var visibleMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Metadata = new MarkupMetadata { Label = "Visible" }
        };
        var filteredOutMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Metadata = new MarkupMetadata { Label = "Hidden" }
        };

        vm.Markups.Add(visibleMarkup);
        vm.Markups.Add(filteredOutMarkup);
        vm.MarkupTool.LabelSearch = "Visible";

        var changedCount = vm.ApplyFilteredMarkupAssignment("Reviewer B", "Coordinator");

        Assert.Equal(1, changedCount);
        Assert.Equal("Reviewer B", visibleMarkup.AssignedTo);
        Assert.Single(visibleMarkup.Replies);
        Assert.True(visibleMarkup.Replies[0].IsAuditEntry);
        Assert.Equal(MarkupReplyKind.AssignmentAudit, visibleMarkup.Replies[0].Kind);
        Assert.Equal("Assignment changed: (unassigned) -> Reviewer B", visibleMarkup.Replies[0].Text);
        Assert.Null(filteredOutMarkup.AssignedTo);
        Assert.Empty(filteredOutMarkup.Replies);
    }

    [Fact]
    public void ApplySelectedIssueGroupAssignment_UpdatesOnlySelectedBucket()
    {
        var vm = new MainViewModel();
        var bucketMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            AssignedTo = "Field Crew",
            Metadata = new MarkupMetadata { Label = "Bucket A" }
        };
        var otherMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            AssignedTo = "Coordinator",
            Metadata = new MarkupMetadata { Label = "Bucket B" }
        };

        vm.Markups.Add(bucketMarkup);
        vm.Markups.Add(otherMarkup);
        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Assignee;
        vm.MarkupTool.SelectedIssueGroup = Assert.Single(vm.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Field Crew"));

        var changedCount = vm.ApplySelectedIssueGroupAssignment("QA", "Coordinator");

        Assert.Equal(1, changedCount);
        Assert.Equal("QA", bucketMarkup.AssignedTo);
        Assert.Single(bucketMarkup.Replies);
        Assert.Equal(MarkupReplyKind.AssignmentAudit, bucketMarkup.Replies[0].Kind);
        Assert.Equal("Coordinator", otherMarkup.AssignedTo);
        Assert.Empty(otherMarkup.Replies);
    }

    [Fact]
    public void MarkupTool_LabelSearch_MatchesAssignedTo()
    {
        var vm = new MainViewModel();
        var matchingMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            AssignedTo = "Coordinator",
            Metadata = new MarkupMetadata { Label = "Issue A" }
        };
        var otherMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Metadata = new MarkupMetadata { Label = "Issue B" }
        };

        vm.Markups.Add(matchingMarkup);
        vm.Markups.Add(otherMarkup);

        vm.MarkupTool.LabelSearch = "coordinator";

        Assert.Single(vm.MarkupTool.FilteredMarkups);
        Assert.Same(matchingMarkup, vm.MarkupTool.FilteredMarkups[0]);
    }

    [Fact]
    public void MarkupTool_AssigneeFilter_RestrictsVisibleMarkupList()
    {
        var vm = new MainViewModel();
        var assignedMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            AssignedTo = "Field Crew",
            Metadata = new MarkupMetadata { Label = "Assigned" }
        };
        var unassignedMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Metadata = new MarkupMetadata { Label = "Unassigned" }
        };

        vm.Markups.Add(assignedMarkup);
        vm.Markups.Add(unassignedMarkup);
        vm.MarkupTool.RefreshReviewContext();

        Assert.Contains("Field Crew", vm.MarkupTool.AssigneeFilterOptions);
        Assert.Contains("(unassigned)", vm.MarkupTool.AssigneeFilterOptions);

        vm.MarkupTool.AssigneeFilter = "Field Crew";

        var filtered = Assert.Single(vm.MarkupTool.FilteredMarkups);
        Assert.Same(assignedMarkup, filtered);
    }

    [Fact]
    public void MarkupTool_AssigneeFilter_MatchesUnassignedMarkup()
    {
        var vm = new MainViewModel();
        var assignedMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            AssignedTo = "Coordinator",
            Metadata = new MarkupMetadata { Label = "Assigned" }
        };
        var unassignedMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Metadata = new MarkupMetadata { Label = "Unassigned" }
        };

        vm.Markups.Add(assignedMarkup);
        vm.Markups.Add(unassignedMarkup);

        vm.MarkupTool.AssigneeFilter = "(unassigned)";

        var filtered = Assert.Single(vm.MarkupTool.FilteredMarkups);
        Assert.Same(unassignedMarkup, filtered);
    }

    [Fact]
    public void MarkupTool_SelectedStructuredMarkup_ExposesAnnotationMetadata()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "PANEL-A"
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField] = DrawingAnnotationMarkupService.ScheduleTableAnnotationKind;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField] = DrawingAnnotationMarkupService.TextRoleCell;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField] = "NAME";

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasStructuredSelection);
        Assert.True(vm.MarkupTool.HasTextEditableSelection);
        Assert.True(vm.MarkupTool.HasTextShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupTextDetails);
        Assert.Equal(DrawingAnnotationMarkupService.ScheduleTableAnnotationKind, vm.MarkupTool.SelectedMarkupAnnotationKind);
        Assert.Equal(DrawingAnnotationMarkupService.TextRoleCell, vm.MarkupTool.SelectedMarkupAnnotationRole);
        Assert.Equal("NAME", vm.MarkupTool.SelectedMarkupAnnotationKey);
        Assert.Equal("Direct text edit available for structured schedule, legend, and title-block text", vm.MarkupTool.SelectedMarkupTextEditSummary);
        Assert.Equal("Shortcut: F2", vm.MarkupTool.SelectedMarkupTextShortcutHint);
        Assert.Equal("Current Value: PANEL-A", vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_SelectedPlainMarkup_HidesStructuredAnnotationMetadata()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle
        };

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.False(vm.MarkupTool.HasStructuredSelection);
        Assert.False(vm.MarkupTool.HasTextEditableSelection);
        Assert.False(vm.MarkupTool.HasTextShortcutHint);
        Assert.False(vm.MarkupTool.HasSelectedMarkupTextDetails);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAnnotationKind);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAnnotationRole);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAnnotationKey);
        Assert.Equal("Direct text editing is currently available for structured schedule, legend, and title-block text only", vm.MarkupTool.SelectedMarkupTextEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupTextShortcutHint);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_SelectedLiveScheduleMarkup_ReportsManagedTextAsReadOnly()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "PANEL-A"
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField] = DrawingAnnotationMarkupService.ScheduleTableAnnotationKind;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField] = DrawingAnnotationMarkupService.TextRoleCell;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField] = "NAME";
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.LiveScheduleInstanceIdField] = "schedule-1";

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasStructuredSelection);
        Assert.False(vm.MarkupTool.HasTextEditableSelection);
        Assert.False(vm.MarkupTool.HasTextShortcutHint);
        Assert.False(vm.MarkupTool.HasSelectedMarkupTextDetails);
        Assert.Equal("Live schedules regenerate from project data and are not edited directly", vm.MarkupTool.SelectedMarkupTextEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupTextShortcutHint);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_SelectedLiveTitleBlockBoundField_ReportsReadOnlyBinding()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "Tower Renovation"
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField] = DrawingAnnotationMarkupService.TitleBlockAnnotationKind;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField] = DrawingAnnotationMarkupService.TextRoleFieldValue;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField] = "PROJECT";
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.LiveTitleBlockInstanceIdField] = "title-1";

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasStructuredSelection);
        Assert.False(vm.MarkupTool.HasTextEditableSelection);
        Assert.Equal("This title block field stays bound to sheet/project data and is not edited directly", vm.MarkupTool.SelectedMarkupTextEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupTextShortcutHint);
    }

    [Fact]
    public void MarkupTool_SelectedLiveTitleBlockUnboundField_AllowsEditing()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "Engineer A"
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField] = DrawingAnnotationMarkupService.TitleBlockAnnotationKind;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField] = DrawingAnnotationMarkupService.TextRoleFieldValue;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField] = "DRAWN BY";
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.LiveTitleBlockInstanceIdField] = "title-1";

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasTextEditableSelection);
        Assert.Equal("Direct text edit updates the live title block instance for unbound fields", vm.MarkupTool.SelectedMarkupTextEditSummary);
        Assert.Equal("Shortcut: F2", vm.MarkupTool.SelectedMarkupTextShortcutHint);
        Assert.Equal("Current Value: Engineer A", vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_RefreshSelectedMarkupPresentation_UpdatesTextDetailsAfterEdit()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "PANEL-A"
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField] = DrawingAnnotationMarkupService.ScheduleTableAnnotationKind;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField] = DrawingAnnotationMarkupService.TextRoleCell;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField] = "NAME";

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        markup.TextContent = "PANEL-B";
        vm.MarkupTool.RefreshSelectedMarkupPresentation();

        Assert.Equal("Current Value: PANEL-B", vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_SelectedCircleMarkup_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 12
        };
        markup.Vertices.Add(new Point(0, 0));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.True(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric edit available: radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+G", vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal("Radius: 12", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedRectangleMarkup_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(5, 10, 24, 12)
        };
        markup.Vertices.Add(new Point(5, 10));
        markup.Vertices.Add(new Point(29, 22));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.True(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric edit available: width and height", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+G", vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal($"Width: 24{Environment.NewLine}Height: 12", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedStampMarkup_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Stamp,
            BoundingRect = new Rect(100, 200, 120, 30),
            TextContent = "APPROVED"
        };
        markup.Vertices.Add(new Point(160, 215));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.True(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric edit available: width and height", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+G", vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal($"Width: 120{Environment.NewLine}Height: 30", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_GroupedArcSelection_DisablesGeometryEditability()
    {
        var vm = new MainViewModel();
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10
        };
        selectedMarkup.Vertices.Add(new Point(0, 0));
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var groupedPeer = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "peer"
        };
        groupedPeer.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        vm.Markups.Add(selectedMarkup);
        vm.Markups.Add(groupedPeer);
        vm.MarkupTool.SelectedMarkup = selectedMarkup;

        Assert.False(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.False(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.False(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric geometry editing is disabled for grouped selections", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedArcMarkup_ExposesGeometryDetails()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 18.5,
            ArcStartDeg = 30,
            ArcSweepDeg = 120
        };
        markup.Vertices.Add(new Point(0, 0));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal(
            $"Radius: 18.5{Environment.NewLine}Start: 30 deg{Environment.NewLine}End: 150 deg{Environment.NewLine}Sweep: 120 deg",
            vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedTwoPointDimension_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0) },
            TextContent = "12"
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: length and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Length: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedThreePointDimension_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(6, 3) },
            TextContent = "12"
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: length and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Length: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedRadialDimension_ReportsRadialGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: radius and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Radius: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedRadialMeasurement_ReportsRadialGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: radius and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Radius: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedDiameterDimension_ReportsDiameterGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(-6, 0), new Point(6, 0), new Point(9, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: diameter and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Diameter: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedDiameterMeasurement_ReportsDiameterGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(-6, 0), new Point(6, 0), new Point(9, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: diameter and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Diameter: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedAngularDimension_ReportsAngularGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(0, 12), new Point(10, 10) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: angle and radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Angle: 90 deg{Environment.NewLine}Radius: 8", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedAngularMeasurement_ReportsAngularGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(0, 12), new Point(10, 10) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: angle and radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Angle: 90 deg{Environment.NewLine}Radius: 8", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedArcLengthDimension_ReportsArcLengthGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            TextContent = "12"
        };
        markup.Metadata.Subject = "ArcLength";
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: arc length and radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Arc Length: 15.71{Environment.NewLine}Radius: 10{Environment.NewLine}Start: 0 deg{Environment.NewLine}Sweep: 90 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedArcLengthMeasurement_ReportsArcLengthGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            TextContent = "12"
        };
        markup.Metadata.Subject = "ArcLength";
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: arc length and radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Arc Length: 15.71{Environment.NewLine}Radius: 10{Environment.NewLine}Start: 0 deg{Environment.NewLine}Sweep: 90 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_UnsupportedSelection_IncludesSpecializedMeasurementAvailabilityInSummary()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "NOTE",
            BoundingRect = new Rect(0, 0, 20, 10)
        };
        markup.Vertices.Add(new Point(0, 10));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.False(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Contains("angular dimension or measurement", vm.MarkupTool.SelectedMarkupGeometryEditSummary, StringComparison.Ordinal);
        Assert.Contains("arc-length dimension or measurement", vm.MarkupTool.SelectedMarkupGeometryEditSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkupTool_RefreshSelectedMarkupPresentation_UpdatesGeometryDetailsAfterEdit()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 12
        };
        markup.Vertices.Add(new Point(0, 0));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        markup.Radius = 24.25;
        vm.MarkupTool.RefreshSelectedMarkupPresentation();

        Assert.Equal("Radius: 24.25", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedTextMarkup_ReportsAppearanceEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "PANEL-A",
            BoundingRect = new Rect(10, 10, 40, 12),
            Appearance = new MarkupAppearance
            {
                StrokeColor = "#FF112233",
                StrokeWidth = 1.5,
                FillColor = "#40112233",
                Opacity = 0.75,
                FontFamily = "Consolas",
                FontSize = 14,
                DashArray = string.Empty
            }
        };
        markup.Vertices.Add(new Point(10, 22));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasAppearanceEditableSelection);
        Assert.True(vm.MarkupTool.HasAppearanceShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupAppearanceDetails);
        Assert.Equal("Appearance edit available: stroke color, width, opacity, fill, font family, font size", vm.MarkupTool.SelectedMarkupAppearanceEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+A", vm.MarkupTool.SelectedMarkupAppearanceShortcutHint);
        Assert.Equal(
            $"Stroke: #FF112233{Environment.NewLine}Width: 1.5{Environment.NewLine}Opacity: 0.75{Environment.NewLine}Fill: #40112233{Environment.NewLine}Font: Consolas{Environment.NewLine}Font Size: 14",
            vm.MarkupTool.SelectedMarkupAppearanceDetails);
    }

    [Fact]
    public void MarkupTool_GroupedAppearanceSelection_DisablesAppearanceEditability()
    {
        var vm = new MainViewModel();
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Vertices = { new Point(0, 0), new Point(10, 10) }
        };
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";
        selectedMarkup.UpdateBoundingRect();

        var groupedPeer = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "peer"
        };
        groupedPeer.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        vm.Markups.Add(selectedMarkup);
        vm.Markups.Add(groupedPeer);
        vm.MarkupTool.SelectedMarkup = selectedMarkup;

        Assert.False(vm.MarkupTool.HasAppearanceEditableSelection);
        Assert.False(vm.MarkupTool.HasAppearanceShortcutHint);
        Assert.False(vm.MarkupTool.HasSelectedMarkupAppearanceDetails);
        Assert.Equal("Appearance editing is disabled for grouped selections", vm.MarkupTool.SelectedMarkupAppearanceEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAppearanceShortcutHint);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAppearanceDetails);
    }

    [Fact]
    public void MarkupTool_RefreshSelectedMarkupPresentation_UpdatesAppearanceDetailsAfterEdit()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 5) },
            Appearance = new MarkupAppearance
            {
                StrokeColor = "#FF0000",
                StrokeWidth = 2,
                FillColor = "#00000000",
                Opacity = 1,
                FontFamily = "Arial",
                FontSize = 10,
                DashArray = string.Empty
            }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        markup.Appearance.StrokeColor = "#FF00AA00";
        markup.Appearance.StrokeWidth = 3.5;
        markup.Appearance.Opacity = 0.6;
        markup.Appearance.DashArray = "6,3";
        vm.MarkupTool.RefreshSelectedMarkupPresentation();

        Assert.Equal(
            $"Stroke: #FF00AA00{Environment.NewLine}Width: 3.5{Environment.NewLine}Opacity: 0.6{Environment.NewLine}Dash: 6,3",
            vm.MarkupTool.SelectedMarkupAppearanceDetails);
    }

    [Fact]
    public void MarkupTool_SelectedPolylineMarkup_ReportsPathEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 5) }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasPathEditableSelection);
        Assert.True(vm.MarkupTool.HasPathShortcutHint);
        Assert.True(vm.MarkupTool.HasPathVertexInsertCandidate);
        Assert.True(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.True(vm.MarkupTool.HasSelectedMarkupPathDetails);
        Assert.Equal(
            "Direct path edit available: drag grips to reposition points, or double-click a segment to insert a vertex",
            vm.MarkupTool.SelectedMarkupPathEditSummary);
        Assert.Equal("Click Insert Vertex, then click a segment on the canvas", vm.MarkupTool.SelectedMarkupPathInsertSummary);
        Assert.Equal("Shortcut: Delete or Backspace removes the active vertex after selecting a grip", vm.MarkupTool.SelectedMarkupPathShortcutHint);
        Assert.Equal("Select a vertex grip, then delete it from the keyboard or command surface", vm.MarkupTool.SelectedMarkupPathDeleteSummary);
        Assert.Equal(
            $"Vertices: 3{Environment.NewLine}Minimum: 2{Environment.NewLine}Insert: Double-click a segment or use Insert Vertex{Environment.NewLine}Delete: Active vertex can be removed",
            vm.MarkupTool.SelectedMarkupPathDetails);
    }

    [Fact]
    public void MarkupTool_SelectedDimensionMarkup_ReportsPathEditabilityWithoutInsertHint()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(6, 3) },
            TextContent = "12'-0\""
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasPathEditableSelection);
        Assert.False(vm.MarkupTool.HasPathVertexInsertCandidate);
        Assert.True(vm.MarkupTool.HasPathShortcutHint);
        Assert.True(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.Equal("Direct path edit available: drag grips to reposition points", vm.MarkupTool.SelectedMarkupPathEditSummary);
        Assert.Equal("Vertex insertion is currently available for polyline, polygon, callout, leader note, and revision cloud markups only", vm.MarkupTool.SelectedMarkupPathInsertSummary);
        Assert.Equal(
            $"Vertices: 3{Environment.NewLine}Minimum: 2{Environment.NewLine}Delete: Active vertex can be removed",
            vm.MarkupTool.SelectedMarkupPathDetails);
    }

    [Fact]
    public void MarkupTool_GroupedPathSelection_DisablesPathEditability()
    {
        var vm = new MainViewModel();
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 5) }
        };
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var groupedPeer = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "peer"
        };
        groupedPeer.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        vm.Markups.Add(selectedMarkup);
        vm.Markups.Add(groupedPeer);
        vm.MarkupTool.SelectedMarkup = selectedMarkup;

        Assert.False(vm.MarkupTool.HasPathEditableSelection);
        Assert.False(vm.MarkupTool.HasPathShortcutHint);
        Assert.False(vm.MarkupTool.HasPathVertexInsertCandidate);
        Assert.False(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.False(vm.MarkupTool.HasSelectedMarkupPathDetails);
        Assert.Equal("Path editing is disabled for grouped selections", vm.MarkupTool.SelectedMarkupPathEditSummary);
        Assert.Equal("Vertex insertion is disabled for grouped selections", vm.MarkupTool.SelectedMarkupPathInsertSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupPathShortcutHint);
        Assert.Equal("Vertex deletion is disabled for grouped selections", vm.MarkupTool.SelectedMarkupPathDeleteSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupPathDetails);
    }

    [Fact]
    public void MarkupTool_SelectedMinimumVertexPath_DisablesVertexDeletionCandidate()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0) }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasPathEditableSelection);
        Assert.False(vm.MarkupTool.HasPathShortcutHint);
        Assert.False(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.Equal("Vertex deletion is unavailable when the selected path is already at its minimum vertex count", vm.MarkupTool.SelectedMarkupPathDeleteSummary);
    }

    [Fact]
    public void MarkupTool_AllSheetsScope_AggregatesMarkupsAcrossSheets()
    {
        var vm = new MainViewModel();
        var firstSheet = vm.SelectedSheet;
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Vertices = { new Point(0, 0), new Point(5, 5) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);

        var secondSheet = vm.AddSheet("Review");
        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.Resolved,
            TextContent = "Reviewed",
            Vertices = { new Point(10, 10) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;

        Assert.NotNull(firstSheet);
        Assert.NotNull(secondSheet);
        Assert.Equal(2, vm.MarkupTool.TotalCount);
        Assert.Equal(1, vm.MarkupTool.OpenCount);
        Assert.Equal(1, vm.MarkupTool.ResolvedCount);
        Assert.Equal(2, vm.MarkupTool.FilteredMarkups.Count);
        Assert.Contains(vm.MarkupTool.FilteredMarkups, markup => markup.ReviewSheetDisplayText == firstSheet.DisplayName);
        Assert.Contains(vm.MarkupTool.FilteredMarkups, markup => markup.ReviewSheetDisplayText == secondSheet.DisplayName);
    }

    [Fact]
    public void RevealMarkup_SelectsOwningSheetAndMarkup()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Vertices = { new Point(0, 0), new Point(4, 4) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);
        var firstSheet = vm.SelectedSheet;

        vm.AddSheet("Review");
        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 4,
            Vertices = { new Point(20, 20) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;

        var revealed = vm.RevealMarkup(firstMarkup);

        Assert.True(revealed);
        Assert.Equal(firstSheet?.Id, vm.SelectedSheet?.Id);
        Assert.Same(firstMarkup, vm.MarkupTool.SelectedMarkup);
    }

    [Fact]
    public void MarkupTool_ResolveVisibleCommand_CurrentSheetScope_OnlyUpdatesActiveSheet()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Vertices = { new Point(0, 0), new Point(5, 5) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);

        vm.AddSheet("Review");
        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.Open,
            TextContent = "Needs review",
            Vertices = { new Point(10, 10) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.ResolveVisibleCommand.Execute(null);

        Assert.Equal(MarkupStatus.Open, firstMarkup.Status);
        Assert.Equal(MarkupStatus.Resolved, secondMarkup.Status);
    }

    [Fact]
    public void GetFilteredReviewMarkups_AllSheetsScope_UsesCurrentFilters()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Vertices = { new Point(0, 0), new Point(6, 6) }
        };
        firstMarkup.Metadata.Label = "Open item";
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);
        var firstSheet = vm.SelectedSheet;

        var secondSheet = vm.AddSheet("Review");
        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.Resolved,
            TextContent = "Done",
            Vertices = { new Point(12, 12) }
        };
        secondMarkup.Metadata.Label = "Resolved item";
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
        vm.MarkupTool.StatusFilter = MarkupRecord.GetStatusDisplayText(MarkupStatus.Resolved);

        var filtered = vm.GetFilteredReviewMarkups();

        var markup = Assert.Single(filtered);
        Assert.Same(secondMarkup, markup);
        Assert.Equal(secondSheet?.DisplayName, markup.ReviewSheetDisplayText);
        Assert.DoesNotContain(filtered, item => item.ReviewSheetDisplayText == firstSheet?.DisplayName);
    }

    [Fact]
    public void MarkupTool_IssueGroups_GroupVisibleReviewSetBySheet()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Vertices = { new Point(0, 0), new Point(6, 6) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);
        var firstSheet = vm.SelectedSheet;

        var secondSheet = vm.AddSheet("Review");
        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.InProgress,
            TextContent = "Pending",
            Vertices = { new Point(12, 12) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;
        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Sheet;

        Assert.NotNull(firstSheet);
        Assert.NotNull(secondSheet);
        Assert.Equal(2, vm.MarkupTool.IssueGroups.Count);
        Assert.Contains(vm.MarkupTool.IssueGroups, group => group.DisplayName == firstSheet.DisplayName && group.Count == 1 && group.OpenCount == 1);
        Assert.Contains(vm.MarkupTool.IssueGroups, group => group.DisplayName == secondSheet.DisplayName && group.Count == 1 && group.OpenCount == 1);
    }

    [Fact]
    public void MarkupTool_IssueGroups_ExposeStatusAndOwnerMixForSheetBuckets()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            AssignedTo = "Field Crew",
            Vertices = { new Point(0, 0), new Point(6, 6) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);

        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.Open,
            AssignedTo = "Field Crew",
            TextContent = "Pending",
            Vertices = { new Point(12, 12) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        var thirdMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Status = MarkupStatus.Resolved,
            Vertices = { new Point(18, 18) }
        };
        thirdMarkup.UpdateBoundingRect();
        vm.AddMarkup(thirdMarkup);

        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Sheet;

        var group = Assert.Single(vm.MarkupTool.IssueGroups.Where(item => item.Count == 2));
        Assert.Equal("Flow: Open 2 | Owners: Field Crew 2", group.BreakdownText);
    }

    [Fact]
    public void MarkupTool_SelectedIssueGroup_FiltersVisibleMarkupList()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Vertices = { new Point(0, 0), new Point(6, 6) }
        };
        firstMarkup.Metadata.Author = "Paul";
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);

        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.Resolved,
            TextContent = "Done",
            Vertices = { new Point(12, 12) }
        };
        secondMarkup.Metadata.Author = "Casey";
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Author;
        var authorGroup = Assert.Single(vm.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Paul"));

        vm.MarkupTool.SelectedIssueGroup = authorGroup;

        var filtered = vm.GetFilteredReviewMarkups();
        var markup = Assert.Single(filtered);
        Assert.Same(firstMarkup, markup);
        Assert.Equal("Paul | 1 issue(s)", vm.MarkupTool.SelectedIssueGroupSummary);
    }

    [Fact]
    public void MarkupTool_IssueGroups_GroupVisibleReviewSetByAssignee()
    {
        var vm = new MainViewModel();
        var assignedMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            AssignedTo = "Field Crew",
            Vertices = { new Point(0, 0), new Point(6, 6) }
        };
        assignedMarkup.UpdateBoundingRect();
        vm.AddMarkup(assignedMarkup);

        var unassignedMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.Resolved,
            TextContent = "Done",
            Vertices = { new Point(12, 12) }
        };
        unassignedMarkup.UpdateBoundingRect();
        vm.AddMarkup(unassignedMarkup);

        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Assignee;

        Assert.Equal(2, vm.MarkupTool.IssueGroups.Count);
        Assert.Contains(vm.MarkupTool.IssueGroups, group => group.DisplayName == "Field Crew" && group.Count == 1 && group.OpenCount == 1);
        Assert.Contains(vm.MarkupTool.IssueGroups, group => group.DisplayName == "(unassigned)" && group.Count == 1 && group.OpenCount == 0);
    }

    [Fact]
    public void MarkupTool_SelectedAssigneeIssueGroup_FiltersVisibleMarkupList()
    {
        var vm = new MainViewModel();
        var assignedMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            AssignedTo = "Field Crew",
            Status = MarkupStatus.InProgress,
            Vertices = { new Point(0, 0), new Point(6, 6) }
        };
        assignedMarkup.UpdateBoundingRect();
        vm.AddMarkup(assignedMarkup);

        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            AssignedTo = "Coordinator",
            Status = MarkupStatus.Open,
            TextContent = "Pending",
            Vertices = { new Point(12, 12) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Assignee;
        var assigneeGroup = Assert.Single(vm.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Field Crew"));

        vm.MarkupTool.SelectedIssueGroup = assigneeGroup;

        var filtered = vm.GetFilteredReviewMarkups();
        var markup = Assert.Single(filtered);
        Assert.Same(assignedMarkup, markup);
        Assert.Equal("Field Crew | 1 issue(s)", vm.MarkupTool.SelectedIssueGroupSummary);
    }

    [Fact]
    public void MarkupTool_SelectedIssueGroupBreakdown_UsesBucketMixForAssigneeBuckets()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            AssignedTo = "Field Crew",
            Status = MarkupStatus.Open,
            Vertices = { new Point(0, 0), new Point(6, 6) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);

        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            AssignedTo = "Field Crew",
            Status = MarkupStatus.Open,
            TextContent = "Pending",
            Vertices = { new Point(12, 12) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        var thirdMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            AssignedTo = "Field Crew",
            Status = MarkupStatus.Resolved,
            Vertices = { new Point(18, 18) }
        };
        thirdMarkup.UpdateBoundingRect();
        vm.AddMarkup(thirdMarkup);

        vm.MarkupTool.IssueGroupMode = MarkupIssueGroupMode.Assignee;
        var assigneeGroup = Assert.Single(vm.MarkupTool.IssueGroups.Where(group => group.DisplayName == "Field Crew"));

        vm.MarkupTool.SelectedIssueGroup = assigneeGroup;

        Assert.Equal("Flow: Open 2, Resolved 1", vm.MarkupTool.SelectedIssueGroupBreakdown);
    }
}
