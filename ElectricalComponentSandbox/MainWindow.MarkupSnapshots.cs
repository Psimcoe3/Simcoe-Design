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

    internal string GetSelectedMarkupReviewSnapshotDetailsForTesting()
        => SelectedMarkupReviewSnapshotDetailsTextBlock?.Text ?? string.Empty;

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
            SelectedMarkupReviewSnapshotDetailsTextBlock == null ||
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
            SelectedMarkupReviewSnapshotComparisonTextBlock == null ||
            SelectedMarkupReviewSnapshotDetailsTextBlock == null)
        {
            return;
        }

        if (snapshot == null)
        {
            SelectedMarkupReviewSnapshotSummaryTextBlock.Text = "Select a published review set to inspect its saved scope, filters, and issue counts.";
            SelectedMarkupReviewSnapshotComparisonTextBlock.Text = string.Empty;
            SelectedMarkupReviewSnapshotDetailsTextBlock.Text = string.Empty;
            return;
        }

        var comparison = BuildMarkupReviewSnapshotComparison(snapshot);
        SelectedMarkupReviewSnapshotSummaryTextBlock.Text = BuildSelectedMarkupReviewSnapshotSummary(snapshot);
        SelectedMarkupReviewSnapshotComparisonTextBlock.Text = BuildMarkupReviewSnapshotComparisonSummary(comparison);
        SelectedMarkupReviewSnapshotDetailsTextBlock.Text = BuildMarkupReviewSnapshotComparisonDetails(comparison);
    }

    private static string BuildSelectedMarkupReviewSnapshotSummary(MarkupReviewSnapshot snapshot)
    {
        var publisher = string.IsNullOrWhiteSpace(snapshot.PublishedBy) ? "(unknown)" : snapshot.PublishedBy;
        var filters = string.IsNullOrWhiteSpace(snapshot.FilterSummary)
            ? "Published without additional filters."
            : snapshot.FilterSummary;
        return $"{snapshot.DisplayName}: {snapshot.ScopeDisplayText}  |  {snapshot.IssueCount} issue(s)  |  {snapshot.OpenCount} open/in progress. Published {snapshot.PublishedDisplayText} by {publisher}. Filters: {filters}";
    }

    private MarkupReviewSnapshotComparisonResult BuildMarkupReviewSnapshotComparison(MarkupReviewSnapshot snapshot)
    {
        if (snapshot.Markups.Count == 0)
            return new MarkupReviewSnapshotComparisonResult(IsEmptySnapshot: true);

        var snapshotById = snapshot.Markups
            .GroupBy(markup => markup.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var currentById = _viewModel.GetFilteredReviewMarkups()
            .GroupBy(markup => markup.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var unchanged = 0;
        var statusChanged = 0;
        var ownershipChanged = 0;
        var changedIssues = new List<string>();

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

             changedIssues.Add(BuildMarkupReviewSnapshotChangedIssueLine(snapshotMarkup, currentMarkup, sameStatus, sameAssignee));
        }

        var newIssues = currentById.Keys
            .Where(markupId => !snapshotById.ContainsKey(markupId))
            .Select(markupId => currentById[markupId])
            .ToList();
        var missingIssues = snapshotById.Keys
            .Where(markupId => !currentById.ContainsKey(markupId))
            .Select(markupId => snapshotById[markupId])
            .ToList();

        return new MarkupReviewSnapshotComparisonResult(
            IsEmptySnapshot: false,
            UnchangedCount: unchanged,
            StatusChangedCount: statusChanged,
            OwnershipChangedCount: ownershipChanged,
            NewIssueCount: newIssues.Count,
            MissingIssueCount: missingIssues.Count,
            ChangedIssues: changedIssues,
            NewIssues: newIssues.Select(markup => BuildMarkupReviewSnapshotIssueLabel(markup)).ToList(),
            MissingIssues: missingIssues.Select(markup => BuildMarkupReviewSnapshotIssueLabel(markup)).ToList());
    }

    private static string BuildMarkupReviewSnapshotComparisonSummary(MarkupReviewSnapshotComparisonResult comparison)
    {
        if (comparison.IsEmptySnapshot)
            return "This published review set contains no issues.";

        if (comparison.StatusChangedCount == 0 && comparison.OwnershipChangedCount == 0 && comparison.NewIssueCount == 0 && comparison.MissingIssueCount == 0)
            return "Current filtered review set matches this published review set exactly.";

        return $"Current filtered review set: {comparison.UnchangedCount} unchanged, {comparison.StatusChangedCount} status changed, {comparison.OwnershipChangedCount} ownership changed, {comparison.NewIssueCount} new, {comparison.MissingIssueCount} missing.";
    }

    private static string BuildMarkupReviewSnapshotComparisonDetails(MarkupReviewSnapshotComparisonResult comparison)
    {
        if (comparison.IsEmptySnapshot)
            return string.Empty;

        if (comparison.StatusChangedCount == 0 && comparison.OwnershipChangedCount == 0 && comparison.NewIssueCount == 0 && comparison.MissingIssueCount == 0)
            return "No issue-level differences from the current filtered review set.";

        var sections = new List<string>();
        AppendMarkupReviewSnapshotDetailSection(sections, "Changed issues", comparison.ChangedIssues);
        AppendMarkupReviewSnapshotDetailSection(sections, "New issues", comparison.NewIssues);
        AppendMarkupReviewSnapshotDetailSection(sections, "Missing issues", comparison.MissingIssues);
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static void AppendMarkupReviewSnapshotDetailSection(List<string> sections, string title, IReadOnlyList<string> issues)
    {
        if (issues.Count == 0)
            return;

        var lines = new List<string>(issues.Count + 1)
        {
            title + ":"
        };

        foreach (var issue in issues.Take(5))
            lines.Add($"- {issue}");

        if (issues.Count > 5)
            lines.Add($"- +{issues.Count - 5} more");

        sections.Add(string.Join(Environment.NewLine, lines));
    }

    private static string BuildMarkupReviewSnapshotChangedIssueLine(MarkupRecord snapshotMarkup, MarkupRecord currentMarkup, bool sameStatus, bool sameAssignee)
    {
        var deltas = new List<string>(2);
        if (!sameStatus)
        {
            deltas.Add($"status {MarkupRecord.GetStatusDisplayText(snapshotMarkup.Status)} -> {MarkupRecord.GetStatusDisplayText(currentMarkup.Status)}");
        }

        if (!sameAssignee)
        {
            deltas.Add($"owner {FormatAssigneeForDisplay(snapshotMarkup.AssignedTo)} -> {FormatAssigneeForDisplay(currentMarkup.AssignedTo)}");
        }

        return $"{BuildMarkupReviewSnapshotIssueLabel(currentMarkup)} [{string.Join("; ", deltas)}]";
    }

    private static string BuildMarkupReviewSnapshotIssueLabel(MarkupRecord markup)
    {
        if (!string.IsNullOrWhiteSpace(markup.Metadata.Label))
            return markup.Metadata.Label.Trim();

        if (!string.IsNullOrWhiteSpace(markup.TextContent))
            return markup.TextContent.Trim();

        return markup.TypeDisplayText;
    }

    private static string FormatAssigneeForDisplay(string? assignee)
        => string.IsNullOrWhiteSpace(assignee) ? "(unassigned)" : assignee.Trim();

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

internal sealed record MarkupReviewSnapshotComparisonResult(
    bool IsEmptySnapshot,
    int UnchangedCount = 0,
    int StatusChangedCount = 0,
    int OwnershipChangedCount = 0,
    int NewIssueCount = 0,
    int MissingIssueCount = 0,
    IReadOnlyList<string>? ChangedIssues = null,
    IReadOnlyList<string>? NewIssues = null,
    IReadOnlyList<string>? MissingIssues = null)
{
    public IReadOnlyList<string> ChangedIssues { get; } = ChangedIssues ?? Array.Empty<string>();
    public IReadOnlyList<string> NewIssues { get; } = NewIssues ?? Array.Empty<string>();
    public IReadOnlyList<string> MissingIssues { get; } = MissingIssues ?? Array.Empty<string>();
}