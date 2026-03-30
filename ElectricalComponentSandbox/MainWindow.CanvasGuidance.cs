using System.Windows;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    internal void UpdateCanvasGuidanceForTesting() => UpdateCanvasGuidance();

    private void UpdateCanvasGuidance()
    {
        if (_viewModel == null || PlanCanvasGuidanceCard == null || ViewportGuidanceCard == null)
            return;

        var selectedComponents = GetSelectedComponents();
        var hasSelectedMarkup = _viewModel.MarkupTool.SelectedMarkup != null;
        var hasAnyComponents = _viewModel.Components.Count > 0;
        var hasAnyMarkups = _viewModel.Markups.Count > 0;
        var hasReferenceUnderlay = _viewModel.PdfUnderlay != null;
        var shouldShowEmptyState = !hasAnyComponents && !hasAnyMarkups && selectedComponents.Count == 0 && !hasSelectedMarkup;

        PlanCanvasGuidanceCard.Visibility = shouldShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        ViewportGuidanceCard.Visibility = shouldShowEmptyState ? Visibility.Visible : Visibility.Collapsed;

        PlanCanvasGuidanceTitleTextBlock.Text = hasReferenceUnderlay ? "Reference drawing is ready" : "Start the 2D plan";
        PlanCanvasGuidanceSummaryTextBlock.Text = hasReferenceUnderlay
            ? "Trace over the imported underlay by placing the first conduit run or equipment footprint in plan view."
            : "Import a drawing underlay or place the first conduit run to begin layout work.";
        PlanCanvasGuidanceHintTextBlock.Text = hasReferenceUnderlay
            ? "Use the reference as your starting geometry, then switch back to 3D to verify elevation and routing."
            : "2D is the fastest place to trace, place, and review before checking the model in 3D.";

        ViewportGuidanceTitleTextBlock.Text = hasReferenceUnderlay ? "3D model is waiting for placed parts" : "3D workspace is empty";
        ViewportGuidanceSummaryTextBlock.Text = hasReferenceUnderlay
            ? "The underlay is loaded in plan, but there are no placed components yet to review in 3D."
            : "Place the first part to begin shaping the model in 3D.";
        ViewportGuidanceHintTextBlock.Text = hasReferenceUnderlay
            ? "Start in the 2D plan to trace the reference drawing, then return here to check fit and clearance."
            : "You can start in 2D to trace a reference drawing, then return here to verify elevation and fit.";
    }
}