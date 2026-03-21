using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Xml.Linq;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.ViewModels;

/// <summary>
/// ViewModel for the Layer Manager panel/dialog.
/// Mirrors Autodesk / Bluebeam layer manager: visible, locked, freeze, plot, color, lineweight, linetype.
/// </summary>
public class LayerManagerViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<Layer> _projectLayers;
    private LayerRowViewModel? _selectedRow;

    /// <summary>Flat list of layer rows shown in the layer manager grid</summary>
    public ObservableCollection<LayerRowViewModel> Rows { get; } = new();

    public LayerRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set { _selectedRow = value; OnPropertyChanged(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand AddLayerCommand   { get; }
    public ICommand DeleteLayerCommand{ get; }
    public ICommand RenameLayerCommand{ get; }
    public ICommand MoveUpCommand     { get; }
    public ICommand MoveDownCommand   { get; }
    public ICommand SelectAllCommand  { get; }

    public LayerManagerViewModel(ObservableCollection<Layer> projectLayers)
    {
        _projectLayers = projectLayers;

        AddLayerCommand    = new RelayCommand(_ => AddLayer());
        DeleteLayerCommand = new RelayCommand(_ => DeleteSelected(), _ => CanDeleteSelected());
        RenameLayerCommand = new RelayCommand(name => RenameSelected(name as string));
        MoveUpCommand      = new RelayCommand(_ => MoveUp(),   _ => CanMoveUp());
        MoveDownCommand    = new RelayCommand(_ => MoveDown(), _ => CanMoveDown());
        SelectAllCommand   = new RelayCommand(_ => SetAllVisible(true));

        Refresh();
        _projectLayers.CollectionChanged += (_, _) => Refresh();
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    /// <summary>Rebuilds the Rows collection from the project layer list</summary>
    public void Refresh()
    {
        // Preserve selection by id
        var selectedId = SelectedRow?.Layer.Id;
        Rows.Clear();

        foreach (var layer in _projectLayers)
        {
            var row = new LayerRowViewModel(layer);
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        SelectedRow = Rows.FirstOrDefault(r => r.Layer.Id == selectedId);
    }

    // ── Command implementations ───────────────────────────────────────────────

    private void AddLayer()
    {
        int index = _projectLayers.Count + 1;
        var layer = new Layer { Name = $"Layer {index}" };
        _projectLayers.Add(layer);
        SelectedRow = Rows.LastOrDefault();
    }

    private bool CanDeleteSelected() =>
        SelectedRow != null && SelectedRow.Layer.Id != "default";

    private void DeleteSelected()
    {
        if (SelectedRow == null || SelectedRow.Layer.Id == "default") return;
        _projectLayers.Remove(SelectedRow.Layer);
    }

    private void RenameSelected(string? newName)
    {
        if (SelectedRow == null || string.IsNullOrWhiteSpace(newName)) return;
        SelectedRow.Name = newName.Trim();
    }

    private bool CanMoveUp() => SelectedRow != null && Rows.IndexOf(SelectedRow) > 0;

    private void MoveUp()
    {
        if (SelectedRow == null) return;
        int idx = _projectLayers.IndexOf(SelectedRow.Layer);
        if (idx > 0)
        {
            _projectLayers.Move(idx, idx - 1);
            SelectedRow = Rows.FirstOrDefault(r => r.Layer == SelectedRow.Layer);
        }
    }

    private bool CanMoveDown() =>
        SelectedRow != null && Rows.IndexOf(SelectedRow) < Rows.Count - 1;

    private void MoveDown()
    {
        if (SelectedRow == null) return;
        int idx = _projectLayers.IndexOf(SelectedRow.Layer);
        if (idx >= 0 && idx < _projectLayers.Count - 1)
        {
            _projectLayers.Move(idx, idx + 1);
            SelectedRow = Rows.FirstOrDefault(r => r.Layer == SelectedRow.Layer);
        }
    }

    private void SetAllVisible(bool visible)
    {
        foreach (var row in Rows)
            row.IsVisible = visible;
    }

    // ── Bulk operations ───────────────────────────────────────────────────────

    public void FreezeAllExcept(string layerId)
    {
        foreach (var row in Rows)
            row.IsFrozen = row.Layer.Id != layerId;
    }

    public void SetPlotAll(bool plot)
    {
        foreach (var row in Rows)
            row.IsPlotted = plot;
    }

    // ── XML import / export (for DXF-like layer table sharing) ───────────────

    public string ExportXml()
    {
        var root = new XElement("LayerTable",
            Rows.Select(r => new XElement("Layer",
                new XAttribute("id",          r.Layer.Id),
                new XAttribute("name",        r.Name),
                new XAttribute("color",       r.Color),
                new XAttribute("lineWeight",  r.LineWeight),
                new XAttribute("lineType",    r.LineType),
                new XAttribute("visible",     r.IsVisible),
                new XAttribute("locked",      r.IsLocked),
                new XAttribute("frozen",      r.IsFrozen),
                new XAttribute("plotted",     r.IsPlotted))));

        return root.ToString();
    }

    public void ImportXml(string xml)
    {
        var root = XElement.Parse(xml);
        foreach (var el in root.Elements("Layer"))
        {
            string id = (string?)el.Attribute("id") ?? Guid.NewGuid().ToString();
            var existing = _projectLayers.FirstOrDefault(l => l.Id == id);
            if (existing != null)
            {
                existing.Name       = (string?)el.Attribute("name")       ?? existing.Name;
                existing.Color      = (string?)el.Attribute("color")      ?? existing.Color;
                existing.IsVisible  = bool.Parse((string?)el.Attribute("visible")  ?? "true");
                existing.IsLocked   = bool.Parse((string?)el.Attribute("locked")   ?? "false");
                existing.IsFrozen   = bool.Parse((string?)el.Attribute("frozen")   ?? "false");
                existing.IsPlotted  = bool.Parse((string?)el.Attribute("plotted")  ?? "true");

                if (double.TryParse((string?)el.Attribute("lineWeight"), out double lw))
                    existing.LineWeight = lw;

                if (Enum.TryParse<LineType>((string?)el.Attribute("lineType"), out var lt))
                    existing.LineType = lt;
            }
            else
            {
                var layer = new Layer { Id = id };
                layer.Name       = (string?)el.Attribute("name")       ?? "Imported";
                layer.Color      = (string?)el.Attribute("color")      ?? "#808080";
                layer.IsVisible  = bool.Parse((string?)el.Attribute("visible")  ?? "true");
                layer.IsLocked   = bool.Parse((string?)el.Attribute("locked")   ?? "false");
                layer.IsFrozen   = bool.Parse((string?)el.Attribute("frozen")   ?? "false");
                layer.IsPlotted  = bool.Parse((string?)el.Attribute("plotted")  ?? "true");
                Enum.TryParse((string?)el.Attribute("lineType"), out LineType lt2);
                layer.LineType = lt2;
                _projectLayers.Add(layer);
            }
        }

        Refresh();
    }

    // ── Row change propagation ────────────────────────────────────────────────

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Row VM writes directly into the Layer model — no further action needed
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>
/// Single row in the layer manager grid.  Wraps a <see cref="Layer"/> and exposes
/// bindable "checkbox / dropdown" properties that write directly through to the model.
/// </summary>
public class LayerRowViewModel : INotifyPropertyChanged
{
    public Layer Layer { get; }

    public LayerRowViewModel(Layer layer) => Layer = layer;

    public string Name
    {
        get => Layer.Name;
        set { Layer.Name = value; OnPropertyChanged(); }
    }

    public string Color
    {
        get => Layer.Color;
        set { Layer.Color = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => Layer.IsVisible;
        set { Layer.IsVisible = value; OnPropertyChanged(); }
    }

    public bool IsLocked
    {
        get => Layer.IsLocked;
        set { Layer.IsLocked = value; OnPropertyChanged(); }
    }

    public bool IsFrozen
    {
        get => Layer.IsFrozen;
        set { Layer.IsFrozen = value; OnPropertyChanged(); }
    }

    public bool IsPlotted
    {
        get => Layer.IsPlotted;
        set { Layer.IsPlotted = value; OnPropertyChanged(); }
    }

    public double LineWeight
    {
        get => Layer.LineWeight;
        set { Layer.LineWeight = value >= 0 ? value : 0; OnPropertyChanged(); }
    }

    public LineType LineType
    {
        get => Layer.LineType;
        set { Layer.LineType = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => Layer.Description;
        set { Layer.Description = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

// ── Minimal RelayCommand ──────────────────────────────────────────────────────

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => _execute(p);
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
