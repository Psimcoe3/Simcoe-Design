using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ElectricalComponentSandbox.Models;

public enum ProjectParameterBindingTarget
{
    Width,
    Height,
    Depth,
    Elevation
}

public class ProjectParameterDefinition : INotifyPropertyChanged
{
    private string _name = "Project Parameter";
    private double _value = 1.0;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}