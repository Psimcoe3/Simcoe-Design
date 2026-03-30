using System.Linq;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
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
