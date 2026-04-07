using System;
using System.Linq;
using System.Text;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    // ── Markup Review Workflow ───────────────────────────────────────────────

    internal bool ExecuteEditMarkupGeometryCommandForTesting(string input)
        => TryEditSelectedMarkupGeometry(input, showFeedbackIfUnsupported: false);

    internal bool ExecuteEditMarkupAppearanceCommandForTesting(string input)
        => TryEditSelectedMarkupAppearance(input, showFeedbackIfUnsupported: false);

    internal bool ExecuteInsertVertexCommandForTesting()
    {
        var wasPending = _isPendingMarkupVertexInsertion;
        InsertSelectedMarkupVertex_Click(this, new RoutedEventArgs());
        return !wasPending && _isPendingMarkupVertexInsertion;
    }

    internal bool ExecuteDeleteVertexCommandForTesting()
    {
        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var previousVertexCount = selectedMarkup?.Vertices.Count ?? -1;
        DeleteSelectedMarkupVertex_Click(this, new RoutedEventArgs());
        return selectedMarkup != null && selectedMarkup.Vertices.Count < previousVertexCount;
    }

    internal bool ExecuteAddMarkupReplyCommandForTesting(string replyText, string? author = null)
        => TryAddReplyToSelectedMarkup(replyText, author ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal bool ExecuteSetSelectedMarkupStatusForTesting(MarkupStatus newStatus, string? author = null)
        => TrySetSelectedMarkupStatus(newStatus, author ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal bool ExecuteAssignSelectedMarkupForTesting(string? assignee, string? actor = null)
        => TryAssignSelectedMarkup(assignee, actor ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal bool ExecuteSetSelectedIssueGroupStatusForTesting(MarkupStatus newStatus, string? author = null)
        => TrySetSelectedIssueGroupStatus(newStatus, $"{MarkupRecord.GetStatusDisplayText(newStatus)} Bucket", author ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal bool ExecuteAssignSelectedIssueGroupForTesting(string? assignee, string? actor = null)
        => TryAssignSelectedIssueGroup(assignee, actor ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal bool ExecuteApproveSelectedIssueGroupForTesting(string? author = null)
        => TrySetSelectedIssueGroupStatus(MarkupStatus.Approved, "Approve Bucket", author ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal bool ExecuteRejectSelectedIssueGroupForTesting(string? author = null)
        => TrySetSelectedIssueGroupStatus(MarkupStatus.Rejected, "Reject Bucket", author ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    private void EditSelectedMarkupGeometry_Click(object sender, RoutedEventArgs e)
    {
        TryEditSelectedMarkupGeometry(showFeedbackIfUnsupported: true);
    }

    private void EditSelectedMarkupAppearance_Click(object sender, RoutedEventArgs e)
    {
        TryEditSelectedMarkupAppearance(showFeedbackIfUnsupported: true);
    }

    private void EditSelectedStructuredMarkup_Click(object sender, RoutedEventArgs e)
    {
        TryEditSelectedStructuredMarkupText(showFeedbackIfUnsupported: true);
    }

    private void InsertSelectedMarkupVertex_Click(object sender, RoutedEventArgs e)
    {
        TryBeginSelectedMarkupVertexInsertion(showFeedbackIfUnsupported: true);
    }

    private void DeleteSelectedMarkupVertex_Click(object sender, RoutedEventArgs e)
    {
        TryDeleteSelectedMarkupVertex(showFeedbackIfUnsupported: true);
    }

    private void MarkupListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TryEditSelectedStructuredMarkupText(showFeedbackIfUnsupported: false);
    }

    private void ApproveMarkup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedMarkupStatus(MarkupStatus.Approved, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void RejectMarkup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedMarkupStatus(MarkupStatus.Rejected, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void ResolveMarkup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedMarkupStatus(MarkupStatus.Resolved, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void VoidMarkup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedMarkupStatus(MarkupStatus.Void, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void AssignSelectedMarkup_Click(object sender, RoutedEventArgs e)
    {
        var defaultValue = _viewModel.MarkupTool.SelectedMarkup?.AssignedTo ?? Environment.UserName;
        var input = PromptInput("Assign Markup", "Enter assignee name. Leave blank to clear assignment:", defaultValue);
        if (input == null)
            return;

        TryAssignSelectedMarkup(input, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void AssignVisibleMarkups_Click(object sender, RoutedEventArgs e)
    {
        var input = PromptInput("Assign Visible Markups", "Enter assignee name for all visible issues. Leave blank to clear assignment:", Environment.UserName);
        if (input == null)
            return;

        TryAssignVisibleMarkups(input, Environment.UserName);
    }

    private void ResolveVisibleMarkups_Click(object sender, RoutedEventArgs e)
    {
        TrySetVisibleMarkupStatus(MarkupStatus.Resolved, "Resolve Visible", Environment.UserName);
    }

    private void VoidVisibleMarkups_Click(object sender, RoutedEventArgs e)
    {
        TrySetVisibleMarkupStatus(MarkupStatus.Void, "Void Visible", Environment.UserName);
    }

    private void AssignSelectedIssueGroup_Click(object sender, RoutedEventArgs e)
    {
        var input = PromptInput("Assign Selected Bucket", "Enter assignee name for the selected issue bucket. Leave blank to clear assignment:", Environment.UserName);
        if (input == null)
            return;

        TryAssignSelectedIssueGroup(input, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void ResolveSelectedIssueGroup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedIssueGroupStatus(MarkupStatus.Resolved, "Resolve Bucket", Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void ApproveSelectedIssueGroup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedIssueGroupStatus(MarkupStatus.Approved, "Approve Bucket", Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void RejectSelectedIssueGroup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedIssueGroupStatus(MarkupStatus.Rejected, "Reject Bucket", Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void VoidSelectedIssueGroup_Click(object sender, RoutedEventArgs e)
    {
        TrySetSelectedIssueGroupStatus(MarkupStatus.Void, "Void Bucket", Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private void AddMarkupReply_Click(object sender, RoutedEventArgs e)
    {
        var defaultReply = _viewModel.MarkupTool.SelectedMarkup?.Replies.LastOrDefault()?.Text ?? string.Empty;
        var input = PromptInput("Add Markup Reply", "Enter reply text:", defaultReply);
        if (string.IsNullOrWhiteSpace(input))
            return;

        TryAddReplyToSelectedMarkup(input, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private bool TryAddReplyToSelectedMarkup(string replyText, string author, bool showFeedbackIfUnavailable)
    {
        var markup = _viewModel.MarkupTool.SelectedMarkup;
        if (markup == null)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select a markup first.", "Add Reply",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(replyText))
            return false;

        var utcNow = DateTime.UtcNow;
        var reply = new MarkupReply
        {
            ParentReplyId = MarkupThreadingService.GetLatestReplyId(markup.Replies),
            Author = string.IsNullOrWhiteSpace(author) ? Environment.UserName : author,
            Text = replyText.Trim(),
            CreatedUtc = utcNow,
            ModifiedUtc = utcNow
        };

        _viewModel.UndoRedo.Execute(new MarkupReplyAction(markup, reply));

        ActionLogService.Instance.Log(LogCategory.Component,
            "Markup replied", $"Id: {markup.Id}");

        _viewModel.MarkupTool.RefreshReviewContext();
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        return true;
    }

    private bool TrySetSelectedMarkupStatus(MarkupStatus newStatus, string actor, bool showFeedbackIfUnavailable)
    {
        var markup = _viewModel.MarkupTool.SelectedMarkup;
        if (markup == null)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select a markup first.", MarkupRecord.GetStatusDisplayText(newStatus),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (!_viewModel.TryApplySelectedMarkupStatus(newStatus, actor))
            return false;

        ActionLogService.Instance.Log(LogCategory.Component,
            $"Markup {MarkupRecord.GetStatusDisplayText(newStatus).ToLowerInvariant()}", $"Id: {markup.Id}");

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        return true;
    }

    private bool TrySetVisibleMarkupStatus(MarkupStatus newStatus, string action, string actor)
    {
        var updatedCount = _viewModel.ApplyFilteredMarkupStatus(newStatus, actor);
        if (updatedCount == 0)
        {
            MessageBox.Show("No visible markups were eligible for that status change.", action,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        ActionLogService.Instance.Log(LogCategory.Component,
            $"Markup {action.ToLowerInvariant()}", $"Count: {updatedCount}");

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        return true;
    }

    private bool TrySetSelectedIssueGroupStatus(MarkupStatus newStatus, string action, string actor, bool showFeedbackIfUnavailable)
    {
        if (!_viewModel.MarkupTool.HasSelectedIssueGroup)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select an issue bucket first.", action,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var updatedCount = _viewModel.ApplySelectedIssueGroupStatus(newStatus, actor);
        if (updatedCount == 0)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("No markups in the selected bucket were eligible for that status change.", action,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        ActionLogService.Instance.Log(LogCategory.Component,
            $"Markup {action.ToLowerInvariant()}", $"Count: {updatedCount}");

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        return true;
    }

    private bool TryAssignSelectedMarkup(string? assignee, string actor, bool showFeedbackIfUnavailable)
    {
        var markup = _viewModel.MarkupTool.SelectedMarkup;
        if (markup == null)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select a markup first.", "Assign Markup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (!_viewModel.TryAssignSelectedMarkup(assignee, actor))
            return false;

        ActionLogService.Instance.Log(LogCategory.Component,
            "Markup assigned", $"Id: {markup.Id}, Assignee: {markup.AssignedToDisplayText}");

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        return true;
    }

    private bool TryAssignVisibleMarkups(string? assignee, string actor)
    {
        var updatedCount = _viewModel.ApplyFilteredMarkupAssignment(assignee, actor);
        if (updatedCount == 0)
        {
            MessageBox.Show("No visible markups were eligible for that assignment change.", "Assign Visible Markups",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var assigneeDisplay = string.IsNullOrWhiteSpace(assignee) ? "(unassigned)" : assignee.Trim();
        ActionLogService.Instance.Log(LogCategory.Component,
            "Visible markups assigned", $"Count: {updatedCount}, Assignee: {assigneeDisplay}");

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        return true;
    }

    private bool TryAssignSelectedIssueGroup(string? assignee, string actor, bool showFeedbackIfUnavailable)
    {
        if (!_viewModel.MarkupTool.HasSelectedIssueGroup)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select an issue bucket first.", "Assign Selected Bucket",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var updatedCount = _viewModel.ApplySelectedIssueGroupAssignment(assignee, actor);
        if (updatedCount == 0)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("No markups in the selected bucket were eligible for that assignment change.", "Assign Selected Bucket",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var assigneeDisplay = string.IsNullOrWhiteSpace(assignee) ? "(unassigned)" : assignee.Trim();
        ActionLogService.Instance.Log(LogCategory.Component,
            "Selected bucket assigned", $"Count: {updatedCount}, Assignee: {assigneeDisplay}");

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        return true;
    }

    private void MarkupSummary_Click(object sender, RoutedEventArgs e)
    {
        var reviewMarkups = _viewModel.GetReviewMarkups();
        if (reviewMarkups.Count == 0)
        {
            MessageBox.Show("No markups in the project.", "Markup Summary",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("MARKUP REVIEW SUMMARY");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine();

        if (_viewModel.MarkupTool.ReviewScope == MarkupReviewScope.AllSheets)
            sb.AppendLine($"Scope: All Sheets ({_viewModel.Sheets.Count})");
        else
            sb.AppendLine($"Scope: Current Sheet ({_viewModel.SelectedSheet?.DisplayName ?? "(none)"})");

        sb.AppendLine();

        var byStatus = reviewMarkups
            .GroupBy(m => m.Status)
            .OrderBy(g => g.Key);

        foreach (var group in byStatus)
        {
            sb.AppendLine($"  {group.Key,-12}  {group.Count(),4} markup(s)");
        }

        sb.AppendLine($"\n  {"TOTAL",-12}  {reviewMarkups.Count,4} markup(s)");
        sb.AppendLine();
        sb.AppendLine(new string('─', 50));

        var byType = reviewMarkups
            .GroupBy(m => m.Type)
            .OrderByDescending(g => g.Count());

        sb.AppendLine("\nBy Type:");
        foreach (var group in byType)
        {
            sb.AppendLine($"  {group.Key,-20}  {group.Count(),4}");
        }

        var authors = reviewMarkups
            .Where(m => !string.IsNullOrEmpty(m.Metadata.Author))
            .GroupBy(m => m.Metadata.Author)
            .OrderByDescending(g => g.Count());

        if (authors.Any())
        {
            sb.AppendLine("\nBy Author:");
            foreach (var group in authors)
            {
                sb.AppendLine($"  {group.Key,-20}  {group.Count(),4}");
            }
        }

        if (_viewModel.MarkupTool.ReviewScope == MarkupReviewScope.AllSheets)
        {
            var bySheet = reviewMarkups
                .GroupBy(_viewModel.GetMarkupSheetDisplayName)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            sb.AppendLine("\nBy Sheet:");
            foreach (var group in bySheet)
            {
                sb.AppendLine($"  {group.Key,-20}  {group.Count(),4}");
            }
        }

        // Highlight open issues
        var openCount = reviewMarkups.Count(m =>
            m.Status == MarkupStatus.Open || m.Status == MarkupStatus.InProgress);
        if (openCount > 0)
        {
            sb.AppendLine($"\n⚠ {openCount} markup(s) still open/in-progress");
        }

        MessageBox.Show(sb.ToString(), "Markup Summary",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
