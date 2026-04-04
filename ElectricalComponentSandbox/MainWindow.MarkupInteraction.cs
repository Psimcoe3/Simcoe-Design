using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

internal enum MarkupHandleOverlayMode
{
    None,
    DirectGeometry,
    Vertices,
    Resize
}

public partial class MainWindow
{
    private const string UnsupportedGeometryEditMessage = "Numeric geometry editing is currently available for polyline, polygon, circle, arc, rectangle, stamp, hyperlink, box, panel, angular dimension or measurement, arc-length dimension or measurement, and line-style dimension or measurement markups.";
    private const string LineGeometryVertexCountMessage = "Numeric geometry editing for dimensions and measurements is currently available for line-style markups with 2 or 3 points. Angular dimensions or measurements use angle/radius semantics, and arc-length dimensions or measurements use arc length/radius semantics.";

    private bool _isDraggingMarkupArcAngle = false;
    private readonly MarkupInteractionService _markupInteractionService = new();
    private bool _isPendingMarkupVertexInsertion = false;
    private bool _isDraggingMarkup = false;
    private bool _isDraggingMarkupRadius = false;
    private bool _isDraggingMarkupVertex = false;
    private bool _isResizingMarkup = false;
    private MarkupArcAngleHandle _activeMarkupArcAngleHandle = MarkupArcAngleHandle.None;
    private MarkupResizeHandle _activeMarkupResizeHandle = MarkupResizeHandle.None;
    private int _activeMarkupVertexIndex = -1;
    private Rect _markupResizeStartBounds = Rect.Empty;
    private readonly List<MarkupRecord> _draggedMarkups = new();
    private readonly List<MarkupRecord> _resizedMarkups = new();
    private readonly Dictionary<string, MarkupGeometrySnapshot> _markupDragStartSnapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MarkupGeometrySnapshot> _markupResizeStartSnapshots = new(StringComparer.Ordinal);
    private MarkupRecord? _arcAngleDraggedMarkup;
    private MarkupGeometrySnapshot? _markupArcAngleStartSnapshot;
    private MarkupRecord? _radiusDraggedMarkup;
    private MarkupGeometrySnapshot? _markupRadiusStartSnapshot;
    private MarkupRecord? _vertexDraggedMarkup;
    private MarkupGeometrySnapshot? _markupVertexStartSnapshot;

    internal bool IsPendingMarkupVertexInsertionForTesting => _isPendingMarkupVertexInsertion;
    internal int ActiveMarkupVertexIndexForTesting => _activeMarkupVertexIndex;
    internal bool BeginSelectedMarkupVertexInsertionForTesting(bool showFeedbackIfUnsupported = false)
        => TryBeginSelectedMarkupVertexInsertion(showFeedbackIfUnsupported);
    internal void CancelPendingMarkupVertexInsertionForTesting(bool logCancellation = false)
        => CancelPendingMarkupVertexInsertion(logCancellation);
    internal void SetActiveMarkupVertexIndexForTesting(int index) => _activeMarkupVertexIndex = index;
    internal bool BeginSelectedMarkupArcAngleDragForTesting(Point canvasPoint)
        => TryStartMarkupArcAngleDrag(canvasPoint);
    internal bool BeginSelectedMarkupRadiusDragForTesting(Point canvasPoint)
        => TryStartMarkupRadiusDrag(canvasPoint);
    internal bool BeginSelectedMarkupResizeDragForTesting(Point canvasPoint)
        => TryStartMarkupResizeDrag(canvasPoint);
    internal void UpdateDraggedMarkupArcAnglePreviewForTesting(Point canvasPoint)
        => UpdateDraggedMarkupArcAnglePreview(canvasPoint);
    internal void UpdateDraggedMarkupRadiusPreviewForTesting(Point canvasPoint)
        => UpdateDraggedMarkupRadiusPreview(canvasPoint);
    internal void UpdateMarkupResizePreviewForTesting(Point canvasPoint)
        => UpdateMarkupResizePreview(canvasPoint);
    internal bool BeginSelectedMarkupSelectionDragForTesting(Point canvasPoint)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        StartMarkupSelectionDrag(selectedMarkup, canvasPoint);
        return true;
    }
    internal bool BeginSelectedMarkupVertexDragForTesting(Point canvasPoint)
        => TryStartMarkupVertexDrag(canvasPoint);
    internal void UpdateDraggedMarkupPreviewForTesting(Point canvasPoint)
        => UpdateDraggedMarkupPreview(canvasPoint);
    internal void UpdateDraggedMarkupVertexPreviewForTesting(Point canvasPoint)
        => UpdateDraggedMarkupVertexPreview(canvasPoint);
    internal void FinishMarkupSelectionDragForTesting() => FinishMarkupSelectionDrag();
    internal void FinishMarkupResizeDragForTesting() => FinishMarkupResizeDrag();
    internal void FinishMarkupArcAngleDragForTesting() => FinishMarkupArcAngleDrag();
    internal void FinishMarkupRadiusDragForTesting() => FinishMarkupRadiusDrag();
    internal void FinishMarkupVertexDragForTesting() => FinishMarkupVertexDrag();
    internal static bool IsLineGeometryReadoutEligibleForTesting(MarkupRecord markup, int activeVertexIndex)
        => IsLineGeometryReadoutEligible(markup, activeVertexIndex);
    internal static string BuildLineGeometryReadoutForTesting(MarkupRecord markup)
        => BuildLineGeometryReadout(markup);
    internal static string GetUnsupportedGeometryEditMessageForTesting()
        => UnsupportedGeometryEditMessage;
    internal static string GetLineGeometryVertexCountMessageForTesting()
        => LineGeometryVertexCountMessage;
    internal static bool TryBuildGeometryPromptForTesting(MarkupRecord markup, out string title, out string prompt, out string defaultValue)
        => TryBuildGeometryPrompt(markup, out title, out prompt, out defaultValue);
    internal void DrawSelectedMarkupOverlayForTesting(ICanvas2DRenderer renderer)
        => DrawSelectedMarkupOverlay(renderer);
    internal bool HandlePendingMarkupVertexInsertionClickForTesting(Point canvasPoint)
    {
        if (!_isPendingMarkupVertexInsertion)
            return false;

        if (!TryInsertMarkupVertex(canvasPoint))
            return false;

        CancelPendingMarkupVertexInsertion(logCancellation: false);
        return true;
    }
    internal bool TryDeleteSelectedMarkupVertexForTesting(bool showFeedbackIfUnsupported = false)
        => TryDeleteSelectedMarkupVertex(showFeedbackIfUnsupported);
    internal bool ExecuteDeleteShortcutForTesting()
    {
        if (DeleteSelectedMarkupVertex())
            return true;

        DeleteComponent_Click(this, new RoutedEventArgs());
        return false;
    }
    internal void SelectMarkupOnCanvasForTesting(MarkupRecord markup) => SelectMarkupOnCanvas(markup);

    private bool TryStartMarkupArcAngleDrag(Point canvasPoint)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1 || !_markupInteractionService.CanEditArcAngles(selectedMarkup))
            return false;

        var handle = _markupInteractionService.HitTestArcAngleHandle(canvasPoint, selectedMarkup, GetMarkupHitTolerance());
        if (handle == MarkupArcAngleHandle.None)
            return false;

        _isDraggingMarkupArcAngle = true;
        _activeMarkupArcAngleHandle = handle;
        _arcAngleDraggedMarkup = selectedMarkup;
        _markupArcAngleStartSnapshot = _markupInteractionService.Capture(selectedMarkup);
        _lastMousePosition = canvasPoint;
        _dragStartCanvasPosition = canvasPoint;
        _mobileSelectionCandidate = _isMobileView;

        PlanCanvas.CaptureMouse();
        SkiaBackground.RequestRedraw();
        return true;
    }

    private bool TryStartMarkupRadiusDrag(Point canvasPoint)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1 || !_markupInteractionService.CanEditRadius(selectedMarkup))
            return false;

        if (!_markupInteractionService.HitTestRadiusHandle(canvasPoint, selectedMarkup, GetMarkupHitTolerance()))
            return false;

        _isDraggingMarkupRadius = true;
        _radiusDraggedMarkup = selectedMarkup;
        _markupRadiusStartSnapshot = _markupInteractionService.Capture(selectedMarkup);
        _lastMousePosition = canvasPoint;
        _dragStartCanvasPosition = canvasPoint;
        _mobileSelectionCandidate = _isMobileView;

        PlanCanvas.CaptureMouse();
        SkiaBackground.RequestRedraw();
        return true;
    }

    private bool TryInsertMarkupVertex(Point canvasPoint)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1 || !_markupInteractionService.CanInsertVertices(selectedMarkup))
            return false;

        var snappedPoint = ApplyDrawingSnap(canvasPoint);

        if (_markupInteractionService.HitTestVertexHandle(snappedPoint, selectedMarkup, GetMarkupHitTolerance()) >= 0)
            return false;

        if (!_markupInteractionService.TryFindInsertionPoint(snappedPoint, selectedMarkup, GetMarkupHitTolerance(), out var insertIndex, out var projectedPoint))
            return false;

        var before = _markupInteractionService.Capture(selectedMarkup);
        if (!_markupInteractionService.InsertVertex(selectedMarkup, insertIndex, projectedPoint))
            return false;

        var after = _markupInteractionService.Capture(selectedMarkup);
        _activeMarkupVertexIndex = insertIndex >= selectedMarkup.Vertices.Count ? selectedMarkup.Vertices.Count - 1 : insertIndex;
        _viewModel.UndoRedo.Execute(new MarkupGeometryChangeAction(
            "Insert vertex into",
            _markupInteractionService,
            selectedMarkup,
            before,
            after));
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Markup vertex inserted", $"Type: {selectedMarkup.TypeDisplayText}");
        return true;
    }

    private bool TryBeginSelectedMarkupVertexInsertion(bool showFeedbackIfUnsupported)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Select a path-based markup first.", "Insert Vertex",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Vertex insertion is available for a single selected path markup only.", "Insert Vertex",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (!_markupInteractionService.CanInsertVertices(selectedMarkup))
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("The selected markup type does not support vertex insertion.", "Insert Vertex",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        _isPendingMarkupVertexInsertion = true;
        _activeMarkupVertexIndex = -1;
        UpdatePlanCanvasCursor();
        SkiaBackground.RequestRedraw();
        return true;
    }

    private void CancelPendingMarkupVertexInsertion(bool logCancellation = true)
    {
        if (!_isPendingMarkupVertexInsertion)
            return;

        _isPendingMarkupVertexInsertion = false;
        if (logCancellation)
            ActionLogService.Instance.Log(LogCategory.Edit, "Markup vertex insertion cancelled", "Pending segment pick cleared");

        UpdatePlanCanvasCursor();
        SkiaBackground.RequestRedraw();
    }

    private bool DeleteSelectedMarkupVertex()
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup || _activeMarkupVertexIndex < 0)
            return false;

        if (!_markupInteractionService.CanDeleteVertex(selectedMarkup))
            return false;

        var before = _markupInteractionService.Capture(selectedMarkup);
        if (!_markupInteractionService.DeleteVertex(selectedMarkup, _activeMarkupVertexIndex))
            return false;

        var after = _markupInteractionService.Capture(selectedMarkup);
        _activeMarkupVertexIndex = Math.Min(_activeMarkupVertexIndex, selectedMarkup.Vertices.Count - 1);
        _viewModel.UndoRedo.Execute(new MarkupGeometryChangeAction(
            "Delete vertex from",
            _markupInteractionService,
            selectedMarkup,
            before,
            after));
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Markup vertex deleted", $"Type: {selectedMarkup.TypeDisplayText}");
        return true;
    }

    private bool TryDeleteSelectedMarkupVertex(bool showFeedbackIfUnsupported)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Select a path-based markup first.", "Delete Vertex",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Vertex deletion is available for a single selected path markup only.", "Delete Vertex",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (!_markupInteractionService.CanDeleteVertex(selectedMarkup))
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("The selected markup is already at its minimum vertex count.", "Delete Vertex",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (_activeMarkupVertexIndex < 0)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Click a vertex grip first, then delete it.", "Delete Vertex",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        return DeleteSelectedMarkupVertex();
    }

    private bool TryEditStructuredMarkupText(Point canvasPoint)
    {
        var hit = _canvasInteractionShadowTree.HitTest(canvasPoint, GetMarkupHitTolerance());
        if (hit?.Kind != ShadowGeometryTree.ShadowNodeKind.Markup || hit.Source is not MarkupRecord markup)
            return false;

        SelectMarkupOnCanvas(markup);

        return TryEditStructuredMarkupText(markup);
    }

    private bool TryEditSelectedStructuredMarkupText(bool showFeedbackIfUnsupported)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Select a structured annotation text markup first.", "Edit Annotation Text",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        return TryEditStructuredMarkupText(selectedMarkup, showFeedbackIfUnsupported);
    }

    private bool TryEditStructuredMarkupText(MarkupRecord markup, bool showFeedbackIfUnsupported = false)
    {
        if (TryEditLiveTitleBlockField(markup, showFeedbackIfUnsupported, out var liveTitleBlockHandled))
            return liveTitleBlockHandled;

        if (!CanEditStructuredMarkupText(markup, out var annotationKind, out var textRole))
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("The selected markup is not an editable structured annotation text field.", "Edit Annotation Text",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var title = GetStructuredMarkupEditTitle(annotationKind, textRole);
        var prompt = GetStructuredMarkupEditPrompt(markup, annotationKind, textRole);
        var input = PromptInput(title, prompt, markup.TextContent);
        if (input == null)
            return true;

        if (string.Equals(input, markup.TextContent, StringComparison.Ordinal))
            return true;

        var action = new MarkupTextChangeAction(markup, input);
        _viewModel.UndoRedo.Execute(action);
        _viewModel.MarkupTool.RefreshSelectedMarkupPresentation();
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Structured markup text edited",
            $"Kind: {annotationKind}, Role: {textRole}, Label: {markup.Metadata.Label}");
        return true;
    }

    private bool TryEditLiveTitleBlockField(MarkupRecord markup, bool showFeedbackIfUnsupported, out bool handled)
    {
        handled = false;

        if (markup.Type != MarkupType.Text ||
            !markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationKindField, out var annotationKind) ||
            !string.Equals(annotationKind, DrawingAnnotationMarkupService.TitleBlockAnnotationKind, StringComparison.Ordinal) ||
            !markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveTitleBlockInstanceIdField, out var instanceId) ||
            string.IsNullOrWhiteSpace(instanceId) ||
            !markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationTextRoleField, out var textRole) ||
            !string.Equals(textRole, DrawingAnnotationMarkupService.TextRoleFieldValue, StringComparison.Ordinal))
        {
            return false;
        }

        handled = true;
        var fieldLabel = markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationTextKeyField, out var textKey)
            ? textKey ?? string.Empty
            : markup.Metadata.Label;

        if (TitleBlockService.IsLiveBoundFieldLabel(fieldLabel))
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("This title block field stays bound to sheet/project data and is not edited directly.", "Edit Title Block Field",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return true;
        }

        var title = GetStructuredMarkupEditTitle(annotationKind, textRole);
        var prompt = GetStructuredMarkupEditPrompt(markup, annotationKind, textRole);
        var input = PromptInput(title, prompt, markup.TextContent);
        if (input == null || string.Equals(input, markup.TextContent, StringComparison.Ordinal))
            return true;

        var action = new LiveTitleBlockFieldEditAction(_viewModel, instanceId, fieldLabel, markup.TextContent, input);
        _viewModel.UndoRedo.Execute(action);
        _viewModel.MarkupTool.RefreshSelectedMarkupPresentation();
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Live title block field edited",
            $"Field: {fieldLabel}, Sheet: {_viewModel.SelectedSheet?.DisplayName ?? "(none)"}");
        return true;
    }

    private bool TryEditSelectedMarkupGeometry(bool showFeedbackIfUnsupported)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Select a single polyline, polygon, circle, arc, rectangle, stamp, hyperlink, box, panel, dimension, or measurement markup first.", "Edit Geometry",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (!TryBuildGeometryPrompt(selectedMarkup, out var title, out var prompt, out var defaultValue))
            return ShowUnsupportedGeometryEditMessage(showFeedbackIfUnsupported);

        var input = PromptInput(title, prompt, defaultValue);
        if (input == null)
            return true;

        return TryEditSelectedMarkupGeometry(input, showFeedbackIfUnsupported);
    }

    private bool TryEditSelectedMarkupGeometry(string input, bool showFeedbackIfUnsupported)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Numeric geometry editing is available for a single selected polyline, polygon, circle, arc, rectangle, stamp, hyperlink, box, panel, dimension, or measurement markup.", "Edit Geometry",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        return selectedMarkup.Type switch
        {
            MarkupType.Rectangle or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel => TryEditSelectedBoundsGeometry(selectedMarkup, input, showFeedbackIfUnsupported),
            MarkupType.Circle => TryEditSelectedCircleGeometry(selectedMarkup, input, showFeedbackIfUnsupported),
            MarkupType.Arc => TryEditSelectedArcGeometry(selectedMarkup, input, showFeedbackIfUnsupported),
            MarkupType.Polyline or MarkupType.Polygon => TryEditSelectedPolylineGeometry(selectedMarkup, input, showFeedbackIfUnsupported),
            MarkupType.Dimension or MarkupType.Measurement when IsArcLengthDimension(selectedMarkup) => TryEditSelectedArcLengthGeometry(selectedMarkup, input, showFeedbackIfUnsupported),
            MarkupType.Dimension or MarkupType.Measurement when IsAngularDimension(selectedMarkup) => TryEditSelectedAngularGeometry(selectedMarkup, input, showFeedbackIfUnsupported),
            MarkupType.Dimension or MarkupType.Measurement => TryEditSelectedLineGeometry(selectedMarkup, input, showFeedbackIfUnsupported),
            _ => ShowUnsupportedGeometryEditMessage(showFeedbackIfUnsupported)
        };
    }

    private bool TryEditSelectedMarkupAppearance(bool showFeedbackIfUnsupported)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Select a single markup first.", "Edit Appearance",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1)
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show("Appearance editing is available for a single selected markup only.", "Edit Appearance",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var input = PromptInput(
            $"Edit {selectedMarkup.TypeDisplayText} Appearance",
            BuildMarkupAppearancePrompt(selectedMarkup),
            BuildMarkupAppearanceDefaultValue(selectedMarkup));
        if (input == null)
            return true;

        return TryEditSelectedMarkupAppearance(input, showFeedbackIfUnsupported);
    }

    private bool TryEditSelectedMarkupAppearance(string input, bool showFeedbackIfUnsupported)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1)
            return false;

        if (!TryParseMarkupAppearanceAssignments(input, out var values, out var errorMessage))
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show(errorMessage, $"Edit {selectedMarkup.TypeDisplayText} Appearance", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return true;
        }

        var before = _markupInteractionService.Capture(selectedMarkup);
        if (!ApplyMarkupAppearanceAssignments(selectedMarkup, values, out errorMessage))
        {
            if (showFeedbackIfUnsupported)
            {
                MessageBox.Show(errorMessage, $"Edit {selectedMarkup.TypeDisplayText} Appearance", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return true;
        }

        return CommitMarkupAppearanceEdit(selectedMarkup, before);
    }

    private bool TryEditSelectedLineGeometry(MarkupRecord markup, string input, bool showValidationFeedback)
    {
        if (markup.Vertices.Count < 2 || markup.Vertices.Count > 3)
        {
            if (showValidationFeedback)
            {
                MessageBox.Show(LineGeometryVertexCountMessage, "Edit Geometry",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        var start = markup.Vertices[0];
        var end = markup.Vertices[1];
        var delta = end - start;
        var length = delta.Length;
        var angleDeg = Math.Atan2(delta.Y, delta.X) * 180.0 / Math.PI;
        var lengthKey = GetLineGeometryLengthKey(markup);
        var lengthLabel = GetLineGeometryLengthLabel(markup);
        if (!TryParseMarkupGeometryAssignments(input, out var values, out var errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        if (!TryGetLineGeometryLengthAssignment(values, markup, length, out var nextLength) || nextLength <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, $"{lengthLabel} must be a positive number.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "angle", angleDeg, out var nextAngleDeg))
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Angle must be numeric.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        var before = _markupInteractionService.Capture(markup);
        if (!_markupInteractionService.SetLineGeometry(markup, nextLength, nextAngleDeg))
            return false;

        return CommitMarkupGeometryEdit(
            markup,
            before,
            $"Edit {markup.TypeDisplayText.ToLowerInvariant()} geometry",
            FormattableString.Invariant($"Type: {markup.TypeDisplayText}, {lengthLabel}: {nextLength:0.##}, Angle: {nextAngleDeg:0.##}"));
    }

    private bool TryEditSelectedAngularGeometry(MarkupRecord markup, string input, bool showValidationFeedback)
    {
        if (markup.Vertices.Count < 3)
            return false;

        if (!TryParseMarkupGeometryAssignments(input, out var values, out var errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "angle", Math.Abs(markup.ArcSweepDeg), out var nextAngleDeg) || nextAngleDeg <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Angle must be a positive number.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "radius", markup.Radius, out var nextRadius) || nextRadius <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Radius must be a positive number.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        var before = _markupInteractionService.Capture(markup);
        if (!_markupInteractionService.SetAngularGeometry(markup, nextAngleDeg, nextRadius))
            return false;

        return CommitMarkupGeometryEdit(
            markup,
            before,
            $"Edit {markup.TypeDisplayText.ToLowerInvariant()} geometry",
            FormattableString.Invariant($"Type: {markup.TypeDisplayText}, Angle: {Math.Abs(markup.ArcSweepDeg):0.##}, Radius: {markup.Radius:0.##}"));
    }

    private bool TryEditSelectedArcLengthGeometry(MarkupRecord markup, string input, bool showValidationFeedback)
    {
        if (markup.Vertices.Count < 3)
            return false;

        if (!TryParseMarkupGeometryAssignments(input, out var values, out var errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        var currentArcLength = Math.Abs(markup.ArcSweepDeg) * Math.PI / 180.0 * markup.Radius;
        if (!TryGetAssignment(values, "arclength", currentArcLength, out var nextArcLength) || nextArcLength <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Arc length must be a positive number.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "radius", markup.Radius, out var nextRadius) || nextRadius <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Radius must be a positive number.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        var before = _markupInteractionService.Capture(markup);
        if (!_markupInteractionService.SetArcLengthGeometry(markup, nextArcLength, nextRadius))
            return false;

        return CommitMarkupGeometryEdit(
            markup,
            before,
            $"Edit {markup.TypeDisplayText.ToLowerInvariant()} geometry",
            FormattableString.Invariant($"Type: {markup.TypeDisplayText}, Arc Length: {nextArcLength:0.##}, Radius: {markup.Radius:0.##}, Sweep: {markup.ArcSweepDeg:0.##}"));
    }

    private bool TryEditSelectedPolylineGeometry(MarkupRecord markup, string input, bool showValidationFeedback)
    {
        if (!TryParseMarkupGeometryAssignments(input, out var values, out var errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        var minCount = markup.Type == MarkupType.Polygon ? 3 : 2;
        if (!TryExtractVertices(values, minCount, out var vertices, out errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        var before = _markupInteractionService.Capture(markup);
        if (!_markupInteractionService.SetPolylineGeometry(markup, vertices))
            return false;

        return CommitMarkupGeometryEdit(
            markup,
            before,
            $"Edit {markup.TypeDisplayText.ToLowerInvariant()} geometry",
            FormattableString.Invariant($"Type: {markup.TypeDisplayText}, Vertices: {vertices.Count}"));
    }

    private static bool TryExtractVertices(Dictionary<string, double> values, int minimumCount, out List<Point> vertices, out string errorMessage)
    {
        vertices = new List<Point>();
        errorMessage = string.Empty;

        for (int i = 1; ; i++)
        {
            var hasX = values.TryGetValue($"x{i}", out var x);
            var hasY = values.TryGetValue($"y{i}", out var y);

            if (!hasX && !hasY)
                break;

            if (hasX != hasY)
            {
                errorMessage = $"Vertex {i} is incomplete — both x{i} and y{i} are required.";
                return false;
            }

            vertices.Add(new Point(x, y));
        }

        if (vertices.Count < minimumCount)
        {
            errorMessage = $"At least {minimumCount} vertices are required (x1, y1, x2, y2, …).";
            return false;
        }

        return true;
    }

    private bool TryEditSelectedBoundsGeometry(MarkupRecord markup, string input, bool showValidationFeedback)
    {
        var bounds = markup.BoundingRect != Rect.Empty
            ? markup.BoundingRect
            : new Rect(markup.Vertices[0], markup.Vertices[1]);

        if (!TryParseMarkupGeometryAssignments(input, out var values, out var errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "width", bounds.Width, out var width) || width <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Width must be a positive number.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "height", bounds.Height, out var height) || height <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Height must be a positive number.", $"Edit {markup.TypeDisplayText} Geometry");
            return true;
        }

        var before = _markupInteractionService.Capture(markup);
        if (!_markupInteractionService.SetBoundsGeometry(markup, width, height))
            return false;

        return CommitMarkupGeometryEdit(
            markup,
            before,
            $"Edit {markup.TypeDisplayText.ToLowerInvariant()} geometry",
            FormattableString.Invariant($"Type: {markup.TypeDisplayText}, Width: {markup.BoundingRect.Width:0.##}, Height: {markup.BoundingRect.Height:0.##}"));
    }

    private bool TryEditSelectedCircleGeometry(MarkupRecord markup, string input, bool showValidationFeedback)
    {
        if (!TryParseMarkupGeometryAssignments(input, out var values, out var errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, "Edit Circle Geometry");
            return true;
        }

        if (!TryGetAssignmentOrScalar(values, input, "radius", markup.Radius, out var radius))
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Enter a numeric radius value.", "Edit Circle Geometry");
            return true;
        }

        if (radius <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Radius must be a positive number.", "Edit Circle Geometry");
            return true;
        }

        var before = _markupInteractionService.Capture(markup);
        _markupInteractionService.SetRadius(markup, radius);
        return CommitMarkupGeometryEdit(markup, before, "Edit circle geometry", $"Type: {markup.TypeDisplayText}, Radius: {markup.Radius:0.##}");
    }

    private bool TryEditSelectedArcGeometry(MarkupRecord markup, string input, bool showValidationFeedback)
    {
        if (!TryParseMarkupGeometryAssignments(input, out var values, out var errorMessage))
        {
            ShowGeometryValidationWarning(showValidationFeedback, errorMessage, "Edit Arc Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "radius", markup.Radius, out var radius) || radius <= 0)
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Radius must be a positive number.", "Edit Arc Geometry");
            return true;
        }

        if (!TryGetAssignment(values, "start", markup.ArcStartDeg, out var startAngleDeg))
        {
            ShowGeometryValidationWarning(showValidationFeedback, "Start angle must be numeric.", "Edit Arc Geometry");
            return true;
        }

        var hasSweep = TryGetOptionalAssignment(values, "sweep", out var sweepAngleDeg);
        var hasEnd = TryGetOptionalAssignment(values, "end", out var endAngleDeg);

        var before = _markupInteractionService.Capture(markup);
        if (!_markupInteractionService.SetArcGeometry(markup, radius, startAngleDeg, hasSweep ? sweepAngleDeg : null, hasEnd ? endAngleDeg : null))
            return false;

        return CommitMarkupGeometryEdit(
            markup,
            before,
            "Edit arc geometry",
            FormattableString.Invariant($"Type: {markup.TypeDisplayText}, Radius: {markup.Radius:0.##}, Start: {markup.ArcStartDeg:0.##}, Sweep: {markup.ArcSweepDeg:0.##}"));
    }

    private bool CommitMarkupGeometryEdit(MarkupRecord markup, MarkupGeometrySnapshot before, string actionName, string logDetails)
    {
        var after = _markupInteractionService.Capture(markup);
        if (MarkupSnapshotsEqual(before, after))
            return true;

        _viewModel.UndoRedo.Execute(new MarkupGeometryChangeAction(
            actionName,
            _markupInteractionService,
            markup,
            before,
            after));
        _viewModel.MarkupTool.RefreshSelectedMarkupPresentation();
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Markup geometry edited", logDetails);
        return true;
    }

    private static void ShowGeometryValidationWarning(bool showValidationFeedback, string message, string title)
    {
        if (!showValidationFeedback)
            return;

        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private bool CommitMarkupAppearanceEdit(MarkupRecord markup, MarkupGeometrySnapshot before)
    {
        var after = _markupInteractionService.Capture(markup);
        if (MarkupSnapshotsEqual(before, after))
            return true;

        _viewModel.UndoRedo.Execute(new MarkupGeometryChangeAction(
            "Edit appearance for",
            _markupInteractionService,
            markup,
            before,
            after));
        _viewModel.MarkupTool.RefreshSelectedMarkupPresentation();
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Markup appearance edited",
            $"Type: {markup.TypeDisplayText}, Stroke: {markup.Appearance.StrokeColor}, Width: {markup.Appearance.StrokeWidth:0.##}, Opacity: {markup.Appearance.Opacity:0.##}");
        return true;
    }

    private static string BuildMarkupAppearancePrompt(MarkupRecord markup)
    {
        var fields = new List<string>
        {
            "stroke=#AARRGGBB or #RRGGBB",
            "width=number",
            "opacity=0-1 or 0-100"
        };

        if (SupportsMarkupFillAppearance(markup))
            fields.Add("fill=#AARRGGBB or none");

        if (SupportsMarkupFontAppearance(markup))
        {
            fields.Add("font=family name");
            fields.Add("fontsize=number");
        }

        if (SupportsMarkupDashAppearance(markup))
            fields.Add("dash=csv lengths, e.g. 6,3");

        return "Enter one or more appearance values as key=value pairs.\n\n" + string.Join(Environment.NewLine, fields);
    }

    private static string BuildMarkupAppearanceDefaultValue(MarkupRecord markup)
    {
        var lines = new List<string>
        {
            $"stroke={markup.Appearance.StrokeColor}",
            FormattableString.Invariant($"width={markup.Appearance.StrokeWidth:0.##}"),
            FormattableString.Invariant($"opacity={markup.Appearance.Opacity:0.##}")
        };

        if (SupportsMarkupFillAppearance(markup))
            lines.Add($"fill={markup.Appearance.FillColor}");

        if (SupportsMarkupFontAppearance(markup))
        {
            lines.Add($"font={markup.Appearance.FontFamily}");
            lines.Add(FormattableString.Invariant($"fontsize={markup.Appearance.FontSize:0.##}"));
        }

        if (SupportsMarkupDashAppearance(markup) && !string.IsNullOrWhiteSpace(markup.Appearance.DashArray))
            lines.Add($"dash={markup.Appearance.DashArray}");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryParseMarkupAppearanceAssignments(string input, out Dictionary<string, string> values, out string errorMessage)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        errorMessage = string.Empty;

        var tokens = input.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var separatorIndex = token.IndexOf('=');
            if (separatorIndex < 0)
            {
                errorMessage = "Use key=value pairs such as stroke=#FF0000 or opacity=0.8.";
                return false;
            }

            var key = token[..separatorIndex].Trim();
            var valueText = token[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(valueText))
            {
                errorMessage = "Use key=value pairs such as stroke=#FF0000 or opacity=0.8.";
                return false;
            }

            values[key] = valueText;
        }

        return true;
    }

    private bool ApplyMarkupAppearanceAssignments(MarkupRecord markup, Dictionary<string, string> values, out string errorMessage)
    {
        errorMessage = string.Empty;
        var changed = false;

        if (values.TryGetValue("stroke", out var strokeText))
        {
            if (!TryNormalizeMarkupColor(strokeText, allowNone: false, out var strokeColor))
            {
                errorMessage = "Stroke color must be a valid WPF color such as #FF0000 or #FFFF0000.";
                return false;
            }

            markup.Appearance.StrokeColor = strokeColor;
            changed = true;
        }

        if (values.TryGetValue("width", out var widthText))
        {
            if (!TryParseMarkupGeometryNumber(widthText, out var width) || width <= 0)
            {
                errorMessage = "Width must be a positive number.";
                return false;
            }

            markup.Appearance.StrokeWidth = width;
            changed = true;
        }

        if (values.TryGetValue("fill", out var fillText))
        {
            if (!TryNormalizeMarkupColor(fillText, allowNone: true, out var fillColor))
            {
                errorMessage = "Fill color must be a valid WPF color or 'none'.";
                return false;
            }

            markup.Appearance.FillColor = fillColor;
            changed = true;
        }

        if (values.TryGetValue("opacity", out var opacityText))
        {
            if (!TryParseMarkupGeometryNumber(opacityText, out var opacity))
            {
                errorMessage = "Opacity must be numeric.";
                return false;
            }

            if (opacity > 1.0 && opacity <= 100.0)
                opacity /= 100.0;

            if (opacity < 0.0 || opacity > 1.0)
            {
                errorMessage = "Opacity must be between 0 and 1, or between 0 and 100 for percent input.";
                return false;
            }

            markup.Appearance.Opacity = opacity;
            changed = true;
        }

        if (values.TryGetValue("font", out var fontFamily) || values.TryGetValue("fontfamily", out fontFamily))
        {
            if (string.IsNullOrWhiteSpace(fontFamily))
            {
                errorMessage = "Font family cannot be empty.";
                return false;
            }

            markup.Appearance.FontFamily = fontFamily.Trim();
            changed = true;
        }

        if (values.TryGetValue("fontsize", out var fontSizeText))
        {
            if (!TryParseMarkupGeometryNumber(fontSizeText, out var fontSize) || fontSize <= 0)
            {
                errorMessage = "Font size must be a positive number.";
                return false;
            }

            markup.Appearance.FontSize = fontSize;
            changed = true;
        }

        if (values.TryGetValue("dash", out var dashText))
        {
            if (!TryNormalizeDashPattern(dashText, out var dashPattern))
            {
                errorMessage = "Dash must be a comma-separated list of positive numbers, or 'solid'.";
                return false;
            }

            markup.Appearance.DashArray = dashPattern;
            changed = true;
        }

        if (!changed)
        {
            errorMessage = "Enter at least one appearance assignment.";
            return false;
        }

        if (SupportsMarkupFontAppearance(markup) && values.ContainsKey("fontsize"))
        {
            var anchor = markup.Vertices.Count > 0 ? markup.Vertices[0] : markup.BoundingRect.Location;
            var align = GetMarkupTextAlign(markup);
            markup.BoundingRect = EstimateMarkupTextBounds(anchor, markup.TextContent, markup.Appearance.FontSize, align);
        }

        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    private static bool TryNormalizeMarkupColor(string input, bool allowNone, out string normalized)
    {
        if (allowNone && string.Equals(input, "none", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "#00000000";
            return true;
        }

        try
        {
            var parsed = (Color)ColorConverter.ConvertFromString(input)!;
            normalized = parsed.ToString(CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static bool TryNormalizeDashPattern(string input, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(input) || string.Equals(input, "solid", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "none", StringComparison.OrdinalIgnoreCase))
        {
            normalized = string.Empty;
            return true;
        }

        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            normalized = string.Empty;
            return false;
        }

        var values = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (!TryParseMarkupGeometryNumber(part, out var length) || length <= 0)
            {
                normalized = string.Empty;
                return false;
            }

            values.Add(length.ToString("0.##", CultureInfo.InvariantCulture));
        }

        normalized = string.Join(',', values);
        return true;
    }

    private static bool SupportsMarkupFillAppearance(MarkupRecord markup)
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

    private static bool SupportsMarkupFontAppearance(MarkupRecord markup)
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

    private static bool SupportsMarkupDashAppearance(MarkupRecord markup)
    {
        return markup.Type != MarkupType.Text && markup.Type != MarkupType.Stamp;
    }

    private static bool ShowUnsupportedGeometryEditMessage(bool showFeedbackIfUnsupported)
    {
        if (showFeedbackIfUnsupported)
        {
            MessageBox.Show(UnsupportedGeometryEditMessage, "Edit Geometry",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return false;
    }

    private static string BuildPolylineGeometryDefaultValue(MarkupRecord markup)
    {
        var lines = new List<string>(markup.Vertices.Count * 2);
        for (int i = 0; i < markup.Vertices.Count; i++)
        {
            lines.Add(FormattableString.Invariant($"x{i + 1}={markup.Vertices[i].X:0.##}"));
            lines.Add(FormattableString.Invariant($"y{i + 1}={markup.Vertices[i].Y:0.##}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildBoundsGeometryDefaultValue(MarkupRecord markup)
    {
        var bounds = markup.BoundingRect != Rect.Empty
            ? markup.BoundingRect
            : new Rect(markup.Vertices[0], markup.Vertices[1]);

        return string.Join(Environment.NewLine, new[]
        {
            FormattableString.Invariant($"width={bounds.Width:0.##}"),
            FormattableString.Invariant($"height={bounds.Height:0.##}")
        });
    }

    private static string BuildAngularGeometryDefaultValue(MarkupRecord markup)
    {
        return string.Join(Environment.NewLine, new[]
        {
            FormattableString.Invariant($"angle={Math.Abs(markup.ArcSweepDeg):0.##}"),
            FormattableString.Invariant($"radius={markup.Radius:0.##}")
        });
    }

    private static string BuildArcLengthGeometryDefaultValue(MarkupRecord markup)
    {
        var arcLength = Math.Abs(markup.ArcSweepDeg) * Math.PI / 180.0 * markup.Radius;
        return string.Join(Environment.NewLine, new[]
        {
            FormattableString.Invariant($"arclength={arcLength:0.##}"),
            FormattableString.Invariant($"radius={markup.Radius:0.##}")
        });
    }

    private static bool TryBuildGeometryPrompt(MarkupRecord markup, out string title, out string prompt, out string defaultValue)
    {
        title = string.Empty;
        prompt = string.Empty;
        defaultValue = string.Empty;

        switch (markup.Type)
        {
            case MarkupType.Rectangle:
            case MarkupType.Stamp:
            case MarkupType.Hyperlink:
            case MarkupType.Box:
            case MarkupType.Panel:
                title = $"Edit {markup.TypeDisplayText} Geometry";
                prompt = "Enter width and height. The markup's top-left corner stays fixed.\n\nExamples:\nwidth=24\nheight=12";
                defaultValue = BuildBoundsGeometryDefaultValue(markup);
                return true;
            case MarkupType.Circle:
                title = "Edit Circle Geometry";
                prompt = "Enter radius:\n\nExamples:\n12\nradius=12";
                defaultValue = FormattableString.Invariant($"radius={markup.Radius:0.##}");
                return true;
            case MarkupType.Arc:
                title = "Edit Arc Geometry";
                prompt = "Enter radius, start angle, and either end or sweep.\n\nExamples:\nradius=12\nstart=30\nend=150\n\nor\nradius=12\nstart=30\nsweep=120";
                defaultValue = BuildArcGeometryDefaultValue(markup);
                return true;
            case MarkupType.Polyline:
            case MarkupType.Polygon:
                var minVerts = markup.Type == MarkupType.Polygon ? 3 : 2;
                title = $"Edit {markup.TypeDisplayText} Geometry";
                prompt = $"Enter vertex coordinates as x1=…, y1=…, x2=…, y2=…, etc. At least {minVerts} vertices are required.\n\nExamples:\nx1=10\ny1=20\nx2=30\ny2=40";
                defaultValue = BuildPolylineGeometryDefaultValue(markup);
                return true;
            case MarkupType.Dimension:
            case MarkupType.Measurement:
                if (IsArcLengthDimension(markup))
                {
                    title = $"Edit {markup.TypeDisplayText} Geometry";
                    prompt = "Enter arc length and optional radius. The arc center and start angle stay fixed.\n\nExamples:\narclength=24\nradius=12";
                    defaultValue = BuildArcLengthGeometryDefaultValue(markup);
                    return true;
                }

                if (IsAngularDimension(markup))
                {
                    title = $"Edit {markup.TypeDisplayText} Geometry";
                    prompt = "Enter angle and optional radius. The vertex and first ray stay fixed.\n\nExamples:\nangle=45\nradius=18";
                    defaultValue = BuildAngularGeometryDefaultValue(markup);
                    return true;
                }

                if (markup.Vertices.Count < 2 || markup.Vertices.Count > 3)
                    return false;

                var start = markup.Vertices[0];
                var end = markup.Vertices[1];
                var delta = end - start;
                var angleDeg = Math.Atan2(delta.Y, delta.X) * 180.0 / Math.PI;
                var lengthKey = GetLineGeometryLengthKey(markup);
                title = $"Edit {markup.TypeDisplayText} Geometry";
                prompt = $"Enter {GetLineGeometryLengthLabel(markup).ToLowerInvariant()} and optional angle. The first point stays fixed.\n\nExamples:\n{lengthKey}=24\nangle=30";
                defaultValue = BuildLineGeometryDefaultValue(lengthKey, delta.Length, angleDeg);
                return true;
            default:
                return false;
        }
    }

    private static string BuildArcGeometryDefaultValue(MarkupRecord markup)
    {
        var endAngle = NormalizeMarkupAngle(markup.ArcStartDeg + markup.ArcSweepDeg);
        return string.Join(Environment.NewLine, new[]
        {
            FormattableString.Invariant($"radius={markup.Radius:0.##}"),
            FormattableString.Invariant($"start={NormalizeMarkupAngle(markup.ArcStartDeg):0.##}"),
            FormattableString.Invariant($"end={endAngle:0.##}")
        });
    }

    private static string BuildLineGeometryDefaultValue(string lengthKey, double length, double angleDeg)
    {
        return string.Join(Environment.NewLine, new[]
        {
            FormattableString.Invariant($"{lengthKey}={length:0.##}"),
            FormattableString.Invariant($"angle={NormalizeMarkupAngle(angleDeg):0.##}")
        });
    }

    private static bool TryGetLineGeometryLengthAssignment(Dictionary<string, double> values, MarkupRecord markup, double fallbackValue, out double value)
    {
        var lengthKey = GetLineGeometryLengthKey(markup);
        if (values.TryGetValue(lengthKey, out value))
            return true;

        if (!string.Equals(lengthKey, "length", StringComparison.OrdinalIgnoreCase) && values.TryGetValue("length", out value))
            return true;

        value = fallbackValue;
        return true;
    }

    private static string GetLineGeometryLengthKey(MarkupRecord markup)
    {
        if (string.Equals(markup.Metadata.Subject, "Radial", StringComparison.OrdinalIgnoreCase))
            return "radius";

        if (string.Equals(markup.Metadata.Subject, "Diameter", StringComparison.OrdinalIgnoreCase))
            return "diameter";

        return "length";
    }

    private static string GetLineGeometryLengthLabel(MarkupRecord markup)
    {
        if (string.Equals(markup.Metadata.Subject, "Radial", StringComparison.OrdinalIgnoreCase))
            return "Radius";

        if (string.Equals(markup.Metadata.Subject, "Diameter", StringComparison.OrdinalIgnoreCase))
            return "Diameter";

        return "Length";
    }

    private static bool IsLineGeometryReadoutEligible(MarkupRecord markup, int activeVertexIndex)
    {
        if (activeVertexIndex < 0 || activeVertexIndex > 1)
            return false;

        if (markup.Type is not MarkupType.Dimension and not MarkupType.Measurement)
            return false;

        if (markup.Vertices.Count < 2 || markup.Vertices.Count > 3)
            return false;

        return !IsAngularDimension(markup) && !IsArcLengthDimension(markup);
    }

    private Point GetConstrainedLineGeometryDragPoint(MarkupRecord markup, int activeVertexIndex, Point canvasPoint)
    {
        var snappedPoint = ApplyDrawingSnap(canvasPoint);
        var fixedPoint = activeVertexIndex == 0 ? markup.Vertices[1] : markup.Vertices[0];
        var snapIncrementDeg = GetMarkupAngleSnapIncrement();
        if (snapIncrementDeg <= 0)
            return snappedPoint;

        return ConstrainToAngleIncrement(fixedPoint, snappedPoint, snapIncrementDeg);
    }

    private static string BuildLineGeometryReadout(MarkupRecord markup)
    {
        if (markup.Vertices.Count < 2)
            return string.Empty;

        var start = markup.Vertices[0];
        var end = markup.Vertices[1];
        var delta = end - start;
        var length = delta.Length;
        var angleDeg = NormalizeMarkupAngle(Math.Atan2(delta.Y, delta.X) * 180.0 / Math.PI);
        return FormattableString.Invariant($"{GetLineGeometryLengthLabel(markup)} {length:0.##}  Angle {angleDeg:0.##} deg");
    }

    private static bool IsAngularDimension(MarkupRecord markup)
    {
        return markup.Type is MarkupType.Dimension or MarkupType.Measurement &&
               string.Equals(markup.Metadata.Subject, "Angular", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArcLengthDimension(MarkupRecord markup)
    {
        return markup.Type is MarkupType.Dimension or MarkupType.Measurement &&
               string.Equals(markup.Metadata.Subject, "ArcLength", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseMarkupGeometryAssignments(string input, out Dictionary<string, double> values, out string errorMessage)
    {
        values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        errorMessage = string.Empty;
        var tokens = input
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var separatorIndex = token.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            var key = token[..separatorIndex].Trim();
            var valueText = token[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(valueText))
            {
                errorMessage = "Use key=value pairs such as radius=12 or start=30.";
                return false;
            }

            if (!TryParseMarkupGeometryNumber(valueText, out var value))
            {
                errorMessage = $"'{valueText}' is not a valid number.";
                return false;
            }

            values[key] = value;
        }

        return true;
    }

    private static bool TryGetAssignment(Dictionary<string, double> values, string key, double fallbackValue, out double value)
    {
        if (values.TryGetValue(key, out value))
            return true;

        value = fallbackValue;
        return true;
    }

    private static bool TryGetOptionalAssignment(Dictionary<string, double> values, string key, out double value)
    {
        return values.TryGetValue(key, out value);
    }

    private static bool TryGetAssignmentOrScalar(Dictionary<string, double> values, string input, string key, double fallbackValue, out double value)
    {
        if (values.TryGetValue(key, out value))
            return true;

        var trimmed = input.Trim();
        if (trimmed.Contains('=') || trimmed.Contains('\n') || trimmed.Contains('\r') || trimmed.Contains(';'))
        {
            value = fallbackValue;
            return false;
        }

        return TryParseMarkupGeometryNumber(trimmed, out value);
    }

    private static bool TryParseMarkupGeometryNumber(string input, out double value)
    {
        return double.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value);
    }

    private bool DeleteSelectedMarkupSelection()
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count == 0)
            return false;

        var actions = selectionSet
            .Select(markup => new RemoveItemAction<MarkupRecord>(_viewModel.Markups, markup, $"{markup.TypeDisplayText} markup"))
            .Cast<IUndoableAction>()
            .ToList();

        ClearMarkupSelection();
        _viewModel.UndoRedo.Execute(actions.Count == 1
            ? actions[0]
            : new CompositeAction("Delete markup annotation", actions));
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Markup deleted", $"Count: {actions.Count}");
        return true;
    }

    private bool TryStartMarkupVertexDrag(Point canvasPoint)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (selectionSet.Count != 1 || !_markupInteractionService.CanEditVertices(selectedMarkup))
            return false;

        var vertexIndex = _markupInteractionService.HitTestVertexHandle(canvasPoint, selectedMarkup, GetMarkupHitTolerance());
        if (vertexIndex < 0)
            return false;

        _isDraggingMarkupVertex = true;
        _activeMarkupVertexIndex = vertexIndex;
        _vertexDraggedMarkup = selectedMarkup;
        _markupVertexStartSnapshot = _markupInteractionService.Capture(selectedMarkup);
        _lastMousePosition = canvasPoint;
        _dragStartCanvasPosition = canvasPoint;
        _mobileSelectionCandidate = _isMobileView;

        PlanCanvas.CaptureMouse();
        SkiaBackground.RequestRedraw();
        return true;
    }

    private bool TryStartMarkupResizeDrag(Point canvasPoint)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return false;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        if (!_markupInteractionService.CanResize(selectionSet))
            return false;

        var bounds = _markupInteractionService.GetAggregateBounds(selectionSet);
        var handle = _markupInteractionService.HitTestResizeHandle(canvasPoint, bounds, GetMarkupHitTolerance());
        if (handle == MarkupResizeHandle.None)
            return false;

        _isResizingMarkup = true;
        _activeMarkupResizeHandle = handle;
        _markupResizeStartBounds = bounds;
        _lastMousePosition = canvasPoint;
        _dragStartCanvasPosition = canvasPoint;
        _mobileSelectionCandidate = _isMobileView;
        _resizedMarkups.Clear();
        _markupResizeStartSnapshots.Clear();

        foreach (var markup in selectionSet)
        {
            _resizedMarkups.Add(markup);
            _markupResizeStartSnapshots[markup.Id] = _markupInteractionService.Capture(markup);
        }

        PlanCanvas.CaptureMouse();
        SkiaBackground.RequestRedraw();
        return true;
    }

    private bool TryStartMarkupSelectionDrag(Point canvasPoint)
    {
        var hit = _canvasInteractionShadowTree.HitTest(canvasPoint, GetMarkupHitTolerance());
        if (hit?.Kind != ShadowGeometryTree.ShadowNodeKind.Markup || hit.Source is not MarkupRecord markup)
            return false;

        StartMarkupSelectionDrag(markup, canvasPoint);
        return true;
    }

    private void StartMarkupSelectionDrag(MarkupRecord markup, Point canvasPoint)
    {
        SelectMarkupOnCanvas(markup);

        _isDraggingMarkup = true;
        _lastMousePosition = canvasPoint;
        _dragStartCanvasPosition = canvasPoint;
        _mobileSelectionCandidate = _isMobileView;
        _draggedMarkups.Clear();
        _markupDragStartSnapshots.Clear();

        foreach (var candidate in _markupInteractionService.GetSelectionSet(markup, _viewModel.Markups))
        {
            _draggedMarkups.Add(candidate);
            _markupDragStartSnapshots[candidate.Id] = _markupInteractionService.Capture(candidate);
        }

        PlanCanvas.CaptureMouse();
        SkiaBackground.RequestRedraw();
    }

    private void UpdateMarkupResizePreview(Point canvasPoint)
    {
        if (!_isResizingMarkup || _resizedMarkups.Count == 0 || _markupResizeStartBounds == Rect.Empty)
            return;

        BeginFastInteractionMode();
        if (_mobileSelectionCandidate &&
            (Math.Abs(canvasPoint.X - _dragStartCanvasPosition.X) > 4 || Math.Abs(canvasPoint.Y - _dragStartCanvasPosition.Y) > 4))
        {
            _mobileSelectionCandidate = false;
        }

        var snappedPoint = ApplyDrawingSnap(canvasPoint);
        var resizedBounds = _markupInteractionService.BuildResizedBounds(
            _markupResizeStartBounds,
            snappedPoint,
            _activeMarkupResizeHandle,
            GetMarkupMinimumSize());

        foreach (var markup in _resizedMarkups)
        {
            if (_markupResizeStartSnapshots.TryGetValue(markup.Id, out var snapshot))
                _markupInteractionService.Resize(markup, snapshot, _markupResizeStartBounds, resizedBounds);
        }

        _lastMousePosition = snappedPoint;
        SkiaBackground.RequestRedraw();
    }

    private void UpdateDraggedMarkupRadiusPreview(Point canvasPoint)
    {
        if (!_isDraggingMarkupRadius || _radiusDraggedMarkup == null || _radiusDraggedMarkup.Vertices.Count == 0)
            return;

        BeginFastInteractionMode();
        if (_mobileSelectionCandidate &&
            (Math.Abs(canvasPoint.X - _dragStartCanvasPosition.X) > 4 || Math.Abs(canvasPoint.Y - _dragStartCanvasPosition.Y) > 4))
        {
            _mobileSelectionCandidate = false;
        }

        var snappedPoint = ApplyDrawingSnap(canvasPoint);
        var center = _markupInteractionService.GetRadiusPivotPoint(_radiusDraggedMarkup);
        var radius = (snappedPoint - center).Length;
        _markupInteractionService.SetRadius(_radiusDraggedMarkup, radius);
        _lastMousePosition = canvasPoint;
        SkiaBackground.RequestRedraw();
    }

    private void UpdateDraggedMarkupArcAnglePreview(Point canvasPoint)
    {
        if (!_isDraggingMarkupArcAngle || _arcAngleDraggedMarkup == null || _activeMarkupArcAngleHandle == MarkupArcAngleHandle.None)
            return;

        BeginFastInteractionMode();
        if (_mobileSelectionCandidate &&
            (Math.Abs(canvasPoint.X - _dragStartCanvasPosition.X) > 4 || Math.Abs(canvasPoint.Y - _dragStartCanvasPosition.Y) > 4))
        {
            _mobileSelectionCandidate = false;
        }

        var snapIncrementDeg = GetMarkupAngleSnapIncrement();
        var dragPoint = snapIncrementDeg > 0 ? canvasPoint : ApplyDrawingSnap(canvasPoint);
        _markupInteractionService.SetArcAngleFromPoint(_arcAngleDraggedMarkup, _activeMarkupArcAngleHandle, dragPoint, snapIncrementDeg);
        _lastMousePosition = canvasPoint;
        SkiaBackground.RequestRedraw();
    }

    private void UpdateDraggedMarkupVertexPreview(Point canvasPoint)
    {
        if (!_isDraggingMarkupVertex || _vertexDraggedMarkup == null || _activeMarkupVertexIndex < 0)
            return;

        BeginFastInteractionMode();
        if (_mobileSelectionCandidate &&
            (Math.Abs(canvasPoint.X - _dragStartCanvasPosition.X) > 4 || Math.Abs(canvasPoint.Y - _dragStartCanvasPosition.Y) > 4))
        {
            _mobileSelectionCandidate = false;
        }

        if (IsLineGeometryReadoutEligible(_vertexDraggedMarkup, _activeMarkupVertexIndex))
        {
            var constrainedPoint = GetConstrainedLineGeometryDragPoint(_vertexDraggedMarkup, _activeMarkupVertexIndex, canvasPoint);
            if (_activeMarkupVertexIndex == 0)
            {
                _markupInteractionService.SetLineGeometryByEndpoints(
                    _vertexDraggedMarkup,
                    constrainedPoint,
                    _vertexDraggedMarkup.Vertices[1]);
            }
            else
            {
                _markupInteractionService.SetLineGeometryByEndpoints(
                    _vertexDraggedMarkup,
                    _vertexDraggedMarkup.Vertices[0],
                    constrainedPoint);
            }
        }
        else
        {
            var snappedPoint = ApplyDrawingSnap(canvasPoint);
            _markupInteractionService.MoveVertex(_vertexDraggedMarkup, _activeMarkupVertexIndex, snappedPoint);
        }

        _lastMousePosition = canvasPoint;
        SkiaBackground.RequestRedraw();
    }

    private void UpdateDraggedMarkupPreview(Point canvasPoint)
    {
        if (!_isDraggingMarkup || _draggedMarkups.Count == 0)
            return;

        BeginFastInteractionMode();
        var snappedPoint = ApplyDrawingSnap(canvasPoint);
        var delta = snappedPoint - _lastMousePosition;
        if (_mobileSelectionCandidate &&
            (Math.Abs(canvasPoint.X - _dragStartCanvasPosition.X) > 4 || Math.Abs(canvasPoint.Y - _dragStartCanvasPosition.Y) > 4))
        {
            _mobileSelectionCandidate = false;
        }

        if (delta.X == 0 && delta.Y == 0)
            return;

        foreach (var markup in _draggedMarkups)
            _markupInteractionService.Translate(markup, new Vector(delta.X, delta.Y));

        _lastMousePosition = snappedPoint;
        SkiaBackground.RequestRedraw();
    }

    private void FinishMarkupResizeDrag()
    {
        if (!_isResizingMarkup)
            return;

        var actions = _resizedMarkups
            .Where(markup => _markupResizeStartSnapshots.TryGetValue(markup.Id, out var snapshot) &&
                             !MarkupSnapshotsEqual(snapshot, _markupInteractionService.Capture(markup)))
            .Select(markup => new MarkupGeometryChangeAction(
                "Resize",
                _markupInteractionService,
                markup,
                _markupResizeStartSnapshots[markup.Id],
                _markupInteractionService.Capture(markup)))
            .Cast<IUndoableAction>()
            .ToList();

        _isResizingMarkup = false;
        _activeMarkupResizeHandle = MarkupResizeHandle.None;
        _markupResizeStartBounds = Rect.Empty;
        _resizedMarkups.Clear();
        _markupResizeStartSnapshots.Clear();

        if (actions.Count > 0)
        {
            _viewModel.UndoRedo.Execute(new CompositeAction("Resize markup annotation", actions));
            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Edit, "Markup resized", $"Count: {actions.Count}");
        }
        else
        {
            SkiaBackground.RequestRedraw();
        }
    }

    private void FinishMarkupRadiusDrag()
    {
        if (!_isDraggingMarkupRadius || _radiusDraggedMarkup == null || _markupRadiusStartSnapshot == null)
            return;

        var currentSnapshot = _markupInteractionService.Capture(_radiusDraggedMarkup);
        var changed = !MarkupSnapshotsEqual(_markupRadiusStartSnapshot, currentSnapshot);

        _isDraggingMarkupRadius = false;
        var markup = _radiusDraggedMarkup;
        var startSnapshot = _markupRadiusStartSnapshot;
        _radiusDraggedMarkup = null;
        _markupRadiusStartSnapshot = null;

        if (changed)
        {
            _viewModel.UndoRedo.Execute(new MarkupGeometryChangeAction(
                "Edit radius of",
                _markupInteractionService,
                markup,
                startSnapshot,
                currentSnapshot));
            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Edit, "Markup radius edited", $"Type: {markup.TypeDisplayText}");
        }
        else
        {
            SkiaBackground.RequestRedraw();
        }
    }

    private void FinishMarkupArcAngleDrag()
    {
        if (!_isDraggingMarkupArcAngle || _arcAngleDraggedMarkup == null || _markupArcAngleStartSnapshot == null)
            return;

        var currentSnapshot = _markupInteractionService.Capture(_arcAngleDraggedMarkup);
        var changed = !MarkupSnapshotsEqual(_markupArcAngleStartSnapshot, currentSnapshot);

        _isDraggingMarkupArcAngle = false;
        _activeMarkupArcAngleHandle = MarkupArcAngleHandle.None;
        var markup = _arcAngleDraggedMarkup;
        var startSnapshot = _markupArcAngleStartSnapshot;
        _arcAngleDraggedMarkup = null;
        _markupArcAngleStartSnapshot = null;

        if (changed)
        {
            _viewModel.UndoRedo.Execute(new MarkupGeometryChangeAction(
                "Edit arc angle of",
                _markupInteractionService,
                markup,
                startSnapshot,
                currentSnapshot));
            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Edit, "Markup arc edited", $"Type: {markup.TypeDisplayText}");
        }
        else
        {
            SkiaBackground.RequestRedraw();
        }
    }

    private void FinishMarkupVertexDrag()
    {
        if (!_isDraggingMarkupVertex || _vertexDraggedMarkup == null || _markupVertexStartSnapshot == null)
            return;

        var currentSnapshot = _markupInteractionService.Capture(_vertexDraggedMarkup);
        var changed = !MarkupSnapshotsEqual(_markupVertexStartSnapshot, currentSnapshot);

        _isDraggingMarkupVertex = false;
        var markup = _vertexDraggedMarkup;
        var startSnapshot = _markupVertexStartSnapshot;
        _vertexDraggedMarkup = null;
        _markupVertexStartSnapshot = null;

        if (changed)
        {
            _viewModel.UndoRedo.Execute(new MarkupGeometryChangeAction(
                "Edit",
                _markupInteractionService,
                markup,
                startSnapshot,
                currentSnapshot));
            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Edit, "Markup vertex edited", $"Type: {markup.TypeDisplayText}");
        }
        else
        {
            SkiaBackground.RequestRedraw();
        }
    }

    private void FinishMarkupSelectionDrag()
    {
        if (!_isDraggingMarkup)
            return;

        var actions = _draggedMarkups
            .Where(markup => _markupDragStartSnapshots.TryGetValue(markup.Id, out var snapshot) &&
                             !MarkupSnapshotsEqual(snapshot, _markupInteractionService.Capture(markup)))
            .Select(markup => new MarkupGeometryChangeAction(
                "Move",
                _markupInteractionService,
                markup,
                _markupDragStartSnapshots[markup.Id],
                _markupInteractionService.Capture(markup)))
            .Cast<IUndoableAction>()
            .ToList();

        _isDraggingMarkup = false;
        _draggedMarkups.Clear();
        _markupDragStartSnapshots.Clear();

        if (actions.Count > 0)
        {
            _viewModel.UndoRedo.Execute(new CompositeAction("Move markup annotation", actions));
            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Edit, "Markup moved", $"Count: {actions.Count}");
        }
        else
        {
            SkiaBackground.RequestRedraw();
        }
    }

    private void SelectMarkupOnCanvas(MarkupRecord markup)
    {
        if (!ReferenceEquals(_viewModel.MarkupTool.SelectedMarkup, markup))
        {
            _activeMarkupVertexIndex = -1;
            CancelPendingMarkupVertexInsertion(logCancellation: false);
        }

        _selectedSketchPrimitive = null;
        _viewModel.ClearComponentSelection();

        _viewModel.MarkupTool.SelectedMarkup = markup;
    }

    private void ClearMarkupSelection()
    {
        CancelPendingMarkupVertexInsertion(logCancellation: false);
        _activeMarkupVertexIndex = -1;
        _isDraggingMarkupArcAngle = false;
        _activeMarkupArcAngleHandle = MarkupArcAngleHandle.None;
        _arcAngleDraggedMarkup = null;
        _markupArcAngleStartSnapshot = null;
        _radiusDraggedMarkup = null;
        _markupRadiusStartSnapshot = null;
        _vertexDraggedMarkup = null;
        _markupVertexStartSnapshot = null;
        _viewModel.MarkupTool.SelectedMarkup = null;
    }

    private void DrawSelectedMarkupOverlay(ICanvas2DRenderer renderer)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        var bounds = _markupInteractionService.GetAggregateBounds(selectionSet);
        if (bounds == Rect.Empty)
            return;

        var canEditVertices = selectionSet.Count == 1 && _markupInteractionService.CanEditVertices(selectedMarkup);
        var canEditArcAngles = selectionSet.Count == 1 && _markupInteractionService.CanEditArcAngles(selectedMarkup);
        var canEditRadius = selectionSet.Count == 1 && _markupInteractionService.CanEditRadius(selectedMarkup);
        var handleMode = GetMarkupHandleOverlayMode(
            canEditArcAngles,
            canEditRadius,
            canEditVertices,
            _markupInteractionService.CanResize(selectionSet));

        var highlightStyle = new RenderStyle
        {
            StrokeColor = "#FFFF8A00",
            StrokeWidth = 2.0,
            DashPattern = new[] { 8f, 4f }
        };

        renderer.DrawRect(bounds, highlightStyle);

        if (handleMode == MarkupHandleOverlayMode.DirectGeometry && canEditArcAngles)
        {
            foreach (var handle in _markupInteractionService.GetArcAngleHandles(selectedMarkup))
            {
                renderer.DrawGrip(
                    _markupInteractionService.GetArcAngleHandlePoint(selectedMarkup, handle),
                    hot: _isDraggingMarkupArcAngle && handle == _activeMarkupArcAngleHandle);
            }

            if (_isDraggingMarkupArcAngle)
            {
                DrawMarkupArcReadout(renderer, selectedMarkup);
            }
        }

        if (handleMode == MarkupHandleOverlayMode.DirectGeometry && canEditRadius)
        {
            renderer.DrawGrip(
                _markupInteractionService.GetRadiusHandlePoint(selectedMarkup),
                hot: _isDraggingMarkupRadius);

            if (_isDraggingMarkupRadius)
            {
                DrawMarkupRadiusReadout(renderer, selectedMarkup);
            }
        }

        if (handleMode == MarkupHandleOverlayMode.Vertices)
        {
            var points = _markupInteractionService.GetVertexHandlePoints(selectedMarkup);
            for (int i = 0; i < points.Count; i++)
            {
                renderer.DrawGrip(
                    points[i],
                    hot: _isDraggingMarkupVertex && i == _activeMarkupVertexIndex);
            }

            if (_isDraggingMarkupVertex && IsLineGeometryReadoutEligible(selectedMarkup, _activeMarkupVertexIndex))
            {
                DrawMarkupLineGeometryReadout(renderer, selectedMarkup, points[_activeMarkupVertexIndex]);
            }
        }
        else if (handleMode == MarkupHandleOverlayMode.Resize)
        {
            foreach (var handle in Enum.GetValues<MarkupResizeHandle>())
            {
                if (handle == MarkupResizeHandle.None)
                    continue;

                renderer.DrawGrip(
                    _markupInteractionService.GetResizeHandlePoint(bounds, handle),
                    hot: _isResizingMarkup && handle == _activeMarkupResizeHandle);
            }
        }
    }

    internal static MarkupHandleOverlayMode GetMarkupHandleOverlayMode(
        bool canEditArcAngles,
        bool canEditRadius,
        bool canEditVertices,
        bool canResize)
    {
        if (canEditArcAngles || canEditRadius)
            return MarkupHandleOverlayMode.DirectGeometry;

        if (canEditVertices)
            return MarkupHandleOverlayMode.Vertices;

        if (canResize)
            return MarkupHandleOverlayMode.Resize;

        return MarkupHandleOverlayMode.None;
    }

    private double GetMarkupHitTolerance()
    {
        return Math.Max(4.0, 8.0 / Math.Max(PlanCanvasScale.ScaleX, 0.1));
    }

    private double GetMarkupMinimumSize()
    {
        return Math.Max(6.0, 12.0 / Math.Max(PlanCanvasScale.ScaleX, 0.1));
    }

    private double GetMarkupAngleSnapIncrement()
    {
        if (_viewModel.IsPolarActive && _viewModel.PolarIncrementDeg > 0)
            return _viewModel.PolarIncrementDeg;

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            return 15.0;

        return 0.0;
    }

    private void DrawMarkupArcReadout(ICanvas2DRenderer renderer, MarkupRecord markup)
    {
        var activeHandlePoint = _markupInteractionService.GetArcAngleHandlePoint(markup, _activeMarkupArcAngleHandle);
        var anchor = GetMarkupReadoutAnchor(renderer, activeHandlePoint, markup.BoundingRect);
        var snapIncrementDeg = GetMarkupAngleSnapIncrement();
        string readout;

        if (IsAngularDimension(markup))
        {
            readout = FormattableString.Invariant(
                $"Angle {Math.Abs(markup.ArcSweepDeg):0.#} deg  Radius {markup.Radius:0.##}");
        }
        else if (IsArcLengthDimension(markup))
        {
            var arcLength = Math.Abs(markup.ArcSweepDeg) * Math.PI / 180.0 * markup.Radius;
            readout = FormattableString.Invariant(
                $"Arc Length {arcLength:0.##}  Sweep {markup.ArcSweepDeg:0.#} deg  Radius {markup.Radius:0.##}");
        }
        else
        {
            var endAngle = NormalizeMarkupAngle(markup.ArcStartDeg + markup.ArcSweepDeg);
            readout = FormattableString.Invariant(
                $"Start {NormalizeMarkupAngle(markup.ArcStartDeg):0.#} deg  End {endAngle:0.#} deg  Sweep {markup.ArcSweepDeg:0.#} deg");
        }

        if (snapIncrementDeg > 0)
            readout += FormattableString.Invariant($"  Snap {snapIncrementDeg:0.#} deg");

        renderer.DrawTextBox(anchor, readout, new RenderStyle
        {
            StrokeColor = "#FF111827",
            FontSize = 12.0,
            Bold = true
        }, boxFill: "#F2FFF7ED", padding: 6.0);
    }

    private void DrawMarkupRadiusReadout(ICanvas2DRenderer renderer, MarkupRecord markup)
    {
        var handlePoint = _markupInteractionService.GetRadiusHandlePoint(markup);
        var anchor = GetMarkupReadoutAnchor(renderer, handlePoint, markup.BoundingRect);
        var readout = FormattableString.Invariant($"Radius {markup.Radius:0.##}");

        renderer.DrawTextBox(anchor, readout, new RenderStyle
        {
            StrokeColor = "#FF111827",
            FontSize = 12.0,
            Bold = true
        }, boxFill: "#F2EFF6FF", padding: 6.0);
    }

    private void DrawMarkupLineGeometryReadout(ICanvas2DRenderer renderer, MarkupRecord markup, Point handlePoint)
    {
        var anchor = GetMarkupReadoutAnchor(renderer, handlePoint, markup.BoundingRect);
        var readout = BuildLineGeometryReadout(markup);

        renderer.DrawTextBox(anchor, readout, new RenderStyle
        {
            StrokeColor = "#FF111827",
            FontSize = 12.0,
            Bold = true
        }, boxFill: "#F6EFF6FF", padding: 6.0);
    }

    private static Point GetMarkupReadoutAnchor(ICanvas2DRenderer renderer, Point handlePoint, Rect bounds)
    {
        var offset = Math.Max(12.0 / Math.Max(renderer.Zoom, 0.1), 2.0);
        var x = handlePoint.X + offset;
        var y = handlePoint.Y - offset;

        if (bounds != Rect.Empty)
        {
            x = Math.Max(x, bounds.Left + offset * 0.5);
            y = Math.Max(y, bounds.Top + offset * 1.5);
        }

        return new Point(x, y);
    }

    private static Point ConstrainToAngleIncrement(Point anchor, Point target, double incrementDeg)
    {
        if (incrementDeg <= 0)
            return target;

        var dx = target.X - anchor.X;
        var dy = target.Y - anchor.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1)
            return target;

        var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var snappedAngleDeg = Math.Round(angleDeg / incrementDeg) * incrementDeg;
        var radians = snappedAngleDeg * Math.PI / 180.0;
        return new Point(
            anchor.X + dist * Math.Cos(radians),
            anchor.Y + dist * Math.Sin(radians));
    }

    private static double NormalizeMarkupAngle(double angleDeg)
    {
        var normalized = angleDeg % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static bool CanEditStructuredMarkupText(MarkupRecord markup, out string annotationKind, out string textRole)
    {
        annotationKind = string.Empty;
        textRole = string.Empty;

        if (markup.Type != MarkupType.Text)
            return false;

        if (!markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationKindField, out var annotationKindValue) ||
            !markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationTextRoleField, out var textRoleValue))
            return false;

        annotationKind = annotationKindValue ?? string.Empty;
        textRole = textRoleValue ?? string.Empty;

        if (string.Equals(annotationKind, DrawingAnnotationMarkupService.ComponentParameterTagAnnotationKind, StringComparison.Ordinal))
            return false;

        if (string.Equals(annotationKind, DrawingAnnotationMarkupService.ScheduleTableAnnotationKind, StringComparison.Ordinal) &&
            markup.Metadata.CustomFields.ContainsKey(DrawingAnnotationMarkupService.LiveScheduleInstanceIdField))
        {
            return false;
        }

        if (string.Equals(annotationKind, DrawingAnnotationMarkupService.TitleBlockAnnotationKind, StringComparison.Ordinal) &&
            markup.Metadata.CustomFields.ContainsKey(DrawingAnnotationMarkupService.LiveTitleBlockInstanceIdField) &&
            string.Equals(textRole, DrawingAnnotationMarkupService.TextRoleFieldValue, StringComparison.Ordinal) &&
            TitleBlockService.IsLiveBoundFieldLabel(markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationTextKeyField, out var textKey)
                ? textKey
                : markup.Metadata.Label))
        {
            return false;
        }

        return textRole is DrawingAnnotationMarkupService.TextRoleTitle or
            DrawingAnnotationMarkupService.TextRoleCell or
            DrawingAnnotationMarkupService.TextRoleFieldValue;
    }

    private static string GetStructuredMarkupEditTitle(string annotationKind, string textRole)
    {
        return (annotationKind, textRole) switch
        {
            (DrawingAnnotationMarkupService.TitleBlockAnnotationKind, DrawingAnnotationMarkupService.TextRoleFieldValue) => "Edit Title Block Field",
            (DrawingAnnotationMarkupService.SymbolLegendAnnotationKind, DrawingAnnotationMarkupService.TextRoleTitle) => "Edit Symbol Legend Title",
            (DrawingAnnotationMarkupService.SymbolLegendAnnotationKind, DrawingAnnotationMarkupService.TextRoleCell) => "Edit Symbol Legend Cell",
            (_, DrawingAnnotationMarkupService.TextRoleTitle) => "Edit Table Title",
            (_, DrawingAnnotationMarkupService.TextRoleCell) => "Edit Table Cell",
            _ => "Edit Annotation Text"
        };
    }

    private static string GetStructuredMarkupEditPrompt(MarkupRecord markup, string annotationKind, string textRole)
    {
        var textKey = markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationTextKeyField, out var key)
            ? key
            : markup.Metadata.Label;

        return (annotationKind, textRole) switch
        {
            (DrawingAnnotationMarkupService.TitleBlockAnnotationKind, DrawingAnnotationMarkupService.TextRoleFieldValue) => $"Update '{textKey}' value:",
            (_, DrawingAnnotationMarkupService.TextRoleTitle) => "Update title text:",
            (_, DrawingAnnotationMarkupService.TextRoleCell) => $"Update '{textKey}' text:",
            _ => "Update text:"
        };
    }

    private static void ApplyMarkupTextState(MarkupRecord markup, MarkupTextState state)
    {
        markup.TextContent = state.Text;
        markup.BoundingRect = state.Bounds;
        markup.Metadata.ModifiedUtc = state.ModifiedUtc;
    }

    private static MarkupTextState CaptureMarkupTextState(MarkupRecord markup)
    {
        return new MarkupTextState(markup.TextContent, markup.BoundingRect, markup.Metadata.ModifiedUtc);
    }

    private static MarkupTextState BuildUpdatedMarkupTextState(MarkupRecord markup, string text)
    {
        var anchor = markup.Vertices.Count > 0 ? markup.Vertices[0] : markup.BoundingRect.Location;
        var align = GetMarkupTextAlign(markup);
        var bounds = EstimateMarkupTextBounds(anchor, text, markup.Appearance.FontSize, align);
        return new MarkupTextState(text, bounds, DateTime.UtcNow);
    }

    private static TextAlign GetMarkupTextAlign(MarkupRecord markup)
    {
        if (!markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.TextAlignField, out var alignValue))
            return TextAlign.Left;

        return Enum.TryParse<TextAlign>(alignValue, ignoreCase: true, out var align)
            ? align
            : TextAlign.Left;
    }

    private static Rect EstimateMarkupTextBounds(Point anchor, string text, double fontSize, TextAlign align)
    {
        var width = Math.Max(fontSize, text.Length * fontSize * 0.55);
        var height = fontSize * 1.35;
        var x = align switch
        {
            TextAlign.Center => anchor.X - width / 2.0,
            TextAlign.Right => anchor.X - width,
            _ => anchor.X
        };

        return new Rect(x, anchor.Y - height, width, height);
    }

    private static bool MarkupSnapshotsEqual(MarkupGeometrySnapshot left, MarkupGeometrySnapshot right)
    {
        if (left.BoundingRect != right.BoundingRect ||
            left.Vertices.Count != right.Vertices.Count ||
            left.Radius != right.Radius ||
            left.ArcStartDeg != right.ArcStartDeg ||
            left.ArcSweepDeg != right.ArcSweepDeg ||
            left.FontSize != right.FontSize ||
            left.StrokeWidth != right.StrokeWidth ||
            !string.Equals(left.StrokeColor, right.StrokeColor, StringComparison.Ordinal) ||
            !string.Equals(left.FillColor, right.FillColor, StringComparison.Ordinal) ||
            left.Opacity != right.Opacity ||
            !string.Equals(left.FontFamily, right.FontFamily, StringComparison.Ordinal) ||
            !string.Equals(left.DashArray, right.DashArray, StringComparison.Ordinal))
            return false;

        for (int i = 0; i < left.Vertices.Count; i++)
        {
            if (left.Vertices[i] != right.Vertices[i])
                return false;
        }

        return true;
    }
}

internal sealed class MarkupGeometryChangeAction : IUndoableAction
{
    private readonly string _verb;
    private readonly MarkupInteractionService _markupInteractionService;
    private readonly MarkupRecord _markup;
    private readonly MarkupGeometrySnapshot _oldSnapshot;
    private readonly MarkupGeometrySnapshot _newSnapshot;

    public MarkupGeometryChangeAction(
        string verb,
        MarkupInteractionService markupInteractionService,
        MarkupRecord markup,
        MarkupGeometrySnapshot oldSnapshot,
        MarkupGeometrySnapshot newSnapshot)
    {
        _verb = verb;
        _markupInteractionService = markupInteractionService;
        _markup = markup;
        _oldSnapshot = oldSnapshot;
        _newSnapshot = newSnapshot;
    }

    public string Description => $"{_verb} {_markup.TypeDisplayText}";

    public void Execute() => _markupInteractionService.Apply(_markup, _newSnapshot);

    public void Undo() => _markupInteractionService.Apply(_markup, _oldSnapshot);
}

internal readonly record struct MarkupTextState(string Text, Rect Bounds, DateTime ModifiedUtc);

internal sealed class MarkupTextChangeAction : IUndoableAction
{
    private readonly MarkupRecord _markup;
    private readonly MarkupTextState _oldState;
    private readonly MarkupTextState _newState;

    public string Description => $"Edit text { _markup.TypeDisplayText}";

    public MarkupTextChangeAction(MarkupRecord markup, string newText)
    {
        _markup = markup;
        _oldState = CaptureState(markup);
        _newState = BuildUpdatedState(markup, newText);
    }

    public void Execute() => ApplyState(_markup, _newState);

    public void Undo() => ApplyState(_markup, _oldState);

    private static void ApplyState(MarkupRecord markup, MarkupTextState state)
    {
        markup.TextContent = state.Text;
        markup.BoundingRect = state.Bounds;
        markup.Metadata.ModifiedUtc = state.ModifiedUtc;
    }

    private static MarkupTextState CaptureState(MarkupRecord markup)
    {
        return new MarkupTextState(markup.TextContent, markup.BoundingRect, markup.Metadata.ModifiedUtc);
    }

    private static MarkupTextState BuildUpdatedState(MarkupRecord markup, string text)
    {
        var anchor = markup.Vertices.Count > 0 ? markup.Vertices[0] : markup.BoundingRect.Location;
        var align = GetTextAlign(markup);
        var bounds = EstimateBounds(anchor, text, markup.Appearance.FontSize, align);
        return new MarkupTextState(text, bounds, DateTime.UtcNow);
    }

    private static TextAlign GetTextAlign(MarkupRecord markup)
    {
        if (!markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.TextAlignField, out var alignValue))
            return TextAlign.Left;

        return Enum.TryParse<TextAlign>(alignValue, ignoreCase: true, out var align)
            ? align
            : TextAlign.Left;
    }

    private static Rect EstimateBounds(Point anchor, string text, double fontSize, TextAlign align)
    {
        var width = Math.Max(fontSize, text.Length * fontSize * 0.55);
        var height = fontSize * 1.35;
        var x = align switch
        {
            TextAlign.Center => anchor.X - width / 2.0,
            TextAlign.Right => anchor.X - width,
            _ => anchor.X
        };

        return new Rect(x, anchor.Y - height, width, height);
    }
}

internal sealed class LiveTitleBlockFieldEditAction : IUndoableAction
{
    private readonly MainViewModel _viewModel;
    private readonly string _instanceId;
    private readonly string _fieldLabel;
    private readonly string _oldValue;
    private readonly string _newValue;

    public LiveTitleBlockFieldEditAction(MainViewModel viewModel, string instanceId, string fieldLabel, string oldValue, string newValue)
    {
        _viewModel = viewModel;
        _instanceId = instanceId;
        _fieldLabel = fieldLabel;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public string Description => $"Edit live title block field {_fieldLabel}";

    public void Execute() => _viewModel.UpdateLiveTitleBlockFieldValue(_instanceId, _fieldLabel, _newValue);

    public void Undo() => _viewModel.UpdateLiveTitleBlockFieldValue(_instanceId, _fieldLabel, _oldValue);
}
