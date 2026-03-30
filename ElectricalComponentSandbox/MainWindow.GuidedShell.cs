using System;
using System.Windows;
using System.Windows.Controls;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private const string WorkspaceOnboardingActionImportReference = "import-reference";
    private const string WorkspaceOnboardingActionOpenProject = "open-project";
    private const string WorkspaceOnboardingActionAddConduit = "add-conduit";
    private const string WorkspaceOnboardingActionDrawRoute = "draw-route";
    private const string WorkspaceOnboardingActionShowPlan = "show-plan";
    private const string WorkspaceOnboardingActionShowModel = "show-model";
    private const string WorkspaceOnboardingActionShowInspector = "show-inspector";

    private readonly struct WorkspaceOnboardingState(
        bool isVisible,
        string progressText,
        string titleText,
        string summaryText,
        string checklistText,
        string primaryActionLabel,
        string primaryActionKey,
        string secondaryActionLabel,
        string secondaryActionKey)
    {
        public static WorkspaceOnboardingState Hidden { get; } = new(
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);

        public bool IsVisible { get; } = isVisible;
        public string ProgressText { get; } = progressText;
        public string TitleText { get; } = titleText;
        public string SummaryText { get; } = summaryText;
        public string ChecklistText { get; } = checklistText;
        public string PrimaryActionLabel { get; } = primaryActionLabel;
        public string PrimaryActionKey { get; } = primaryActionKey;
        public string SecondaryActionLabel { get; } = secondaryActionLabel;
        public string SecondaryActionKey { get; } = secondaryActionKey;
    }

    internal void UpdateWorkspaceOverviewForTesting() => UpdateWorkspaceOverview();

    private void UpdateWorkspaceOverview()
    {
        if (WorkspaceOverviewTitleTextBlock == null || DataContext is not ViewModels.MainViewModel)
            return;

        var selectedComponents = GetSelectedComponents();
        var activeSheet = _viewModel.SelectedSheet;
        var activeView = ViewTabs.SelectedIndex == 0 ? "3D viewport" : "2D plan";
        var markupTool = _viewModel.MarkupTool;

        WorkspaceOverviewTitleTextBlock.Text = activeSheet == null
            ? "No active drawing sheet"
            : $"{activeSheet.DisplayName} is active";
        WorkspaceOverviewDetailTextBlock.Text = $"{_viewModel.Sheets.Count} sheet(s) | {_viewModel.Components.Count} component(s) | Working in {activeView}";
        WorkspaceFocusTextBlock.Text = BuildWorkspaceFocusSummary(selectedComponents.Count, markupTool.SelectedMarkup != null);
        WorkspaceNextStepTextBlock.Text = BuildWorkspaceNextStepSummary(selectedComponents.Count, markupTool.TotalCount);
        WorkspaceReviewTextBlock.Text = BuildWorkspaceReviewSummary(markupTool.TotalCount, markupTool.OpenCount, markupTool.FilteredCount);
        WorkspaceGuidanceTextBlock.Text = BuildWorkspaceGuidanceSummary(selectedComponents.Count, markupTool.SelectedMarkup != null);
        WorkspaceHintTextBlock.Text = BuildWorkspaceHintSummary(selectedComponents.Count, markupTool.TotalCount);
        UpdateWorkspaceOnboarding(selectedComponents.Count, markupTool.SelectedMarkup != null);
        UpdateMobileTopBarExperience();
    }

    private void WorkspaceOnboardingAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string actionKey })
            return;

        ExecuteWorkspaceOnboardingAction(actionKey, sender, e);
    }

    private void ExecuteWorkspaceOnboardingAction(string actionKey, object sender, RoutedEventArgs e)
    {
        switch (actionKey)
        {
            case WorkspaceOnboardingActionImportReference:
                ImportPdf_Click(sender, e);
                break;
            case WorkspaceOnboardingActionOpenProject:
                OpenProject_Click(sender, e);
                break;
            case WorkspaceOnboardingActionAddConduit:
                AddConduit_Click(sender, e);
                break;
            case WorkspaceOnboardingActionDrawRoute:
                DrawConduit_Click(sender, e);
                break;
            case WorkspaceOnboardingActionShowPlan:
                Show2DView_Click(sender, e);
                break;
            case WorkspaceOnboardingActionShowModel:
                Show3DView_Click(sender, e);
                break;
            case WorkspaceOnboardingActionShowInspector:
                if (_isMobileView)
                    SetMobilePane(MobilePane.Properties);
                SelectInspectorTab(PropertiesInspectorTab);
                break;
        }

        UpdateWorkspaceOverview();
    }

    private void DismissWorkspaceOnboarding_Click(object sender, RoutedEventArgs e)
    {
        _isWorkspaceOnboardingDismissed = true;
        UpdateWorkspaceOverview();
    }

    private void UpdateWorkspaceOnboarding(int selectedComponentCount, bool hasSelectedMarkup)
    {
        var onboardingCard = FindName("WorkspaceOnboardingCard") as Border;
        var progressTextBlock = FindName("WorkspaceOnboardingProgressTextBlock") as TextBlock;
        var titleTextBlock = FindName("WorkspaceOnboardingTitleTextBlock") as TextBlock;
        var summaryTextBlock = FindName("WorkspaceOnboardingSummaryTextBlock") as TextBlock;
        var checklistTextBlock = FindName("WorkspaceOnboardingChecklistTextBlock") as TextBlock;
        var primaryActionButton = FindName("WorkspaceOnboardingPrimaryActionButton") as Button;
        var secondaryActionButton = FindName("WorkspaceOnboardingSecondaryActionButton") as Button;

        if (onboardingCard == null || progressTextBlock == null || titleTextBlock == null ||
            summaryTextBlock == null || checklistTextBlock == null || primaryActionButton == null ||
            secondaryActionButton == null)
        {
            return;
        }

        var onboardingState = BuildWorkspaceOnboardingState(selectedComponentCount, hasSelectedMarkup);
        if (!onboardingState.IsVisible)
        {
            onboardingCard.Visibility = Visibility.Collapsed;
            return;
        }

        onboardingCard.Visibility = Visibility.Visible;
        progressTextBlock.Text = onboardingState.ProgressText;
        titleTextBlock.Text = onboardingState.TitleText;
        summaryTextBlock.Text = onboardingState.SummaryText;
        checklistTextBlock.Text = onboardingState.ChecklistText;
        SetWorkspaceOnboardingButton(primaryActionButton, onboardingState.PrimaryActionLabel, onboardingState.PrimaryActionKey);
        SetWorkspaceOnboardingButton(secondaryActionButton, onboardingState.SecondaryActionLabel, onboardingState.SecondaryActionKey);
    }

    private WorkspaceOnboardingState BuildWorkspaceOnboardingState(int selectedComponentCount, bool hasSelectedMarkup)
    {
        if (_isWorkspaceOnboardingDismissed)
            return WorkspaceOnboardingState.Hidden;

        var hasReferenceContext = _viewModel.PdfUnderlay != null || !string.IsNullOrWhiteSpace(_currentFilePath);
        var hasPlacedLayout = _viewModel.Components.Count > 0;
        var hasInspectedSelection = selectedComponentCount > 0 || hasSelectedMarkup;
        if (hasReferenceContext && hasPlacedLayout && hasInspectedSelection)
            return WorkspaceOnboardingState.Hidden;

        var checklistText = BuildWorkspaceOnboardingChecklist(
            hasReferenceContext,
            hasPlacedLayout,
            hasInspectedSelection);

        if (!hasReferenceContext)
        {
            return new WorkspaceOnboardingState(
                true,
                "Step 1 of 3",
                "Bring in project context",
                "Import a reference drawing or open an existing project so the plan starts from real geometry instead of a blank canvas.",
                checklistText,
                "Import Reference",
                WorkspaceOnboardingActionImportReference,
                "Open Project",
                WorkspaceOnboardingActionOpenProject);
        }

        if (!hasPlacedLayout)
        {
            return new WorkspaceOnboardingState(
                true,
                "Step 2 of 3",
                "Create the first layout element",
                "Place a conduit or start a routed run so the workspace has something tangible to edit and review.",
                checklistText,
                "Add Conduit",
                WorkspaceOnboardingActionAddConduit,
                "Draw Route",
                WorkspaceOnboardingActionDrawRoute);
        }

        return new WorkspaceOnboardingState(
            true,
            "Step 3 of 3",
            "Inspect what you placed",
            "Click a placed part or review note so the inspector can guide the next round of edits, dimensions, or markup work.",
            checklistText,
            "Show 2D Plan",
            WorkspaceOnboardingActionShowPlan,
            "Show 3D View",
            WorkspaceOnboardingActionShowModel);
    }

    private static string BuildWorkspaceOnboardingChecklist(bool hasReferenceContext, bool hasPlacedLayout, bool hasInspectedSelection)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Reference: {(hasReferenceContext ? "done" : "next")}",
            $"Layout: {(hasPlacedLayout ? "done" : hasReferenceContext ? "next" : "later")}",
            $"Inspect: {(hasInspectedSelection ? "done" : hasPlacedLayout ? "next" : "later")}"
        });
    }

    private static void SetWorkspaceOnboardingButton(Button button, string content, string actionKey)
    {
        button.Content = content;
        button.Tag = actionKey;
    }

    private string BuildWorkspaceFocusSummary(int selectedComponentCount, bool hasSelectedMarkup)
    {
        if (hasSelectedMarkup)
            return "A review markup is selected. Use the Markups tab to reply, assign ownership, or update status from one place.";

        if (selectedComponentCount > 1)
            return $"{selectedComponentCount} components are selected. Shared fields on the right update the whole group together.";

        if (selectedComponentCount == 1)
            return $"Editing {_viewModel.SelectedComponent?.Name ?? "the selected component"}. Use the Properties tab to adjust size, material, elevation, and references.";

        return "Nothing is selected yet. Pick a sheet, choose a part from the catalog, or click something in the canvas to start editing.";
    }

    private string BuildWorkspaceNextStepSummary(int selectedComponentCount, int reviewIssueCount)
    {
        if (_viewModel.Components.Count == 0 && reviewIssueCount == 0)
            return "Import a reference drawing or add the first component so the workspace has a concrete starting point.";

        if (selectedComponentCount > 0)
            return "Refine the current selection in the Properties tab, then switch to 2D Plan View to place or route it precisely.";

        if (reviewIssueCount > 0)
            return "Open the Markups tab to work through the next issue bucket, assign ownership, and close review items in order.";

        return "Choose a component from the catalog or start a conduit route so the project moves from setup into layout work.";
    }

    private static string BuildWorkspaceReviewSummary(int totalIssues, int openIssues, int filteredIssues)
    {
        if (totalIssues == 0)
            return "No review issues yet. Add callouts, stamps, or revision clouds when you need coordination notes or approvals.";

        return $"{totalIssues} issue(s) tracked, {openIssues} still open, {filteredIssues} visible in the current review slice.";
    }

    private string BuildWorkspaceGuidanceSummary(int selectedComponentCount, bool hasSelectedMarkup)
    {
        if (hasSelectedMarkup)
            return "You are in review mode. The Markups tab keeps replies, assignment, and approval history together for the selected issue.";

        if (selectedComponentCount > 1)
            return "You have a multi-selection. Fill only the shared fields you want to push across the selected set.";

        if (selectedComponentCount == 1)
            return "The right panel is now focused on the selected part. Update properties there, then reposition or dimension it in the plan view.";

        return "Start with a reference drawing, then place a few parts or sketch a conduit run so the project has a visible backbone.";
    }

    private static string BuildWorkspaceHintSummary(int selectedComponentCount, int reviewIssueCount)
    {
        if (selectedComponentCount > 0)
            return "Recommended path: edit the selection, verify dimensions, then capture any coordination notes in Markups.";

        if (reviewIssueCount > 0)
            return "Recommended path: review issue buckets from highest priority to lowest, then resolve or approve the visible slice.";

        return "Recommended path: import a sheet, add or route components, then review issues in the Markups tab.";
    }
}
