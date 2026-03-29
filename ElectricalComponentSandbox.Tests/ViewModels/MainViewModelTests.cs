using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Constructor_InitializesLibraryComponents()
    {
        var vm = new MainViewModel();
        var expectedTemplates = ElectricalComponentCatalog.CreateLibraryTemplates();

        Assert.Equal(expectedTemplates.Count, vm.LibraryComponents.Count);
        foreach (var template in expectedTemplates)
        {
            Assert.Contains(vm.LibraryComponents, c => c.Type == template.Type && c.Name == template.Name);
        }
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Conduit);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Box);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Panel);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Support);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.CableTray);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Hanger);
    }

    [Fact]
    public void Constructor_ComponentsIsEmpty()
    {
        var vm = new MainViewModel();

        Assert.Empty(vm.Components);
    }

    [Fact]
    public void Constructor_DefaultSettings()
    {
        var vm = new MainViewModel();

        Assert.True(vm.ShowGrid);
        Assert.True(vm.SnapToGrid);
        Assert.Equal(1.0, vm.GridSize);
        Assert.Null(vm.SelectedComponent);
    }

    [Fact]
    public void AddComponent_Conduit_AddsAndSelects()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Conduit);

        Assert.Single(vm.Components);
        Assert.IsType<ConduitComponent>(vm.Components[0]);
        Assert.Equal(vm.Components[0], vm.SelectedComponent);
    }

    [Fact]
    public void AddComponent_Box_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Box);

        Assert.Single(vm.Components);
        Assert.IsType<BoxComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_Panel_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Panel);

        Assert.Single(vm.Components);
        Assert.IsType<PanelComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_Support_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Support);

        Assert.Single(vm.Components);
        Assert.IsType<SupportComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_MultipleTimes_AddsAll()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Conduit);
        vm.AddComponent(ComponentType.Box);
        vm.AddComponent(ComponentType.Panel);

        Assert.Equal(3, vm.Components.Count);
    }

    [Fact]
    public void DeleteSelectedComponent_RemovesComponent()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Conduit);
        Assert.Single(vm.Components);

        vm.DeleteSelectedComponent();

        Assert.Empty(vm.Components);
        Assert.Null(vm.SelectedComponent);
    }

    [Fact]
    public void DeleteSelectedComponent_NothingSelected_DoesNothing()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Conduit);
        vm.SelectedComponent = null;

        vm.DeleteSelectedComponent();

        Assert.Single(vm.Components);
    }

    [Fact]
    public void MoveComponent_UpdatesPosition()
    {
        var vm = new MainViewModel();
        vm.SnapToGrid = false;
        vm.AddComponent(ComponentType.Conduit);

        vm.MoveComponent(new Vector3D(5, 10, 15));

        Assert.Equal(new Point3D(5, 10, 15), vm.SelectedComponent!.Position);
    }

    [Fact]
    public void MoveComponent_WithSnapToGrid_SnapsPosition()
    {
        var vm = new MainViewModel();
        vm.SnapToGrid = true;
        vm.GridSize = 2.0;
        vm.AddComponent(ComponentType.Conduit);

        vm.MoveComponent(new Vector3D(3.3, 4.7, 5.1));

        // Expect snapped values: round(3.3/2)*2=4, round(4.7/2)*2=4, round(5.1/2)*2=6
        Assert.Equal(4.0, vm.SelectedComponent!.Position.X);
        Assert.Equal(4.0, vm.SelectedComponent.Position.Y);
        Assert.Equal(6.0, vm.SelectedComponent.Position.Z);
    }

    [Fact]
    public void MoveComponent_NoSelection_DoesNothing()
    {
        var vm = new MainViewModel();
        vm.SelectedComponent = null;

        vm.MoveComponent(new Vector3D(5, 5, 5)); // Should not throw
    }

    [Fact]
    public void RotateComponent_UpdatesRotation()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);

        vm.RotateComponent(new Vector3D(45, 90, 180));

        Assert.Equal(new Vector3D(45, 90, 180), vm.SelectedComponent!.Rotation);
    }

    [Fact]
    public void ScaleComponent_UpdatesScale()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);

        vm.ScaleComponent(new Vector3D(2, 3, 4));

        Assert.Equal(new Vector3D(2, 3, 4), vm.SelectedComponent!.Scale);
    }

    [Fact]
    public void SelectedComponent_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.AddComponent(ComponentType.Conduit);

        Assert.Equal(nameof(vm.SelectedComponent), propertyName);
    }

    [Fact]
    public void ShowGrid_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.ShowGrid = false;

        Assert.Equal(nameof(vm.ShowGrid), propertyName);
    }

    [Fact]
    public void SnapToGrid_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.SnapToGrid = false;

        Assert.Equal(nameof(vm.SnapToGrid), propertyName);
    }

    [Fact]
    public void GridSize_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.GridSize = 5.0;

        Assert.Equal(nameof(vm.GridSize), propertyName);
    }

    [Fact]
    public void GridSize_SetToZero_ClampsToMinimum()
    {
        var vm = new MainViewModel();

        vm.GridSize = 0;

        Assert.Equal(0.1, vm.GridSize);
    }

    [Fact]
    public void FileService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.FileService);
    }

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

    // ===== New Feature Tests =====

    [Fact]
    public void AddComponent_CableTray_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.CableTray);

        Assert.Single(vm.Components);
        Assert.IsType<CableTrayComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_Hanger_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Hanger);

        Assert.Single(vm.Components);
        Assert.IsType<HangerComponent>(vm.Components[0]);
    }

    [Fact]
    public void Constructor_InitializesDefaultLayer()
    {
        var vm = new MainViewModel();

        Assert.Single(vm.Layers);
        Assert.Equal("default", vm.Layers[0].Id);
        Assert.NotNull(vm.ActiveLayer);
    }

    [Fact]
    public void AddLayer_CreatesNewLayer()
    {
        var vm = new MainViewModel();

        var layer = vm.AddLayer("Electrical");

        Assert.Equal(2, vm.Layers.Count);
        Assert.Equal("Electrical", layer.Name);
    }

    [Fact]
    public void RemoveLayer_MovesComponentsToDefault()
    {
        var vm = new MainViewModel();
        var layer = vm.AddLayer("Test Layer");
        vm.ActiveLayer = layer;
        vm.AddComponent(ComponentType.Box);
        var comp = vm.Components[0];
        Assert.Equal(layer.Id, comp.LayerId);

        vm.RemoveLayer(layer);

        Assert.Equal("default", comp.LayerId);
        Assert.Single(vm.Layers);
    }

    [Fact]
    public void RemoveLayer_CannotRemoveDefault()
    {
        var vm = new MainViewModel();
        var defaultLayer = vm.Layers[0];

        vm.RemoveLayer(defaultLayer);

        Assert.Single(vm.Layers); // Still there
    }

    [Fact]
    public void UndoRedo_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.UndoRedo);
        Assert.False(vm.UndoRedo.CanUndo);
    }

    [Fact]
    public void AddComponent_IsUndoable()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Box);
        Assert.Single(vm.Components);

        vm.Undo();
        Assert.Empty(vm.Components);

        vm.Redo();
        Assert.Single(vm.Components);
    }

    [Fact]
    public void DeleteSelectedComponent_IsUndoable()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);

        vm.DeleteSelectedComponent();
        Assert.Empty(vm.Components);

        vm.Undo();
        Assert.Single(vm.Components);
    }

    [Fact]
    public void UnitSystemName_Default_IsImperial()
    {
        var vm = new MainViewModel();

        Assert.Equal("Imperial", vm.UnitSystemName);
    }

    [Fact]
    public void UnitSystemName_CanBeChanged()
    {
        var vm = new MainViewModel();

        vm.UnitSystemName = "Metric";

        Assert.Equal("Metric", vm.UnitSystemName);
        Assert.Equal(UnitSystem.Metric, vm.UnitConverter.CurrentSystem);
    }

    [Fact]
    public void ToProjectModel_CreatesCorrectModel()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);
        vm.GridSize = 2.0;

        var project = vm.ToProjectModel();

        Assert.Single(project.Components);
        Assert.Equal(2.0, project.GridSize);
        Assert.Single(project.Layers);
    }

    [Fact]
    public void LoadFromProject_RestoresState()
    {
        var vm = new MainViewModel();
        var project = new ProjectModel
        {
            GridSize = 5.0,
            ShowGrid = false,
            UnitSystem = "Metric"
        };
        project.Components.Add(new BoxComponent());
        project.Components.Add(new PanelComponent());
        project.Layers.Add(Layer.CreateDefault());

        vm.LoadFromProject(project);

        Assert.Equal(2, vm.Components.Count);
        Assert.Equal(5.0, vm.GridSize);
        Assert.False(vm.ShowGrid);
        Assert.Equal("Metric", vm.UnitSystemName);
        Assert.Null(vm.SelectedComponent);
    }

    [Fact]
    public void ProjectFileService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.ProjectFileService);
    }

    [Fact]
    public void BomExport_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.BomExport);
    }

    [Fact]
    public void SnapService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.SnapService);
    }

    [Fact]
    public void CalibrationService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.CalibrationService);
    }

    [Fact]
    public void PdfUnderlay_Default_IsNull()
    {
        var vm = new MainViewModel();

        Assert.Null(vm.PdfUnderlay);
    }

    [Fact]
    public void PdfUnderlay_CanBeSet()
    {
        var vm = new MainViewModel();
        var underlay = new PdfUnderlay { FilePath = "test.pdf" };

        vm.PdfUnderlay = underlay;

        Assert.NotNull(vm.PdfUnderlay);
        Assert.Equal("test.pdf", vm.PdfUnderlay.FilePath);
    }

    [Fact]
    public void AddComponent_AssignsActiveLayerId()
    {
        var vm = new MainViewModel();
        var newLayer = vm.AddLayer("Test");
        vm.ActiveLayer = newLayer;

        vm.AddComponent(ComponentType.Box);

        Assert.Equal(newLayer.Id, vm.Components[0].LayerId);
    }
}
