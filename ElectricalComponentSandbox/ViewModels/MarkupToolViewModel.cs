using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.ViewModels;

/// <summary>
/// Describes the active drawing / annotation tool — mirroring Bluebeam Revu toolbar modes.
/// </summary>
public enum MarkupToolMode
{
    None,           // Select / pan (default)
    Line,
    Polyline,
    Polygon,
    Rectangle,
    Circle,
    Arc,
    Text,
    Callout,        // Callout with leader line
    RevisionCloud,  // Revision cloud
    LeaderNote,     // Leader + note text
    Stamp,          // Approval / status stamp
    Hatch,          // Filled hatch region
    MeasureDistance,// Point-to-point distance readout
    MeasureArea,    // Click-polygon area readout
    Hyperlink,      // Click region that opens a URL
    Dimension,      // All dimension sub-types below are one mode; DimensionSubType selects
}

/// <summary>Sub-type for the Dimension tool mode</summary>
public enum DimensionSubType
{
    Linear,
    Aligned,
    Angular,
    Radial,
    Diameter,
    ArcLength
}

/// <summary>
/// ViewModel driving the markup / annotation toolbar and the active markups list panel.
///
/// Responsibilities:
///  - Track active tool mode and properties
///  - Maintain a filtered/sorted view of all markups
///  - Expose punch-list status filter and bulk status change
///  - Provide commands for each tool button
/// </summary>
public class MarkupToolViewModel : INotifyPropertyChanged
{
    // ── Backing state ─────────────────────────────────────────────────────────

    private readonly ObservableCollection<MarkupRecord> _allMarkups;
    private MarkupToolMode  _activeMode = MarkupToolMode.None;
    private DimensionSubType _dimSubType = DimensionSubType.Linear;
    private string          _statusFilter = "All";
    private string          _typeFilter   = "All";
    private string          _labelSearch  = string.Empty;
    private MarkupRecord?   _selectedMarkup;

    // ── Filtered view ─────────────────────────────────────────────────────────

    /// <summary>Markups currently visible in the list panel (after status + type + text filters)</summary>
    public ObservableCollection<MarkupRecord> FilteredMarkups { get; } = new();

    // ── Active tool ───────────────────────────────────────────────────────────

    public MarkupToolMode ActiveMode
    {
        get => _activeMode;
        set { _activeMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsToolActive)); ApplyFilter(); }
    }

    /// <summary>True when any markup tool (non-Select) is active</summary>
    public bool IsToolActive => _activeMode != MarkupToolMode.None;

    public DimensionSubType DimensionSubType
    {
        get => _dimSubType;
        set { _dimSubType = value; OnPropertyChanged(); }
    }

    // ── Default appearance for new markups ────────────────────────────────────

    public MarkupAppearance DefaultAppearance { get; } = new()
    {
        StrokeColor = "#FF0000",
        StrokeWidth = 2.0,
        FillColor   = "#40FF0000",
        Opacity     = 1.0,
        FontFamily  = "Arial",
        FontSize    = 10.0
    };

    // ── List filters ──────────────────────────────────────────────────────────

    /// <summary>"All", "Open", "InProgress", "Approved", "Void"</summary>
    public string StatusFilter
    {
        get => _statusFilter;
        set { _statusFilter = value; OnPropertyChanged(); ApplyFilter(); }
    }

    /// <summary>"All" or any MarkupType.ToString()</summary>
    public string TypeFilter
    {
        get => _typeFilter;
        set { _typeFilter = value; OnPropertyChanged(); ApplyFilter(); }
    }

    /// <summary>Free-text search against Label and TextContent</summary>
    public string LabelSearch
    {
        get => _labelSearch;
        set { _labelSearch = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public MarkupRecord? SelectedMarkup
    {
        get => _selectedMarkup;
        set { _selectedMarkup = value; OnPropertyChanged(); }
    }

    // ── Statistics (for status bar) ───────────────────────────────────────────

    public int TotalCount    => _allMarkups.Count;
    public int OpenCount     => _allMarkups.Count(m => m.Status == MarkupStatus.Open);
    public int InProgressCount => _allMarkups.Count(m => m.Status == MarkupStatus.InProgress);
    public int ApprovedCount => _allMarkups.Count(m => m.Status == MarkupStatus.Approved);

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SetToolNoneCommand     { get; }
    public ICommand SetToolLineCommand     { get; }
    public ICommand SetToolPolylineCommand { get; }
    public ICommand SetToolPolygonCommand  { get; }
    public ICommand SetToolRectangleCommand{ get; }
    public ICommand SetToolCircleCommand   { get; }
    public ICommand SetToolArcCommand      { get; }
    public ICommand SetToolTextCommand     { get; }
    public ICommand SetToolCalloutCommand  { get; }
    public ICommand SetToolRevCloudCommand { get; }
    public ICommand SetToolDimensionCommand{ get; }
    public ICommand SetToolMeasureDistCommand { get; }
    public ICommand SetToolMeasureAreaCommand { get; }
    public ICommand SetToolHatchCommand    { get; }
    public ICommand SetToolStampCommand    { get; }
    public ICommand SetToolHyperlinkCommand{ get; }
    public ICommand DeleteSelectedCommand  { get; }
    public ICommand ApproveSelectedCommand { get; }
    public ICommand RejectSelectedCommand  { get; }
    public ICommand SetAllStatusCommand    { get; }
    public ICommand ClearFiltersCommand    { get; }

    public MarkupToolViewModel(ObservableCollection<MarkupRecord> allMarkups)
    {
        _allMarkups = allMarkups;
        _allMarkups.CollectionChanged += (_, _) => { ApplyFilter(); OnCountsChanged(); };

        SetToolNoneCommand      = new RelayCommand(_ => ActiveMode = MarkupToolMode.None);
        SetToolLineCommand      = new RelayCommand(_ => ActiveMode = MarkupToolMode.Line);
        SetToolPolylineCommand  = new RelayCommand(_ => ActiveMode = MarkupToolMode.Polyline);
        SetToolPolygonCommand   = new RelayCommand(_ => ActiveMode = MarkupToolMode.Polygon);
        SetToolRectangleCommand = new RelayCommand(_ => ActiveMode = MarkupToolMode.Rectangle);
        SetToolCircleCommand    = new RelayCommand(_ => ActiveMode = MarkupToolMode.Circle);
        SetToolArcCommand       = new RelayCommand(_ => ActiveMode = MarkupToolMode.Arc);
        SetToolTextCommand      = new RelayCommand(_ => ActiveMode = MarkupToolMode.Text);
        SetToolCalloutCommand   = new RelayCommand(_ => ActiveMode = MarkupToolMode.Callout);
        SetToolRevCloudCommand  = new RelayCommand(_ => ActiveMode = MarkupToolMode.RevisionCloud);
        SetToolDimensionCommand = new RelayCommand(_ => ActiveMode = MarkupToolMode.Dimension);
        SetToolMeasureDistCommand = new RelayCommand(_ => ActiveMode = MarkupToolMode.MeasureDistance);
        SetToolMeasureAreaCommand = new RelayCommand(_ => ActiveMode = MarkupToolMode.MeasureArea);
        SetToolHatchCommand     = new RelayCommand(_ => ActiveMode = MarkupToolMode.Hatch);
        SetToolStampCommand     = new RelayCommand(_ => ActiveMode = MarkupToolMode.Stamp);
        SetToolHyperlinkCommand = new RelayCommand(_ => ActiveMode = MarkupToolMode.Hyperlink);

        DeleteSelectedCommand  = new RelayCommand(_ => DeleteSelected(), _ => SelectedMarkup != null);
        ApproveSelectedCommand = new RelayCommand(_ => SetSelectedStatus(MarkupStatus.Approved), _ => SelectedMarkup != null);
        RejectSelectedCommand  = new RelayCommand(_ => SetSelectedStatus(MarkupStatus.Void), _ => SelectedMarkup != null);

        SetAllStatusCommand = new RelayCommand(param =>
        {
            if (param is string s && Enum.TryParse<MarkupStatus>(s, out var status))
                SetAllVisible(status);
        });

        ClearFiltersCommand = new RelayCommand(_ =>
        {
            _statusFilter = "All";
            _typeFilter = "All";
            _labelSearch = string.Empty;
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(LabelSearch));
            ApplyFilter();
        });

        ApplyFilter();
    }

    // ── Filter logic ──────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var filtered = (IEnumerable<MarkupRecord>)_allMarkups;

        if (_statusFilter != "All" && Enum.TryParse<MarkupStatus>(_statusFilter, out var status))
            filtered = filtered.Where(m => m.Status == status);

        if (_typeFilter != "All" && Enum.TryParse<MarkupType>(_typeFilter, out var mtype))
            filtered = filtered.Where(m => m.Type == mtype);

        if (!string.IsNullOrWhiteSpace(_labelSearch))
        {
            var lower = _labelSearch.ToLowerInvariant();
            filtered = filtered.Where(m =>
                m.Metadata.Label.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                m.TextContent.Contains(lower, StringComparison.OrdinalIgnoreCase));
        }

        FilteredMarkups.Clear();
        foreach (var m in filtered)
            FilteredMarkups.Add(m);
    }

    // ── Punch-list operations ─────────────────────────────────────────────────

    private void DeleteSelected()
    {
        if (SelectedMarkup == null) return;
        _allMarkups.Remove(SelectedMarkup);
        SelectedMarkup = null;
    }

    private void SetSelectedStatus(MarkupStatus s)
    {
        if (SelectedMarkup == null) return;
        SelectedMarkup.Status = s;
        // Re-apply filter in case it changes visibility
        ApplyFilter();
        OnCountsChanged();
    }

    private void SetAllVisible(MarkupStatus status)
    {
        foreach (var m in FilteredMarkups.ToList())
            m.Status = status;
        OnCountsChanged();
    }

    /// <summary>
    /// Stamps markup <paramref name="record"/> with the current author and UTC time.
    /// Call when a new markup is committed.
    /// </summary>
    public void Stamp(MarkupRecord record, string author)
    {
        record.Metadata.Author      = author;
        record.Metadata.CreatedUtc  = DateTime.UtcNow;
        record.Metadata.ModifiedUtc = DateTime.UtcNow;
        record.Appearance.StrokeColor = DefaultAppearance.StrokeColor;
        record.Appearance.StrokeWidth = DefaultAppearance.StrokeWidth;
        record.Appearance.FillColor   = DefaultAppearance.FillColor;
        record.Appearance.Opacity     = DefaultAppearance.Opacity;
        record.Appearance.FontFamily  = DefaultAppearance.FontFamily;
        record.Appearance.FontSize    = DefaultAppearance.FontSize;
    }

    private void OnCountsChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(OpenCount));
        OnPropertyChanged(nameof(InProgressCount));
        OnPropertyChanged(nameof(ApprovedCount));
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
