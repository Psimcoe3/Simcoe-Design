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

    private void ManageProjectParameters_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = PromptInput(
                "Project Parameters",
                $"Current project parameters:\n\n{BuildProjectParameterCatalogPrompt()}\n\n" +
                "1. Add parameter\n" +
                "2. Update parameter\n" +
                "3. Delete parameter\n\n" +
                "Enter an action number:",
                "1");
            if (input == null)
                return;

            switch (input.Trim())
            {
                case "1":
                    AddProjectParameter();
                    break;
                case "2":
                    UpdateProjectParameter();
                    break;
                case "3":
                    DeleteProjectParameter();
                    break;
                default:
                    MessageBox.Show("Enter 1, 2, or 3.", "Project Parameters", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Property, "Project parameter management failed", ex);
            MessageBox.Show($"Unable to manage project parameters: {ex.Message}", "Project Parameters", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddProjectParameter()
    {
        var name = PromptInput("Add Project Parameter", "Parameter name:", $"Parameter {_viewModel.ProjectParameters.Count + 1}");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var valueInput = PromptInput(
            "Add Project Parameter",
            "Parameter value (feet-inches or decimal feet):",
            FormatLengthForInput(GetSuggestedProjectParameterValue()));
        if (valueInput == null)
            return;

        var value = ParseLengthInput(valueInput, "project parameter value");
        var parameter = _viewModel.UpsertProjectParameter(name, value);
        ActionLogService.Instance.Log(LogCategory.Property, "Project parameter added",
            $"Name: {parameter.Name}, Value: {parameter.Value:0.###}");
        HandleProjectParameterCatalogChanged();
    }

    private void UpdateProjectParameter()
    {
        var parameter = PromptForProjectParameterSelection("Update Project Parameter", "Select the parameter to update:");
        if (parameter == null)
            return;

        var name = PromptInput("Update Project Parameter", "Parameter name:", parameter.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var valueInput = PromptInput(
            "Update Project Parameter",
            "Parameter value (feet-inches or decimal feet):",
            FormatLengthForInput(parameter.Value));
        if (valueInput == null)
            return;

        var value = ParseLengthInput(valueInput, "project parameter value");
        _viewModel.UpsertProjectParameter(name, value, parameter.Id);
        ActionLogService.Instance.Log(LogCategory.Property, "Project parameter updated",
            $"Name: {name.Trim()}, Value: {value:0.###}");
        HandleProjectParameterCatalogChanged();
    }

    private void DeleteProjectParameter()
    {
        var parameter = PromptForProjectParameterSelection("Delete Project Parameter", "Select the parameter to delete:");
        if (parameter == null)
            return;

        var confirmation = MessageBox.Show(
            $"Delete project parameter '{parameter.Name}'?\n\nBound components will keep their current numeric values and be unbound.",
            "Delete Project Parameter",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
            return;

        if (_viewModel.RemoveProjectParameter(parameter.Id))
        {
            ActionLogService.Instance.Log(LogCategory.Property, "Project parameter deleted", $"Name: {parameter.Name}");
            HandleProjectParameterCatalogChanged();
        }
    }

    private ProjectParameterDefinition? PromptForProjectParameterSelection(string title, string prompt)
    {
        if (_viewModel.ProjectParameters.Count == 0)
        {
            MessageBox.Show("No project parameters are defined yet.", title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var list = BuildProjectParameterCatalogPrompt();
        var input = PromptInput(title, $"{prompt}\n\n{list}\n\nEnter a number or exact name:", "1");
        if (input == null)
            return null;

        if (int.TryParse(input.Trim(), out var ordinal) && ordinal >= 1 && ordinal <= _viewModel.ProjectParameters.Count)
            return _viewModel.ProjectParameters[ordinal - 1];

        return _viewModel.ProjectParameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, input.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private string BuildProjectParameterCatalogPrompt()
    {
        if (_viewModel.ProjectParameters.Count == 0)
            return "(none)";

        return string.Join(
            "\n",
            _viewModel.ProjectParameters.Select((parameter, index) =>
                $"{index + 1}. {parameter.Name} = {FormatLengthForInput(parameter.Value)}"));
    }

    private double GetSuggestedProjectParameterValue()
    {
        var selectedComponent = _viewModel.SelectedComponent;
        return selectedComponent?.Parameters.Width ?? 1.0;
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
            ProjectParameterBindingSummaryTextBlock == null)
        {
            return;
        }

        var options = BuildProjectParameterBindingOptions();
        ApplyProjectParameterBindingOptions(WidthParameterBindingComboBox, options);
        ApplyProjectParameterBindingOptions(HeightParameterBindingComboBox, options);
        ApplyProjectParameterBindingOptions(DepthParameterBindingComboBox, options);
        ApplyProjectParameterBindingOptions(ElevationParameterBindingComboBox, options);

        if (components.Count == 0)
        {
            SelectProjectParameterBinding(WidthParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(HeightParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(DepthParameterBindingComboBox, parameterId: null, allowUnset: false);
            SelectProjectParameterBinding(ElevationParameterBindingComboBox, parameterId: null, allowUnset: false);
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
        }
        else
        {
            SelectSharedProjectParameterBinding(WidthParameterBindingComboBox, components, ProjectParameterBindingTarget.Width);
            SelectSharedProjectParameterBinding(HeightParameterBindingComboBox, components, ProjectParameterBindingTarget.Height);
            SelectSharedProjectParameterBinding(DepthParameterBindingComboBox, components, ProjectParameterBindingTarget.Depth);
            SelectSharedProjectParameterBinding(ElevationParameterBindingComboBox, components, ProjectParameterBindingTarget.Elevation);
        }

        ProjectParameterBindingSummaryTextBlock.Text = BuildProjectParameterBindingSummaryText(components, isMultiSelection);
    }

    private IReadOnlyList<ProjectParameterBindingOption> BuildProjectParameterBindingOptions()
    {
        var options = new List<ProjectParameterBindingOption>
        {
            new() { ParameterId = null, DisplayName = "(Direct value)" }
        };

        options.AddRange(_viewModel.ProjectParameters
            .Select(parameter => new ProjectParameterBindingOption
            {
                ParameterId = parameter.Id,
                DisplayName = $"{parameter.Name} ({FormatLengthForInput(parameter.Value)})"
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
            return "No project parameters are defined yet. Use Manage Project Parameters to create reusable width, height, depth, or elevation controls.";

        if (components.Count == 0)
            return $"{_viewModel.ProjectParameters.Count} project parameter(s) available for component dimensions.";

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

    private void StageProjectParameterValueChange(
        string parameterId,
        double value,
        string fieldName,
        Dictionary<string, (double Value, string FieldName)> stagedValues,
        ISet<string>? impactedDimensionalParameterIds = null)
    {
        if (stagedValues.TryGetValue(parameterId, out var existing) && Math.Abs(existing.Value - value) > 1e-9)
        {
            var parameter = _viewModel.GetProjectParameter(parameterId);
            var displayName = parameter?.Name ?? parameterId;
            throw new InvalidOperationException(
                $"Project parameter '{displayName}' cannot receive conflicting values from {existing.FieldName} and {fieldName} in the same apply action.");
        }

        stagedValues[parameterId] = (value, fieldName);
        impactedDimensionalParameterIds?.Add(parameterId);
    }

    private HashSet<string> CommitProjectParameterValueChanges(Dictionary<string, (double Value, string FieldName)> stagedValues)
    {
        var changedParameterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (parameterId, stagedValue) in stagedValues)
        {
            var parameter = _viewModel.GetProjectParameter(parameterId)
                ?? throw new InvalidOperationException($"Project parameter '{parameterId}' no longer exists.");

            if (Math.Abs(parameter.Value - stagedValue.Value) > 1e-9)
                parameter.Value = stagedValue.Value;

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
        switch (target)
        {
            case ProjectParameterBindingTarget.Width:
                component.Parameters.Width = value;
                break;
            case ProjectParameterBindingTarget.Height:
                component.Parameters.Height = value;
                break;
            case ProjectParameterBindingTarget.Depth:
                component.Parameters.Depth = value;
                break;
            case ProjectParameterBindingTarget.Elevation:
                component.Parameters.Elevation = value;
                break;
        }
    }
}