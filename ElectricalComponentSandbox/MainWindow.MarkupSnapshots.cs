using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly HashSet<string> _collapsedMarkupReviewSnapshotDiffGroupKeys = new(StringComparer.OrdinalIgnoreCase);
    private string? _pendingMarkupReviewSnapshotDiffAnchorGroupStateKey;
    private string? _pendingMarkupReviewSnapshotDiffSelectedEntryKey;
    private bool _pendingMarkupReviewSnapshotDiffListFocus;
    private bool _isRestoringMarkupReviewSnapshotDiffSelection;

    internal bool PublishMarkupReviewSnapshotForTesting(string name, string? actor = null)
        => TryPublishMarkupReviewSnapshot(name, actor ?? "Test Reviewer", showFeedbackIfUnavailable: false);

    internal bool DeleteSelectedMarkupReviewSnapshotForTesting()
        => TryDeleteSelectedMarkupReviewSnapshot(confirmDeletion: false, showFeedbackIfUnavailable: false);

    internal bool RenameSelectedMarkupReviewSnapshotForTesting(string name)
        => TryRenameSelectedMarkupReviewSnapshot(name, showFeedbackIfUnavailable: false);

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

    internal IReadOnlyList<string> GetMarkupReviewSnapshotDiffTitlesForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => !entry.IsGroupHeader)
            .Select(entry => entry.Title)
            .ToList() ?? new List<string>();
    }

    internal IReadOnlyList<string> GetMarkupReviewSnapshotDiffHeaderTextsForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => entry.IsGroupHeader)
            .Select(entry => entry.SheetContextText)
            .ToList() ?? new List<string>();
    }

    internal IReadOnlyList<string> GetMarkupReviewSnapshotDiffHeaderBadgeTextsForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => entry.IsGroupHeader)
            .Select(BuildMarkupReviewSnapshotDiffHeaderBadgeTextForTesting)
            .ToList() ?? new List<string>();
    }

    internal IReadOnlyList<string> GetMarkupReviewSnapshotDiffHeaderPriorityKeysForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => entry.IsGroupHeader)
            .Select(entry => entry.HeaderPriorityKey)
            .ToList() ?? new List<string>();
    }

    internal IReadOnlyList<string> GetMarkupReviewSnapshotDiffHeaderToggleTextsForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => entry.IsGroupHeader)
            .Select(entry => entry.HeaderToggleText)
            .ToList() ?? new List<string>();
    }

    internal IReadOnlyList<string> GetMarkupReviewSnapshotDiffHeaderCollapseOthersTextsForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => entry.IsGroupHeader)
            .Select(entry => entry.HeaderCollapseOthersText)
            .ToList() ?? new List<string>();
    }

    internal IReadOnlyList<bool> GetMarkupReviewSnapshotDiffHeaderCollapseOthersEnabledStatesForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => entry.IsGroupHeader)
            .Select(entry => entry.HeaderCanCollapseOthers)
            .ToList() ?? new List<bool>();
    }

    internal IReadOnlyList<bool> GetMarkupReviewSnapshotDiffHeaderCollapseOthersVisibleStatesForTesting()
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .Where(entry => entry.IsGroupHeader)
            .Select(entry => entry.HeaderShowsCollapseOthers)
            .ToList() ?? new List<bool>();
    }

    internal string GetMarkupReviewSnapshotDiffRevealHintForTesting(string title)
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .FirstOrDefault(candidate => string.Equals(candidate.Title, title, StringComparison.OrdinalIgnoreCase))
            ?.RevealHintText ?? string.Empty;
    }

    internal string GetMarkupReviewSnapshotDiffSheetContextForTesting(string title)
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .FirstOrDefault(candidate => string.Equals(candidate.Title, title, StringComparison.OrdinalIgnoreCase))
            ?.SheetContextText ?? string.Empty;
    }

    internal string GetMarkupReviewSnapshotDiffDisplayedSheetContextForTesting(string title)
    {
        UpdateMarkupReviewSnapshotUi();
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .FirstOrDefault(candidate => string.Equals(candidate.Title, title, StringComparison.OrdinalIgnoreCase))
            ?.DisplaySheetContextText ?? string.Empty;
    }

    internal string GetSelectedMarkupReviewSnapshotDiffTitleForTesting()
        => (MarkupReviewSnapshotDiffListBox?.SelectedItem as MarkupReviewSnapshotDiffEntry)?.Title ?? string.Empty;

    internal bool SelectMarkupReviewSnapshotDiffEntryForTesting(string title)
    {
        UpdateMarkupReviewSnapshotUi();
        if (MarkupReviewSnapshotDiffListBox == null)
            return false;

        var entry = MarkupReviewSnapshotDiffListBox.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .FirstOrDefault(candidate => string.Equals(candidate.Title, title, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return false;

        MarkupReviewSnapshotDiffListBox.SelectedItem = entry;
        return true;
    }

    internal bool ToggleMarkupReviewSnapshotDiffHeaderForTesting(string headerSheetTitleText)
    {
        UpdateMarkupReviewSnapshotUi();
        var entry = MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .FirstOrDefault(candidate => candidate.IsGroupHeader && string.Equals(candidate.HeaderSheetTitleText, headerSheetTitleText, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return false;

        ToggleMarkupReviewSnapshotDiffHeader(entry);
        return true;
    }

    internal bool CollapseOtherMarkupReviewSnapshotDiffGroupsForTesting(string headerSheetTitleText)
    {
        UpdateMarkupReviewSnapshotUi();
        var entry = MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .FirstOrDefault(candidate => candidate.IsGroupHeader && string.Equals(candidate.HeaderSheetTitleText, headerSheetTitleText, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return false;

        return TryCollapseOtherMarkupReviewSnapshotDiffGroups(entry);
    }

    internal bool CollapseAllMarkupReviewSnapshotDiffGroupsForTesting()
        => TrySetMarkupReviewSnapshotDiffGroupsCollapsed(collapseAll: true);

    internal bool ExpandAllMarkupReviewSnapshotDiffGroupsForTesting()
        => TrySetMarkupReviewSnapshotDiffGroupsCollapsed(collapseAll: false);

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

    private void DeleteSelectedMarkupReviewSnapshot_Click(object sender, RoutedEventArgs e)
        => TryDeleteSelectedMarkupReviewSnapshot(confirmDeletion: true, showFeedbackIfUnavailable: true);

    private void MarkupReviewSnapshotDiffHeaderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not MarkupReviewSnapshotDiffEntry entry)
            return;

        ToggleMarkupReviewSnapshotDiffHeader(entry);
        e.Handled = true;
    }

    private void MarkupReviewSnapshotDiffCollapseOthersButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not MarkupReviewSnapshotDiffEntry entry)
            return;

        if (!entry.HeaderCanCollapseOthers)
        {
            e.Handled = true;
            return;
        }

        TryCollapseOtherMarkupReviewSnapshotDiffGroups(entry);
        e.Handled = true;
    }

    private void CollapseAllMarkupReviewSnapshotDiffGroupsButton_Click(object sender, RoutedEventArgs e)
        => TrySetMarkupReviewSnapshotDiffGroupsCollapsed(collapseAll: true);

    private void ExpandAllMarkupReviewSnapshotDiffGroupsButton_Click(object sender, RoutedEventArgs e)
        => TrySetMarkupReviewSnapshotDiffGroupsCollapsed(collapseAll: false);

    private void RenameSelectedMarkupReviewSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = GetSelectedMarkupReviewSnapshot();
        if (snapshot == null)
        {
            MessageBox.Show("Select a published review set first.", "Rename Review Set",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var input = PromptInput(
            "Rename Review Set",
            "Enter a new title for the selected published review set. Leave blank to use the default timestamp label:",
            snapshot.Name);
        if (input == null)
            return;

        TryRenameSelectedMarkupReviewSnapshot(input, showFeedbackIfUnavailable: true);
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

    private bool TryDeleteSelectedMarkupReviewSnapshot(bool confirmDeletion, bool showFeedbackIfUnavailable)
    {
        var snapshot = GetSelectedMarkupReviewSnapshot();
        if (snapshot == null)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select a published review set first.", "Delete Review Set",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (confirmDeletion)
        {
            var choice = MessageBox.Show(
                $"Delete published review set '{snapshot.DisplayName}'? This removes the saved snapshot from the project file.",
                "Delete Review Set",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (choice != MessageBoxResult.Yes)
                return false;
        }

        var removed = _viewModel.RemoveMarkupReviewSnapshot(snapshot.Id);
        if (!removed)
            return false;

        ClearMarkupReviewSnapshotDiffGroupState(snapshot.Id);

        if (string.Equals(_selectedMarkupReviewSnapshotId, snapshot.Id, StringComparison.Ordinal))
            _selectedMarkupReviewSnapshotId = null;

        UpdateMarkupReviewSnapshotUi();
        ActionLogService.Instance.Log(LogCategory.Component,
            "Review snapshot deleted", $"Id: {snapshot.Id}, Name: {snapshot.DisplayName}");
        return true;
    }

    private bool TryRenameSelectedMarkupReviewSnapshot(string name, bool showFeedbackIfUnavailable)
    {
        var snapshot = GetSelectedMarkupReviewSnapshot();
        if (snapshot == null)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select a published review set first.", "Rename Review Set",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var previousDisplayName = snapshot.DisplayName;
        var renamed = _viewModel.RenameMarkupReviewSnapshot(snapshot.Id, name);
        if (!renamed)
            return false;

        _selectedMarkupReviewSnapshotId = snapshot.Id;
        UpdateMarkupReviewSnapshotUi();
        ActionLogService.Instance.Log(LogCategory.Component,
            "Review snapshot renamed", $"Id: {snapshot.Id}, Name: {previousDisplayName} -> {snapshot.DisplayName}");
        return true;
    }

    private MarkupReviewSnapshot? GetSelectedMarkupReviewSnapshot()
    {
        return MarkupReviewSnapshotListBox?.SelectedItem as MarkupReviewSnapshot
            ?? _viewModel.MarkupReviewSnapshots.FirstOrDefault(candidate => string.Equals(candidate.Id, _selectedMarkupReviewSnapshotId, StringComparison.Ordinal));
    }

    private void MarkupReviewSnapshotListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedMarkupReviewSnapshotId = (MarkupReviewSnapshotListBox?.SelectedItem as MarkupReviewSnapshot)?.Id;
        UpdateSelectedMarkupReviewSnapshotUi(MarkupReviewSnapshotListBox?.SelectedItem as MarkupReviewSnapshot);
    }

    private void MarkupReviewSnapshotDiffListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRestoringMarkupReviewSnapshotDiffSelection)
            return;

        if (MarkupReviewSnapshotDiffListBox?.SelectedItem is not MarkupReviewSnapshotDiffEntry entry)
        {
            return;
        }

        if (entry.IsGroupHeader)
        {
            MarkupReviewSnapshotDiffListBox.SelectedItem = null;
            return;
        }

        if (entry.CanRevealLiveMarkup && !string.IsNullOrWhiteSpace(entry.CurrentMarkupId))
        {
            var markup = _viewModel.GetFilteredReviewMarkups()
                .FirstOrDefault(candidate => string.Equals(candidate.Id, entry.CurrentMarkupId, StringComparison.Ordinal));
            if (markup == null)
                return;

            if (_viewModel.RevealMarkup(markup))
            {
                SelectInspectorTab(MarkupsInspectorTab);
                MarkupListView?.ScrollIntoView(_viewModel.MarkupTool.SelectedMarkup);
                QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
            }

            return;
        }

        RevealMarkupReviewSnapshotSheetContext(entry);
    }

    private void UpdateMarkupReviewSnapshotUi()
    {
        if (MarkupReviewSnapshotSummaryTextBlock == null ||
            MarkupReviewSnapshotListBox == null ||
            SelectedMarkupReviewSnapshotSummaryTextBlock == null ||
            SelectedMarkupReviewSnapshotComparisonTextBlock == null ||
            SelectedMarkupReviewSnapshotDetailsTextBlock == null ||
            MarkupReviewSnapshotDiffListBox == null ||
            ExpandAllMarkupReviewSnapshotDiffGroupsButton == null ||
            CollapseAllMarkupReviewSnapshotDiffGroupsButton == null ||
            PublishMarkupReviewSnapshotButton == null ||
            RenameMarkupReviewSnapshotButton == null ||
            DeleteMarkupReviewSnapshotButton == null)
        {
            return;
        }

        var retainedDiffSelectionKey = GetSelectedMarkupReviewSnapshotDiffEntryKey();
        var snapshots = _viewModel.MarkupReviewSnapshots.ToList();
        MarkupReviewSnapshotSummaryTextBlock.Text = BuildMarkupReviewSnapshotSummary(snapshots);
        PublishMarkupReviewSnapshotButton.IsEnabled = _viewModel.GetFilteredReviewMarkups().Count > 0;

        var retainedSnapshotId = _selectedMarkupReviewSnapshotId;
        MarkupReviewSnapshotListBox.ItemsSource = snapshots;

        var selectedSnapshot = snapshots.FirstOrDefault(snapshot => string.Equals(snapshot.Id, retainedSnapshotId, StringComparison.Ordinal))
            ?? snapshots.FirstOrDefault();
        MarkupReviewSnapshotListBox.SelectedItem = selectedSnapshot;
        RenameMarkupReviewSnapshotButton.IsEnabled = selectedSnapshot != null;
        DeleteMarkupReviewSnapshotButton.IsEnabled = selectedSnapshot != null;
        _selectedMarkupReviewSnapshotId = selectedSnapshot?.Id;
        if (!string.IsNullOrWhiteSpace(retainedDiffSelectionKey) &&
            selectedSnapshot != null &&
            string.Equals(selectedSnapshot.Id, retainedSnapshotId, StringComparison.Ordinal))
        {
            PrepareMarkupReviewSnapshotDiffSelectionRetention(retainedDiffSelectionKey);
        }

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
            SelectedMarkupReviewSnapshotDetailsTextBlock == null ||
            MarkupReviewSnapshotDiffListBox == null ||
            ExpandAllMarkupReviewSnapshotDiffGroupsButton == null ||
            CollapseAllMarkupReviewSnapshotDiffGroupsButton == null ||
            RenameMarkupReviewSnapshotButton == null ||
            DeleteMarkupReviewSnapshotButton == null)
        {
            return;
        }

        if (snapshot == null)
        {
            RenameMarkupReviewSnapshotButton.IsEnabled = false;
            DeleteMarkupReviewSnapshotButton.IsEnabled = false;
            SelectedMarkupReviewSnapshotSummaryTextBlock.Text = "Select a published review set to inspect its saved scope, filters, and issue counts.";
            SelectedMarkupReviewSnapshotComparisonTextBlock.Text = string.Empty;
            SelectedMarkupReviewSnapshotDetailsTextBlock.Text = string.Empty;
            MarkupReviewSnapshotDiffListBox.ItemsSource = null;
            ExpandAllMarkupReviewSnapshotDiffGroupsButton.IsEnabled = false;
            CollapseAllMarkupReviewSnapshotDiffGroupsButton.IsEnabled = false;
            _pendingMarkupReviewSnapshotDiffAnchorGroupStateKey = null;
            _pendingMarkupReviewSnapshotDiffSelectedEntryKey = null;
            _pendingMarkupReviewSnapshotDiffListFocus = false;
            return;
        }

        RenameMarkupReviewSnapshotButton.IsEnabled = true;
        DeleteMarkupReviewSnapshotButton.IsEnabled = true;
        var comparison = BuildMarkupReviewSnapshotComparison(snapshot);
        var diffHeaderEntries = comparison.DiffEntries.Where(entry => entry.IsGroupHeader).ToList();
        SelectedMarkupReviewSnapshotSummaryTextBlock.Text = BuildSelectedMarkupReviewSnapshotSummary(snapshot);
        SelectedMarkupReviewSnapshotComparisonTextBlock.Text = BuildMarkupReviewSnapshotComparisonSummary(comparison);
        SelectedMarkupReviewSnapshotDetailsTextBlock.Text = BuildMarkupReviewSnapshotComparisonDetails(comparison);
        MarkupReviewSnapshotDiffListBox.ItemsSource = comparison.DiffEntries;
        ExpandAllMarkupReviewSnapshotDiffGroupsButton.IsEnabled = diffHeaderEntries.Any(entry => string.Equals(entry.HeaderToggleText, "[+]", StringComparison.Ordinal));
        CollapseAllMarkupReviewSnapshotDiffGroupsButton.IsEnabled = diffHeaderEntries.Any(entry => string.Equals(entry.HeaderToggleText, "[-]", StringComparison.Ordinal));
        ApplyMarkupReviewSnapshotDiffSelectionRetention();
        ApplyMarkupReviewSnapshotDiffAnchor();
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
        var diffEntries = new List<MarkupReviewSnapshotDiffEntry>();

        foreach (var snapshotMarkup in snapshot.Markups
                     .Where(markup => currentById.ContainsKey(markup.Id))
                     .GroupBy(markup => markup.Id, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            var currentMarkup = currentById[snapshotMarkup.Id];
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
            diffEntries.Add(BuildMarkupReviewSnapshotChangedIssueEntry(snapshotMarkup, currentMarkup, sameStatus, sameAssignee));
        }

        var newIssues = _viewModel.GetFilteredReviewMarkups()
            .Where(markup => !snapshotById.ContainsKey(markup.Id))
            .GroupBy(markup => markup.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var missingIssues = snapshot.Markups
            .Where(markup => !currentById.ContainsKey(markup.Id))
            .GroupBy(markup => markup.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        diffEntries.AddRange(newIssues.Select(BuildMarkupReviewSnapshotNewIssueEntry));
        diffEntries.AddRange(missingIssues.Select(markup => BuildMarkupReviewSnapshotMissingIssueEntry(markup, snapshot)));
        diffEntries = BuildMarkupReviewSnapshotDiffDisplayEntries(snapshot.Id, diffEntries);

        return new MarkupReviewSnapshotComparisonResult(
            IsEmptySnapshot: false,
            UnchangedCount: unchanged,
            StatusChangedCount: statusChanged,
            OwnershipChangedCount: ownershipChanged,
            NewIssueCount: newIssues.Count,
            MissingIssueCount: missingIssues.Count,
            ChangedIssues: changedIssues,
            NewIssues: newIssues.Select(markup => BuildMarkupReviewSnapshotIssueLabel(markup)).ToList(),
            MissingIssues: missingIssues.Select(markup => BuildMarkupReviewSnapshotIssueLabel(markup)).ToList(),
            DiffEntries: diffEntries);
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

    private MarkupReviewSnapshotDiffEntry BuildMarkupReviewSnapshotChangedIssueEntry(MarkupRecord snapshotMarkup, MarkupRecord currentMarkup, bool sameStatus, bool sameAssignee)
    {
        var deltas = new List<string>(2);
        if (!sameStatus)
            deltas.Add($"Status: {MarkupRecord.GetStatusDisplayText(snapshotMarkup.Status)} -> {MarkupRecord.GetStatusDisplayText(currentMarkup.Status)}");
        if (!sameAssignee)
            deltas.Add($"Owner: {FormatAssigneeForDisplay(snapshotMarkup.AssignedTo)} -> {FormatAssigneeForDisplay(currentMarkup.AssignedTo)}");

        return new MarkupReviewSnapshotDiffEntry(
            Key: $"changed:{currentMarkup.Id}",
            CategoryKey: "changed",
            CategoryDisplayText: "Changed",
            Title: BuildMarkupReviewSnapshotIssueLabel(currentMarkup),
            SheetContextText: BuildMarkupReviewSnapshotLiveIssueSheetContextText(currentMarkup),
            DisplaySheetContextText: BuildMarkupReviewSnapshotLiveIssueSheetContextText(currentMarkup),
            DetailText: string.Join("  |  ", deltas),
            RevealHintText: "Select to focus the live review issue.",
            CurrentMarkupId: currentMarkup.Id,
            SheetContextSortKey: BuildMarkupReviewSnapshotLiveIssueSheetContextSortKey(currentMarkup),
            CategorySortOrder: 0);
    }

    private MarkupReviewSnapshotDiffEntry BuildMarkupReviewSnapshotNewIssueEntry(MarkupRecord markup)
    {
        return new MarkupReviewSnapshotDiffEntry(
            Key: $"new:{markup.Id}",
            CategoryKey: "new",
            CategoryDisplayText: "New",
            Title: BuildMarkupReviewSnapshotIssueLabel(markup),
            SheetContextText: BuildMarkupReviewSnapshotLiveIssueSheetContextText(markup),
            DisplaySheetContextText: BuildMarkupReviewSnapshotLiveIssueSheetContextText(markup),
            DetailText: "Added after snapshot publication.",
            RevealHintText: "Select to focus the live review issue.",
            CurrentMarkupId: markup.Id,
            SheetContextSortKey: BuildMarkupReviewSnapshotLiveIssueSheetContextSortKey(markup),
            CategorySortOrder: 1);
    }

    private MarkupReviewSnapshotDiffEntry BuildMarkupReviewSnapshotMissingIssueEntry(MarkupRecord markup, MarkupReviewSnapshot snapshot)
    {
        var sourceSheetDisplayName = string.IsNullOrWhiteSpace(markup.ReviewSheetDisplayText)
            ? snapshot.SourceSheetDisplayName
            : markup.ReviewSheetDisplayText.Trim();
        var sourceSheetId = !string.IsNullOrWhiteSpace(markup.ReviewSheetId)
            ? markup.ReviewSheetId.Trim()
            : (snapshot.Scope == MarkupReviewSnapshotScope.CurrentSheet ? snapshot.SourceSheetId : string.Empty);
        var canResolveRecordedSheet = CanResolveMarkupReviewSnapshotDiffSheet(sourceSheetId, sourceSheetDisplayName);
        var detailText = "Not present in the current filtered review set.";
        var sheetContextText = BuildMarkupReviewSnapshotMissingIssueSheetContextText(sourceSheetId, sourceSheetDisplayName);
        var revealHintText = string.IsNullOrWhiteSpace(sourceSheetDisplayName) && string.IsNullOrWhiteSpace(sourceSheetId)
            ? "Snapshot only - no sheet context is available to focus."
            : canResolveRecordedSheet
                ? "Select to switch to the recorded sheet context."
                : "Recorded sheet no longer exists in the current project.";

        return new MarkupReviewSnapshotDiffEntry(
            Key: $"missing:{markup.Id}",
            CategoryKey: "missing",
            CategoryDisplayText: "Missing",
            Title: BuildMarkupReviewSnapshotIssueLabel(markup),
            SheetContextText: sheetContextText,
            DisplaySheetContextText: sheetContextText,
            DetailText: detailText,
            RevealHintText: revealHintText,
            CurrentMarkupId: null,
            SourceSheetId: sourceSheetId,
            SourceSheetDisplayName: sourceSheetDisplayName,
            SheetContextSortKey: BuildMarkupReviewSnapshotMissingIssueSheetContextSortKey(sourceSheetId, sourceSheetDisplayName),
            CategorySortOrder: 2);
    }

    private string BuildMarkupReviewSnapshotLiveIssueSheetContextText(MarkupRecord markup)
    {
        var sheetDisplayName = _viewModel.GetMarkupSheetDisplayName(markup);
        return string.IsNullOrWhiteSpace(sheetDisplayName)
            ? "Sheet: (unknown)"
            : $"Sheet: {sheetDisplayName}";
    }

    private string BuildMarkupReviewSnapshotLiveIssueSheetContextSortKey(MarkupRecord markup)
        => NormalizeMarkupReviewSnapshotDiffSheetContextSortKey(_viewModel.GetMarkupSheetDisplayName(markup));

    private string BuildMarkupReviewSnapshotMissingIssueSheetContextText(string? sourceSheetId, string? sourceSheetDisplayName)
    {
        var resolvedSheet = ResolveMarkupReviewSnapshotDiffSheet(sourceSheetId, sourceSheetDisplayName);
        if (resolvedSheet != null)
            return $"Sheet: {resolvedSheet.DisplayName}";

        if (!string.IsNullOrWhiteSpace(sourceSheetDisplayName))
            return $"Snapshot sheet: {sourceSheetDisplayName}";

        return "Snapshot sheet: (unknown)";
    }

    private string BuildMarkupReviewSnapshotMissingIssueSheetContextSortKey(string? sourceSheetId, string? sourceSheetDisplayName)
    {
        var resolvedSheet = ResolveMarkupReviewSnapshotDiffSheet(sourceSheetId, sourceSheetDisplayName);
        return NormalizeMarkupReviewSnapshotDiffSheetContextSortKey(resolvedSheet?.DisplayName ?? sourceSheetDisplayName);
    }

    private static List<MarkupReviewSnapshotDiffEntry> SortMarkupReviewSnapshotDiffEntries(IEnumerable<MarkupReviewSnapshotDiffEntry> diffEntries)
    {
        return diffEntries
            .OrderBy(entry => entry.SheetContextSortKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.CategorySortOrder)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<MarkupReviewSnapshotDiffEntry> BuildMarkupReviewSnapshotDiffDisplayEntries(string snapshotId, IEnumerable<MarkupReviewSnapshotDiffEntry> diffEntries)
    {
        var sortedEntries = SortMarkupReviewSnapshotDiffEntries(diffEntries);
        var displayEntries = new List<MarkupReviewSnapshotDiffEntry>(sortedEntries.Count * 2);
        var sheetGroups = sortedEntries
            .GroupBy(entry => entry.SheetContextSortKey, StringComparer.OrdinalIgnoreCase)
            .Select(sheetGroup =>
            {
                var sheetEntries = sheetGroup.ToList();
                var headerEntry = sheetEntries[0];
                var groupStateKey = BuildMarkupReviewSnapshotDiffGroupStateKey(snapshotId, headerEntry.SheetContextSortKey);
                var isCollapsed = _collapsedMarkupReviewSnapshotDiffGroupKeys.Contains(groupStateKey);
                return (
                    SheetEntries: sheetEntries,
                    HeaderEntry: headerEntry,
                    GroupStateKey: groupStateKey,
                    IsCollapsed: isCollapsed);
            })
            .ToList();
        var groupStates = sheetGroups
            .Select(sheetGroup => (sheetGroup.GroupStateKey, sheetGroup.IsCollapsed))
            .ToList();

        foreach (var sheetGroup in sheetGroups)
        {
            var collapseOthersState = BuildMarkupReviewSnapshotDiffHeaderCollapseOthersState(groupStates, sheetGroup.GroupStateKey, sheetGroup.IsCollapsed);
            displayEntries.Add(BuildMarkupReviewSnapshotDiffHeaderEntry(
                sheetGroup.HeaderEntry.SheetContextText,
                sheetGroup.HeaderEntry.SheetContextSortKey,
                sheetGroup.SheetEntries,
                sheetGroup.GroupStateKey,
                sheetGroup.IsCollapsed,
                collapseOthersState.Text,
                collapseOthersState.IsEnabled,
                collapseOthersState.IsVisible));

            if (!sheetGroup.IsCollapsed)
                displayEntries.AddRange(sheetGroup.SheetEntries.Select(entry => entry with { DisplaySheetContextText = string.Empty }));
        }

        return displayEntries;
    }

    private static MarkupReviewSnapshotDiffEntry BuildMarkupReviewSnapshotDiffHeaderEntry(string sheetContextText, string sheetContextSortKey, IReadOnlyList<MarkupReviewSnapshotDiffEntry> sheetEntries, string groupStateKey, bool isCollapsed, string headerCollapseOthersText, bool headerCanCollapseOthers, bool headerShowsCollapseOthers)
    {
        var groupHeaderSheetText = BuildMarkupReviewSnapshotDiffGroupHeaderSheetText(sheetContextText);
        var groupHeaderPriorityKey = BuildMarkupReviewSnapshotDiffHeaderPriorityKey(sheetEntries);
        var groupHeaderIssueCountText = BuildMarkupReviewSnapshotDiffGroupHeaderIssueCountText(sheetEntries.Count);
        var groupHeaderChangedCountText = BuildMarkupReviewSnapshotDiffGroupHeaderCategoryCountText(sheetEntries, "changed", "changed");
        var groupHeaderNewCountText = BuildMarkupReviewSnapshotDiffGroupHeaderCategoryCountText(sheetEntries, "new", "new");
        var groupHeaderMissingCountText = BuildMarkupReviewSnapshotDiffGroupHeaderCategoryCountText(sheetEntries, "missing", "missing");

        return new MarkupReviewSnapshotDiffEntry(
            Key: $"header:{sheetContextSortKey}",
            CategoryKey: "header",
            CategoryDisplayText: string.Empty,
            Title: string.Empty,
            SheetContextText: BuildMarkupReviewSnapshotDiffGroupHeaderText(sheetContextText, sheetEntries),
            DisplaySheetContextText: string.Empty,
            DetailText: string.Empty,
            RevealHintText: string.Empty,
            CurrentMarkupId: null,
            SheetContextSortKey: sheetContextSortKey,
            CategorySortOrder: -1,
            IsGroupHeader: true,
            HeaderGroupStateKey: groupStateKey,
            HeaderToggleText: isCollapsed ? "[+]" : "[-]",
            HeaderPriorityKey: groupHeaderPriorityKey,
            HeaderSheetTitleText: groupHeaderSheetText,
            HeaderIssueCountText: groupHeaderIssueCountText,
            HeaderChangedCountText: groupHeaderChangedCountText,
            HeaderNewCountText: groupHeaderNewCountText,
            HeaderMissingCountText: groupHeaderMissingCountText,
            HeaderCollapseOthersText: headerCollapseOthersText,
            HeaderCanCollapseOthers: headerCanCollapseOthers,
            HeaderShowsCollapseOthers: headerShowsCollapseOthers);
    }

    private static (string Text, bool IsEnabled, bool IsVisible) BuildMarkupReviewSnapshotDiffHeaderCollapseOthersState(IReadOnlyList<(string GroupStateKey, bool IsCollapsed)> groupStates, string groupStateKey, bool isCollapsed)
    {
        if (groupStates.Count <= 1)
            return (Text: string.Empty, IsEnabled: false, IsVisible: false);

        var hasExpandedSibling = groupStates.Any(groupState =>
            !string.Equals(groupState.GroupStateKey, groupStateKey, StringComparison.OrdinalIgnoreCase) &&
            !groupState.IsCollapsed);
        if (!isCollapsed && !hasExpandedSibling)
            return (Text: "Isolated", IsEnabled: false, IsVisible: true);

        return (Text: "Collapse Others", IsEnabled: true, IsVisible: true);
    }

    private static string BuildMarkupReviewSnapshotDiffGroupStateKey(string snapshotId, string sheetContextSortKey)
        => snapshotId + "|" + sheetContextSortKey;

    private static string BuildMarkupReviewSnapshotDiffGroupHeaderText(string sheetContextText, IReadOnlyList<MarkupReviewSnapshotDiffEntry> sheetEntries)
    {
        var issueCountText = BuildMarkupReviewSnapshotDiffGroupHeaderIssueCountText(sheetEntries.Count);
        var categorySummary = BuildMarkupReviewSnapshotDiffGroupCategorySummary(sheetEntries);
        return string.IsNullOrWhiteSpace(categorySummary)
            ? $"{BuildMarkupReviewSnapshotDiffGroupHeaderSheetText(sheetContextText)} ({issueCountText})"
            : $"{BuildMarkupReviewSnapshotDiffGroupHeaderSheetText(sheetContextText)} ({issueCountText}: {categorySummary})";
    }

    private static string BuildMarkupReviewSnapshotDiffGroupHeaderIssueCountText(int issueCount)
        => $"{issueCount} issue{(issueCount == 1 ? string.Empty : "s")}";

    private static string BuildMarkupReviewSnapshotDiffHeaderPriorityKey(IReadOnlyList<MarkupReviewSnapshotDiffEntry> sheetEntries)
    {
        if (sheetEntries.Any(entry => string.Equals(entry.CategoryKey, "missing", StringComparison.Ordinal)))
            return "missing";

        if (sheetEntries.Any(entry => string.Equals(entry.CategoryKey, "changed", StringComparison.Ordinal)))
            return "changed";

        if (sheetEntries.Any(entry => string.Equals(entry.CategoryKey, "new", StringComparison.Ordinal)))
            return "new";

        return string.Empty;
    }

    private static string BuildMarkupReviewSnapshotDiffGroupCategorySummary(IReadOnlyList<MarkupReviewSnapshotDiffEntry> sheetEntries)
    {
        var summaryParts = new List<string>(3);
        AppendMarkupReviewSnapshotDiffGroupCategorySummary(summaryParts, BuildMarkupReviewSnapshotDiffGroupHeaderCategoryCountText(sheetEntries, "changed", "changed"));
        AppendMarkupReviewSnapshotDiffGroupCategorySummary(summaryParts, BuildMarkupReviewSnapshotDiffGroupHeaderCategoryCountText(sheetEntries, "new", "new"));
        AppendMarkupReviewSnapshotDiffGroupCategorySummary(summaryParts, BuildMarkupReviewSnapshotDiffGroupHeaderCategoryCountText(sheetEntries, "missing", "missing"));
        return string.Join(", ", summaryParts);
    }

    private static void AppendMarkupReviewSnapshotDiffGroupCategorySummary(List<string> summaryParts, string categorySummary)
    {
        if (!string.IsNullOrWhiteSpace(categorySummary))
            summaryParts.Add(categorySummary);
    }

    private static string BuildMarkupReviewSnapshotDiffGroupHeaderCategoryCountText(IReadOnlyList<MarkupReviewSnapshotDiffEntry> sheetEntries, string categoryKey, string label)
    {
        var count = sheetEntries.Count(entry => string.Equals(entry.CategoryKey, categoryKey, StringComparison.Ordinal));
        return count > 0 ? $"{count} {label}" : string.Empty;
    }

    private static string BuildMarkupReviewSnapshotDiffHeaderBadgeTextForTesting(MarkupReviewSnapshotDiffEntry entry)
    {
        var badges = new[]
        {
            entry.HeaderIssueCountText,
            entry.HeaderChangedCountText,
            entry.HeaderNewCountText,
            entry.HeaderMissingCountText
        };

        return string.Join(" | ", badges.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string BuildMarkupReviewSnapshotDiffGroupHeaderSheetText(string sheetContextText)
    {
        const string snapshotSheetPrefix = "Snapshot sheet:";
        if (!sheetContextText.StartsWith(snapshotSheetPrefix, StringComparison.Ordinal))
            return sheetContextText;

        var snapshotSheetName = sheetContextText[snapshotSheetPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(snapshotSheetName)
            ? "Snapshot-only sheet"
            : $"Snapshot-only sheet: {snapshotSheetName}";
    }

    private void ToggleMarkupReviewSnapshotDiffHeader(MarkupReviewSnapshotDiffEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.HeaderGroupStateKey))
            return;

        PrepareMarkupReviewSnapshotDiffAnchor(entry.HeaderGroupStateKey, focusList: true);
        PrepareMarkupReviewSnapshotDiffSelectionRetention(GetSelectedMarkupReviewSnapshotDiffEntryKey());

        if (_collapsedMarkupReviewSnapshotDiffGroupKeys.Contains(entry.HeaderGroupStateKey))
            _collapsedMarkupReviewSnapshotDiffGroupKeys.Remove(entry.HeaderGroupStateKey);
        else
            _collapsedMarkupReviewSnapshotDiffGroupKeys.Add(entry.HeaderGroupStateKey);

        UpdateSelectedMarkupReviewSnapshotUi(GetSelectedMarkupReviewSnapshot());
    }

    private bool TryCollapseOtherMarkupReviewSnapshotDiffGroups(MarkupReviewSnapshotDiffEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.HeaderGroupStateKey) || !entry.HeaderCanCollapseOthers)
            return false;

        var snapshot = GetSelectedMarkupReviewSnapshot();
        if (snapshot == null)
            return false;

        var comparison = BuildMarkupReviewSnapshotComparison(snapshot);
        var groupStateKeys = comparison.DiffEntries
            .Where(candidate => candidate.IsGroupHeader && !string.IsNullOrWhiteSpace(candidate.HeaderGroupStateKey))
            .Select(candidate => candidate.HeaderGroupStateKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (groupStateKeys.Count == 0)
            return false;

        PrepareMarkupReviewSnapshotDiffAnchor(entry.HeaderGroupStateKey, focusList: true);
        PrepareMarkupReviewSnapshotDiffSelectionRetention(GetSelectedMarkupReviewSnapshotDiffEntryKey());

        foreach (var groupStateKey in groupStateKeys)
        {
            if (string.Equals(groupStateKey, entry.HeaderGroupStateKey, StringComparison.OrdinalIgnoreCase))
                _collapsedMarkupReviewSnapshotDiffGroupKeys.Remove(groupStateKey);
            else
                _collapsedMarkupReviewSnapshotDiffGroupKeys.Add(groupStateKey);
        }

        UpdateSelectedMarkupReviewSnapshotUi(snapshot);
        return true;
    }

    private bool TrySetMarkupReviewSnapshotDiffGroupsCollapsed(bool collapseAll)
    {
        var snapshot = GetSelectedMarkupReviewSnapshot();
        if (snapshot == null)
            return false;

        var anchorGroupStateKey = GetMarkupReviewSnapshotDiffAnchorGroupStateKey();

        var comparison = BuildMarkupReviewSnapshotComparison(snapshot);
        var groupStateKeys = comparison.DiffEntries
            .Where(entry => entry.IsGroupHeader && !string.IsNullOrWhiteSpace(entry.HeaderGroupStateKey))
            .Select(entry => entry.HeaderGroupStateKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (groupStateKeys.Count == 0)
            return false;

        if (string.IsNullOrWhiteSpace(anchorGroupStateKey))
            anchorGroupStateKey = groupStateKeys[0];

        PrepareMarkupReviewSnapshotDiffAnchor(anchorGroupStateKey, focusList: true);
        PrepareMarkupReviewSnapshotDiffSelectionRetention(GetSelectedMarkupReviewSnapshotDiffEntryKey());

        foreach (var groupStateKey in groupStateKeys)
        {
            if (collapseAll)
                _collapsedMarkupReviewSnapshotDiffGroupKeys.Add(groupStateKey);
            else
                _collapsedMarkupReviewSnapshotDiffGroupKeys.Remove(groupStateKey);
        }

        UpdateSelectedMarkupReviewSnapshotUi(snapshot);
        return true;
    }

    private string? GetMarkupReviewSnapshotDiffAnchorGroupStateKey()
    {
        return MarkupReviewSnapshotDiffListBox?.Items
            .OfType<MarkupReviewSnapshotDiffEntry>()
            .FirstOrDefault(entry => entry.IsGroupHeader && !string.IsNullOrWhiteSpace(entry.HeaderGroupStateKey))
            ?.HeaderGroupStateKey;
    }

    private string? GetSelectedMarkupReviewSnapshotDiffEntryKey()
    {
        return (MarkupReviewSnapshotDiffListBox?.SelectedItem as MarkupReviewSnapshotDiffEntry) is { IsGroupHeader: false } entry
            ? entry.Key
            : null;
    }

    private void PrepareMarkupReviewSnapshotDiffAnchor(string? groupStateKey, bool focusList)
    {
        if (string.IsNullOrWhiteSpace(groupStateKey))
            return;

        _pendingMarkupReviewSnapshotDiffAnchorGroupStateKey = groupStateKey;
        _pendingMarkupReviewSnapshotDiffListFocus = focusList;
    }

    private void PrepareMarkupReviewSnapshotDiffSelectionRetention(string? entryKey)
    {
        if (string.IsNullOrWhiteSpace(entryKey))
            return;

        _pendingMarkupReviewSnapshotDiffSelectedEntryKey = entryKey;
    }

    private void ApplyMarkupReviewSnapshotDiffSelectionRetention()
    {
        if (MarkupReviewSnapshotDiffListBox == null)
            return;

        var entryKey = _pendingMarkupReviewSnapshotDiffSelectedEntryKey;
        _pendingMarkupReviewSnapshotDiffSelectedEntryKey = null;

        var retainedEntry = string.IsNullOrWhiteSpace(entryKey)
            ? null
            : MarkupReviewSnapshotDiffListBox.Items
                .OfType<MarkupReviewSnapshotDiffEntry>()
                .FirstOrDefault(entry => !entry.IsGroupHeader && string.Equals(entry.Key, entryKey, StringComparison.Ordinal));

        _isRestoringMarkupReviewSnapshotDiffSelection = true;
        try
        {
            MarkupReviewSnapshotDiffListBox.SelectedItem = retainedEntry;
        }
        finally
        {
            _isRestoringMarkupReviewSnapshotDiffSelection = false;
        }
    }

    private void ApplyMarkupReviewSnapshotDiffAnchor()
    {
        if (MarkupReviewSnapshotDiffListBox == null)
            return;

        var groupStateKey = _pendingMarkupReviewSnapshotDiffAnchorGroupStateKey;
        var focusList = _pendingMarkupReviewSnapshotDiffListFocus;
        _pendingMarkupReviewSnapshotDiffAnchorGroupStateKey = null;
        _pendingMarkupReviewSnapshotDiffListFocus = false;

        if (string.IsNullOrWhiteSpace(groupStateKey))
            return;

        var listBox = MarkupReviewSnapshotDiffListBox;
        listBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            var anchorEntry = listBox.Items
                .OfType<MarkupReviewSnapshotDiffEntry>()
                .FirstOrDefault(entry => entry.IsGroupHeader && string.Equals(entry.HeaderGroupStateKey, groupStateKey, StringComparison.OrdinalIgnoreCase))
                ?? listBox.Items.OfType<MarkupReviewSnapshotDiffEntry>().FirstOrDefault();
            if (anchorEntry == null)
                return;

            listBox.ScrollIntoView(anchorEntry);
            if (focusList)
                listBox.Focus();
        }), DispatcherPriority.Background);
    }

    private void ClearMarkupReviewSnapshotDiffGroupState(string snapshotId)
    {
        _collapsedMarkupReviewSnapshotDiffGroupKeys.RemoveWhere(key => key.StartsWith(snapshotId + "|", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeMarkupReviewSnapshotDiffSheetContextSortKey(string? sheetDisplayName)
        => string.IsNullOrWhiteSpace(sheetDisplayName) ? "~" : sheetDisplayName.Trim();

    private void RevealMarkupReviewSnapshotSheetContext(MarkupReviewSnapshotDiffEntry entry)
    {
        var targetSheet = ResolveMarkupReviewSnapshotDiffSheet(entry.SourceSheetId, entry.SourceSheetDisplayName);
        if (targetSheet == null)
        {
            ClearMarkupReviewSnapshotLiveSelection();
            return;
        }

        if (!ReferenceEquals(_viewModel.SelectedSheet, targetSheet))
            _viewModel.SelectSheet(targetSheet);

        ClearMarkupReviewSnapshotLiveSelection();
    }

    private void ClearMarkupReviewSnapshotLiveSelection()
    {
        _viewModel.MarkupTool.SelectedMarkup = null;
        SelectInspectorTab(MarkupsInspectorTab);
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
    }

    private bool CanResolveMarkupReviewSnapshotDiffSheet(string? sourceSheetId, string? sourceSheetDisplayName)
        => ResolveMarkupReviewSnapshotDiffSheet(sourceSheetId, sourceSheetDisplayName) != null;

    private DrawingSheet? ResolveMarkupReviewSnapshotDiffSheet(string? sourceSheetId, string? sourceSheetDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(sourceSheetId))
        {
            var sheetById = _viewModel.Sheets.FirstOrDefault(sheet => string.Equals(sheet.Id, sourceSheetId, StringComparison.Ordinal));
            if (sheetById != null)
                return sheetById;
        }

        if (!string.IsNullOrWhiteSpace(sourceSheetDisplayName))
        {
            var sheetByDisplayName = _viewModel.Sheets.FirstOrDefault(sheet => string.Equals(sheet.DisplayName, sourceSheetDisplayName, StringComparison.Ordinal));
            if (sheetByDisplayName != null)
                return sheetByDisplayName;
        }

        return null;
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
    IReadOnlyList<string>? MissingIssues = null,
    IReadOnlyList<MarkupReviewSnapshotDiffEntry>? DiffEntries = null)
{
    public IReadOnlyList<string> ChangedIssues { get; } = ChangedIssues ?? Array.Empty<string>();
    public IReadOnlyList<string> NewIssues { get; } = NewIssues ?? Array.Empty<string>();
    public IReadOnlyList<string> MissingIssues { get; } = MissingIssues ?? Array.Empty<string>();
    public IReadOnlyList<MarkupReviewSnapshotDiffEntry> DiffEntries { get; } = DiffEntries ?? Array.Empty<MarkupReviewSnapshotDiffEntry>();
}

internal sealed record MarkupReviewSnapshotDiffEntry(
    string Key,
    string CategoryKey,
    string CategoryDisplayText,
    string Title,
    string SheetContextText,
    string DisplaySheetContextText,
    string DetailText,
    string RevealHintText,
    string? CurrentMarkupId,
    string? SourceSheetId = null,
    string? SourceSheetDisplayName = null,
    string SheetContextSortKey = "~",
    int CategorySortOrder = int.MaxValue,
    bool IsGroupHeader = false,
    string HeaderGroupStateKey = "",
    string HeaderToggleText = "",
    string HeaderPriorityKey = "",
    string HeaderSheetTitleText = "",
    string HeaderIssueCountText = "",
    string HeaderChangedCountText = "",
    string HeaderNewCountText = "",
    string HeaderMissingCountText = "",
    string HeaderCollapseOthersText = "",
    bool HeaderCanCollapseOthers = false,
    bool HeaderShowsCollapseOthers = true)
{
    public bool CanRevealLiveMarkup => !string.IsNullOrWhiteSpace(CurrentMarkupId);
    public bool HasHeaderChangedCount => !string.IsNullOrWhiteSpace(HeaderChangedCountText);
    public bool HasHeaderNewCount => !string.IsNullOrWhiteSpace(HeaderNewCountText);
    public bool HasHeaderMissingCount => !string.IsNullOrWhiteSpace(HeaderMissingCountText);
}