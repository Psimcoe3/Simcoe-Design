using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

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
            RefreshSelectedMarkupPresentation();
        }
    }

    public bool HasSelectedMarkup => _selectedMarkup != null;

    public bool HasStructuredSelection =>
        _selectedMarkup?.Metadata.CustomFields.ContainsKey(DrawingAnnotationMarkupService.AnnotationKindField) == true &&
        _selectedMarkup.Metadata.CustomFields.ContainsKey(DrawingAnnotationMarkupService.AnnotationTextRoleField);

    public string SelectedMarkupAnnotationKind => GetSelectedMarkupCustomField(DrawingAnnotationMarkupService.AnnotationKindField);

    public string SelectedMarkupAnnotationRole => GetSelectedMarkupCustomField(DrawingAnnotationMarkupService.AnnotationTextRoleField);

    public string SelectedMarkupAnnotationKey => GetSelectedMarkupCustomField(DrawingAnnotationMarkupService.AnnotationTextKeyField);

    public bool HasTextEditableSelection =>
        _selectedMarkup?.Type == MarkupType.Text &&
        HasStructuredSelection;

    public string SelectedMarkupTextEditSummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            return HasTextEditableSelection
                ? "Direct text edit available for structured schedule, legend, and title-block text"
                : "Direct text editing is currently available for structured schedule, legend, and title-block text only";
        }
    }

    public bool HasTextShortcutHint => HasTextEditableSelection;

    public string SelectedMarkupTextShortcutHint =>
        HasTextEditableSelection
            ? "Shortcut: F2"
            : string.Empty;

    public bool HasSelectedMarkupTextDetails => HasTextEditableSelection;

    public string SelectedMarkupTextDetails
    {
        get
        {
            if (_selectedMarkup == null || !HasTextEditableSelection)
                return string.Empty;

            return string.IsNullOrWhiteSpace(_selectedMarkup.TextContent)
                ? "Current Value: <empty>"
                : $"Current Value: {_selectedMarkup.TextContent}";
        }
    }

    public bool HasPathEditableSelection =>
        _selectedMarkup is { } markup &&
        GetSelectionSet(markup).Count == 1 &&
        CanEditPath(markup);

    public string SelectedMarkupPathEditSummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            if (HasPathEditableSelection)
            {
                return CanInsertVertices(_selectedMarkup)
                    ? "Direct path edit available: drag grips to reposition points, or double-click a segment to insert a vertex"
                    : "Direct path edit available: drag grips to reposition points";
            }

            return GetSelectionSet(_selectedMarkup).Count > 1
                ? "Path editing is disabled for grouped selections"
                : "Path editing is currently available for polyline, polygon, callout, leader note, revision cloud, dimension, and measurement markups only";
        }
    }

    public bool HasPathShortcutHint =>
        HasPathEditableSelection &&
        _selectedMarkup != null &&
        CanDeleteVertex(_selectedMarkup);

    public string SelectedMarkupPathShortcutHint =>
        HasPathShortcutHint
            ? "Shortcut: Delete or Backspace removes the active vertex"
            : string.Empty;

    public bool HasSelectedMarkupPathDetails => HasPathEditableSelection;

    public string SelectedMarkupPathDetails
    {
        get
        {
            if (_selectedMarkup == null || !HasPathEditableSelection)
                return string.Empty;

            var lines = new List<string>
            {
                $"Vertices: {_selectedMarkup.Vertices.Count}",
                $"Minimum: {GetMinimumVertexCount(_selectedMarkup)}"
            };

            if (CanInsertVertices(_selectedMarkup))
                lines.Add("Insert: Double-click a segment");

            lines.Add(CanDeleteVertex(_selectedMarkup)
                ? "Delete: Active vertex can be removed"
                : "Delete: Minimum vertex count reached");

            return string.Join(Environment.NewLine, lines);
        }
    }

    public bool HasGeometryEditableSelection =>
        _selectedMarkup is { } markup &&
        GetSelectionSet(markup).Count == 1 &&
        (markup.Type is MarkupType.Circle or MarkupType.Arc or MarkupType.Rectangle or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel ||
         IsLineGeometryEditable(markup));

    public string SelectedMarkupGeometryEditSummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            if (HasGeometryEditableSelection)
            {
                return _selectedMarkup.Type switch
                {
                    MarkupType.Circle => "Numeric edit available: radius",
                    MarkupType.Arc => "Numeric edit available: radius, start, end, or sweep",
                    MarkupType.Dimension or MarkupType.Measurement => "Numeric edit available: length and angle",
                    MarkupType.Rectangle or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel => "Numeric edit available: width and height",
                    _ => string.Empty
                };
            }

            return GetSelectionSet(_selectedMarkup).Count > 1
                ? "Numeric geometry editing is disabled for grouped selections"
                : "Numeric geometry editing is currently available for circle, arc, rectangle, stamp, hyperlink, box, panel, and line-style dimension or measurement markups only";
        }
    }

    public bool HasGeometryShortcutHint => HasGeometryEditableSelection;

    public string SelectedMarkupGeometryShortcutHint =>
        HasGeometryEditableSelection
            ? "Shortcut: Ctrl+Shift+G"
            : string.Empty;

    public bool HasSelectedMarkupGeometryDetails => HasGeometryEditableSelection;

    public string SelectedMarkupGeometryDetails
    {
        get
        {
            if (_selectedMarkup == null || !HasGeometryEditableSelection)
                return string.Empty;

            if (_selectedMarkup.Type == MarkupType.Circle)
                return $"Radius: {FormatGeometryValue(_selectedMarkup.Radius)}";

            if (_selectedMarkup.Type is MarkupType.Dimension or MarkupType.Measurement)
            {
                var delta = _selectedMarkup.Vertices[1] - _selectedMarkup.Vertices[0];
                var length = delta.Length;
                var angle = NormalizeMarkupAngle(Math.Atan2(delta.Y, delta.X) * 180.0 / Math.PI);
                return string.Join(Environment.NewLine, new[]
                {
                    $"Length: {FormatGeometryValue(length)}",
                    $"Angle: {FormatGeometryValue(angle)} deg"
                });
            }

            if (_selectedMarkup.Type is MarkupType.Rectangle or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel)
            {
                var rect = _selectedMarkup.BoundingRect;
                return string.Join(Environment.NewLine, new[]
                {
                    $"Width: {FormatGeometryValue(rect.Width)}",
                    $"Height: {FormatGeometryValue(rect.Height)}"
                });
            }

            var start = NormalizeMarkupAngle(_selectedMarkup.ArcStartDeg);
            var end = NormalizeMarkupAngle(_selectedMarkup.ArcStartDeg + _selectedMarkup.ArcSweepDeg);

            return string.Join(Environment.NewLine, new[]
            {
                $"Radius: {FormatGeometryValue(_selectedMarkup.Radius)}",
                $"Start: {FormatGeometryValue(start)} deg",
                $"End: {FormatGeometryValue(end)} deg",
                $"Sweep: {FormatGeometryValue(_selectedMarkup.ArcSweepDeg)} deg"
            });
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
            RefreshSelectedMarkupPresentation();
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

    public void RefreshSelectedMarkupPresentation()
    {
        OnPropertyChanged(nameof(HasSelectedMarkup));
        OnPropertyChanged(nameof(HasStructuredSelection));
        OnPropertyChanged(nameof(SelectedMarkupAnnotationKind));
        OnPropertyChanged(nameof(SelectedMarkupAnnotationRole));
        OnPropertyChanged(nameof(SelectedMarkupAnnotationKey));
        OnPropertyChanged(nameof(HasTextEditableSelection));
        OnPropertyChanged(nameof(SelectedMarkupTextEditSummary));
        OnPropertyChanged(nameof(HasTextShortcutHint));
        OnPropertyChanged(nameof(SelectedMarkupTextShortcutHint));
        OnPropertyChanged(nameof(HasSelectedMarkupTextDetails));
        OnPropertyChanged(nameof(SelectedMarkupTextDetails));
        OnPropertyChanged(nameof(HasPathEditableSelection));
        OnPropertyChanged(nameof(SelectedMarkupPathEditSummary));
        OnPropertyChanged(nameof(HasPathShortcutHint));
        OnPropertyChanged(nameof(SelectedMarkupPathShortcutHint));
        OnPropertyChanged(nameof(HasSelectedMarkupPathDetails));
        OnPropertyChanged(nameof(SelectedMarkupPathDetails));
        OnPropertyChanged(nameof(HasGeometryEditableSelection));
        OnPropertyChanged(nameof(SelectedMarkupGeometryEditSummary));
        OnPropertyChanged(nameof(HasGeometryShortcutHint));
        OnPropertyChanged(nameof(SelectedMarkupGeometryShortcutHint));
        OnPropertyChanged(nameof(HasSelectedMarkupGeometryDetails));
        OnPropertyChanged(nameof(SelectedMarkupGeometryDetails));
        CommandManager.InvalidateRequerySuggested();
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

    private string GetSelectedMarkupCustomField(string fieldName)
    {
        if (_selectedMarkup == null)
            return string.Empty;

        return _selectedMarkup.Metadata.CustomFields.TryGetValue(fieldName, out var value)
            ? value ?? string.Empty
            : string.Empty;
    }

    private IReadOnlyList<MarkupRecord> GetSelectionSet(MarkupRecord selectedMarkup)
    {
        var groupId = selectedMarkup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationGroupIdField, out var value)
            ? value
            : null;

        if (string.IsNullOrWhiteSpace(groupId))
            return new[] { selectedMarkup };

        return _allMarkups
            .Where(markup => markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationGroupIdField, out var markupGroupId) &&
                             string.Equals(markupGroupId, groupId, StringComparison.Ordinal))
            .ToList();
    }

    private static double NormalizeMarkupAngle(double angleDegrees)
    {
        var normalized = angleDegrees % 360.0;
        if (normalized < 0)
            normalized += 360.0;

        return normalized;
    }

    private static string FormatGeometryValue(double value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static bool CanEditPath(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polyline => true,
            MarkupType.Polygon => true,
            MarkupType.Callout => true,
            MarkupType.LeaderNote => true,
            MarkupType.RevisionCloud => true,
            MarkupType.Dimension => true,
            MarkupType.Measurement => true,
            _ => false
        };
    }

    private static bool CanInsertVertices(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polyline => true,
            MarkupType.Polygon => true,
            MarkupType.Callout => true,
            MarkupType.LeaderNote => true,
            MarkupType.RevisionCloud => true,
            _ => false
        };
    }

    private static int GetMinimumVertexCount(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polygon => 3,
            MarkupType.Polyline => 2,
            MarkupType.RevisionCloud => 2,
            MarkupType.Callout => 2,
            MarkupType.LeaderNote => 2,
            MarkupType.Dimension => 2,
            MarkupType.Measurement => 2,
            _ => 0
        };
    }

    private static bool CanDeleteVertex(MarkupRecord markup)
    {
        return CanEditPath(markup) && markup.Vertices.Count > GetMinimumVertexCount(markup);
    }

    private static bool IsLineGeometryEditable(MarkupRecord markup)
    {
        if (markup.Type == MarkupType.Measurement)
            return markup.Vertices.Count >= 2 && markup.Vertices.Count <= 3;

        if (markup.Type != MarkupType.Dimension)
            return false;

        if (string.Equals(markup.Metadata.Subject, "Angular", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(markup.Metadata.Subject, "ArcLength", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return markup.Vertices.Count >= 2 && markup.Vertices.Count <= 3;
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
