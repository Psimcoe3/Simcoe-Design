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
    private string          _layerFilter  = "All";
    private string          _labelSearch  = string.Empty;
    private MarkupRecord?   _selectedMarkup;

    // ── Filtered view ─────────────────────────────────────────────────────────

    /// <summary>Markups currently visible in the list panel (after status + type + text filters)</summary>
    public ObservableCollection<MarkupRecord> FilteredMarkups { get; } = new();

    /// <summary>Valid status filter values for the Markups panel</summary>
    public IReadOnlyList<string> StatusFilterOptions { get; } = new[]
    {
        "All",
        MarkupRecord.GetStatusDisplayText(MarkupStatus.Open),
        MarkupRecord.GetStatusDisplayText(MarkupStatus.InProgress),
        MarkupRecord.GetStatusDisplayText(MarkupStatus.Resolved),
        MarkupRecord.GetStatusDisplayText(MarkupStatus.Approved),
        MarkupRecord.GetStatusDisplayText(MarkupStatus.Rejected),
        MarkupRecord.GetStatusDisplayText(MarkupStatus.Void)
    };

    /// <summary>Valid markup type filter values for the Markups panel</summary>
    public IReadOnlyList<string> TypeFilterOptions { get; } =
        new[] { "All" }
            .Concat(Enum.GetValues<MarkupType>().Select(MarkupRecord.GetTypeDisplayText))
            .ToArray();

    /// <summary>Distinct layer IDs represented by the current markup set</summary>
    public ObservableCollection<string> LayerFilterOptions { get; } = new() { "All" };

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

    /// <summary>"All", "Open", "In Progress", "Resolved", "Void"</summary>
    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            _statusFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
            if (_statusFilter != "All" && !TryParseStatusFilter(_statusFilter, out _))
                _statusFilter = "All";
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    /// <summary>"All" or any <see cref="MarkupType"/> display label.</summary>
    public string TypeFilter
    {
        get => _typeFilter;
        set
        {
            _typeFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
            if (_typeFilter != "All" && !TryParseTypeFilter(_typeFilter, out _))
                _typeFilter = "All";
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    /// <summary>"All" or an exact markup LayerId.</summary>
    public string LayerFilter
    {
        get => _layerFilter;
        set
        {
            _layerFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
            if (_layerFilter != "All" &&
                !LayerFilterOptions.Contains(_layerFilter, StringComparer.OrdinalIgnoreCase))
            {
                _layerFilter = "All";
            }

            OnPropertyChanged();
            ApplyFilter();
        }
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
        set
        {
            _selectedMarkup = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ── Statistics (for status bar) ───────────────────────────────────────────

    public int TotalCount      => _allMarkups.Count;
    public int OpenCount       => _allMarkups.Count(m => m.Status == MarkupStatus.Open);
    public int InProgressCount => _allMarkups.Count(m => m.Status == MarkupStatus.InProgress);
    public int ResolvedCount   => _allMarkups.Count(m => m.Status == MarkupStatus.Resolved);
    public int ApprovedCount   => _allMarkups.Count(m => m.Status == MarkupStatus.Approved);
    public int RejectedCount   => _allMarkups.Count(m => m.Status == MarkupStatus.Rejected);
    public int VoidCount       => _allMarkups.Count(m => m.Status == MarkupStatus.Void);
    public int FilteredCount   => FilteredMarkups.Count;

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
        _allMarkups.CollectionChanged += (_, _) =>
        {
            RefreshLayerFilterOptions();
            ApplyFilter();
            OnCountsChanged();
        };

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
        RejectSelectedCommand  = new RelayCommand(_ => SetSelectedStatus(MarkupStatus.Rejected), _ => SelectedMarkup != null);

        SetAllStatusCommand = new RelayCommand(param =>
        {
            if (param is string s && TryParseStatusFilter(s, out var status))
                SetAllVisible(status);
        });

        ClearFiltersCommand = new RelayCommand(_ =>
        {
            _statusFilter = "All";
            _typeFilter = "All";
            _layerFilter = "All";
            _labelSearch = string.Empty;
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(LayerFilter));
            OnPropertyChanged(nameof(LabelSearch));
            ApplyFilter();
            CommandManager.InvalidateRequerySuggested();
        });

        RefreshLayerFilterOptions();
        ApplyFilter();
    }

    // ── Filter logic ──────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var filtered = (IEnumerable<MarkupRecord>)_allMarkups;

        if (_statusFilter != "All" && TryParseStatusFilter(_statusFilter, out var status))
            filtered = filtered.Where(m => m.Status == status);

        if (_typeFilter != "All" && TryParseTypeFilter(_typeFilter, out var mtype))
            filtered = filtered.Where(m => m.Type == mtype);

        if (_layerFilter != "All")
            filtered = filtered.Where(m => string.Equals(m.LayerId, _layerFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(_labelSearch))
        {
            var lower = _labelSearch.ToLowerInvariant();
            filtered = filtered.Where(m =>
                m.Metadata.Label.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                m.TextContent.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                m.Metadata.Subject.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                m.LayerId.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                m.Metadata.Author.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                (m.StatusNote?.Contains(lower, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        filtered = filtered
            .OrderByDescending(m => m.Metadata.ModifiedUtc)
            .ThenByDescending(m => m.Metadata.CreatedUtc)
            .ThenBy(m => m.Metadata.Label, StringComparer.OrdinalIgnoreCase);

        FilteredMarkups.Clear();
        foreach (var m in filtered)
            FilteredMarkups.Add(m);

        OnPropertyChanged(nameof(FilteredCount));
        CommandManager.InvalidateRequerySuggested();
    }

    // ── Punch-list operations ─────────────────────────────────────────────────

    private void DeleteSelected()
    {
        if (SelectedMarkup == null) return;
        _allMarkups.Remove(SelectedMarkup);
        SelectedMarkup = null;
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetSelectedStatus(MarkupStatus s)
    {
        if (SelectedMarkup == null) return;
        SelectedMarkup.Status = s;
        SelectedMarkup.Metadata.ModifiedUtc = DateTime.UtcNow;
        // Re-apply filter in case it changes visibility
        ApplyFilter();
        OnCountsChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetAllVisible(MarkupStatus status)
    {
        foreach (var m in FilteredMarkups.ToList())
        {
            m.Status = status;
            m.Metadata.ModifiedUtc = DateTime.UtcNow;
        }

        ApplyFilter();
        OnCountsChanged();
        CommandManager.InvalidateRequerySuggested();
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
        OnPropertyChanged(nameof(ResolvedCount));
        OnPropertyChanged(nameof(ApprovedCount));
        OnPropertyChanged(nameof(RejectedCount));
        OnPropertyChanged(nameof(VoidCount));
        OnPropertyChanged(nameof(FilteredCount));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshLayerFilterOptions()
    {
        var layers = _allMarkups
            .Select(markup => markup.LayerId)
            .Where(layerId => !string.IsNullOrWhiteSpace(layerId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(layerId => layerId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        LayerFilterOptions.Clear();
        LayerFilterOptions.Add("All");
        foreach (var layerId in layers)
            LayerFilterOptions.Add(layerId);

        if (_layerFilter != "All" &&
            !LayerFilterOptions.Contains(_layerFilter, StringComparer.OrdinalIgnoreCase))
        {
            _layerFilter = "All";
            OnPropertyChanged(nameof(LayerFilter));
        }
    }

    private static bool TryParseStatusFilter(string filter, out MarkupStatus status)
    {
        foreach (var candidate in Enum.GetValues<MarkupStatus>())
        {
            if (string.Equals(filter, candidate.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, MarkupRecord.GetStatusDisplayText(candidate), StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
        }

        status = default;
        return false;
    }

    private static bool TryParseTypeFilter(string filter, out MarkupType type)
    {
        foreach (var candidate in Enum.GetValues<MarkupType>())
        {
            if (string.Equals(filter, candidate.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, MarkupRecord.GetTypeDisplayText(candidate), StringComparison.OrdinalIgnoreCase))
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
