using ElectricalComponentSandbox.Models;
using System.Globalization;

namespace ElectricalComponentSandbox.Services;

internal readonly record struct ProjectParameterUsageSummary(int BindingCount, int ComponentCount, string TargetSummary)
{
    public static ProjectParameterUsageSummary Empty { get; } = new(0, 0, "-");
}

internal static class ProjectParameterScheduleSupport
{
    private sealed class MutableUsageSummary
    {
        public int BindingCount { get; set; }
        public int ComponentCount { get; set; }
        public HashSet<ProjectParameterBindingTarget> Targets { get; } = [];

        public ProjectParameterUsageSummary ToSummary()
            => new(BindingCount, ComponentCount, BuildTargetSummary(Targets));
    }

    private static readonly ProjectParameterBindingTarget[] OrderedTargets = ProjectParameterBindingTargetExtensions.OrderedTargets.ToArray();

    public static IReadOnlyDictionary<string, ProjectParameterDefinition> CreateParameterLookup(IEnumerable<ProjectParameterDefinition>? parameters)
    {
        return (parameters ?? Enumerable.Empty<ProjectParameterDefinition>())
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Id))
            .GroupBy(parameter => parameter.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, ProjectParameterUsageSummary> BuildUsageMap(IEnumerable<ElectricalComponent>? components)
    {
        var usageByParameterId = new Dictionary<string, MutableUsageSummary>(StringComparer.Ordinal);

        foreach (var component in components ?? Enumerable.Empty<ElectricalComponent>())
        {
            var parametersUsedByComponent = new HashSet<string>(StringComparer.Ordinal);

            foreach (var target in OrderedTargets)
            {
                var parameterId = component.Parameters.GetBinding(target);
                if (string.IsNullOrWhiteSpace(parameterId))
                    continue;

                if (!usageByParameterId.TryGetValue(parameterId, out var usage))
                {
                    usage = new MutableUsageSummary();
                    usageByParameterId.Add(parameterId, usage);
                }

                usage.BindingCount++;
                usage.Targets.Add(target);
                if (parametersUsedByComponent.Add(parameterId))
                    usage.ComponentCount++;
            }
        }

        return usageByParameterId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToSummary(),
            StringComparer.Ordinal);
    }

    public static string BuildComponentBindingSummary(ElectricalComponent component, IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        var bindings = OrderedTargets
            .Select(target => BuildBindingLabel(component.Parameters, target, parameterLookup))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        return bindings.Count == 0 ? "Direct" : string.Join("; ", bindings);
    }

    public static string FormatParameterValue(ProjectParameterDefinition parameter, bool useFriendlyLengthFormatting = false)
    {
        Func<double, string> formatter = parameter.ValueKind == ProjectParameterValueKind.Length && useFriendlyLengthFormatting
            ? value => UnitConversionService.FormatFeetInches(value)
            : static value => value.ToString("0.###", CultureInfo.InvariantCulture);
        return parameter.GetValueText(formatter);
    }

    public static string BuildComponentTagText(
        ElectricalComponent component,
        ProjectParameterBindingTarget target,
        IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup,
        bool useFriendlyLengthFormatting = false)
    {
        var valueText = ResolveComponentFieldValue(component, target, parameterLookup, useFriendlyLengthFormatting);
        return $"{target.GetDisplayName()}: {valueText}";
    }

    public static ProjectParameterUsageSummary GetUsage(ProjectParameterDefinition parameter, IReadOnlyList<ElectricalComponent> components)
    {
        var bindingCount = 0;
        var componentCount = 0;
        var targets = new HashSet<ProjectParameterBindingTarget>();

        foreach (var component in components)
        {
            var componentBindingCount = 0;
            foreach (var target in OrderedTargets)
            {
                if (!string.Equals(component.Parameters.GetBinding(target), parameter.Id, StringComparison.Ordinal))
                    continue;

                bindingCount++;
                componentBindingCount++;
                targets.Add(target);
            }

            if (componentBindingCount > 0)
                componentCount++;
        }

        var targetSummary = targets.Count == 0
            ? "-"
            : string.Join(", ",
                targets
                    .OrderBy(target => Array.IndexOf(OrderedTargets, target))
                    .Select(GetTargetLabel));

        return new ProjectParameterUsageSummary(bindingCount, componentCount, targetSummary);
    }

    public static string FormatUsageSummary(ProjectParameterUsageSummary usage)
        => usage.BindingCount == 0
            ? "Not bound"
            : $"{usage.ComponentCount} comp{(usage.ComponentCount == 1 ? string.Empty : "s")} / {usage.BindingCount} binding{(usage.BindingCount == 1 ? string.Empty : "s")}";

    private static string BuildTargetSummary(IReadOnlySet<ProjectParameterBindingTarget> targets)
    {
        if (targets.Count == 0)
            return "-";

        var labels = new List<string>(targets.Count);
        foreach (var target in OrderedTargets)
        {
            if (targets.Contains(target))
                labels.Add(GetTargetLabel(target));
        }

        return string.Join(", ", labels);
    }

    private static string? BuildBindingLabel(ComponentParameters parameters, ProjectParameterBindingTarget target, IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        var parameterId = parameters.GetBinding(target);
        if (string.IsNullOrWhiteSpace(parameterId))
            return null;

        var parameterName = parameterLookup.TryGetValue(parameterId, out var parameter)
            ? parameter.Name
            : "(missing)";
        return $"{GetTargetLabel(target)}={parameterName}";
    }

    private static string GetTargetLabel(ProjectParameterBindingTarget target)
    {
        return target.GetShortDisplayName();
    }

    private static string ResolveComponentFieldValue(
        ElectricalComponent component,
        ProjectParameterBindingTarget target,
        IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup,
        bool useFriendlyLengthFormatting)
    {
        var parameterId = component.Parameters.GetBinding(target);
        if (!string.IsNullOrWhiteSpace(parameterId) &&
            parameterLookup.TryGetValue(parameterId, out var parameter) &&
            parameter.ValueKind == target.GetValueKind())
        {
            return FormatParameterValue(parameter, useFriendlyLengthFormatting);
        }

        return target.GetValueKind() switch
        {
            ProjectParameterValueKind.Length => useFriendlyLengthFormatting
                ? UnitConversionService.FormatFeetInches(component.Parameters.GetLengthValue(target))
                : component.Parameters.GetLengthValue(target).ToString("0.###", CultureInfo.InvariantCulture),
            ProjectParameterValueKind.Text => component.Parameters.GetTextValue(target),
            _ => string.Empty
        };
    }
}