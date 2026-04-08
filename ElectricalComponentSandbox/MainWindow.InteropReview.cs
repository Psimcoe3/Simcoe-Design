using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    internal bool ApplyInteropReviewStateForTesting(ComponentInteropReviewStatus reviewStatus, string? actor = null, string? reviewNote = null)
        => TryApplyInteropReviewStateToSelection(reviewStatus, actor ?? "Test Reviewer", showFeedbackIfUnavailable: false, promptForNote: false, reviewNote: reviewNote);

    internal bool ClearInteropReviewStateForTesting()
        => TryClearInteropReviewStateFromSelection(showFeedbackIfUnavailable: false);

    internal void UpdateInteropReviewQueueForTesting()
        => UpdateInteropReviewQueueUi();

    internal IReadOnlyList<string> GetInteropReviewGroupDisplayNamesForTesting()
    {
        UpdateInteropReviewQueueUi();
        return InteropReviewGroupListBox?.Items
            .OfType<InteropReviewGroupItem>()
            .Select(item => item.DisplayName)
            .ToList() ?? new List<string>();
    }

    internal bool SelectInteropReviewGroupForTesting(string displayName)
    {
        UpdateInteropReviewQueueUi();
        if (InteropReviewGroupListBox == null)
            return false;

        var item = InteropReviewGroupListBox.Items
            .OfType<InteropReviewGroupItem>()
            .FirstOrDefault(candidate => string.Equals(candidate.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return false;

        InteropReviewGroupListBox.SelectedItem = item;
        return true;
    }

    internal bool ExecuteSelectInteropReviewGroupForTesting(bool reviewCandidatesOnly)
        => TrySelectInteropReviewGroup(reviewCandidatesOnly, showFeedbackIfUnavailable: false);

    internal string GetInteropReviewSummaryForTesting()
        => InteropReviewSummaryTextBlock?.Text ?? string.Empty;

    internal string GetSelectedInteropReviewGroupSummaryForTesting()
        => SelectedInteropReviewGroupSummaryTextBlock?.Text ?? string.Empty;

    internal string GetSelectedInteropReviewGroupActionSummaryForTesting()
        => SelectedInteropReviewGroupActionSummaryTextBlock?.Text ?? string.Empty;

    internal bool ApplyInteropReviewStateToSelectedGroupForTesting(ComponentInteropReviewStatus reviewStatus, string? actor = null, string? reviewNote = null)
        => TryApplyInteropReviewStateToGroup(reviewStatus, actor ?? "Test Reviewer", showFeedbackIfUnavailable: false, promptForNote: false, reviewNote: reviewNote);

    private void MarkInteropReviewed_Click(object sender, RoutedEventArgs e)
    {
        TryApplyInteropReviewStateToSelection(ComponentInteropReviewStatus.Reviewed, Environment.UserName, showFeedbackIfUnavailable: true, promptForNote: true);
    }

    private void MarkInteropNeedsChanges_Click(object sender, RoutedEventArgs e)
    {
        TryApplyInteropReviewStateToSelection(ComponentInteropReviewStatus.NeedsChanges, Environment.UserName, showFeedbackIfUnavailable: true, promptForNote: true);
    }

    private void ClearInteropReview_Click(object sender, RoutedEventArgs e)
    {
        TryClearInteropReviewStateFromSelection(showFeedbackIfUnavailable: true);
    }

    private bool TryApplyInteropReviewStateToSelection(
        ComponentInteropReviewStatus reviewStatus,
        string actor,
        bool showFeedbackIfUnavailable,
        bool promptForNote = false,
        string? reviewNote = null)
    {
        var selectedComponents = GetSelectedComponents()
            .Where(component => GetInteropSourceGroupKey(component.InteropMetadata) != null)
            .ToList();
        if (selectedComponents.Count == 0)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select one or more imported components first.", "Source Review",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (promptForNote)
        {
            reviewNote = PromptInteropReviewNote(reviewStatus, selectedComponents.Count);
            if (reviewNote == null)
                return false;
        }

        var utcNow = DateTime.UtcNow;
        var trimmedActor = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor.Trim();
        var trimmedNote = reviewNote?.Trim() ?? string.Empty;

        foreach (var component in selectedComponents)
        {
            component.InteropMetadata.ReviewStatus = reviewStatus;
            component.InteropMetadata.ReviewedBy = trimmedActor;
            component.InteropMetadata.ReviewNote = trimmedNote;
            component.InteropMetadata.LastReviewedUtc = utcNow;
        }

        UpdatePropertiesPanel();
        UpdateContextualInspector();
        UpdateInteropReviewQueueUi();
        return true;
    }

    private bool TryClearInteropReviewStateFromSelection(bool showFeedbackIfUnavailable)
    {
        var selectedComponents = GetSelectedComponents()
            .Where(component => GetInteropSourceGroupKey(component.InteropMetadata) != null)
            .Where(component => HasRecordedInteropReview(component.InteropMetadata))
            .ToList();
        if (selectedComponents.Count == 0)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("No recorded source review metadata is attached to the current imported selection.", "Clear Source Review",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        foreach (var component in selectedComponents)
        {
            component.InteropMetadata.ReviewStatus = ComponentInteropReviewStatus.Unreviewed;
            component.InteropMetadata.ReviewedBy = string.Empty;
            component.InteropMetadata.ReviewNote = string.Empty;
            component.InteropMetadata.LastReviewedUtc = null;
        }

        UpdatePropertiesPanel();
        UpdateContextualInspector();
        UpdateInteropReviewQueueUi();
        return true;
    }

    private static string? PromptInteropReviewNote(ComponentInteropReviewStatus reviewStatus, int componentCount)
    {
        var actionText = reviewStatus == ComponentInteropReviewStatus.Reviewed ? "Mark Reviewed" : "Needs Changes";
        var defaultValue = reviewStatus == ComponentInteropReviewStatus.Reviewed
            ? "Reviewed imported source group."
            : "Imported source group needs follow-up changes.";
        return PromptInput(
            actionText,
            $"Enter an optional review note for {componentCount} imported component(s). Leave blank to store the review decision without a note:",
            defaultValue);
    }

    private void InteropReviewGroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedInteropReviewGroupKey = (InteropReviewGroupListBox?.SelectedItem as InteropReviewGroupItem)?.SourceGroupKey;
        UpdateSelectedInteropReviewGroupUi(InteropReviewGroupListBox?.SelectedItem as InteropReviewGroupItem);
    }

    private void SelectInteropReviewGroup_Click(object sender, RoutedEventArgs e)
    {
        TrySelectInteropReviewGroup(reviewCandidatesOnly: false, showFeedbackIfUnavailable: true);
    }

    private void SelectInteropReviewCandidates_Click(object sender, RoutedEventArgs e)
    {
        TrySelectInteropReviewGroup(reviewCandidatesOnly: true, showFeedbackIfUnavailable: true);
    }

    private void MarkInteropReviewGroupReviewed_Click(object sender, RoutedEventArgs e)
    {
        TryApplyInteropReviewStateToGroup(ComponentInteropReviewStatus.Reviewed, Environment.UserName, showFeedbackIfUnavailable: true, promptForNote: true);
    }

    private void MarkInteropReviewGroupNeedsChanges_Click(object sender, RoutedEventArgs e)
    {
        TryApplyInteropReviewStateToGroup(ComponentInteropReviewStatus.NeedsChanges, Environment.UserName, showFeedbackIfUnavailable: true, promptForNote: true);
    }

    private bool TrySelectInteropReviewGroup(bool reviewCandidatesOnly, bool showFeedbackIfUnavailable)
    {
        var selectedGroup = InteropReviewGroupListBox?.SelectedItem as InteropReviewGroupItem;
        if (selectedGroup == null)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select an imported review group first.", "Imported Component Review",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var nextSelection = _viewModel.Components
            .Where(component => string.Equals(GetInteropSourceGroupKey(component.InteropMetadata), selectedGroup.SourceGroupKey, StringComparison.OrdinalIgnoreCase))
            .Where(component => !reviewCandidatesOnly || NeedsInteropReview(component.InteropMetadata))
            .ToList();
        if (nextSelection.Count == 0)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("No imported components in the selected group currently need review.", "Imported Component Review",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        _viewModel.SetSelectedComponents(nextSelection, nextSelection[0]);
        SelectInspectorTab(PropertiesInspectorTab);
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        UpdateContextualInspector();
        return true;
    }

    private bool TryApplyInteropReviewStateToGroup(
        ComponentInteropReviewStatus reviewStatus,
        string actor,
        bool showFeedbackIfUnavailable,
        bool promptForNote = false,
        string? reviewNote = null)
    {
        var selectedGroup = InteropReviewGroupListBox?.SelectedItem as InteropReviewGroupItem;
        if (selectedGroup == null)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("Select an imported review group first.", "Imported Component Review",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var groupComponents = _viewModel.Components
            .Where(component => string.Equals(GetInteropSourceGroupKey(component.InteropMetadata), selectedGroup.SourceGroupKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (groupComponents.Count == 0)
        {
            if (showFeedbackIfUnavailable)
            {
                MessageBox.Show("The selected imported review group no longer has matching components.", "Imported Component Review",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (promptForNote)
        {
            reviewNote = PromptInteropReviewNote(reviewStatus, groupComponents.Count);
            if (reviewNote == null)
                return false;
        }

        var utcNow = DateTime.UtcNow;
        var trimmedActor = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor.Trim();
        var trimmedNote = reviewNote?.Trim() ?? string.Empty;

        foreach (var component in groupComponents)
        {
            component.InteropMetadata.ReviewStatus = reviewStatus;
            component.InteropMetadata.ReviewedBy = trimmedActor;
            component.InteropMetadata.ReviewNote = trimmedNote;
            component.InteropMetadata.LastReviewedUtc = utcNow;
        }

        UpdatePropertiesPanel();
        UpdateContextualInspector();
        UpdateInteropReviewQueueUi();
        return true;
    }

    private void UpdateInteropReviewQueueUi()
    {
        if (InteropReviewSummaryTextBlock == null ||
            InteropReviewGroupListBox == null ||
            SelectedInteropReviewGroupSummaryTextBlock == null ||
            SelectedInteropReviewGroupActionSummaryTextBlock == null ||
            SelectInteropReviewGroupButton == null ||
            SelectInteropReviewCandidatesButton == null ||
            MarkInteropReviewGroupReviewedButton == null ||
            MarkInteropReviewGroupNeedsChangesButton == null)
        {
            return;
        }

        var groups = BuildInteropReviewGroups();
        var retainedGroupKey = _selectedInteropReviewGroupKey;
        InteropReviewSummaryTextBlock.Text = BuildInteropReviewQueueSummary(groups);
        InteropReviewGroupListBox.ItemsSource = groups;

        var selectedGroup = groups.FirstOrDefault(group => string.Equals(group.SourceGroupKey, retainedGroupKey, StringComparison.OrdinalIgnoreCase))
            ?? groups.FirstOrDefault();

        InteropReviewGroupListBox.SelectedItem = selectedGroup;
        _selectedInteropReviewGroupKey = selectedGroup?.SourceGroupKey;
        UpdateSelectedInteropReviewGroupUi(selectedGroup);
    }

    private List<InteropReviewGroupItem> BuildInteropReviewGroups()
    {
        return _viewModel.Components
            .Where(component => GetInteropSourceGroupKey(component.InteropMetadata) != null)
            .GroupBy(component => GetInteropSourceGroupKey(component.InteropMetadata)!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First().InteropMetadata;
                var reviewCandidates = group.Count(component => NeedsInteropReview(component.InteropMetadata));
                var reviewed = group.Count(component => IsInteropReviewAcknowledged(component.InteropMetadata));
                var needsChanges = group.Count(component => component.InteropMetadata.ReviewStatus == ComponentInteropReviewStatus.NeedsChanges);
                var sourceSystem = NormalizeInteropText(first.SourceSystem) ?? "External";
                return new InteropReviewGroupItem
                {
                    SourceGroupKey = group.Key,
                    DisplayName = BuildInteropSourceGroupLabel(first),
                    SourceSystem = sourceSystem,
                    Count = group.Count(),
                    ReviewCandidateCount = reviewCandidates,
                    ReviewedCount = reviewed,
                    NeedsChangesCount = needsChanges,
                    SecondaryText = $"{sourceSystem} | {reviewCandidates} need review | {reviewed} reviewed",
                    BreakdownText = needsChanges > 0
                        ? $"{needsChanges} flagged as needing changes. Select review candidates to focus only the outstanding imported updates."
                        : "Select the source group to inspect all imported components tied to this source."
                };
            })
            .OrderByDescending(group => group.ReviewCandidateCount)
            .ThenByDescending(group => group.NeedsChangesCount)
            .ThenBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildInteropReviewQueueSummary(IReadOnlyList<InteropReviewGroupItem> groups)
    {
        if (groups.Count == 0)
            return "No imported components with recorded source metadata are available for review.";

        var totalComponents = groups.Sum(group => group.Count);
        var reviewCandidates = groups.Sum(group => group.ReviewCandidateCount);
        var reviewed = groups.Sum(group => group.ReviewedCount);
        var needsChanges = groups.Sum(group => group.NeedsChangesCount);
        return $"Imported components: {totalComponents} across {groups.Count} source group(s). {reviewCandidates} need review, {reviewed} reviewed, and {needsChanges} flagged as needing changes.";
    }

    private void UpdateSelectedInteropReviewGroupUi(InteropReviewGroupItem? selectedGroup)
    {
        if (SelectedInteropReviewGroupSummaryTextBlock == null ||
            SelectedInteropReviewGroupActionSummaryTextBlock == null ||
            SelectInteropReviewGroupButton == null ||
            SelectInteropReviewCandidatesButton == null ||
            MarkInteropReviewGroupReviewedButton == null ||
            MarkInteropReviewGroupNeedsChangesButton == null)
        {
            return;
        }

        if (selectedGroup == null)
        {
            SelectedInteropReviewGroupSummaryTextBlock.Text = "Select a source group to focus imported review work.";
            SelectedInteropReviewGroupActionSummaryTextBlock.Text = "The selected group can then drive a component selection or narrow the current work to only outstanding imported review candidates.";
            SelectInteropReviewGroupButton.IsEnabled = false;
            SelectInteropReviewCandidatesButton.IsEnabled = false;
            MarkInteropReviewGroupReviewedButton.IsEnabled = false;
            MarkInteropReviewGroupNeedsChangesButton.IsEnabled = false;
            return;
        }

        SelectedInteropReviewGroupSummaryTextBlock.Text = $"{selectedGroup.DisplayName} includes {selectedGroup.Count} imported component(s).";
        SelectedInteropReviewGroupActionSummaryTextBlock.Text = $"{selectedGroup.ReviewCandidateCount} need review, {selectedGroup.ReviewedCount} reviewed, and {selectedGroup.NeedsChangesCount} flagged as needing changes. Select the source group to inspect everything or isolate only review candidates.";
        SelectInteropReviewGroupButton.IsEnabled = true;
        SelectInteropReviewCandidatesButton.IsEnabled = selectedGroup.ReviewCandidateCount > 0;
        MarkInteropReviewGroupReviewedButton.IsEnabled = true;
        MarkInteropReviewGroupNeedsChangesButton.IsEnabled = true;
    }
}