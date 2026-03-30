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

    internal void UpdateContextualInspectorForTesting() => UpdateContextualInspector();

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
        UpdateInspectorModeButtons();
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
}