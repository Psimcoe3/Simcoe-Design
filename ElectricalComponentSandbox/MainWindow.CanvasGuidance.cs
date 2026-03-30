using System.Windows;
using System.Windows.Controls;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private const string PlanGuidanceActionImportReference = "import-reference";
    private const string PlanGuidanceActionAddConduit = "add-conduit";
    private const string PlanGuidanceActionFinishConduit = "finish-conduit";
    private const string PlanGuidanceActionCancelInteraction = "cancel-interaction";
    private const string PlanGuidanceActionFinishSketchLine = "finish-sketch-line";
    private const string PlanGuidanceActionExitSketchRectangle = "exit-sketch-rectangle";
    private const string PlanGuidanceActionConvertSketch = "convert-sketch";
    private const string PlanGuidanceActionExitConduitEdit = "exit-conduit-edit";

    internal void UpdateCanvasGuidanceForTesting() => UpdateCanvasGuidance();

    private void PlanCanvasGuidanceAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string actionKey })
            return;

        switch (actionKey)
        {
            case PlanGuidanceActionImportReference:
                ImportPdf_Click(sender, e);
                break;
            case PlanGuidanceActionAddConduit:
                AddConduit_Click(sender, e);
                break;
            case PlanGuidanceActionFinishConduit:
                FinishDrawingConduit();
                break;
            case PlanGuidanceActionCancelInteraction:
                TryCancelActiveInteraction(sender, e);
                break;
            case PlanGuidanceActionFinishSketchLine:
                SketchLine_Click(sender, e);
                break;
            case PlanGuidanceActionExitSketchRectangle:
                SketchRectangle_Click(sender, e);
                break;
            case PlanGuidanceActionConvertSketch:
                ConvertSketch_Click(sender, e);
                break;
            case PlanGuidanceActionExitConduitEdit:
                ToggleEditConduitPath_Click(sender, e);
                break;
        }

        UpdateCanvasGuidance();
    }

    private void UpdateCanvasGuidance()
    {
        if (_viewModel == null || PlanCanvasGuidanceCard == null || ViewportGuidanceCard == null ||
            PlanCanvasGuidancePrimaryActionButton == null || PlanCanvasGuidanceSecondaryActionButton == null)
        {
            return;
        }

        var selectedComponents = GetSelectedComponents();
        var hasSelectedMarkup = _viewModel.MarkupTool.SelectedMarkup != null;
        var hasAnyComponents = _viewModel.Components.Count > 0;
        var hasAnyMarkups = _viewModel.Markups.Count > 0;
        var hasReferenceUnderlay = _viewModel.PdfUnderlay != null;
        var shouldShowEmptyState = !hasAnyComponents && !hasAnyMarkups && selectedComponents.Count == 0 && !hasSelectedMarkup;

        UpdatePlanCanvasGuidance(shouldShowEmptyState, hasReferenceUnderlay);
        ViewportGuidanceCard.Visibility = shouldShowEmptyState ? Visibility.Visible : Visibility.Collapsed;

        ViewportGuidanceTitleTextBlock.Text = hasReferenceUnderlay ? "3D model is waiting for placed parts" : "3D workspace is empty";
        ViewportGuidanceSummaryTextBlock.Text = hasReferenceUnderlay
            ? "The underlay is loaded in plan, but there are no placed components yet to review in 3D."
            : "Place the first part to begin shaping the model in 3D.";
        ViewportGuidanceHintTextBlock.Text = hasReferenceUnderlay
            ? "Start in the 2D plan to trace the reference drawing, then return here to check fit and clearance."
            : "You can start in 2D to trace a reference drawing, then return here to verify elevation and fit.";
        UpdateMobileTopBarExperience();
    }

    private void UpdatePlanCanvasGuidance(bool shouldShowEmptyState, bool hasReferenceUnderlay)
    {
        if (_isDrawingConduit)
        {
            ShowPlanCanvasGuidance(
                "Drawing conduit route",
                "Click to place each bend point in plan. Double-click or choose Finish Route when the run is complete.",
                "Use Esc to cancel the route if you need to restart from a different point.",
                "Finish Route",
                PlanGuidanceActionFinishConduit,
                "Cancel",
                PlanGuidanceActionCancelInteraction);
            return;
        }

        if (_isEditingConduitPath)
        {
            ShowPlanCanvasGuidance(
                "Editing conduit path",
                "Drag bend handles to reshape the selected conduit run directly in the plan view.",
                "Choose Finish Path Edit when the route looks right, or press Esc to leave edit mode.",
                "Finish Path Edit",
                PlanGuidanceActionExitConduitEdit,
                "Cancel",
                PlanGuidanceActionCancelInteraction);
            return;
        }

        if (_isSketchLineMode)
        {
            ShowPlanCanvasGuidance(
                "Sketch line mode",
                "Click to place line vertices for a temporary sketch path. Choose Finish Sketch Line when the draft is complete.",
                "Hold Shift while placing the next point to constrain the segment angle, then convert the sketch into conduit or support geometry.",
                "Finish Sketch Line",
                PlanGuidanceActionFinishSketchLine,
                "Cancel",
                PlanGuidanceActionCancelInteraction);
            return;
        }

        if (_isSketchRectangleMode)
        {
            ShowPlanCanvasGuidance(
                "Sketch rectangle mode",
                "Drag in the plan view to define a sketch rectangle for later conversion into boxes, panels, or other rectangular geometry.",
                "Release the mouse to create the sketch, then select it and use Convert Sketch when you are ready.",
                "Leave Rectangle Mode",
                PlanGuidanceActionExitSketchRectangle,
                "Cancel",
                PlanGuidanceActionCancelInteraction);
            return;
        }

        if (_selectedSketchPrimitive != null)
        {
            ShowPlanCanvasGuidance(
                "Sketch is ready to convert",
                "A sketch primitive is selected and can be converted into conduit, equipment, or support geometry from the current plan view.",
                "Use Convert Sketch to turn the draft into modeled elements, or keep sketching if you still need construction lines.",
                "Convert Sketch",
                PlanGuidanceActionConvertSketch,
                "Add Conduit",
                PlanGuidanceActionAddConduit);
            return;
        }

        if (_isPendingMarkupVertexInsertion)
        {
            ShowPlanCanvasGuidance(
                "Markup vertex insertion",
                "Click a segment on the selected markup to insert a new vertex at that location.",
                "Use Esc to cancel if you selected the wrong issue or no longer need another control point.",
                "Cancel Insert",
                PlanGuidanceActionCancelInteraction,
                null,
                null);
            return;
        }

        if (_isFreehandDrawing)
        {
            ShowPlanCanvasGuidance(
                "Freehand conduit mode",
                "Continue dragging across the plan to sketch a conduit route with freehand input.",
                "Release the gesture to finish, or press Esc to cancel the freehand route.",
                "Cancel",
                PlanGuidanceActionCancelInteraction,
                null,
                null);
            return;
        }

        if (!shouldShowEmptyState)
        {
            PlanCanvasGuidanceCard.Visibility = Visibility.Collapsed;
            return;
        }

        ShowPlanCanvasGuidance(
            hasReferenceUnderlay ? "Reference drawing is ready" : "Start the 2D plan",
            hasReferenceUnderlay
                ? "Trace over the imported underlay by placing the first conduit run or equipment footprint in plan view."
                : "Import a drawing underlay or place the first conduit run to begin layout work.",
            hasReferenceUnderlay
                ? "Use the reference as your starting geometry, then switch back to 3D to verify elevation and routing."
                : "2D is the fastest place to trace, place, and review before checking the model in 3D.",
            "Import Reference",
            PlanGuidanceActionImportReference,
            "Add Conduit",
            PlanGuidanceActionAddConduit);
    }

    private void ShowPlanCanvasGuidance(
        string title,
        string summary,
        string hint,
        string primaryActionLabel,
        string primaryActionKey,
        string? secondaryActionLabel,
        string? secondaryActionKey)
    {
        PlanCanvasGuidanceCard.Visibility = Visibility.Visible;
        PlanCanvasGuidanceTitleTextBlock.Text = title;
        PlanCanvasGuidanceSummaryTextBlock.Text = summary;
        PlanCanvasGuidanceHintTextBlock.Text = hint;

        SetPlanCanvasGuidanceButton(PlanCanvasGuidancePrimaryActionButton, primaryActionLabel, primaryActionKey);

        if (!string.IsNullOrWhiteSpace(secondaryActionLabel) && !string.IsNullOrWhiteSpace(secondaryActionKey))
            SetPlanCanvasGuidanceButton(PlanCanvasGuidanceSecondaryActionButton, secondaryActionLabel, secondaryActionKey);
        else
            HidePlanCanvasGuidanceButton(PlanCanvasGuidanceSecondaryActionButton);
    }

    private static void SetPlanCanvasGuidanceButton(Button button, string label, string actionKey)
    {
        button.Visibility = Visibility.Visible;
        button.Content = label;
        button.Tag = actionKey;
    }

    private static void HidePlanCanvasGuidanceButton(Button button)
    {
        button.Visibility = Visibility.Collapsed;
        button.Tag = null;
    }
}