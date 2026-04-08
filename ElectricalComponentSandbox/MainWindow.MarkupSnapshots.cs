using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    internal bool PublishMarkupReviewSnapshotForTesting(string name, string? actor = null)
        => TryPublishMarkupReviewSnapshot(name, actor ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal IReadOnlyList<string> GetMarkupReviewSnapshotDisplayNamesForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotListBox?.Items
            .OfType<MarkupReviewSnapshot>()
            .Select(snapshot => snapshot.DisplayName)
            .ToList() ?? new List<string>();
    }

    internal bool SelectMarkupReviewSnapshotForTesting(string displayName)
    {
        UpdateMarkupReviewSnapshotUi();
        if (MarkupReviewSnapshotListBox == null)
            return false;

        var snapshot = MarkupReviewSnapshotListBox.Items
            .OfType<MarkupReviewSnapshot>()
            .FirstOrDefault(candidate => string.Equals(candidate.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (snapshot == null)
            return false;

        MarkupReviewSnapshotListBox.SelectedItem = snapshot;
        return true;
    }

    internal string GetMarkupReviewSnapshotSummaryForTesting()
        => MarkupReviewSnapshotSummaryTextBlock?.Text ?? string.Empty;

    internal string GetSelectedMarkupReviewSnapshotSummaryForTesting()
        => SelectedMarkupReviewSnapshotSummaryTextBlock?.Text ?? string.Empty;

    internal string GetSelectedMarkupReviewSnapshotComparisonForTesting()
        => SelectedMarkupReviewSnapshotComparisonTextBlock?.Text ?? string.Empty;

    private void PublishMarkupReviewSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var input = PromptInput(
            "Publish Review Set",
            "Enter a name for the current filtered review set:",
            BuildDefaultMarkupReviewSnapshotPromptName());
        if (input == null)
            return;

        TryPublishMarkupReviewSnapshot(input, Environment.UserName, showFeedbackIfUnavailable: true);
    }

    private bool TryPublishMarkupReviewSnapshot(string name, string actor, bool showFeedbackIfUnavailable)
    {
        var reviewMarkups = _viewModel.GetFilteredReviewMarkups();
        if (reviewMarkups.Count == 0)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("No visible review issues are available to publish from the current filter context.", "Publish Review Set",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var snapshot = _viewModel.PublishMarkupReviewSnapshot(name, actor, BuildMarkupReviewSnapshotFilterSummary());
        _selectedMarkupReviewSnapshotId = snapshot.Id;
        UpdateMarkupReviewSnapshotUi();

        ActionLogService.Instance.Log(LogCategory.Component,
            "Review snapshot published", $"Id: {snapshot.Id}, Issues: {snapshot.IssueCount}");
        return true;
    }

    private void MarkupReviewSnapshotListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedMarkupReviewSnapshotId = (MarkupReviewSnapshotListBox?.SelectedItem as MarkupReviewSnapshot)?.Id;
        UpdateSelectedMarkupReviewSnapshotUi(MarkupReviewSnapshotListBox?.SelectedItem as MarkupReviewSnapshot);
    }

    private void UpdateMarkupReviewSnapshotUi()
    {
        if (MarkupReviewSnapshotSummaryTextBlock == null ||
            MarkupReviewSnapshotListBox == null ||
            SelectedMarkupReviewSnapshotSummaryTextBlock == null ||
            SelectedMarkupReviewSnapshotComparisonTextBlock == null ||
            PublishMarkupReviewSnapshotButton == null)
        {
            return;
        }

        var snapshots = _viewModel.MarkupReviewSnapshots.ToList();
        MarkupReviewSnapshotSummaryTextBlock.Text = BuildMarkupReviewSnapshotSummary(snapshots);
        PublishMarkupReviewSnapshotButton.IsEnabled = _viewModel.GetFilteredReviewMarkups().Count > 0;

        var retainedSnapshotId = _selectedMarkupReviewSnapshotId;
        MarkupReviewSnapshotListBox.ItemsSource = snapshots;

        var selectedSnapshot = snapshots.FirstOrDefault(snapshot => string.Equals(snapshot.Id, retainedSnapshotId, StringComparison.Ordinal))
            ?? snapshots.FirstOrDefault();
        MarkupReviewSnapshotListBox.SelectedItem = selectedSnapshot;
        _selectedMarkupReviewSnapshotId = selectedSnapshot?.Id;
        UpdateSelectedMarkupReviewSnapshotUi(selectedSnapshot);
    }

    private string BuildMarkupReviewSnapshotSummary(IReadOnlyList<MarkupReviewSnapshot> snapshots)
    {
        var currentVisibleCount = _viewModel.GetFilteredReviewMarkups().Count;
        if (snapshots.Count == 0)
        {
            return currentVisibleCount == 0
                ? "No published review sets yet. Adjust the current review filters until issues are visible, then publish the filtered set to preserve it with the project."
                : $"No published review sets yet. The current filtered review set contains {currentVisibleCount} issue(s) ready to publish.";
        }

        return $"{snapshots.Count} published review set(s) are stored with this project. The current filtered review set contains {currentVisibleCount} issue(s). Select a snapshot to compare it against the current review context.";
    }

    private void UpdateSelectedMarkupReviewSnapshotUi(MarkupReviewSnapshot? snapshot)
    {
        if (SelectedMarkupReviewSnapshotSummaryTextBlock == null ||
            SelectedMarkupReviewSnapshotComparisonTextBlock == null)
        {
            return;
        }

        if (snapshot == null)
        {
            SelectedMarkupReviewSnapshotSummaryTextBlock.Text = "Select a published review set to inspect its saved scope, filters, and issue counts.";
            SelectedMarkupReviewSnapshotComparisonTextBlock.Text = string.Empty;
            return;
        }

        SelectedMarkupReviewSnapshotSummaryTextBlock.Text = BuildSelectedMarkupReviewSnapshotSummary(snapshot);
        SelectedMarkupReviewSnapshotComparisonTextBlock.Text = BuildMarkupReviewSnapshotComparisonSummary(snapshot);
    }

    private static string BuildSelectedMarkupReviewSnapshotSummary(MarkupReviewSnapshot snapshot)
    {
        var publisher = string.IsNullOrWhiteSpace(snapshot.PublishedBy) ? "(unknown)" : snapshot.PublishedBy;
        var filters = string.IsNullOrWhiteSpace(snapshot.FilterSummary)
            ? "Published without additional filters."
            : snapshot.FilterSummary;
        return $"{snapshot.ScopeDisplayText}  |  {snapshot.IssueCount} issue(s)  |  {snapshot.OpenCount} open/in progress. Published {snapshot.PublishedDisplayText} by {publisher}. Filters: {filters}";
    }

    private string BuildMarkupReviewSnapshotComparisonSummary(MarkupReviewSnapshot snapshot)
    {
        if (snapshot.Markups.Count == 0)
            return "This published review set contains no issues.";

        var snapshotById = snapshot.Markups
            .GroupBy(markup => markup.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var currentById = _viewModel.GetFilteredReviewMarkups()
            .GroupBy(markup => markup.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var unchanged = 0;
        var statusChanged = 0;
        var ownershipChanged = 0;

        foreach (var markupId in snapshotById.Keys.Intersect(currentById.Keys, StringComparer.Ordinal))
        {
            var snapshotMarkup = snapshotById[markupId];
            var currentMarkup = currentById[markupId];
            var sameStatus = snapshotMarkup.Status == currentMarkup.Status;
            var sameAssignee = string.Equals(NormalizeAssignee(snapshotMarkup.AssignedTo), NormalizeAssignee(currentMarkup.AssignedTo), StringComparison.OrdinalIgnoreCase);

            if (sameStatus && sameAssignee)
            {
                unchanged++;
                continue;
            }

            if (!sameStatus)
                statusChanged++;
            if (!sameAssignee)
                ownershipChanged++;
        }

        var newIssues = currentById.Keys.Count(markupId => !snapshotById.ContainsKey(markupId));
        var missingIssues = snapshotById.Keys.Count(markupId => !currentById.ContainsKey(markupId));
        if (statusChanged == 0 && ownershipChanged == 0 && newIssues == 0 && missingIssues == 0)
            return "Current filtered review set matches this published review set exactly.";

        return $"Current filtered review set: {unchanged} unchanged, {statusChanged} status changed, {ownershipChanged} ownership changed, {newIssues} new, {missingIssues} missing.";
    }

    private string BuildMarkupReviewSnapshotFilterSummary()
    {
        var summaryParts = new List<string>
        {
            _viewModel.MarkupTool.ReviewScope == ViewModels.MarkupReviewScope.AllSheets
                ? $"Scope: All Sheets ({_viewModel.Sheets.Count})"
                : $"Scope: Current Sheet ({_viewModel.SelectedSheet?.DisplayName ?? "(none)"})"
        };

        if (!string.Equals(_viewModel.MarkupTool.StatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            summaryParts.Add($"Status: {_viewModel.MarkupTool.StatusFilter}");
        if (!string.Equals(_viewModel.MarkupTool.TypeFilter, "All", StringComparison.OrdinalIgnoreCase))
            summaryParts.Add($"Type: {_viewModel.MarkupTool.TypeFilter}");
        if (!string.Equals(_viewModel.MarkupTool.LayerFilter, "All", StringComparison.OrdinalIgnoreCase))
            summaryParts.Add($"Layer: {_viewModel.MarkupTool.LayerFilter}");
        if (!string.Equals(_viewModel.MarkupTool.AuthorFilter, "All", StringComparison.OrdinalIgnoreCase))
            summaryParts.Add($"Author: {_viewModel.MarkupTool.AuthorFilter}");
        if (!string.Equals(_viewModel.MarkupTool.AssigneeFilter, "All", StringComparison.OrdinalIgnoreCase))
            summaryParts.Add($"Assignee: {_viewModel.MarkupTool.AssigneeFilter}");
        if (!string.IsNullOrWhiteSpace(_viewModel.MarkupTool.LabelSearch))
            summaryParts.Add($"Search: {_viewModel.MarkupTool.LabelSearch.Trim()}");
        if (_viewModel.MarkupTool.SelectedIssueGroup != null)
            summaryParts.Add($"Bucket: {_viewModel.MarkupTool.SelectedIssueGroup.DisplayName}");

        return string.Join("  |  ", summaryParts);
    }

    private string BuildDefaultMarkupReviewSnapshotPromptName()
    {
        var scopePrefix = _viewModel.MarkupTool.ReviewScope == ViewModels.MarkupReviewScope.AllSheets
            ? "All Sheets"
            : _viewModel.SelectedSheet?.DisplayName ?? "Current Sheet";
        return $"{scopePrefix} Review Set {DateTime.Now:yyyy-MM-dd HH:mm}";
    }

    private static string NormalizeAssignee(string? assignee)
        => string.IsNullOrWhiteSpace(assignee) ? string.Empty : assignee.Trim();
}