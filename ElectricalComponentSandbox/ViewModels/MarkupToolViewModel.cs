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

public enum MarkupReviewScope
{
    CurrentSheet,
    AllSheets
}

public enum MarkupIssueGroupMode
{
    Sheet,
    Status,
    Author
}

public sealed class MarkupIssueGroupItemViewModel
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string SecondaryText { get; init; }
    public required int Count { get; init; }
    public required int OpenCount { get; init; }
}

public sealed class MarkupReplyItemViewModel
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public required string Text { get; init; }
    public required string CreatedDisplayText { get; init; }
    public required string ModifiedDisplayText { get; init; }
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

    private readonly ObservableCollection<MarkupRecord> _activeSheetMarkups;
    private readonly Func<IReadOnlyList<MarkupRecord>> _projectMarkupsProvider;
    private readonly Action<MarkupStatus>? _selectedStatusHandler;
    private readonly Action<MarkupStatus>? _visibleStatusHandler;
    private MarkupToolMode  _activeMode = MarkupToolMode.None;
    private DimensionSubType _dimSubType = DimensionSubType.Linear;
    private MarkupReviewScope _reviewScope = MarkupReviewScope.CurrentSheet;
    private MarkupIssueGroupMode _issueGroupMode = MarkupIssueGroupMode.Sheet;
    private string          _statusFilter = "All";
    private string          _typeFilter   = "All";
    private string          _layerFilter  = "All";
    private string          _labelSearch  = string.Empty;
    private MarkupRecord?   _selectedMarkup;
    private MarkupIssueGroupItemViewModel? _selectedIssueGroup;

    // ── Filtered view ─────────────────────────────────────────────────────────

    /// <summary>Markups currently visible in the list panel (after status + type + text filters)</summary>
    public ObservableCollection<MarkupRecord> FilteredMarkups { get; } = new();
    public ObservableCollection<MarkupIssueGroupItemViewModel> IssueGroups { get; } = new();
    public ObservableCollection<MarkupReplyItemViewModel> SelectedMarkupReplies { get; } = new();

    public IReadOnlyList<MarkupReviewScope> ReviewScopeOptions { get; } =
    [
        MarkupReviewScope.CurrentSheet,
        MarkupReviewScope.AllSheets
    ];

    public MarkupReviewScope ReviewScope
    {
        get => _reviewScope;
        set
        {
            if (_reviewScope == value)
                return;

            _reviewScope = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProjectReviewScope));
            ApplyFilter();
            OnCountsChanged();
        }
    }

    public bool IsProjectReviewScope => _reviewScope == MarkupReviewScope.AllSheets;

    public IReadOnlyList<MarkupIssueGroupMode> IssueGroupModeOptions { get; } =
    [
        MarkupIssueGroupMode.Sheet,
        MarkupIssueGroupMode.Status,
        MarkupIssueGroupMode.Author
    ];

    public MarkupIssueGroupMode IssueGroupMode
    {
        get => _issueGroupMode;
        set
        {
            if (_issueGroupMode == value)
                return;

            _issueGroupMode = value;
            OnPropertyChanged();
            SelectedIssueGroup = null;
            ApplyFilter();
        }
    }

    public MarkupIssueGroupItemViewModel? SelectedIssueGroup
    {
        get => _selectedIssueGroup;
        set
        {
            if (ReferenceEquals(_selectedIssueGroup, value))
                return;

            _selectedIssueGroup = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedIssueGroup));
            OnPropertyChanged(nameof(SelectedIssueGroupSummary));
            ApplyFilter();
        }
    }

    public bool HasSelectedIssueGroup => _selectedIssueGroup != null;

    public string SelectedIssueGroupSummary => _selectedIssueGroup == null
        ? "All visible issues"
        : $"{_selectedIssueGroup.DisplayName} | {_selectedIssueGroup.Count} issue(s)";

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

    public bool HasSelectedMarkupReplies => SelectedMarkupReplies.Count > 0;

    public bool HasSelectedMarkupAssignment => _selectedMarkup != null;

    public string SelectedMarkupAssignedTo => _selectedMarkup?.AssignedToDisplayText ?? string.Empty;

    public string SelectedMarkupAssignmentSummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(_selectedMarkup.AssignedTo)
                ? "Issue is currently unassigned."
                : $"Assigned to {_selectedMarkup.AssignedTo}.";
        }
    }

    public int SelectedMarkupReplyCount => _selectedMarkup?.Replies.Count ?? 0;

    public string SelectedMarkupReplySummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            if (SelectedMarkupReplyCount == 0)
                return "No replies yet. Add the first review response for this issue.";

            var latest = _selectedMarkup.Replies
                .OrderByDescending(reply => reply.ModifiedUtc)
                .First();

            var replyLabel = SelectedMarkupReplyCount == 1 ? "reply" : "replies";
            return $"{SelectedMarkupReplyCount} {replyLabel}. Latest by {latest.Author} on {latest.ModifiedDisplayText}";
        }
    }

    public string SelectedMarkupReplySearchSummary => _selectedMarkup == null
        ? string.Empty
        : "Reply text participates in Markups search filtering.";

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
            ? "Shortcut: Delete or Backspace removes the active vertex after selecting a grip"
            : string.Empty;

    public bool HasPathVertexDeleteCandidate =>
        _selectedMarkup is { } markup &&
        GetSelectionSet(markup).Count == 1 &&
        CanDeleteVertex(markup);

    public bool HasPathVertexInsertCandidate =>
        _selectedMarkup is { } markup &&
        GetSelectionSet(markup).Count == 1 &&
        CanInsertVertices(markup);

    public string SelectedMarkupPathInsertSummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            if (HasPathVertexInsertCandidate)
                return "Click Insert Vertex, then click a segment on the canvas";

            return GetSelectionSet(_selectedMarkup).Count > 1
                ? "Vertex insertion is disabled for grouped selections"
                : "Vertex insertion is currently available for polyline, polygon, callout, leader note, and revision cloud markups only";
        }
    }

    public string SelectedMarkupPathDeleteSummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            if (HasPathVertexDeleteCandidate)
                return "Select a vertex grip, then delete it from the keyboard or command surface";

            return GetSelectionSet(_selectedMarkup).Count > 1
                ? "Vertex deletion is disabled for grouped selections"
                : "Vertex deletion is unavailable when the selected path is already at its minimum vertex count";
        }
    }

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
                lines.Add("Insert: Double-click a segment or use Insert Vertex");

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
         MarkupInteractionService.IsPolylineGeometryEditable(markup) ||
         IsLineGeometryEditable(markup));

    public bool HasAppearanceEditableSelection =>
        _selectedMarkup is { } markup &&
        GetSelectionSet(markup).Count == 1;

    public string SelectedMarkupAppearanceEditSummary
    {
        get
        {
            if (_selectedMarkup == null)
                return string.Empty;

            if (!HasAppearanceEditableSelection)
                return "Appearance editing is disabled for grouped selections";

            return GetSelectedMarkupAppearanceCapabilities(_selectedMarkup);
        }
    }

    public bool HasAppearanceShortcutHint => HasAppearanceEditableSelection;

    public string SelectedMarkupAppearanceShortcutHint =>
        HasAppearanceEditableSelection
            ? "Shortcut: Ctrl+Shift+A"
            : string.Empty;

    public bool HasSelectedMarkupAppearanceDetails => HasAppearanceEditableSelection;

    public string SelectedMarkupAppearanceDetails
    {
        get
        {
            if (_selectedMarkup == null || !HasAppearanceEditableSelection)
                return string.Empty;

            var lines = new List<string>
            {
                $"Stroke: {_selectedMarkup.Appearance.StrokeColor}",
                $"Width: {FormatGeometryValue(_selectedMarkup.Appearance.StrokeWidth)}",
                $"Opacity: {FormatOpacityValue(_selectedMarkup.Appearance.Opacity)}"
            };

            if (SupportsFillAppearance(_selectedMarkup))
                lines.Add($"Fill: {_selectedMarkup.Appearance.FillColor}");

            if (SupportsFontAppearance(_selectedMarkup))
            {
                lines.Add($"Font: {_selectedMarkup.Appearance.FontFamily}");
                lines.Add($"Font Size: {FormatGeometryValue(_selectedMarkup.Appearance.FontSize)}");
            }

            if (!string.IsNullOrWhiteSpace(_selectedMarkup.Appearance.DashArray))
                lines.Add($"Dash: {_selectedMarkup.Appearance.DashArray}");

            return string.Join(Environment.NewLine, lines);
        }
    }

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
                    MarkupType.Polyline or MarkupType.Polygon => $"Numeric edit available: {_selectedMarkup.Vertices.Count} vertex coordinates",
                    MarkupType.Dimension or MarkupType.Measurement => GetLineGeometrySummary(_selectedMarkup),
                    MarkupType.Rectangle or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel => "Numeric edit available: width and height",
                    _ => string.Empty
                };
            }

            return GetSelectionSet(_selectedMarkup).Count > 1
                ? "Numeric geometry editing is disabled for grouped selections"
                : "Numeric geometry editing is currently available for polyline, polygon, circle, arc, rectangle, stamp, hyperlink, box, panel, angular dimension, arc-length dimension, and line-style dimension or measurement markups only";
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
                if (IsArcLengthDimension(_selectedMarkup))
                {
                    var arcLength = Math.Abs(_selectedMarkup.ArcSweepDeg) * Math.PI / 180.0 * _selectedMarkup.Radius;
                    return string.Join(Environment.NewLine, new[]
                    {
                        $"Arc Length: {FormatGeometryValue(arcLength)}",
                        $"Radius: {FormatGeometryValue(_selectedMarkup.Radius)}",
                        $"Start: {FormatGeometryValue(NormalizeMarkupAngle(_selectedMarkup.ArcStartDeg))} deg",
                        $"Sweep: {FormatGeometryValue(_selectedMarkup.ArcSweepDeg)} deg"
                    });
                }

                if (IsAngularDimension(_selectedMarkup))
                {
                    return string.Join(Environment.NewLine, new[]
                    {
                        $"Angle: {FormatGeometryValue(Math.Abs(_selectedMarkup.ArcSweepDeg))} deg",
                        $"Radius: {FormatGeometryValue(_selectedMarkup.Radius)}"
                    });
                }

                var delta = _selectedMarkup.Vertices[1] - _selectedMarkup.Vertices[0];
                var length = delta.Length;
                var angle = NormalizeMarkupAngle(Math.Atan2(delta.Y, delta.X) * 180.0 / Math.PI);

                if (IsRadialDimension(_selectedMarkup))
                {
                    return string.Join(Environment.NewLine, new[]
                    {
                        $"Radius: {FormatGeometryValue(length)}",
                        $"Angle: {FormatGeometryValue(angle)} deg"
                    });
                }

                if (IsDiameterDimension(_selectedMarkup))
                {
                    return string.Join(Environment.NewLine, new[]
                    {
                        $"Diameter: {FormatGeometryValue(length)}",
                        $"Angle: {FormatGeometryValue(angle)} deg"
                    });
                }

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

            if (_selectedMarkup.Type is MarkupType.Polyline or MarkupType.Polygon)
            {
                var vertexLines = new List<string>(_selectedMarkup.Vertices.Count + 1)
                {
                    $"Vertices: {_selectedMarkup.Vertices.Count}"
                };

                for (int i = 0; i < _selectedMarkup.Vertices.Count; i++)
                {
                    vertexLines.Add($"  [{i + 1}] ({FormatGeometryValue(_selectedMarkup.Vertices[i].X)}, {FormatGeometryValue(_selectedMarkup.Vertices[i].Y)})");
                }

                return string.Join(Environment.NewLine, vertexLines);
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

    public int TotalCount      => GetReviewSourceMarkups().Count;
    public int OpenCount       => GetReviewSourceMarkups().Count(m => m.Status == MarkupStatus.Open);
    public int InProgressCount => GetReviewSourceMarkups().Count(m => m.Status == MarkupStatus.InProgress);
    public int ResolvedCount   => GetReviewSourceMarkups().Count(m => m.Status == MarkupStatus.Resolved);
    public int ApprovedCount   => GetReviewSourceMarkups().Count(m => m.Status == MarkupStatus.Approved);
    public int RejectedCount   => GetReviewSourceMarkups().Count(m => m.Status == MarkupStatus.Rejected);
    public int VoidCount       => GetReviewSourceMarkups().Count(m => m.Status == MarkupStatus.Void);
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
    public ICommand ResolveVisibleCommand  { get; }
    public ICommand VoidVisibleCommand     { get; }
    public ICommand SetAllStatusCommand    { get; }
    public ICommand ClearIssueGroupCommand { get; }
    public ICommand ClearFiltersCommand    { get; }

    public MarkupToolViewModel(
        ObservableCollection<MarkupRecord> activeSheetMarkups,
        Func<IReadOnlyList<MarkupRecord>> projectMarkupsProvider,
        Action<MarkupStatus>? selectedStatusHandler = null,
        Action<MarkupStatus>? visibleStatusHandler = null)
    {
        _activeSheetMarkups = activeSheetMarkups;
        _projectMarkupsProvider = projectMarkupsProvider;
        _selectedStatusHandler = selectedStatusHandler;
        _visibleStatusHandler = visibleStatusHandler;
        _activeSheetMarkups.CollectionChanged += (_, _) =>
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
        ApproveSelectedCommand = new RelayCommand(_ => ApplySelectedStatus(MarkupStatus.Approved), _ => SelectedMarkup != null);
        RejectSelectedCommand  = new RelayCommand(_ => ApplySelectedStatus(MarkupStatus.Rejected), _ => SelectedMarkup != null);
        ResolveVisibleCommand  = new RelayCommand(_ => ApplyVisibleStatus(MarkupStatus.Resolved), _ => FilteredMarkups.Count > 0);
        VoidVisibleCommand     = new RelayCommand(_ => ApplyVisibleStatus(MarkupStatus.Void), _ => FilteredMarkups.Count > 0);

        SetAllStatusCommand = new RelayCommand(param =>
        {
            if (param is string s && TryParseStatusFilter(s, out var status))
                ApplyVisibleStatus(status);
        });

        ClearIssueGroupCommand = new RelayCommand(_ => SelectedIssueGroup = null, _ => SelectedIssueGroup != null);

        ClearFiltersCommand = new RelayCommand(_ =>
        {
            _statusFilter = "All";
            _typeFilter = "All";
            _layerFilter = "All";
            _labelSearch = string.Empty;
            _selectedIssueGroup = null;
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(LayerFilter));
            OnPropertyChanged(nameof(LabelSearch));
            OnPropertyChanged(nameof(SelectedIssueGroup));
            OnPropertyChanged(nameof(HasSelectedIssueGroup));
            OnPropertyChanged(nameof(SelectedIssueGroupSummary));
            ApplyFilter();
            CommandManager.InvalidateRequerySuggested();
        });

        RefreshLayerFilterOptions();
        ApplyFilter();
    }

    public void RefreshReviewContext()
    {
        RefreshLayerFilterOptions();
        ApplyFilter();
        RefreshSelectedMarkupPresentation();
        OnCountsChanged();
    }

    public IReadOnlyList<MarkupRecord> GetFilteredReviewMarkups()
        => FilteredMarkups.ToList();

    public void RefreshSelectedMarkupPresentation()
    {
        OnPropertyChanged(nameof(HasSelectedMarkup));
        OnPropertyChanged(nameof(HasSelectedMarkupAssignment));
        OnPropertyChanged(nameof(SelectedMarkupAssignedTo));
        OnPropertyChanged(nameof(SelectedMarkupAssignmentSummary));
        RebuildSelectedMarkupReplies();
        OnPropertyChanged(nameof(HasSelectedMarkupReplies));
        OnPropertyChanged(nameof(SelectedMarkupReplyCount));
        OnPropertyChanged(nameof(SelectedMarkupReplySummary));
        OnPropertyChanged(nameof(SelectedMarkupReplySearchSummary));
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
        OnPropertyChanged(nameof(HasPathVertexInsertCandidate));
        OnPropertyChanged(nameof(SelectedMarkupPathInsertSummary));
        OnPropertyChanged(nameof(HasPathVertexDeleteCandidate));
        OnPropertyChanged(nameof(SelectedMarkupPathDeleteSummary));
        OnPropertyChanged(nameof(HasSelectedMarkupPathDetails));
        OnPropertyChanged(nameof(SelectedMarkupPathDetails));
        OnPropertyChanged(nameof(HasGeometryEditableSelection));
        OnPropertyChanged(nameof(HasAppearanceEditableSelection));
        OnPropertyChanged(nameof(SelectedMarkupAppearanceEditSummary));
        OnPropertyChanged(nameof(HasAppearanceShortcutHint));
        OnPropertyChanged(nameof(SelectedMarkupAppearanceShortcutHint));
        OnPropertyChanged(nameof(HasSelectedMarkupAppearanceDetails));
        OnPropertyChanged(nameof(SelectedMarkupAppearanceDetails));
        OnPropertyChanged(nameof(SelectedMarkupGeometryEditSummary));
        OnPropertyChanged(nameof(HasGeometryShortcutHint));
        OnPropertyChanged(nameof(SelectedMarkupGeometryShortcutHint));
        OnPropertyChanged(nameof(HasSelectedMarkupGeometryDetails));
        OnPropertyChanged(nameof(SelectedMarkupGeometryDetails));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RebuildSelectedMarkupReplies()
    {
        SelectedMarkupReplies.Clear();

        if (_selectedMarkup == null)
            return;

        foreach (var reply in _selectedMarkup.Replies
                     .OrderBy(reply => reply.CreatedUtc)
                     .ThenBy(reply => reply.ModifiedUtc)
                     .ThenBy(reply => reply.Id, StringComparer.Ordinal))
        {
            SelectedMarkupReplies.Add(new MarkupReplyItemViewModel
            {
                Id = reply.Id,
                Author = string.IsNullOrWhiteSpace(reply.Author) ? "Unknown" : reply.Author,
                Text = reply.Text,
                CreatedDisplayText = reply.CreatedDisplayText,
                ModifiedDisplayText = reply.ModifiedDisplayText
            });
        }
    }

    // ── Filter logic ──────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var reviewSource = GetReviewSourceMarkups();

        if (_selectedMarkup != null && !reviewSource.Any(markup => string.Equals(markup.Id, _selectedMarkup.Id, StringComparison.Ordinal)))
            SelectedMarkup = null;

        var filtered = reviewSource.AsEnumerable();

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
                (m.AssignedTo?.Contains(lower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.StatusNote?.Contains(lower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                m.Replies.Any(reply =>
                    reply.Text.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                    reply.Author.Contains(lower, StringComparison.OrdinalIgnoreCase)));
        }

        var groupedSource = filtered.ToList();
        RebuildIssueGroups(groupedSource);

        if (_selectedIssueGroup != null)
        {
            filtered = groupedSource.Where(markup => MatchesIssueGroup(markup, _selectedIssueGroup));
        }
        else
        {
            filtered = groupedSource;
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
        _activeSheetMarkups.Remove(SelectedMarkup);
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

    private void ApplySelectedStatus(MarkupStatus status)
    {
        if (_selectedStatusHandler != null)
        {
            _selectedStatusHandler(status);
            return;
        }

        SetSelectedStatus(status);
    }

    private void ApplyVisibleStatus(MarkupStatus status)
    {
        if (_visibleStatusHandler != null)
        {
            _visibleStatusHandler(status);
            return;
        }

        SetAllVisible(status);
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

        return GetReviewSourceMarkups()
            .Where(markup => markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationGroupIdField, out var markupGroupId) &&
                             string.Equals(markupGroupId, groupId, StringComparison.Ordinal))
            .ToList();
    }

    private IReadOnlyList<MarkupRecord> GetReviewSourceMarkups()
    {
        return _reviewScope == MarkupReviewScope.AllSheets
            ? _projectMarkupsProvider()
            : _activeSheetMarkups;
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

    private static string FormatOpacityValue(double value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static string GetSelectedMarkupAppearanceCapabilities(MarkupRecord markup)
    {
        var capabilities = new List<string>
        {
            "stroke color",
            "width",
            "opacity"
        };

        if (SupportsFillAppearance(markup))
            capabilities.Add("fill");

        if (SupportsFontAppearance(markup))
        {
            capabilities.Add("font family");
            capabilities.Add("font size");
        }

        if (SupportsDashAppearance(markup))
            capabilities.Add("dash pattern");

        return $"Appearance edit available: {string.Join(", ", capabilities)}";
    }

    private static bool SupportsFillAppearance(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polygon => true,
            MarkupType.Rectangle => true,
            MarkupType.Circle => true,
            MarkupType.Arc => true,
            MarkupType.Text => true,
            MarkupType.Box => true,
            MarkupType.Panel => true,
            MarkupType.Callout => true,
            MarkupType.Stamp => true,
            MarkupType.Hatch => true,
            MarkupType.Hyperlink => true,
            _ => false
        };
    }

    private static bool SupportsFontAppearance(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Text => true,
            MarkupType.Callout => true,
            MarkupType.LeaderNote => true,
            MarkupType.Stamp => true,
            MarkupType.Dimension => true,
            MarkupType.Measurement => true,
            _ => false
        };
    }

    private static bool SupportsDashAppearance(MarkupRecord markup)
    {
        return markup.Type != MarkupType.Text && markup.Type != MarkupType.Stamp;
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

        if (IsArcLengthDimension(markup))
            return markup.Vertices.Count >= 3 && markup.Radius > 0.1;

        if (IsAngularDimension(markup))
            return markup.Vertices.Count >= 3;

        return markup.Vertices.Count >= 2 && markup.Vertices.Count <= 3;
    }

    private static string GetLineGeometrySummary(MarkupRecord markup)
    {
        if (IsArcLengthDimension(markup))
            return "Numeric edit available: arc length and radius";

        if (IsAngularDimension(markup))
            return "Numeric edit available: angle and radius";

        if (IsRadialDimension(markup))
            return "Numeric edit available: radius and angle";

        if (IsDiameterDimension(markup))
            return "Numeric edit available: diameter and angle";

        return "Numeric edit available: length and angle";
    }

    private static bool IsRadialDimension(MarkupRecord markup)
        => markup.Type == MarkupType.Dimension && string.Equals(markup.Metadata.Subject, "Radial", StringComparison.OrdinalIgnoreCase);

    private static bool IsDiameterDimension(MarkupRecord markup)
        => markup.Type == MarkupType.Dimension && string.Equals(markup.Metadata.Subject, "Diameter", StringComparison.OrdinalIgnoreCase);

    private static bool IsAngularDimension(MarkupRecord markup)
        => markup.Type == MarkupType.Dimension && string.Equals(markup.Metadata.Subject, "Angular", StringComparison.OrdinalIgnoreCase);

    private static bool IsArcLengthDimension(MarkupRecord markup)
        => markup.Type == MarkupType.Dimension && string.Equals(markup.Metadata.Subject, "ArcLength", StringComparison.OrdinalIgnoreCase);

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
        var layers = GetReviewSourceMarkups()
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

    private void RebuildIssueGroups(IReadOnlyList<MarkupRecord> source)
    {
        var groups = source
            .GroupBy(GetIssueGroupKey)
            .Select(group => BuildIssueGroup(group.Key, group))
            .OrderByDescending(group => group.OpenCount)
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        IssueGroups.Clear();
        foreach (var group in groups)
            IssueGroups.Add(group);

        if (_selectedIssueGroup != null)
        {
            var replacement = groups.FirstOrDefault(group => string.Equals(group.Key, _selectedIssueGroup.Key, StringComparison.Ordinal));
            if (replacement == null)
            {
                _selectedIssueGroup = null;
                OnPropertyChanged(nameof(SelectedIssueGroup));
                OnPropertyChanged(nameof(HasSelectedIssueGroup));
                OnPropertyChanged(nameof(SelectedIssueGroupSummary));
            }
            else if (!ReferenceEquals(replacement, _selectedIssueGroup))
            {
                _selectedIssueGroup = replacement;
                OnPropertyChanged(nameof(SelectedIssueGroup));
                OnPropertyChanged(nameof(SelectedIssueGroupSummary));
            }
        }
    }

    private string GetIssueGroupKey(MarkupRecord markup)
    {
        return _issueGroupMode switch
        {
            MarkupIssueGroupMode.Status => $"status:{markup.Status}",
            MarkupIssueGroupMode.Author => $"author:{GetAuthorGroupValue(markup)}",
            _ => $"sheet:{markup.ReviewSheetDisplayText}"
        };
    }

    private MarkupIssueGroupItemViewModel BuildIssueGroup(string key, IEnumerable<MarkupRecord> source)
    {
        var markups = source.ToList();
        var first = markups[0];
        var displayName = _issueGroupMode switch
        {
            MarkupIssueGroupMode.Status => MarkupRecord.GetStatusDisplayText(first.Status),
            MarkupIssueGroupMode.Author => GetAuthorGroupValue(first),
            _ => first.ReviewSheetDisplayText
        };

        var secondaryText = _issueGroupMode switch
        {
            MarkupIssueGroupMode.Status => $"Author groups: {markups.Select(GetAuthorGroupValue).Distinct(StringComparer.OrdinalIgnoreCase).Count()}",
            MarkupIssueGroupMode.Author => $"Sheets: {markups.Select(markup => markup.ReviewSheetDisplayText).Distinct(StringComparer.OrdinalIgnoreCase).Count()}",
            _ => $"Statuses: {markups.Select(markup => MarkupRecord.GetStatusDisplayText(markup.Status)).Distinct(StringComparer.OrdinalIgnoreCase).Count()}"
        };

        return new MarkupIssueGroupItemViewModel
        {
            Key = key,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "(unnamed)" : displayName,
            SecondaryText = secondaryText,
            Count = markups.Count,
            OpenCount = markups.Count(markup => markup.Status is MarkupStatus.Open or MarkupStatus.InProgress)
        };
    }

    private bool MatchesIssueGroup(MarkupRecord markup, MarkupIssueGroupItemViewModel group)
        => string.Equals(GetIssueGroupKey(markup), group.Key, StringComparison.Ordinal);

    private static string GetAuthorGroupValue(MarkupRecord markup)
        => string.IsNullOrWhiteSpace(markup.Metadata.Author) ? "(unassigned)" : markup.Metadata.Author.Trim();

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
