using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.ViewModels;

public abstract class ProjectBrowserItemViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private string _displayName;

    protected ProjectBrowserItemViewModel(string displayName)
    {
        _displayName = displayName;
    }

    public string DisplayName
    {
        get => _displayName;
        protected set
        {
            _displayName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ProjectBrowserItemViewModel> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ProjectBrowserSheetItemViewModel : ProjectBrowserItemViewModel
{
    public ProjectBrowserSheetItemViewModel(DrawingSheet sheet)
        : base(sheet.DisplayName)
    {
        Sheet = sheet;
    }

    public DrawingSheet Sheet { get; }
}

public sealed class ProjectBrowserNamedViewItemViewModel : ProjectBrowserItemViewModel
{
    public ProjectBrowserNamedViewItemViewModel(DrawingSheet sheet, NamedView namedView)
        : base(namedView.Name)
    {
        Sheet = sheet;
        NamedView = namedView;
    }

    public DrawingSheet Sheet { get; }

    public NamedView NamedView { get; }
}