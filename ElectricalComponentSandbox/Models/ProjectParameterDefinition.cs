using Newtonsoft.Json;

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ElectricalComponentSandbox.Models;

public enum ProjectParameterValueKind
{
    Length,
    Text
}

public enum ProjectParameterBindingTarget
{
    Width,
    Height,
    Depth,
    Elevation,
    Material,
    Manufacturer,
    PartNumber,
    ReferenceUrl
}

public static class ProjectParameterBindingTargetExtensions
{
    private static readonly ProjectParameterBindingTarget[] OrderedTargetValues =
    [
        ProjectParameterBindingTarget.Width,
        ProjectParameterBindingTarget.Height,
        ProjectParameterBindingTarget.Depth,
        ProjectParameterBindingTarget.Elevation,
        ProjectParameterBindingTarget.Material,
        ProjectParameterBindingTarget.Manufacturer,
        ProjectParameterBindingTarget.PartNumber,
        ProjectParameterBindingTarget.ReferenceUrl
    ];

    public static IReadOnlyList<ProjectParameterBindingTarget> OrderedTargets => OrderedTargetValues;

    public static ProjectParameterValueKind GetValueKind(this ProjectParameterBindingTarget target)
    {
        return target switch
        {
            ProjectParameterBindingTarget.Width => ProjectParameterValueKind.Length,
            ProjectParameterBindingTarget.Height => ProjectParameterValueKind.Length,
            ProjectParameterBindingTarget.Depth => ProjectParameterValueKind.Length,
            ProjectParameterBindingTarget.Elevation => ProjectParameterValueKind.Length,
            ProjectParameterBindingTarget.Material => ProjectParameterValueKind.Text,
            ProjectParameterBindingTarget.Manufacturer => ProjectParameterValueKind.Text,
            ProjectParameterBindingTarget.PartNumber => ProjectParameterValueKind.Text,
            ProjectParameterBindingTarget.ReferenceUrl => ProjectParameterValueKind.Text,
            _ => ProjectParameterValueKind.Length
        };
    }

    public static string GetDisplayName(this ProjectParameterBindingTarget target)
    {
        return target switch
        {
            ProjectParameterBindingTarget.Width => "Width",
            ProjectParameterBindingTarget.Height => "Height",
            ProjectParameterBindingTarget.Depth => "Depth",
            ProjectParameterBindingTarget.Elevation => "Elevation",
            ProjectParameterBindingTarget.Material => "Material",
            ProjectParameterBindingTarget.Manufacturer => "Manufacturer",
            ProjectParameterBindingTarget.PartNumber => "Part Number",
            ProjectParameterBindingTarget.ReferenceUrl => "Reference URL",
            _ => target.ToString()
        };
    }

    public static string GetShortDisplayName(this ProjectParameterBindingTarget target)
    {
        return target switch
        {
            ProjectParameterBindingTarget.Width => "W",
            ProjectParameterBindingTarget.Height => "H",
            ProjectParameterBindingTarget.Depth => "D",
            ProjectParameterBindingTarget.Elevation => "E",
            ProjectParameterBindingTarget.Material => "MAT",
            ProjectParameterBindingTarget.Manufacturer => "MFG",
            ProjectParameterBindingTarget.PartNumber => "PART",
            ProjectParameterBindingTarget.ReferenceUrl => "REF",
            _ => target.ToString().ToUpperInvariant()
        };
    }
}

public class ProjectParameterDefinition : INotifyPropertyChanged
{
    private string _name = "Project Parameter";
    private double _value = 1.0;
    private string _textValue = string.Empty;
    private ProjectParameterValueKind _valueKind;
    private string _formula = string.Empty;
    private string? _formulaError;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name
    {
        get => _name;
        set
        {
            var nextValue = value?.Trim() ?? string.Empty;
            if (string.Equals(_name, nextValue, StringComparison.Ordinal))
                return;

            _name = nextValue;
            OnPropertyChanged();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(_value - value) <= 1e-9)
                return;

            _value = value;
            OnPropertyChanged();
        }
    }

    public string TextValue
    {
        get => _textValue;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.Equals(_textValue, nextValue, StringComparison.Ordinal))
                return;

            _textValue = nextValue;
            OnPropertyChanged();
        }
    }

    public ProjectParameterValueKind ValueKind
    {
        get => _valueKind;
        set
        {
            if (_valueKind == value)
                return;

            _valueKind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SupportsFormula));
            OnPropertyChanged(nameof(HasFormula));
        }
    }

    public string Formula
    {
        get => _formula;
        set
        {
            var nextValue = value?.Trim() ?? string.Empty;
            if (string.Equals(_formula, nextValue, StringComparison.Ordinal))
                return;

            _formula = nextValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFormula));
        }
    }

    [JsonIgnore]
    public bool SupportsFormula => ValueKind == ProjectParameterValueKind.Length;

    [JsonIgnore]
    public bool HasFormula => SupportsFormula && !string.IsNullOrWhiteSpace(Formula);

    public string GetValueText(Func<double, string>? lengthFormatter = null)
    {
        return ValueKind switch
        {
            ProjectParameterValueKind.Text => TextValue,
            _ => lengthFormatter != null
                ? lengthFormatter(Value)
                : Value.ToString("0.###", CultureInfo.InvariantCulture)
        };
    }

    [JsonIgnore]
    public string? FormulaError
    {
        get => _formulaError;
        internal set
        {
            if (string.Equals(_formulaError, value, StringComparison.Ordinal))
                return;

            _formulaError = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}