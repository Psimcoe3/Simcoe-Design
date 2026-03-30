using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private static readonly Brush ActiveInspectorButtonBackground = new SolidColorBrush(Color.FromRgb(221, 235, 255));
    private static readonly Brush ActiveInspectorButtonBorder = new SolidColorBrush(Color.FromRgb(120, 162, 214));
    private static readonly Brush InactiveInspectorButtonBackground = new SolidColorBrush(Colors.White);
    private static readonly Brush InactiveInspectorButtonBorder = new SolidColorBrush(Color.FromRgb(216, 222, 228));
    private const string InspectorActionImportReference = "import-reference";
    private const string InspectorActionAddConduit = "add-conduit";
    private const string InspectorActionShowReview = "show-review";
    private const string InspectorActionApplyProperties = "apply-properties";
    private const string InspectorActionZoomSelection = "zoom-selection";
    private const string InspectorActionDuplicateSelection = "duplicate-selection";
    private const string InspectorActionAddReply = "add-reply";
    private const string InspectorActionResolveMarkup = "resolve-markup";
    private const string InspectorActionAssignMarkup = "assign-markup";

    internal void UpdateContextualInspectorForTesting() => UpdateContextualInspector();

    private void InspectorActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string actionKey })
            return;

        switch (actionKey)
        {
            case InspectorActionImportReference:
                ImportPdf_Click(sender, e);
                break;
            case InspectorActionAddConduit:
                AddConduit_Click(sender, e);
                break;
            case InspectorActionShowReview:
                SelectInspectorTab(MarkupsInspectorTab);
                break;
            case InspectorActionApplyProperties:
                ApplyProperties_Click(sender, e);
                break;
            case InspectorActionZoomSelection:
                ZoomSelection_Click(sender, e);
                break;
            case InspectorActionDuplicateSelection:
                DuplicateComponent_Click(sender, e);
                break;
            case InspectorActionAddReply:
                AddMarkupReply_Click(sender, e);
                break;
            case InspectorActionResolveMarkup:
                ResolveMarkup_Click(sender, e);
                break;
            case InspectorActionAssignMarkup:
                AssignSelectedMarkup_Click(sender, e);
                break;
        }
    }

    private void ShowInspectorComponentMode_Click(object sender, RoutedEventArgs e)
    {
        SelectInspectorTab(PropertiesInspectorTab);
    }

    private void ShowInspectorMarkupMode_Click(object sender, RoutedEventArgs e)
    {
        SelectInspectorTab(MarkupsInspectorTab);
    }

    private void RightPanelTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, RightPanelTabs))
            return;

        UpdateContextualInspector(autoSelectTab: false);
    }

    private void SelectInspectorTab(TabItem? tab)
    {
        if (RightPanelTabs == null || tab == null)
            return;

        RightPanelTabs.SelectedItem = tab;
        UpdateContextualInspector(autoSelectTab: false);
    }

    private void UpdateContextualInspector(bool autoSelectTab = true)
    {
        if (_viewModel == null || InspectorTitleTextBlock == null || RightPanelTabs == null)
            return;

        var selectedComponents = GetSelectedComponents();
        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;

        if (autoSelectTab)
        {
            if (selectedMarkup != null)
                RightPanelTabs.SelectedItem = MarkupsInspectorTab;
            else if (selectedComponents.Count > 0 || RightPanelTabs.SelectedItem == null)
                RightPanelTabs.SelectedItem = PropertiesInspectorTab;
        }

        InspectorTitleTextBlock.Text = BuildInspectorTitle(selectedComponents.Count, selectedMarkup);
        InspectorSummaryTextBlock.Text = BuildInspectorSummary(selectedComponents.Count, selectedMarkup);
        InspectorHintTextBlock.Text = BuildInspectorHint(selectedComponents.Count, selectedMarkup);
        UpdateInspectorTaskActions(selectedComponents.Count, selectedMarkup);
        UpdateInspectorModeButtons();
        UpdateMobileTopBarExperience();
    }

    private string BuildInspectorTitle(int selectedComponentCount, MarkupRecord? selectedMarkup)
    {
        if (selectedMarkup != null)
            return "Markup Inspector";

        return selectedComponentCount switch
        {
            > 1 => "Selection Inspector",
            1 => "Component Inspector",
            _ => "Contextual Inspector"
        };
    }

    private string BuildInspectorSummary(int selectedComponentCount, MarkupRecord? selectedMarkup)
    {
        if (selectedMarkup != null)
        {
            var label = selectedMarkup.Metadata.Label;
            var subject = selectedMarkup.Metadata.Subject;
            var descriptor = !string.IsNullOrWhiteSpace(label)
                ? $"{label.Trim()} ({selectedMarkup.TypeDisplayText})"
                : !string.IsNullOrWhiteSpace(subject)
                    ? $"{subject.Trim()} ({selectedMarkup.TypeDisplayText})"
                    : selectedMarkup.TypeDisplayText;

            return $"Reviewing {descriptor} with status {selectedMarkup.StatusDisplayText}.";
        }

        if (selectedComponentCount > 1)
            return $"Editing {selectedComponentCount} selected components with shared fields and mixed-value protection.";

        if (selectedComponentCount == 1)
        {
            var component = GetSelectedComponents().First();
            return $"Editing {component.Name} in component details, dimensions, and catalog reference sections.";
        }

        return "Select a component or markup to focus the inspector on the task at hand.";
    }

    private string BuildInspectorHint(int selectedComponentCount, MarkupRecord? selectedMarkup)
    {
        if (selectedMarkup != null)
            return "Use Review & Markups for threaded replies, status updates, assignments, and geometry or appearance edits.";

        if (selectedComponentCount > 0)
            return "Use Component Details for transforms, shared properties, dimensions, and part references.";

        return "Choose Component Details when laying out equipment, or switch to Review & Markups when triaging review comments.";
    }

    private void UpdateInspectorTaskActions(int selectedComponentCount, MarkupRecord? selectedMarkup)
    {
        if (InspectorTaskTitleTextBlock == null || InspectorTaskSummaryTextBlock == null ||
            InspectorPrimaryActionButton == null || InspectorSecondaryActionButton == null || InspectorTertiaryActionButton == null)
        {
            return;
        }

        if (selectedMarkup != null)
        {
            InspectorTaskTitleTextBlock.Text = "Current Review Action";
            InspectorTaskSummaryTextBlock.Text = "Reply to the selected issue, move it toward resolution, or hand it to the right reviewer without leaving the inspector.";
            SetInspectorTaskButton(InspectorPrimaryActionButton, "Add Reply", InspectorActionAddReply, "Add a threaded review reply to the selected markup.");
            SetInspectorTaskButton(InspectorSecondaryActionButton, "Resolve", InspectorActionResolveMarkup, "Resolve the selected markup.");
            SetInspectorTaskButton(InspectorTertiaryActionButton, "Assign", InspectorActionAssignMarkup, "Assign the selected markup to a reviewer.");
            return;
        }

        if (selectedComponentCount > 0)
        {
            InspectorTaskTitleTextBlock.Text = selectedComponentCount > 1 ? "Current Selection Action" : "Current Component Action";
            InspectorTaskSummaryTextBlock.Text = selectedComponentCount > 1
                ? "Apply shared edits, zoom to the affected area, or duplicate the current selection while you refine the layout."
                : "Apply property edits, zoom to the component, or duplicate it while you continue placement and routing.";
            SetInspectorTaskButton(InspectorPrimaryActionButton,
                selectedComponentCount > 1 ? "Apply Shared Changes" : "Apply Changes",
                InspectorActionApplyProperties,
                "Apply the edits currently shown in Component Details.");
            SetInspectorTaskButton(InspectorSecondaryActionButton, "Zoom Selection", InspectorActionZoomSelection, "Zoom the active view to the current component selection.");
            SetInspectorTaskButton(InspectorTertiaryActionButton, "Duplicate", InspectorActionDuplicateSelection, "Duplicate the current component selection.");
            return;
        }

        InspectorTaskTitleTextBlock.Text = "Start Here";
        InspectorTaskSummaryTextBlock.Text = _viewModel.MarkupTool.TotalCount > 0
            ? "Import a reference, place the first conduit run, or open the review queue that is already waiting in the project."
            : "Bring in a reference, place the first conduit run, or jump into markup review once issues start coming in.";
        SetInspectorTaskButton(InspectorPrimaryActionButton, "Import Reference", InspectorActionImportReference, "Import a PDF or image underlay into the 2D plan.");
        SetInspectorTaskButton(InspectorSecondaryActionButton, "Add Conduit", InspectorActionAddConduit, "Start by placing a conduit component in the workspace.");
        SetInspectorTaskButton(InspectorTertiaryActionButton, "Review Markups", InspectorActionShowReview, "Switch the inspector to the review and markups workspace.");
    }

    private void UpdateInspectorModeButtons()
    {
        if (InspectorComponentModeButton == null || InspectorMarkupModeButton == null || RightPanelTabs == null)
            return;

        ApplyInspectorButtonState(InspectorComponentModeButton, ReferenceEquals(RightPanelTabs.SelectedItem, PropertiesInspectorTab));
        ApplyInspectorButtonState(InspectorMarkupModeButton, ReferenceEquals(RightPanelTabs.SelectedItem, MarkupsInspectorTab));
    }

    private static void ApplyInspectorButtonState(Button button, bool isActive)
    {
        button.Background = isActive ? ActiveInspectorButtonBackground : InactiveInspectorButtonBackground;
        button.BorderBrush = isActive ? ActiveInspectorButtonBorder : InactiveInspectorButtonBorder;
        button.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
    }

    private static void SetInspectorTaskButton(Button button, string content, string actionKey, string toolTip)
    {
        button.Visibility = Visibility.Visible;
        button.Content = content;
        button.Tag = actionKey;
        button.ToolTip = toolTip;
    }
}