using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests.ViewModels;

public partial class MainViewModelTests
{
    [Fact]
    public void MarkupTool_SelectedStructuredMarkup_ExposesAnnotationMetadata()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "PANEL-A"
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField] = DrawingAnnotationMarkupService.ScheduleTableAnnotationKind;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField] = DrawingAnnotationMarkupService.TextRoleCell;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField] = "NAME";

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasStructuredSelection);
        Assert.True(vm.MarkupTool.HasTextEditableSelection);
        Assert.True(vm.MarkupTool.HasTextShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupTextDetails);
        Assert.Equal(DrawingAnnotationMarkupService.ScheduleTableAnnotationKind, vm.MarkupTool.SelectedMarkupAnnotationKind);
        Assert.Equal(DrawingAnnotationMarkupService.TextRoleCell, vm.MarkupTool.SelectedMarkupAnnotationRole);
        Assert.Equal("NAME", vm.MarkupTool.SelectedMarkupAnnotationKey);
        Assert.Equal("Direct text edit available for structured schedule, legend, and title-block text", vm.MarkupTool.SelectedMarkupTextEditSummary);
        Assert.Equal("Shortcut: F2", vm.MarkupTool.SelectedMarkupTextShortcutHint);
        Assert.Equal("Current Value: PANEL-A", vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_SelectedPlainMarkup_HidesStructuredAnnotationMetadata()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle
        };

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.False(vm.MarkupTool.HasStructuredSelection);
        Assert.False(vm.MarkupTool.HasTextEditableSelection);
        Assert.False(vm.MarkupTool.HasTextShortcutHint);
        Assert.False(vm.MarkupTool.HasSelectedMarkupTextDetails);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAnnotationKind);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAnnotationRole);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAnnotationKey);
        Assert.Equal("Direct text editing is currently available for structured schedule, legend, and title-block text only", vm.MarkupTool.SelectedMarkupTextEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupTextShortcutHint);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_RefreshSelectedMarkupPresentation_UpdatesTextDetailsAfterEdit()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "PANEL-A"
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationKindField] = DrawingAnnotationMarkupService.ScheduleTableAnnotationKind;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextRoleField] = DrawingAnnotationMarkupService.TextRoleCell;
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationTextKeyField] = "NAME";

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        markup.TextContent = "PANEL-B";
        vm.MarkupTool.RefreshSelectedMarkupPresentation();

        Assert.Equal("Current Value: PANEL-B", vm.MarkupTool.SelectedMarkupTextDetails);
    }

    [Fact]
    public void MarkupTool_SelectedCircleMarkup_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 12
        };
        markup.Vertices.Add(new Point(0, 0));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.True(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric edit available: radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+G", vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal("Radius: 12", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedRectangleMarkup_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            BoundingRect = new Rect(5, 10, 24, 12)
        };
        markup.Vertices.Add(new Point(5, 10));
        markup.Vertices.Add(new Point(29, 22));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.True(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric edit available: width and height", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+G", vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal($"Width: 24{Environment.NewLine}Height: 12", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedStampMarkup_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Stamp,
            BoundingRect = new Rect(100, 200, 120, 30),
            TextContent = "APPROVED"
        };
        markup.Vertices.Add(new Point(160, 215));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.True(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric edit available: width and height", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+G", vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal($"Width: 120{Environment.NewLine}Height: 30", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_GroupedArcSelection_DisablesGeometryEditability()
    {
        var vm = new MainViewModel();
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 10
        };
        selectedMarkup.Vertices.Add(new Point(0, 0));
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var groupedPeer = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "peer"
        };
        groupedPeer.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        vm.Markups.Add(selectedMarkup);
        vm.Markups.Add(groupedPeer);
        vm.MarkupTool.SelectedMarkup = selectedMarkup;

        Assert.False(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.False(vm.MarkupTool.HasGeometryShortcutHint);
        Assert.False(vm.MarkupTool.HasSelectedMarkupGeometryDetails);
        Assert.Equal("Numeric geometry editing is disabled for grouped selections", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupGeometryShortcutHint);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedArcMarkup_ExposesGeometryDetails()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Radius = 18.5,
            ArcStartDeg = 30,
            ArcSweepDeg = 120
        };
        markup.Vertices.Add(new Point(0, 0));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal(
            $"Radius: 18.5{Environment.NewLine}Start: 30 deg{Environment.NewLine}End: 150 deg{Environment.NewLine}Sweep: 120 deg",
            vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedTwoPointDimension_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0) },
            TextContent = "12"
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: length and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Length: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedThreePointDimension_ReportsGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(6, 3) },
            TextContent = "12"
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: length and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Length: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedRadialDimension_ReportsRadialGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: radius and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Radius: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedDiameterDimension_ReportsDiameterGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(-6, 0), new Point(6, 0), new Point(9, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: diameter and angle", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Diameter: 12{Environment.NewLine}Angle: 0 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedAngularDimension_ReportsAngularGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(0, 12), new Point(10, 10) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            Metadata = new MarkupMetadata { Subject = "Angular" }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: angle and radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Angle: 90 deg{Environment.NewLine}Radius: 8", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedArcLengthDimension_ReportsArcLengthGeometryEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90,
            TextContent = "12"
        };
        markup.Metadata.Subject = "ArcLength";
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Equal("Numeric edit available: arc length and radius", vm.MarkupTool.SelectedMarkupGeometryEditSummary);
        Assert.Equal($"Arc Length: 15.71{Environment.NewLine}Radius: 10{Environment.NewLine}Start: 0 deg{Environment.NewLine}Sweep: 90 deg", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_UnsupportedSelection_IncludesArcLengthInAvailabilitySummary()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "NOTE",
            BoundingRect = new Rect(0, 0, 20, 10)
        };
        markup.Vertices.Add(new Point(0, 10));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.False(vm.MarkupTool.HasGeometryEditableSelection);
        Assert.Contains("arc-length dimension", vm.MarkupTool.SelectedMarkupGeometryEditSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkupTool_RefreshSelectedMarkupPresentation_UpdatesGeometryDetailsAfterEdit()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 12
        };
        markup.Vertices.Add(new Point(0, 0));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        markup.Radius = 24.25;
        vm.MarkupTool.RefreshSelectedMarkupPresentation();

        Assert.Equal("Radius: 24.25", vm.MarkupTool.SelectedMarkupGeometryDetails);
    }

    [Fact]
    public void MarkupTool_SelectedTextMarkup_ReportsAppearanceEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "PANEL-A",
            BoundingRect = new Rect(10, 10, 40, 12),
            Appearance = new MarkupAppearance
            {
                StrokeColor = "#FF112233",
                StrokeWidth = 1.5,
                FillColor = "#40112233",
                Opacity = 0.75,
                FontFamily = "Consolas",
                FontSize = 14,
                DashArray = string.Empty
            }
        };
        markup.Vertices.Add(new Point(10, 22));

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasAppearanceEditableSelection);
        Assert.True(vm.MarkupTool.HasAppearanceShortcutHint);
        Assert.True(vm.MarkupTool.HasSelectedMarkupAppearanceDetails);
        Assert.Equal("Appearance edit available: stroke color, width, opacity, fill, font family, font size", vm.MarkupTool.SelectedMarkupAppearanceEditSummary);
        Assert.Equal("Shortcut: Ctrl+Shift+A", vm.MarkupTool.SelectedMarkupAppearanceShortcutHint);
        Assert.Equal(
            $"Stroke: #FF112233{Environment.NewLine}Width: 1.5{Environment.NewLine}Opacity: 0.75{Environment.NewLine}Fill: #40112233{Environment.NewLine}Font: Consolas{Environment.NewLine}Font Size: 14",
            vm.MarkupTool.SelectedMarkupAppearanceDetails);
    }

    [Fact]
    public void MarkupTool_GroupedAppearanceSelection_DisablesAppearanceEditability()
    {
        var vm = new MainViewModel();
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Vertices = { new Point(0, 0), new Point(10, 10) }
        };
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";
        selectedMarkup.UpdateBoundingRect();

        var groupedPeer = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "peer"
        };
        groupedPeer.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        vm.Markups.Add(selectedMarkup);
        vm.Markups.Add(groupedPeer);
        vm.MarkupTool.SelectedMarkup = selectedMarkup;

        Assert.False(vm.MarkupTool.HasAppearanceEditableSelection);
        Assert.False(vm.MarkupTool.HasAppearanceShortcutHint);
        Assert.False(vm.MarkupTool.HasSelectedMarkupAppearanceDetails);
        Assert.Equal("Appearance editing is disabled for grouped selections", vm.MarkupTool.SelectedMarkupAppearanceEditSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAppearanceShortcutHint);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupAppearanceDetails);
    }

    [Fact]
    public void MarkupTool_RefreshSelectedMarkupPresentation_UpdatesAppearanceDetailsAfterEdit()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 5) },
            Appearance = new MarkupAppearance
            {
                StrokeColor = "#FF0000",
                StrokeWidth = 2,
                FillColor = "#00000000",
                Opacity = 1,
                FontFamily = "Arial",
                FontSize = 10,
                DashArray = string.Empty
            }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        markup.Appearance.StrokeColor = "#FF00AA00";
        markup.Appearance.StrokeWidth = 3.5;
        markup.Appearance.Opacity = 0.6;
        markup.Appearance.DashArray = "6,3";
        vm.MarkupTool.RefreshSelectedMarkupPresentation();

        Assert.Equal(
            $"Stroke: #FF00AA00{Environment.NewLine}Width: 3.5{Environment.NewLine}Opacity: 0.6{Environment.NewLine}Dash: 6,3",
            vm.MarkupTool.SelectedMarkupAppearanceDetails);
    }

    [Fact]
    public void MarkupTool_SelectedPolylineMarkup_ReportsPathEditability()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 5) }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasPathEditableSelection);
        Assert.True(vm.MarkupTool.HasPathShortcutHint);
        Assert.True(vm.MarkupTool.HasPathVertexInsertCandidate);
        Assert.True(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.True(vm.MarkupTool.HasSelectedMarkupPathDetails);
        Assert.Equal(
            "Direct path edit available: drag grips to reposition points, or double-click a segment to insert a vertex",
            vm.MarkupTool.SelectedMarkupPathEditSummary);
        Assert.Equal("Click Insert Vertex, then click a segment on the canvas", vm.MarkupTool.SelectedMarkupPathInsertSummary);
        Assert.Equal("Shortcut: Delete or Backspace removes the active vertex after selecting a grip", vm.MarkupTool.SelectedMarkupPathShortcutHint);
        Assert.Equal("Select a vertex grip, then delete it from the keyboard or command surface", vm.MarkupTool.SelectedMarkupPathDeleteSummary);
        Assert.Equal(
            $"Vertices: 3{Environment.NewLine}Minimum: 2{Environment.NewLine}Insert: Double-click a segment or use Insert Vertex{Environment.NewLine}Delete: Active vertex can be removed",
            vm.MarkupTool.SelectedMarkupPathDetails);
    }

    [Fact]
    public void MarkupTool_SelectedDimensionMarkup_ReportsPathEditabilityWithoutInsertHint()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(6, 3) },
            TextContent = "12'-0\""
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasPathEditableSelection);
        Assert.False(vm.MarkupTool.HasPathVertexInsertCandidate);
        Assert.True(vm.MarkupTool.HasPathShortcutHint);
        Assert.True(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.Equal("Direct path edit available: drag grips to reposition points", vm.MarkupTool.SelectedMarkupPathEditSummary);
        Assert.Equal("Vertex insertion is currently available for polyline, polygon, callout, leader note, and revision cloud markups only", vm.MarkupTool.SelectedMarkupPathInsertSummary);
        Assert.Equal(
            $"Vertices: 3{Environment.NewLine}Minimum: 2{Environment.NewLine}Delete: Active vertex can be removed",
            vm.MarkupTool.SelectedMarkupPathDetails);
    }

    [Fact]
    public void MarkupTool_GroupedPathSelection_DisablesPathEditability()
    {
        var vm = new MainViewModel();
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 5) }
        };
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var groupedPeer = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "peer"
        };
        groupedPeer.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        vm.Markups.Add(selectedMarkup);
        vm.Markups.Add(groupedPeer);
        vm.MarkupTool.SelectedMarkup = selectedMarkup;

        Assert.False(vm.MarkupTool.HasPathEditableSelection);
        Assert.False(vm.MarkupTool.HasPathShortcutHint);
        Assert.False(vm.MarkupTool.HasPathVertexInsertCandidate);
        Assert.False(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.False(vm.MarkupTool.HasSelectedMarkupPathDetails);
        Assert.Equal("Path editing is disabled for grouped selections", vm.MarkupTool.SelectedMarkupPathEditSummary);
        Assert.Equal("Vertex insertion is disabled for grouped selections", vm.MarkupTool.SelectedMarkupPathInsertSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupPathShortcutHint);
        Assert.Equal("Vertex deletion is disabled for grouped selections", vm.MarkupTool.SelectedMarkupPathDeleteSummary);
        Assert.Equal(string.Empty, vm.MarkupTool.SelectedMarkupPathDetails);
    }

    [Fact]
    public void MarkupTool_SelectedMinimumVertexPath_DisablesVertexDeletionCandidate()
    {
        var vm = new MainViewModel();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Polyline,
            Vertices = { new Point(0, 0), new Point(10, 0) }
        };
        markup.UpdateBoundingRect();

        vm.Markups.Add(markup);
        vm.MarkupTool.SelectedMarkup = markup;

        Assert.True(vm.MarkupTool.HasPathEditableSelection);
        Assert.False(vm.MarkupTool.HasPathShortcutHint);
        Assert.False(vm.MarkupTool.HasPathVertexDeleteCandidate);
        Assert.Equal("Vertex deletion is unavailable when the selected path is already at its minimum vertex count", vm.MarkupTool.SelectedMarkupPathDeleteSummary);
    }

    [Fact]
    public void MarkupTool_AllSheetsScope_AggregatesMarkupsAcrossSheets()
    {
        var vm = new MainViewModel();
        var firstSheet = vm.SelectedSheet;
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Status = MarkupStatus.Open,
            Vertices = { new Point(0, 0), new Point(5, 5) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);

        var secondSheet = vm.AddSheet("Review");
        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Text,
            Status = MarkupStatus.Resolved,
            TextContent = "Reviewed",
            Vertices = { new Point(10, 10) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;

        Assert.NotNull(firstSheet);
        Assert.NotNull(secondSheet);
        Assert.Equal(2, vm.MarkupTool.TotalCount);
        Assert.Equal(1, vm.MarkupTool.OpenCount);
        Assert.Equal(1, vm.MarkupTool.ResolvedCount);
        Assert.Equal(2, vm.MarkupTool.FilteredMarkups.Count);
        Assert.Contains(vm.MarkupTool.FilteredMarkups, markup => markup.ReviewSheetDisplayText == firstSheet.DisplayName);
        Assert.Contains(vm.MarkupTool.FilteredMarkups, markup => markup.ReviewSheetDisplayText == secondSheet.DisplayName);
    }

    [Fact]
    public void RevealMarkup_SelectsOwningSheetAndMarkup()
    {
        var vm = new MainViewModel();
        var firstMarkup = new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Vertices = { new Point(0, 0), new Point(4, 4) }
        };
        firstMarkup.UpdateBoundingRect();
        vm.AddMarkup(firstMarkup);
        var firstSheet = vm.SelectedSheet;

        vm.AddSheet("Review");
        var secondMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Radius = 4,
            Vertices = { new Point(20, 20) }
        };
        secondMarkup.UpdateBoundingRect();
        vm.AddMarkup(secondMarkup);

        vm.MarkupTool.ReviewScope = MarkupReviewScope.AllSheets;

        var revealed = vm.RevealMarkup(firstMarkup);

        Assert.True(revealed);
        Assert.Equal(firstSheet?.Id, vm.SelectedSheet?.Id);
        Assert.Same(firstMarkup, vm.MarkupTool.SelectedMarkup);
    }
}
