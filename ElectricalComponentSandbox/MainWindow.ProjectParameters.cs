using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private sealed class ProjectParameterBindingOption
    {
        public string? ParameterId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
    }

    private sealed class ProjectParameterEditorItem
    {
        public string ParameterId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    private string? _selectedProjectParameterEditorId;
    private bool _isUpdatingProjectParameterEditor;
    private string? _projectParameterEditorValidationMessage;
    private string _projectParameterEditorPreviewText = string.Empty;

    internal void SetProjectParameterEditorVisibleForTesting(bool isVisible)
        => SetProjectParameterEditorVisibility(isVisible);

    internal void SetProjectParameterSearchForTesting(string searchText)
    {
        if (ProjectParameterSearchTextBox != null)
            ProjectParameterSearchTextBox.Text = searchText;
    }

    internal void SetProjectParameterValueKindForTesting(ProjectParameterValueKind valueKind)
        => SetProjectParameterEditorValueKind(valueKind);

    internal IReadOnlyList<string> GetVisibleProjectParameterNamesForTesting()
    {
        if (ProjectParameterListBox == null)
            return Array.Empty<string>();

        return ProjectParameterListBox.Items
            .OfType<ProjectParameterEditorItem>()
            .Select(item => item.Name)
            .ToList();
    }

    internal bool SelectProjectParameterForTesting(string parameterName)
    {
        if (ProjectParameterListBox == null)
            return false;

        var item = ProjectParameterListBox.Items
            .OfType<ProjectParameterEditorItem>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, parameterName, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return false;

        ProjectParameterListBox.SelectedItem = item;
        return true;
    }

    internal void SetProjectParameterEditorDraftForTesting(
        string name,
        string valueText,
        string formulaText = "",
        ProjectParameterValueKind? valueKind = null)
    {
        if (valueKind.HasValue)
            SetProjectParameterEditorValueKind(valueKind.Value);

        if (ProjectParameterNameEditorTextBox != null)
            ProjectParameterNameEditorTextBox.Text = name;

        if (ProjectParameterValueEditorTextBox != null)
            ProjectParameterValueEditorTextBox.Text = valueText;

        if (ProjectParameterFormulaEditorTextBox != null)
            ProjectParameterFormulaEditorTextBox.Text = formulaText;
    }

    internal void BeginNewProjectParameterDraftForTesting()
        => BeginNewProjectParameterDraft();

    internal bool SaveProjectParameterEditorForTesting()
    {
        SaveProjectParameterEditor();
        return true;
    }

    internal bool DeleteSelectedProjectParameterForTesting()
        => DeleteSelectedProjectParameter(confirmDelete: false, showFeedbackIfNone: false);

    internal (bool IsVisible, string SaveCaption, bool SaveEnabled, bool DeleteEnabled, string UsageText, string SelectedName, string FormulaText, bool ValueReadOnly, string PreviewText, string ValidationText, string SelectedValueKind) GetProjectParameterEditorStateForTesting()
    {
        return (
            ProjectParameterEditorPanel?.Visibility == Visibility.Visible,
            ProjectParameterSaveButton?.Content?.ToString() ?? string.Empty,
            ProjectParameterSaveButton?.IsEnabled == true,
            ProjectParameterDeleteButton?.IsEnabled == true,
            ProjectParameterUsageTextBlock?.Text ?? string.Empty,
            ProjectParameterNameEditorTextBox?.Text ?? string.Empty,
            ProjectParameterFormulaEditorTextBox?.Text ?? string.Empty,
            ProjectParameterValueEditorTextBox?.IsReadOnly == true,
            ProjectParameterPreviewTextBlock?.Text ?? string.Empty,
                ProjectParameterValidationTextBlock?.Text ?? string.Empty,
                GetProjectParameterEditorValueKind().ToString());
    }

    private void ManageProjectParameters_Click(object sender, RoutedEventArgs e)
        => SetProjectParameterEditorVisibility(ProjectParameterEditorPanel?.Visibility != Visibility.Visible);

    private void SetProjectParameterEditorVisibility(bool isVisible)
    {
        if (ProjectParameterEditorPanel == null || ManageProjectParametersButton == null)
            return;

        ProjectParameterEditorPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ManageProjectParametersButton.Content = isVisible
            ? "Hide Project Parameter Editor"
            : "Manage Project Parameters...";

        if (isVisible)
            RefreshProjectParameterEditor(selectFirstIfNeeded: true);
    }

    private void ProjectParameterSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProjectParameterEditor)
            return;

        RefreshProjectParameterEditor(selectFirstIfNeeded: false);
    }

    private void ProjectParameterFormulaEditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProjectParameterEditor)
            return;

        RefreshProjectParameterEditorDraftFeedback();
    }

    private void ProjectParameterNameEditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProjectParameterEditor)
            return;

        RefreshProjectParameterEditorDraftFeedback();
    }

    private void ProjectParameterValueEditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProjectParameterEditor)
            return;

        RefreshProjectParameterEditorDraftFeedback();
    }

    private void ProjectParameterValueKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProjectParameterEditor)
            return;

        var valueKind = GetProjectParameterEditorValueKind();
        if (valueKind != ProjectParameterValueKind.Length && ProjectParameterFormulaEditorTextBox != null)
        {
            _isUpdatingProjectParameterEditor = true;
            ProjectParameterFormulaEditorTextBox.Text = string.Empty;
            _isUpdatingProjectParameterEditor = false;
        }

        if (string.IsNullOrWhiteSpace(_selectedProjectParameterEditorId) && ProjectParameterValueEditorTextBox != null)
        {
            _isUpdatingProjectParameterEditor = true;
            ProjectParameterValueEditorTextBox.Text = GetSuggestedProjectParameterValueText(valueKind);
            _isUpdatingProjectParameterEditor = false;
        }

        RefreshProjectParameterEditorDraftFeedback();
    }

    private void ProjectParameterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProjectParameterEditor)
            return;

        if (ProjectParameterListBox?.SelectedItem is ProjectParameterEditorItem selectedItem)
        {
            LoadProjectParameterEditorSelection(selectedItem.ParameterId);
        }
        else if (!HasProjectParameterEditorDraft())
        {
            LoadNewProjectParameterDraft();
        }
    }

    private void NewProjectParameter_Click(object sender, RoutedEventArgs e)
        => BeginNewProjectParameterDraft();

    private void SaveProjectParameter_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveProjectParameterEditor();
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Property, "Failed to save project parameter", ex);
            MessageBox.Show($"Unable to save the project parameter: {ex.Message}", "Project Parameter Editor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteProjectParameter_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DeleteSelectedProjectParameter(confirmDelete: true, showFeedbackIfNone: true);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Property, "Failed to delete project parameter", ex);
            MessageBox.Show($"Unable to delete the project parameter: {ex.Message}", "Project Parameter Editor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseProjectParameterEditor_Click(object sender, RoutedEventArgs e)
        => SetProjectParameterEditorVisibility(isVisible: false);

    private void BeginNewProjectParameterDraft()
    {
        _selectedProjectParameterEditorId = null;
        if (ProjectParameterListBox != null)
        {
            _isUpdatingProjectParameterEditor = true;
            ProjectParameterListBox.SelectedItem = null;
            _isUpdatingProjectParameterEditor = false;
        }

        LoadNewProjectParameterDraft();
    }

    private void RefreshProjectParameterEditor(bool selectFirstIfNeeded)
    {
        if (ProjectParameterListBox == null)
            return;

        var items = BuildProjectParameterEditorItems().ToList();
        var preserveDraft = _selectedProjectParameterEditorId == null && HasProjectParameterEditorDraft();
        ProjectParameterEditorItem? selectedItem = null;
        if (!string.IsNullOrWhiteSpace(_selectedProjectParameterEditorId))
        {
            selectedItem = items.FirstOrDefault(item => string.Equals(item.ParameterId, _selectedProjectParameterEditorId, StringComparison.Ordinal));
        }

        if (selectedItem == null && selectFirstIfNeeded && items.Count > 0 && !preserveDraft)
            selectedItem = items[0];

        _isUpdatingProjectParameterEditor = true;
        ProjectParameterListBox.ItemsSource = items;
        ProjectParameterListBox.SelectedItem = selectedItem;
        _isUpdatingProjectParameterEditor = false;

        if (selectedItem != null)
        {
            LoadProjectParameterEditorSelection(selectedItem.ParameterId);
            return;
        }

        _selectedProjectParameterEditorId = null;
        if (!preserveDraft)
            LoadNewProjectParameterDraft();
        else
            UpdateProjectParameterEditorCommandState();
    }

    private IEnumerable<ProjectParameterEditorItem> BuildProjectParameterEditorItems()
    {
        var searchText = ProjectParameterSearchTextBox?.Text?.Trim() ?? string.Empty;
        var usageLookup = ProjectParameterScheduleSupport.BuildUsageMap(_viewModel.Components);

        return _viewModel.ProjectParameters
            .Where(parameter => string.IsNullOrWhiteSpace(searchText) ||
                                parameter.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(parameter =>
            {
                var usage = usageLookup.TryGetValue(parameter.Id, out var summary)
                    ? summary
                    : ProjectParameterUsageSummary.Empty;
                var bindingCount = usage.BindingCount;
                var componentCount = usage.ComponentCount;
                var componentSummary = componentCount == 1 ? "1 comp" : $"{componentCount} comps";
                var bindingSummary = bindingCount == 1 ? "1 binding" : $"{bindingCount} bindings";
                return new ProjectParameterEditorItem
                {
                    ParameterId = parameter.Id,
                    Name = parameter.Name,
                    DisplayName = $"[{parameter.ValueKind}] {(parameter.HasFormula ? "fx " : string.Empty)}{parameter.Name}{(string.IsNullOrWhiteSpace(parameter.FormulaError) ? string.Empty : " [invalid]")} = {parameter.GetValueText(FormatLengthForInput)} | {componentSummary} | {bindingSummary}"
                };
            });
    }

    private (int BindingCount, int ComponentCount) GetProjectParameterUsage(string parameterId)
    {
        var usageLookup = ProjectParameterScheduleSupport.BuildUsageMap(_viewModel.Components);
        return usageLookup.TryGetValue(parameterId, out var usage)
            ? (usage.BindingCount, usage.ComponentCount)
            : (0, 0);
    }

    private string GetSuggestedProjectParameterValueText(ProjectParameterValueKind valueKind)
    {
        var selectedComponent = _viewModel.SelectedComponent;
        return valueKind switch
        {
            ProjectParameterValueKind.Text => selectedComponent?.Parameters.Material ?? string.Empty,
            _ => FormatLengthForInput(selectedComponent?.Parameters.Width ?? 1.0)
        };
    }

    private bool HasProjectParameterEditorDraft()
        => ProjectParameterNameEditorTextBox != null && !string.IsNullOrWhiteSpace(ProjectParameterNameEditorTextBox.Text);

    private void LoadProjectParameterEditorSelection(string parameterId)
    {
        var parameter = _viewModel.GetProjectParameter(parameterId);
        if (parameter == null)
        {
            LoadNewProjectParameterDraft();
            return;
        }

        _selectedProjectParameterEditorId = parameter.Id;
        _isUpdatingProjectParameterEditor = true;
        if (ProjectParameterNameEditorTextBox != null)
            ProjectParameterNameEditorTextBox.Text = parameter.Name;
        if (ProjectParameterValueEditorTextBox != null)
            ProjectParameterValueEditorTextBox.Text = parameter.GetValueText(FormatLengthForInput);
        SetProjectParameterEditorValueKind(parameter.ValueKind);
        if (ProjectParameterFormulaEditorTextBox != null)
            ProjectParameterFormulaEditorTextBox.Text = parameter.Formula;
        _isUpdatingProjectParameterEditor = false;

        if (ProjectParameterUsageTextBlock != null)
        {
            var (bindingCount, componentCount) = GetProjectParameterUsage(parameter.Id);
            var usageSummary = bindingCount == 0
                ? $"Not bound yet. {BuildCompatibleTargetSummary(parameter.ValueKind)}"
                : $"Used by {bindingCount} field binding(s) across {componentCount} component(s).";
            var formulaSummary = parameter.HasFormula
                ? $" Formula: {parameter.Formula}"
                : parameter.ValueKind == ProjectParameterValueKind.Text
                    ? " Direct text parameter."
                    : " Fixed value parameter.";

            ProjectParameterUsageTextBlock.Text = string.IsNullOrWhiteSpace(parameter.FormulaError)
                ? usageSummary + formulaSummary
                : $"Formula error: {parameter.FormulaError} Current stored value remains {parameter.GetValueText(FormatLengthForInput)}.";
        }

            RefreshProjectParameterEditorDraftFeedback();
    }

    private void LoadNewProjectParameterDraft()
    {
        _selectedProjectParameterEditorId = null;
        var valueKind = GetProjectParameterEditorValueKind();
        _isUpdatingProjectParameterEditor = true;
        SetProjectParameterEditorValueKind(valueKind);
        if (ProjectParameterNameEditorTextBox != null)
            ProjectParameterNameEditorTextBox.Text = string.Empty;
        if (ProjectParameterValueEditorTextBox != null)
            ProjectParameterValueEditorTextBox.Text = GetSuggestedProjectParameterValueText(valueKind);
        if (ProjectParameterFormulaEditorTextBox != null)
            ProjectParameterFormulaEditorTextBox.Text = string.Empty;
        _isUpdatingProjectParameterEditor = false;

        if (ProjectParameterUsageTextBlock != null)
        {
            ProjectParameterUsageTextBlock.Text = _viewModel.ProjectParameters.Count == 0
                ? $"No project parameters exist yet. Create one to reuse the same dimensional or text value across multiple components. {BuildCompatibleTargetSummary(valueKind)}"
                : $"Enter a name and value, then add the parameter. {BuildCompatibleTargetSummary(valueKind)}";
        }

        RefreshProjectParameterEditorDraftFeedback();
    }

    private void UpdateProjectParameterValueInputState()
    {
        if (ProjectParameterValueEditorTextBox == null || ProjectParameterFormulaEditorTextBox == null)
            return;

        var valueKind = GetProjectParameterEditorValueKind();
        var supportsFormula = valueKind == ProjectParameterValueKind.Length;
        var hasFormula = supportsFormula && !string.IsNullOrWhiteSpace(ProjectParameterFormulaEditorTextBox.Text);
        ProjectParameterFormulaEditorTextBox.IsEnabled = supportsFormula;
        ProjectParameterFormulaEditorTextBox.ToolTip = supportsFormula
            ? null
            : "Text parameters store direct values and do not support formulas.";
        ProjectParameterValueEditorTextBox.IsReadOnly = hasFormula;
        ProjectParameterValueEditorTextBox.ToolTip = hasFormula
            ? "Formula-driven parameters recalculate the value on save. The value box is a computed preview."
            : null;
    }

    private void UpdateProjectParameterEditorCommandState()
    {
        var hasSelection = !string.IsNullOrWhiteSpace(_selectedProjectParameterEditorId);
        var hasName = !string.IsNullOrWhiteSpace(ProjectParameterNameEditorTextBox?.Text);
        if (ProjectParameterSaveButton != null)
        {
            ProjectParameterSaveButton.Content = hasSelection ? "Save Changes" : "Add Parameter";
            ProjectParameterSaveButton.IsEnabled = hasName && string.IsNullOrWhiteSpace(_projectParameterEditorValidationMessage);
        }
        if (ProjectParameterDeleteButton != null)
            ProjectParameterDeleteButton.IsEnabled = hasSelection;
    }

    private void RefreshProjectParameterEditorDraftFeedback()
    {
        UpdateProjectParameterValueInputState();

        if (ProjectParameterNameEditorTextBox == null ||
            ProjectParameterValueEditorTextBox == null ||
            ProjectParameterFormulaEditorTextBox == null)
        {
            return;
        }

        var nameText = ProjectParameterNameEditorTextBox.Text;
        var formulaText = ProjectParameterFormulaEditorTextBox.Text;
        var valueText = ProjectParameterValueEditorTextBox.Text;
        var valueKind = GetProjectParameterEditorValueKind();

        string? validationMessage = null;
        var previewText = string.Empty;

        if (!string.IsNullOrWhiteSpace(nameText) || !string.IsNullOrWhiteSpace(formulaText) || !string.IsNullOrWhiteSpace(_selectedProjectParameterEditorId))
        {
            double draftValue = 0.0;
            var draftTextValue = valueKind == ProjectParameterValueKind.Text ? valueText : string.Empty;
            if (valueKind == ProjectParameterValueKind.Length)
            {
                try
                {
                    draftValue = ParseLengthInput(valueText, "project parameter value");
                }
                catch (Exception ex)
                {
                    validationMessage = ex.Message;
                }
            }

            if (validationMessage == null)
            {
                var preview = _viewModel.PreviewProjectParameter(
                    nameText,
                    draftValue,
                    _selectedProjectParameterEditorId,
                    formulaText,
                    valueKind,
                    draftTextValue);
                validationMessage = preview.ErrorMessage;

                if (preview.HasFormula && string.IsNullOrWhiteSpace(validationMessage))
                {
                    var formattedPreview = FormatLengthForInput(preview.Value);
                    previewText = $"Computed preview: {formattedPreview}";
                    if (!string.Equals(ProjectParameterValueEditorTextBox.Text, formattedPreview, StringComparison.Ordinal))
                    {
                        _isUpdatingProjectParameterEditor = true;
                        ProjectParameterValueEditorTextBox.Text = formattedPreview;
                        _isUpdatingProjectParameterEditor = false;
                    }
                }
                else if (!preview.HasFormula && !string.IsNullOrWhiteSpace(nameText) && string.IsNullOrWhiteSpace(validationMessage))
                {
                    previewText = valueKind == ProjectParameterValueKind.Text
                        ? $"Direct value: {(string.IsNullOrWhiteSpace(preview.TextValue) ? "<empty>" : preview.TextValue)}"
                        : $"Direct value: {FormatLengthForInput(preview.Value)}";
                }
            }
        }

        _projectParameterEditorValidationMessage = validationMessage;
        _projectParameterEditorPreviewText = previewText;

        if (ProjectParameterPreviewTextBlock != null)
        {
            ProjectParameterPreviewTextBlock.Text = _projectParameterEditorPreviewText;
            ProjectParameterPreviewTextBlock.Visibility = string.IsNullOrWhiteSpace(_projectParameterEditorPreviewText)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        if (ProjectParameterValidationTextBlock != null)
        {
            ProjectParameterValidationTextBlock.Text = _projectParameterEditorValidationMessage ?? string.Empty;
            ProjectParameterValidationTextBlock.Visibility = string.IsNullOrWhiteSpace(_projectParameterEditorValidationMessage)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        UpdateProjectParameterEditorCommandState();
    }

    private void EnsureProjectParameterSearchShows(string parameterName)
    {
        if (ProjectParameterSearchTextBox == null)
            return;

        var searchText = ProjectParameterSearchTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText) || parameterName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            return;

        _isUpdatingProjectParameterEditor = true;
        ProjectParameterSearchTextBox.Text = string.Empty;
        _isUpdatingProjectParameterEditor = false;
    }

    private ProjectParameterDefinition SaveProjectParameterEditor()
    {
        if (ProjectParameterNameEditorTextBox == null || ProjectParameterValueEditorTextBox == null)
            throw new InvalidOperationException("Project parameter editor controls are not available.");

        var valueKind = GetProjectParameterEditorValueKind();
        var numericValue = valueKind == ProjectParameterValueKind.Length
            ? ParseLengthInput(ProjectParameterValueEditorTextBox.Text, "project parameter value")
            : 0.0;
        var isNewParameter = string.IsNullOrWhiteSpace(_selectedProjectParameterEditorId);
        var parameter = _viewModel.UpsertProjectParameter(
            ProjectParameterNameEditorTextBox.Text,
            numericValue,
            _selectedProjectParameterEditorId,
            formula: ProjectParameterFormulaEditorTextBox?.Text,
            valueKind: valueKind,
            textValue: valueKind == ProjectParameterValueKind.Text ? ProjectParameterValueEditorTextBox.Text : null);

        _selectedProjectParameterEditorId = parameter.Id;
        EnsureProjectParameterSearchShows(parameter.Name);
        ActionLogService.Instance.Log(LogCategory.Property,
            isNewParameter ? "Project parameter added" : "Project parameter updated",
            $"Name: {parameter.Name}, Value: {parameter.GetValueText(FormatLengthForInput)}");
        HandleProjectParameterCatalogChanged();
        RefreshProjectParameterEditor(selectFirstIfNeeded: false);
        return parameter;
    }

    private bool DeleteSelectedProjectParameter(bool confirmDelete, bool showFeedbackIfNone)
    {
        var parameter = _viewModel.GetProjectParameter(_selectedProjectParameterEditorId);
        if (parameter == null)
        {
            if (showFeedbackIfNone)
            {
                MessageBox.Show("Select a project parameter to delete.", "Project Parameter Editor", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (confirmDelete)
        {
            var confirmation = MessageBox.Show(
                $"Delete project parameter '{parameter.Name}'?\n\nBound components will keep their current numeric values and be unbound.",
                "Delete Project Parameter",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
                return false;
        }

        if (!_viewModel.RemoveProjectParameter(parameter.Id))
            return false;

        _selectedProjectParameterEditorId = null;
        ActionLogService.Instance.Log(LogCategory.Property, "Project parameter deleted", $"Name: {parameter.Name}");
        HandleProjectParameterCatalogChanged();
        RefreshProjectParameterEditor(selectFirstIfNeeded: true);
        return true;
    }

    private void HandleProjectParameterCatalogChanged()
    {
        UpdatePropertiesPanel();
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        UpdateContextualInspector();
    }

    private void RefreshProjectParameterBindingsUi(IReadOnlyList<ElectricalComponent> components, bool isMultiSelection)
    {
        if (WidthParameterBindingComboBox == null ||
            HeightParameterBindingComboBox == null ||
            DepthParameterBindingComboBox == null ||
            ElevationParameterBindingComboBox == null ||
            MaterialParameterBindingComboBox == null ||
            ManufacturerParameterBindingComboBox == null ||
            PartNumberParameterBindingComboBox == null ||
            ReferenceUrlParameterBindingComboBox == null ||
            ProjectParameterBindingSummaryTextBlock == null)
        {
            return;
        }

        ApplyProjectParameterBindingOptions(WidthParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.Width));
        ApplyProjectParameterBindingOptions(HeightParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.Height));
        ApplyProjectParameterBindingOptions(DepthParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.Depth));
        ApplyProjectParameterBindingOptions(ElevationParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.Elevation));
        ApplyProjectParameterBindingOptions(MaterialParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.Material));
        ApplyProjectParameterBindingOptions(ManufacturerParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.Manufacturer));
        ApplyProjectParameterBindingOptions(PartNumberParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.PartNumber));
        ApplyProjectParameterBindingOptions(ReferenceUrlParameterBindingComboBox, BuildProjectParameterBindingOptions(ProjectParameterBindingTarget.ReferenceUrl));

        if (components.Count == 0)
        {
            SelectProjectParameterBinding(WidthParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(HeightParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(DepthParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(ElevationParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(MaterialParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(ManufacturerParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(PartNumberParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(ReferenceUrlParameterBindingComboBox, parameterId: null, allowUnset: false);
            ProjectParameterBindingSummaryTextBlock.Text = BuildProjectParameterBindingSummaryText(components, isMultiSelection: false);
            return;
        }

        if (!isMultiSelection)
        {
            var component = components[0];
            SelectProjectParameterBinding(WidthParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.Width), allowUnset: false);
            SelectProjectParameterBinding(HeightParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.Height), allowUnset: false);
            SelectProjectParameterBinding(DepthParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.Depth), allowUnset: false);
            SelectProjectParameterBinding(ElevationParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.Elevation), allowUnset: false);
            SelectProjectParameterBinding(MaterialParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.Material), allowUnset: false);
            SelectProjectParameterBinding(ManufacturerParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.Manufacturer), allowUnset: false);
            SelectProjectParameterBinding(PartNumberParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.PartNumber), allowUnset: false);
            SelectProjectParameterBinding(ReferenceUrlParameterBindingComboBox, component.Parameters.GetBinding(ProjectParameterBindingTarget.ReferenceUrl), allowUnset: false);
        }
        else
        {
            SelectSharedProjectParameterBinding(WidthParameterBindingComboBox, components, ProjectParameterBindingTarget.Width);
            SelectSharedProjectParameterBinding(HeightParameterBindingComboBox, components, ProjectParameterBindingTarget.Height);
            SelectSharedProjectParameterBinding(DepthParameterBindingComboBox, components, ProjectParameterBindingTarget.Depth);
            SelectSharedProjectParameterBinding(ElevationParameterBindingComboBox, components, ProjectParameterBindingTarget.Elevation);
            SelectSharedProjectParameterBinding(MaterialParameterBindingComboBox, components, ProjectParameterBindingTarget.Material);
            SelectSharedProjectParameterBinding(ManufacturerParameterBindingComboBox, components, ProjectParameterBindingTarget.Manufacturer);
            SelectSharedProjectParameterBinding(PartNumberParameterBindingComboBox, components, ProjectParameterBindingTarget.PartNumber);
            SelectSharedProjectParameterBinding(ReferenceUrlParameterBindingComboBox, components, ProjectParameterBindingTarget.ReferenceUrl);
        }

        ProjectParameterBindingSummaryTextBlock.Text = BuildProjectParameterBindingSummaryText(components, isMultiSelection);

        if (ProjectParameterEditorPanel?.Visibility == Visibility.Visible)
            RefreshProjectParameterEditor(selectFirstIfNeeded: false);
    }

    private IReadOnlyList<ProjectParameterBindingOption> BuildProjectParameterBindingOptions(ProjectParameterBindingTarget target)
    {
        var options = new List<ProjectParameterBindingOption>
        {
            new() { ParameterId = null, DisplayName = "(Direct value)" }
        };

        options.AddRange(_viewModel.ProjectParameters
            .Where(parameter => parameter.ValueKind == target.GetValueKind())
            .Select(parameter => new ProjectParameterBindingOption
            {
                ParameterId = parameter.Id,
                DisplayName = $"{(parameter.HasFormula ? "fx " : string.Empty)}{parameter.Name}{(string.IsNullOrWhiteSpace(parameter.FormulaError) ? string.Empty : " [invalid]")} ({parameter.GetValueText(FormatLengthForInput)})"
            }));

        return options;
    }

    private static void ApplyProjectParameterBindingOptions(ComboBox comboBox, IReadOnlyList<ProjectParameterBindingOption> options)
    {
        comboBox.ItemsSource = options;
    }

    private static void SelectProjectParameterBinding(ComboBox comboBox, string? parameterId, bool allowUnset)
    {
        var options = comboBox.ItemsSource as IEnumerable<ProjectParameterBindingOption>;
        if (options == null)
        {
            comboBox.SelectedIndex = -1;
            return;
        }

        var selected = options.FirstOrDefault(option => string.Equals(option.ParameterId, parameterId, StringComparison.Ordinal));
        if (selected != null)
        {
            comboBox.SelectedItem = selected;
        }
        else
        {
            comboBox.SelectedIndex = allowUnset ? -1 : 0;
        }
    }

    private static void SelectSharedProjectParameterBinding(ComboBox comboBox, IReadOnlyList<ElectricalComponent> components, ProjectParameterBindingTarget target)
    {
        if (TryGetSharedProjectParameterBinding(components, target, out var parameterId))
        {
            SelectProjectParameterBinding(comboBox, parameterId, allowUnset: false);
        }
        else
        {
            comboBox.SelectedIndex = -1;
        }
    }

    private static bool TryGetSharedProjectParameterBinding(IReadOnlyList<ElectricalComponent> components, ProjectParameterBindingTarget target, out string? parameterId)
    {
        parameterId = components.Count == 0 ? null : components[0].Parameters.GetBinding(target);
        for (var index = 1; index < components.Count; index++)
        {
            if (!string.Equals(parameterId, components[index].Parameters.GetBinding(target), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private string BuildProjectParameterBindingSummaryText(IReadOnlyList<ElectricalComponent> components, bool isMultiSelection)
    {
        if (_viewModel.ProjectParameters.Count == 0)
            return "No project parameters are defined yet. Use Manage Project Parameters to create reusable length or text controls for dimensions and metadata fields.";

        if (components.Count == 0)
            return $"{_viewModel.ProjectParameters.Count} project parameter(s) available for compatible component fields.";

        if (!isMultiSelection)
        {
            var component = components[0];
            var boundFields = GetBoundFieldDescriptions(component).ToList();
            if (boundFields.Count == 0)
                return $"{_viewModel.ProjectParameters.Count} project parameter(s) available. This component is currently using direct values.";

            return $"Bound fields: {string.Join("; ", boundFields)}. Editing a bound field updates its project parameter for every component using that binding.";
        }

        var sharedDescriptions = new List<string>();
        var hasMixedBindings = false;

        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.Width, "Width", ref hasMixedBindings);
        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.Height, "Height", ref hasMixedBindings);
        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.Depth, "Depth", ref hasMixedBindings);
        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.Elevation, "Elevation", ref hasMixedBindings);
        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.Material, "Material", ref hasMixedBindings);
        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.Manufacturer, "Manufacturer", ref hasMixedBindings);
        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.PartNumber, "Part Number", ref hasMixedBindings);
        AddSharedBindingDescription(sharedDescriptions, components, ProjectParameterBindingTarget.ReferenceUrl, "Reference URL", ref hasMixedBindings);

        if (sharedDescriptions.Count == 0 && !hasMixedBindings)
            return $"{_viewModel.ProjectParameters.Count} project parameter(s) available. The current selection is using direct values.";

        var parts = new List<string>();
        if (sharedDescriptions.Count > 0)
            parts.Add($"Shared bindings: {string.Join("; ", sharedDescriptions)}.");
        if (hasMixedBindings)
            parts.Add("Blank binding pickers indicate mixed bindings across the selection.");

        parts.Add("Choose a parameter to bind the whole selection or leave a picker unchanged to preserve each component's current binding.");
        return string.Join(" ", parts);
    }

    private IEnumerable<string> GetBoundFieldDescriptions(ElectricalComponent component)
    {
        var width = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.Width));
        if (width != null)
            yield return $"Width -> {width}";

        var height = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.Height));
        if (height != null)
            yield return $"Height -> {height}";

        var depth = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.Depth));
        if (depth != null)
            yield return $"Depth -> {depth}";

        var elevation = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.Elevation));
        if (elevation != null)
            yield return $"Elevation -> {elevation}";

        var material = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.Material));
        if (material != null)
            yield return $"Material -> {material}";

        var manufacturer = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.Manufacturer));
        if (manufacturer != null)
            yield return $"Manufacturer -> {manufacturer}";

        var partNumber = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.PartNumber));
        if (partNumber != null)
            yield return $"Part Number -> {partNumber}";

        var referenceUrl = DescribeProjectParameterBinding(component.Parameters.GetBinding(ProjectParameterBindingTarget.ReferenceUrl));
        if (referenceUrl != null)
            yield return $"Reference URL -> {referenceUrl}";
    }

    private void AddSharedBindingDescription(List<string> sharedDescriptions, IReadOnlyList<ElectricalComponent> components, ProjectParameterBindingTarget target, string fieldLabel, ref bool hasMixedBindings)
    {
        if (!TryGetSharedProjectParameterBinding(components, target, out var parameterId))
        {
            hasMixedBindings = true;
            return;
        }

        var description = DescribeProjectParameterBinding(parameterId);
        if (description != null)
            sharedDescriptions.Add($"{fieldLabel} -> {description}");
    }

    private string? DescribeProjectParameterBinding(string? parameterId)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
            return null;

        var parameter = _viewModel.GetProjectParameter(parameterId);
        return parameter == null
            ? $"missing parameter ({parameterId})"
            : parameter.Name;
    }

    private static bool TryGetExplicitProjectParameterBindingSelection(ComboBox comboBox, out string? parameterId)
    {
        if (comboBox.SelectedItem is ProjectParameterBindingOption option)
        {
            parameterId = option.ParameterId;
            return true;
        }

        parameterId = null;
        return false;
    }

    private ProjectParameterValueKind GetProjectParameterEditorValueKind()
    {
        if (ProjectParameterValueKindComboBox?.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            Enum.TryParse<ProjectParameterValueKind>(tag, out var valueKind))
        {
            return valueKind;
        }

        return ProjectParameterValueKind.Length;
    }

    private void SetProjectParameterEditorValueKind(ProjectParameterValueKind valueKind)
    {
        if (ProjectParameterValueKindComboBox == null)
            return;

        foreach (var item in ProjectParameterValueKindComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is not string tag || !Enum.TryParse<ProjectParameterValueKind>(tag, out var itemKind) || itemKind != valueKind)
                continue;

            ProjectParameterValueKindComboBox.SelectedItem = item;
            return;
        }
    }

    private static string BuildCompatibleTargetSummary(ProjectParameterValueKind valueKind)
    {
        var compatibleTargets = ProjectParameterBindingTargetExtensions.OrderedTargets
            .Where(target => target.GetValueKind() == valueKind)
            .Select(target => target.GetDisplayName())
            .ToList();

        return compatibleTargets.Count == 0
            ? "No compatible component fields are available yet."
            : $"Compatible component fields: {string.Join(", ", compatibleTargets)}.";
    }

    private void StageProjectParameterValueChange(
        string parameterId,
        double value,
        string fieldName,
        Dictionary<string, (double Value, string FieldName)> stagedValues,
        ISet<string>? impactedDimensionalParameterIds = null)
    {
        var parameter = _viewModel.GetProjectParameter(parameterId)
            ?? throw new InvalidOperationException($"Project parameter '{parameterId}' no longer exists.");

        if (parameter.ValueKind != ProjectParameterValueKind.Length)
            throw new InvalidOperationException($"Project parameter '{parameter.Name}' is not a length parameter and cannot be bound to {fieldName}.");

        if (stagedValues.TryGetValue(parameterId, out var existing) && Math.Abs(existing.Value - value) > 1e-9)
        {
            throw new InvalidOperationException(
                $"Project parameter '{parameter.Name}' cannot receive conflicting values from {existing.FieldName} and {fieldName} in the same apply action.");
        }

        stagedValues[parameterId] = (value, fieldName);
        impactedDimensionalParameterIds?.Add(parameterId);
    }

    private void StageProjectParameterTextValueChange(
        string parameterId,
        string value,
        string fieldName,
        Dictionary<string, (string Value, string FieldName)> stagedValues)
    {
        var parameter = _viewModel.GetProjectParameter(parameterId)
            ?? throw new InvalidOperationException($"Project parameter '{parameterId}' no longer exists.");

        if (parameter.ValueKind != ProjectParameterValueKind.Text)
            throw new InvalidOperationException($"Project parameter '{parameter.Name}' is not a text parameter and cannot be bound to {fieldName}.");

        if (stagedValues.TryGetValue(parameterId, out var existing) &&
            !string.Equals(existing.Value, value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Project parameter '{parameter.Name}' cannot receive conflicting values from {existing.FieldName} and {fieldName} in the same apply action.");
        }

        stagedValues[parameterId] = (value, fieldName);
    }

    private HashSet<string> CommitProjectParameterValueChanges(Dictionary<string, (double Value, string FieldName)> stagedValues)
    {
        var changedParameterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (parameterId, stagedValue) in stagedValues)
        {
            var parameter = _viewModel.GetProjectParameter(parameterId)
                ?? throw new InvalidOperationException($"Project parameter '{parameterId}' no longer exists.");

            if (parameter.ValueKind != ProjectParameterValueKind.Length)
                throw new InvalidOperationException($"Project parameter '{parameter.Name}' is not a length parameter.");

            if (Math.Abs(parameter.Value - stagedValue.Value) > 1e-9)
                parameter.Value = stagedValue.Value;

            changedParameterIds.Add(parameterId);
        }

        return changedParameterIds;
    }

    private HashSet<string> CommitProjectParameterTextValueChanges(Dictionary<string, (string Value, string FieldName)> stagedValues)
    {
        var changedParameterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (parameterId, stagedValue) in stagedValues)
        {
            var parameter = _viewModel.GetProjectParameter(parameterId)
                ?? throw new InvalidOperationException($"Project parameter '{parameterId}' no longer exists.");

            if (parameter.ValueKind != ProjectParameterValueKind.Text)
                throw new InvalidOperationException($"Project parameter '{parameter.Name}' is not a text parameter.");

            if (!string.Equals(parameter.TextValue, stagedValue.Value, StringComparison.Ordinal))
                parameter.TextValue = stagedValue.Value;

            changedParameterIds.Add(parameterId);
        }

        return changedParameterIds;
    }

    private IEnumerable<ElectricalComponent> GetComponentsImpactedByProjectParameters(IEnumerable<string> parameterIds)
    {
        var lookup = new HashSet<string>(parameterIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        if (lookup.Count == 0)
            return Enumerable.Empty<ElectricalComponent>();

        return _viewModel.Components.Where(component =>
            lookup.Contains(component.Parameters.GetBinding(ProjectParameterBindingTarget.Width) ?? string.Empty) ||
            lookup.Contains(component.Parameters.GetBinding(ProjectParameterBindingTarget.Height) ?? string.Empty) ||
            lookup.Contains(component.Parameters.GetBinding(ProjectParameterBindingTarget.Depth) ?? string.Empty));
    }

    private static void SetLengthValue(ElectricalComponent component, ProjectParameterBindingTarget target, double value)
    {
        component.Parameters.SetLengthValue(target, value);
    }
}