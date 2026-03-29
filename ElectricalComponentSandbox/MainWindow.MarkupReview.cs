using System.Linq;
using System.Text;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

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
        SetSelectedMarkupStatus(MarkupStatus.Approved, "Approve");
    }

    private void RejectMarkup_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedMarkupStatus(MarkupStatus.Rejected, "Reject");
    }

    private void ResolveMarkup_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedMarkupStatus(MarkupStatus.Resolved, "Resolve");
    }

    private void SetSelectedMarkupStatus(MarkupStatus newStatus, string action)
    {
        var markup = _viewModel.MarkupTool.SelectedMarkup;
        if (markup == null)
        {
            MessageBox.Show("Select a markup first.", action,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _viewModel.UndoRedo.Execute(new MarkupStatusAction(markup, newStatus));
        markup.Metadata.ModifiedUtc = System.DateTime.UtcNow;

        ActionLogService.Instance.Log(LogCategory.Component,
            $"Markup {action.ToLowerInvariant()}d", $"Id: {markup.Id}");

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
    }

    private void MarkupSummary_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Markups.Any())
        {
            MessageBox.Show("No markups in the project.", "Markup Summary",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("MARKUP REVIEW SUMMARY");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine();

        var byStatus = _viewModel.Markups
            .GroupBy(m => m.Status)
            .OrderBy(g => g.Key);

        foreach (var group in byStatus)
        {
            sb.AppendLine($"  {group.Key,-12}  {group.Count(),4} markup(s)");
        }

        sb.AppendLine($"\n  {"TOTAL",-12}  {_viewModel.Markups.Count,4} markup(s)");
        sb.AppendLine();
        sb.AppendLine(new string('─', 50));

        var byType = _viewModel.Markups
            .GroupBy(m => m.Type)
            .OrderByDescending(g => g.Count());

        sb.AppendLine("\nBy Type:");
        foreach (var group in byType)
        {
            sb.AppendLine($"  {group.Key,-20}  {group.Count(),4}");
        }

        var authors = _viewModel.Markups
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

        // Highlight open issues
        var openCount = _viewModel.Markups.Count(m =>
            m.Status == MarkupStatus.Open || m.Status == MarkupStatus.InProgress);
        if (openCount > 0)
        {
            sb.AppendLine($"\n⚠ {openCount} markup(s) still open/in-progress");
        }

        MessageBox.Show(sb.ToString(), "Markup Summary",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
