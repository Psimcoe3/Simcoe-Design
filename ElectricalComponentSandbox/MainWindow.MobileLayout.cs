using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private bool _isMobileView = false;
    private WindowState _desktopWindowState = WindowState.Maximized;
    private double _desktopWidth = 1400;
    private double _desktopHeight = 800;
    private MobilePane _activeMobilePane = MobilePane.Canvas;
    private MobileTheme _mobileTheme = MobileTheme.IOS;
    private const double MobileWindowWidth = 430;
    private const double MobileWindowHeight = 932;

    private void MobileViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isMobileView)
        {
            _desktopWindowState = WindowState;
            _desktopWidth = Width;
            _desktopHeight = Height;
            if (_showDesktopLibraryPanel && LibraryColumn.Width.Value > 0)
                _desktopLibraryColumnWidth = LibraryColumn.Width;
            if (ViewportColumn.Width.Value > 0)
                _desktopViewportColumnWidth = ViewportColumn.Width;
            if (_showDesktopPropertiesPanel && PropertiesColumn.Width.Value > 0)
                _desktopPropertiesColumnWidth = PropertiesColumn.Width;

            WindowState = WindowState.Normal;
            Width = MobileWindowWidth;
            Height = MobileWindowHeight;

            TopMenu.Visibility = Visibility.Collapsed;
            DesktopToolBar.Visibility = Visibility.Collapsed;
            DesktopWorkspaceOverview.Visibility = Visibility.Collapsed;
            MobileTopBar.Visibility = Visibility.Visible;
            MobileBottomNav.Visibility = Visibility.Visible;
            PdfControlsPanel.Visibility = Visibility.Collapsed;

            _isMobileView = true;
            ApplyMobileTheme();
            ViewTabs.SelectedIndex = 1;
            Update2DCanvas();
            SetMobilePane(MobilePane.Canvas);

            MobileViewButton.Content = "Desktop View";
            ActionLogService.Instance.Log(LogCategory.View, "Mobile view enabled", $"Window size set to {MobileWindowWidth}x{MobileWindowHeight}");
        }
        else
        {
            TopMenu.Visibility = Visibility.Visible;
            DesktopToolBar.Visibility = Visibility.Visible;
            DesktopWorkspaceOverview.Visibility = Visibility.Visible;
            MobileTopBar.Visibility = Visibility.Collapsed;
            MobileBottomNav.Visibility = Visibility.Collapsed;
            PdfControlsPanel.Visibility = Visibility.Visible;
            MainContentGrid.Background = Brushes.Transparent;

            Width = _desktopWidth;
            Height = _desktopHeight;
            WindowState = _desktopWindowState;

            MobileViewButton.Content = "Mobile View";
            _isMobileView = false;
            ApplyDesktopPaneLayout();
            UpdateWorkspaceOverview();
            ActionLogService.Instance.Log(LogCategory.View, "Mobile view disabled");
        }
    }

    private void MobileThemeIosMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMobileTheme(MobileTheme.IOS);
    }

    private void MobileThemeAndroidMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMobileTheme(MobileTheme.AndroidMaterial);
    }

    private void MobileCanvasButton_Click(object sender, RoutedEventArgs e)
    {
        SetMobilePane(MobilePane.Canvas);
    }

    private void MobileLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        SetMobilePane(MobilePane.Library);
    }

    private void MobilePropertiesButton_Click(object sender, RoutedEventArgs e)
    {
        SetMobilePane(MobilePane.Properties);
    }

    private void MobileMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            RefreshMobileContextMenus();
            OpenContextMenuFromButton(button, PlacementMode.Bottom);
        }
    }

    private void MobileAddTopButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            RefreshMobileContextMenus();
            OpenContextMenuFromButton(button, PlacementMode.Bottom);
        }
    }

    internal void EnableMobileViewForTesting()
    {
        if (!_isMobileView)
            MobileViewButton_Click(MobileViewButton, new RoutedEventArgs());
    }

    internal void SetMobilePaneForTesting(string paneName)
    {
        SetMobilePane(paneName switch
        {
            "canvas" => MobilePane.Canvas,
            "library" => MobilePane.Library,
            "properties" => MobilePane.Properties,
            _ => MobilePane.Canvas
        });
    }

    internal void UpdateMobileTopBarForTesting() => UpdateMobileTopBarExperience();

    internal (string SectionTitle, string Summary, string AddLabel, string MoreLabel) GetMobileTopBarStateForTesting()
        => (
            MobileSectionTitleText.Text,
            MobileTaskSummaryText.Text,
            MobileAddTopButton.Content?.ToString() ?? string.Empty,
            MobileMoreButton.Content?.ToString() ?? string.Empty);

    internal (bool IsVisible, string Progress, string Title, string PrimaryAction, string SecondaryAction) GetMobileOnboardingInlineStateForTesting()
        => (
            MobileOnboardingActionBar?.Visibility == Visibility.Visible,
            MobileOnboardingProgressText?.Text ?? string.Empty,
            MobileOnboardingActionTitleText?.Text ?? string.Empty,
            MobileOnboardingPrimaryActionButton?.Content?.ToString() ?? string.Empty,
            MobileOnboardingSecondaryActionButton?.Content?.ToString() ?? string.Empty);

    internal (bool IsCompact, Visibility ProgressVisibility, Visibility TitleVisibility) GetMobileOnboardingPresentationStateForTesting()
        => (
            IsCompactMobileOnboarding(BuildMobileOnboardingState(GetSelectedComponents().Count, _viewModel.MarkupTool.SelectedMarkup != null)),
            MobileOnboardingProgressText?.Visibility ?? Visibility.Collapsed,
            MobileOnboardingActionTitleText?.Visibility ?? Visibility.Collapsed);

    internal bool ExecuteMobileOnboardingInlineActionForTesting(string actionName)
    {
        var button = actionName switch
        {
            "primary" => MobileOnboardingPrimaryActionButton,
            "secondary" => MobileOnboardingSecondaryActionButton,
            "dismiss" => MobileOnboardingDismissButton,
            _ => null
        };

        if (button == null || button.Visibility != Visibility.Visible || !button.IsEnabled)
            return false;

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        return true;
    }

    internal (string CanvasLabel, string LibraryLabel, string PropertiesLabel) GetMobileNavigationStateForTesting()
        => (
            MobileCanvasButton.Content?.ToString() ?? string.Empty,
            MobileLibraryButton.Content?.ToString() ?? string.Empty,
            MobilePropertiesButton.Content?.ToString() ?? string.Empty);

    internal string[] GetMobileMenuHeadersForTesting(bool primaryMenu)
    {
        RefreshMobileContextMenus();
        var menu = primaryMenu ? MobileAddMenu : MobileMoreMenu;
        return menu.Items
            .OfType<MenuItem>()
            .Select(item => item.Header?.ToString())
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Cast<string>()
            .ToArray();
    }

    internal bool ExecuteMobileMenuItemForTesting(bool primaryMenu, string header)
    {
        RefreshMobileContextMenus();
        var menu = primaryMenu ? MobileAddMenu : MobileMoreMenu;
        var item = menu.Items.OfType<MenuItem>().FirstOrDefault(candidate => string.Equals(candidate.Header?.ToString(), header, StringComparison.Ordinal));
        if (item == null || !item.IsEnabled)
            return false;

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
        return true;
    }

    private static void OpenContextMenuFromButton(Button button, PlacementMode placementMode)
    {
        if (button.ContextMenu == null)
            return;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = placementMode;
        button.ContextMenu.IsOpen = true;
    }

    private void SetMobileTheme(MobileTheme theme)
    {
        _mobileTheme = theme;
        MobileThemeIosMenuItem.IsChecked = theme == MobileTheme.IOS;
        MobileThemeAndroidMenuItem.IsChecked = theme == MobileTheme.AndroidMaterial;
        ApplyMobileTheme();
    }

    private void ApplyMobileTheme()
    {
        bool isIOS = _mobileTheme == MobileTheme.IOS;
        var primary = new SolidColorBrush(isIOS ? Color.FromRgb(0, 122, 255) : Color.FromRgb(26, 115, 232));
        var border = new SolidColorBrush(isIOS ? Color.FromRgb(198, 198, 200) : Color.FromRgb(218, 220, 224));
        var altSurface = new SolidColorBrush(isIOS ? Color.FromRgb(242, 242, 247) : Color.FromRgb(250, 250, 250));

        MobileTopBar.Background = Brushes.White;
        MobileTopBar.BorderBrush = border;
        MobileBottomNav.Background = Brushes.White;
        MobileBottomNav.BorderBrush = border;
        if (_isMobileView)
        {
            MainContentGrid.Background = altSurface;
        }

        MobileTopBarHeaderGrid.Height = isIOS ? 52 : 56;
        MobileSectionTitleText.FontSize = isIOS ? 17 : 18;
        MobileSectionTitleText.FontWeight = isIOS ? FontWeights.SemiBold : FontWeights.Medium;

        MobileUndoButton.Foreground = primary;
        MobileRedoButton.Foreground = primary;
        MobileAddTopButton.Foreground = primary;
        MobileMoreButton.Foreground = primary;
        MobileUndoButton.Content = isIOS ? "Undo" : "Undo";
        MobileRedoButton.Content = isIOS ? "Redo" : "Redo";
        MobileMoreButton.Content = isIOS ? "More" : "Menu";

        UpdateMobileNavigationVisuals();
        UpdateWorkspaceOverview();
        UpdateMobileTopBarExperience();
    }

    private void SetMobilePane(MobilePane pane)
    {
        _activeMobilePane = pane;
        if (!_isMobileView)
            return;

        LibraryGridSplitter.Visibility = Visibility.Collapsed;
        PropertiesGridSplitter.Visibility = Visibility.Collapsed;

        switch (_activeMobilePane)
        {
            case MobilePane.Canvas:
                LibraryPanelContainer.Visibility = Visibility.Collapsed;
                ViewportPanelContainer.Visibility = Visibility.Visible;
                PropertiesPanelContainer.Visibility = Visibility.Collapsed;
                LibraryColumn.Width = new GridLength(0);
                ViewportColumn.Width = new GridLength(1, GridUnitType.Star);
                PropertiesColumn.Width = new GridLength(0);
                MobileAddTopButton.Visibility = Visibility.Visible;
                ViewTabs.SelectedIndex = 1;
                Update2DCanvas();
                break;

            case MobilePane.Library:
                LibraryPanelContainer.Visibility = Visibility.Visible;
                ViewportPanelContainer.Visibility = Visibility.Collapsed;
                PropertiesPanelContainer.Visibility = Visibility.Collapsed;
                LibraryColumn.Width = new GridLength(1, GridUnitType.Star);
                ViewportColumn.Width = new GridLength(0);
                PropertiesColumn.Width = new GridLength(0);
                MobileAddTopButton.Visibility = Visibility.Visible;
                break;

            case MobilePane.Properties:
                LibraryPanelContainer.Visibility = Visibility.Collapsed;
                ViewportPanelContainer.Visibility = Visibility.Collapsed;
                PropertiesPanelContainer.Visibility = Visibility.Visible;
                LibraryColumn.Width = new GridLength(0);
                ViewportColumn.Width = new GridLength(0);
                PropertiesColumn.Width = new GridLength(1, GridUnitType.Star);
                MobileAddTopButton.Visibility = Visibility.Visible;
                break;
        }

        UpdateMobileNavigationVisuals();
    UpdateMobileTopBarExperience();
    }

    private void UpdateMobileNavigationVisuals()
    {
        bool isIOS = _mobileTheme == MobileTheme.IOS;
        var selectedBrush = isIOS ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(232, 240, 254));
        var selectedText = isIOS ? new SolidColorBrush(Color.FromRgb(0, 122, 255)) : new SolidColorBrush(Color.FromRgb(26, 115, 232));
        var defaultBrush = Brushes.Transparent;
        var defaultText = isIOS ? new SolidColorBrush(Color.FromRgb(142, 142, 147)) : new SolidColorBrush(Color.FromRgb(95, 99, 104));

        MobileCanvasButton.Background = _activeMobilePane == MobilePane.Canvas ? selectedBrush : defaultBrush;
        MobileLibraryButton.Background = _activeMobilePane == MobilePane.Library ? selectedBrush : defaultBrush;
        MobilePropertiesButton.Background = _activeMobilePane == MobilePane.Properties ? selectedBrush : defaultBrush;
        MobileCanvasButton.Foreground = _activeMobilePane == MobilePane.Canvas ? selectedText : defaultText;
        MobileLibraryButton.Foreground = _activeMobilePane == MobilePane.Library ? selectedText : defaultText;
        MobilePropertiesButton.Foreground = _activeMobilePane == MobilePane.Properties ? selectedText : defaultText;
        MobileCanvasButton.FontWeight = _activeMobilePane == MobilePane.Canvas ? FontWeights.SemiBold : (isIOS ? FontWeights.Normal : FontWeights.Medium);
        MobileLibraryButton.FontWeight = _activeMobilePane == MobilePane.Library ? FontWeights.SemiBold : (isIOS ? FontWeights.Normal : FontWeights.Medium);
        MobilePropertiesButton.FontWeight = _activeMobilePane == MobilePane.Properties ? FontWeights.SemiBold : (isIOS ? FontWeights.Normal : FontWeights.Medium);
    }

    private void UpdateMobileTopBarExperience()
    {
        if (!_isMobileView || MobileSectionTitleText == null || MobileTaskSummaryText == null || MobileAddTopButton == null || MobileMoreButton == null)
            return;

        UpdateMobileNavigationLabels();
        MobileSectionTitleText.Text = BuildMobileSectionTitle();
        MobileTaskSummaryText.Text = BuildMobileTaskSummary();
        MobileAddTopButton.Content = BuildMobilePrimaryActionLabel();
        MobileMoreButton.Content = _mobileTheme == MobileTheme.IOS ? "More" : "Menu";
        UpdateMobileOnboardingActionBar();
        RefreshMobileContextMenus();
    }

    private void UpdateMobileOnboardingActionBar()
    {
        if (MobileOnboardingActionBar == null || MobileOnboardingProgressText == null ||
            MobileOnboardingActionTitleText == null || MobileOnboardingPrimaryActionButton == null ||
            MobileOnboardingSecondaryActionButton == null || MobileOnboardingDismissButton == null ||
            MobileOnboardingActionButtonsPanel == null)
        {
            return;
        }

        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var selectedComponentCount = GetSelectedComponents().Count;
        var onboardingState = BuildMobileOnboardingState(selectedComponentCount, selectedMarkup != null);
        if (!onboardingState.IsVisible)
        {
            MobileOnboardingActionBar.Visibility = Visibility.Collapsed;
            return;
        }

        var isCompact = IsCompactMobileOnboarding(onboardingState);
        MobileOnboardingActionBar.Visibility = Visibility.Visible;
        MobileOnboardingActionBar.Padding = isCompact ? new Thickness(10, 6, 10, 6) : new Thickness(10, 8, 10, 8);
        MobileOnboardingProgressText.Text = onboardingState.ProgressText;
        MobileOnboardingProgressText.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        MobileOnboardingActionTitleText.Text = onboardingState.TitleText;
        MobileOnboardingActionTitleText.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        MobileOnboardingActionButtonsPanel.Margin = isCompact ? new Thickness(0) : new Thickness(0, 8, 0, 0);
        SetWorkspaceOnboardingButton(MobileOnboardingPrimaryActionButton, onboardingState.PrimaryActionLabel, onboardingState.PrimaryActionKey);
        SetWorkspaceOnboardingButton(MobileOnboardingSecondaryActionButton, onboardingState.SecondaryActionLabel, onboardingState.SecondaryActionKey);
        MobileOnboardingDismissButton.Content = "Later";
    }

    private void UpdateMobileNavigationLabels()
    {
        if (MobileCanvasButton == null || MobileLibraryButton == null || MobilePropertiesButton == null)
            return;

        MobileCanvasButton.Content = BuildMobileCanvasNavigationLabel();
        MobileLibraryButton.Content = BuildMobileLibraryNavigationLabel();
        MobilePropertiesButton.Content = BuildMobilePropertiesNavigationLabel();
    }

    private string BuildMobileCanvasNavigationLabel()
    {
        var isIOS = _mobileTheme == MobileTheme.IOS;

        if (_isDrawingConduit || _isEditingConduitPath || _isSketchLineMode || _isSketchRectangleMode || _isFreehandDrawing)
            return isIOS ? "Route\nTool" : "Route";

        if (_selectedSketchPrimitive != null)
            return isIOS ? "Convert\nSketch" : "Convert";

        return isIOS ? "Draw\nPlan" : "Draw";
    }

    private string BuildMobileLibraryNavigationLabel()
    {
        var isIOS = _mobileTheme == MobileTheme.IOS;

        if (_pendingPlacementComponent != null)
            return isIOS ? "Place\nPart" : "Place";

        return isIOS ? "Add\nParts" : "Parts";
    }

    private string BuildMobilePropertiesNavigationLabel()
    {
        var isIOS = _mobileTheme == MobileTheme.IOS;
        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var selectedComponentCount = GetSelectedComponents().Count;

        if (_isPendingMarkupVertexInsertion)
            return isIOS ? "Edit\nMarkup" : "Review";

        if (selectedMarkup != null)
            return isIOS ? "Review\nIssue" : "Review";

        if (selectedComponentCount > 1)
            return isIOS ? "Edit\nGroup" : "Edit";

        if (selectedComponentCount == 1)
            return isIOS ? "Edit\nPart" : "Edit";

        return isIOS ? "Inspect\nNext" : "Inspect";
    }

    private string BuildMobileSectionTitle()
    {
        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var selectedComponentCount = GetSelectedComponents().Count;
        var onboardingState = BuildMobileOnboardingState(selectedComponentCount, selectedMarkup != null);

        if (onboardingState.IsVisible)
            return "Getting Started";

        if (selectedMarkup != null)
            return "Review";

        if (selectedComponentCount > 0)
            return _activeMobilePane == MobilePane.Properties ? "Inspector" : "Selection";

        return _activeMobilePane switch
        {
            MobilePane.Canvas => "Plan",
            MobilePane.Library => "Library",
            MobilePane.Properties => "Inspector",
            _ => "Plan"
        };
    }

    private string BuildMobileTaskSummary()
    {
        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var selectedComponentCount = GetSelectedComponents().Count;

        if (_isDrawingConduit)
            return "Place bend points, then finish or cancel the active conduit route.";

        if (_isEditingConduitPath)
            return "Adjust conduit bends directly in plan, then finish the path edit.";

        if (_isSketchLineMode)
            return "Sketch a draft path in plan, then finish the line or cancel the tool.";

        if (_isSketchRectangleMode)
            return "Drag out a sketch rectangle, then leave the tool or keep drafting.";

        if (_selectedSketchPrimitive != null)
            return "The selected sketch can be converted into modeled elements from here.";

        if (_isPendingMarkupVertexInsertion)
            return "Tap a markup segment to insert a new vertex, or cancel the pending edit.";

        if (_isFreehandDrawing)
            return "Continue tracing the freehand route, then release or cancel to stop drawing.";

        var onboardingState = BuildMobileOnboardingState(selectedComponentCount, selectedMarkup != null);
        if (onboardingState.IsVisible)
        {
            if (IsCompactMobileOnboarding(onboardingState))
                return $"{onboardingState.ProgressText}: {onboardingState.TitleText}";

            return $"{onboardingState.ProgressText}: {onboardingState.SummaryText}";
        }

        if (selectedMarkup != null)
            return "Reply, resolve, or assign the selected issue without leaving mobile review.";

        if (selectedComponentCount > 1)
            return "Apply shared edits, duplicate, or zoom to the current selection.";

        if (selectedComponentCount == 1)
            return "Apply property edits, zoom to the part, or duplicate it while you refine the layout.";

        return _activeMobilePane switch
        {
            MobilePane.Library => "Choose a part type, then place it back on the plan.",
            MobilePane.Properties => "Select a component or markup to turn the inspector into the next action workspace.",
            _ => "Import a reference, place the first conduit run, or switch into review when issues arrive."
        };
    }

    private string BuildMobilePrimaryActionLabel()
    {
        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var selectedComponentCount = GetSelectedComponents().Count;
        var onboardingState = BuildMobileOnboardingState(selectedComponentCount, selectedMarkup != null);
        if (onboardingState.IsVisible)
            return "Next";

        if (_activeMobilePane == MobilePane.Library)
            return _mobileTheme == MobileTheme.IOS ? "Place" : "+ Place";

        if (selectedMarkup != null)
            return "Review";

        if (_isDrawingConduit || _isEditingConduitPath || _isSketchLineMode || _isSketchRectangleMode || _isFreehandDrawing || _isPendingMarkupVertexInsertion)
            return "Tool";

        if (_selectedSketchPrimitive != null)
            return "Convert";

        if (selectedComponentCount > 0)
            return "Act";

        return _mobileTheme == MobileTheme.IOS ? "Start" : "+ Start";
    }

    private void RefreshMobileContextMenus()
    {
        if (MobileAddMenu == null || MobileMoreMenu == null)
            return;

        RebuildMobileAddMenu();
        RebuildMobileMoreMenu();
    }

    private void RebuildMobileAddMenu()
    {
        MobileAddMenu.Items.Clear();

        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var selectedComponentCount = GetSelectedComponents().Count;
        var onboardingState = BuildMobileOnboardingState(selectedComponentCount, selectedMarkup != null);
        if (onboardingState.IsVisible)
        {
            AddMobileMenuItem(MobileAddMenu, onboardingState.PrimaryActionLabel, (_, e) => ExecuteWorkspaceOnboardingAction(onboardingState.PrimaryActionKey, _, e));
            AddMobileMenuItem(MobileAddMenu, onboardingState.SecondaryActionLabel, (_, e) => ExecuteWorkspaceOnboardingAction(onboardingState.SecondaryActionKey, _, e));
            return;
        }

        if (_activeMobilePane == MobilePane.Library)
        {
            AddMobileMenuItem(MobileAddMenu, "Conduit", AddConduit_Click);
            AddMobileMenuItem(MobileAddMenu, "Box", AddBox_Click);
            AddMobileMenuItem(MobileAddMenu, "Panel", AddPanel_Click);
            AddMobileMenuItem(MobileAddMenu, "Support", AddSupport_Click);
            AddMobileMenuItem(MobileAddMenu, "Cable Tray", AddCableTray_Click);
            AddMobileMenuItem(MobileAddMenu, "Hanger", AddHanger_Click);
            return;
        }

        if (_isDrawingConduit)
        {
            AddMobileMenuItem(MobileAddMenu, "Finish Route", (_, _) => FinishDrawingConduit());
            AddMobileMenuItem(MobileAddMenu, "Cancel Tool", CancelMobileActiveTool_Click);
            return;
        }

        if (_isEditingConduitPath)
        {
            AddMobileMenuItem(MobileAddMenu, "Finish Path Edit", ToggleEditConduitPath_Click);
            AddMobileMenuItem(MobileAddMenu, "Cancel Tool", CancelMobileActiveTool_Click);
            return;
        }

        if (_isSketchLineMode)
        {
            AddMobileMenuItem(MobileAddMenu, "Finish Sketch Line", SketchLine_Click);
            AddMobileMenuItem(MobileAddMenu, "Cancel Tool", CancelMobileActiveTool_Click);
            return;
        }

        if (_isSketchRectangleMode)
        {
            AddMobileMenuItem(MobileAddMenu, "Leave Rectangle Mode", SketchRectangle_Click);
            AddMobileMenuItem(MobileAddMenu, "Cancel Tool", CancelMobileActiveTool_Click);
            return;
        }

        if (_selectedSketchPrimitive != null)
        {
            AddMobileMenuItem(MobileAddMenu, "Convert Sketch", ConvertSketch_Click);
            AddMobileMenuItem(MobileAddMenu, "Add Conduit", AddConduit_Click);
            return;
        }

        if (_isPendingMarkupVertexInsertion)
        {
            AddMobileMenuItem(MobileAddMenu, "Cancel Insert", CancelMobileActiveTool_Click);
            return;
        }

        if (_isFreehandDrawing)
        {
            AddMobileMenuItem(MobileAddMenu, "Cancel Tool", CancelMobileActiveTool_Click);
            return;
        }

        if (selectedMarkup != null)
        {
            AddMobileMenuItem(MobileAddMenu, "Add Reply", AddMarkupReply_Click);
            AddMobileMenuItem(MobileAddMenu, "Resolve", ResolveMarkup_Click);
            AddMobileMenuItem(MobileAddMenu, "Assign", AssignSelectedMarkup_Click);
            return;
        }

        if (selectedComponentCount > 0)
        {
            AddMobileMenuItem(MobileAddMenu, selectedComponentCount > 1 ? "Apply Shared Changes" : "Apply Changes", ApplyProperties_Click);
            AddMobileMenuItem(MobileAddMenu, "Zoom Selection", ZoomSelection_Click);
            AddMobileMenuItem(MobileAddMenu, "Duplicate", DuplicateComponent_Click);
            return;
        }

        AddMobileMenuItem(MobileAddMenu, "Import Reference", ImportPdf_Click);
        AddMobileMenuItem(MobileAddMenu, "Add Conduit", AddConduit_Click);
        AddMobileMenuItem(MobileAddMenu, "Draw Conduit", DrawConduit_Click);
        AddMobileMenuItem(MobileAddMenu, "Sketch Line", SketchLine_Click);
        AddMobileMenuItem(MobileAddMenu, "Review Markups", ShowMobileMarkupReview_Click);
    }

    private void RebuildMobileMoreMenu()
    {
        MobileMoreMenu.Items.Clear();

        var selectedMarkup = _viewModel.MarkupTool.SelectedMarkup;
        var selectedComponentCount = GetSelectedComponents().Count;
        var hasFocusedWorkflow = HasFocusedMobileWorkflow(selectedComponentCount, selectedMarkup != null);
        var onboardingState = hasFocusedWorkflow
            ? WorkspaceOnboardingState.Hidden
            : BuildWorkspaceOnboardingState(selectedComponentCount, selectedMarkup != null);

        if (_activeMobilePane != MobilePane.Canvas)
            AddMobileMenuItem(MobileMoreMenu, "Show Plan", (_, _) => SetMobilePane(MobilePane.Canvas));

        if (_activeMobilePane != MobilePane.Library)
            AddMobileMenuItem(MobileMoreMenu, "Show Parts", (_, _) => SetMobilePane(MobilePane.Library));

        if (_activeMobilePane != MobilePane.Properties)
            AddMobileMenuItem(MobileMoreMenu, "Show Inspector", (_, _) => SetMobilePane(MobilePane.Properties));

        if (_viewModel.MarkupTool.TotalCount > 0 && selectedMarkup == null)
            AddMobileMenuItem(MobileMoreMenu, "Review Markups", ShowMobileMarkupReview_Click);

        if (_isDrawingConduit || _isEditingConduitPath || _isSketchLineMode || _isSketchRectangleMode || _isPendingMarkupVertexInsertion || _isFreehandDrawing)
            AddMobileMenuItem(MobileMoreMenu, "Cancel Active Tool", CancelMobileActiveTool_Click);

        if (selectedMarkup != null)
        {
            if (_viewModel.MarkupTool.HasGeometryEditableSelection)
                AddMobileMenuItem(MobileMoreMenu, "Edit Geometry...", EditSelectedMarkupGeometry_Click);

            if (_viewModel.MarkupTool.HasAppearanceEditableSelection)
                AddMobileMenuItem(MobileMoreMenu, "Edit Appearance...", EditSelectedMarkupAppearance_Click);

            if (_viewModel.MarkupTool.HasPathVertexInsertCandidate)
                AddMobileMenuItem(MobileMoreMenu, "Insert Vertex", InsertSelectedMarkupVertex_Click);

            if (_viewModel.MarkupTool.HasPathVertexDeleteCandidate)
                AddMobileMenuItem(MobileMoreMenu, "Delete Active Vertex", DeleteSelectedMarkupVertex_Click);
        }

        if (selectedComponentCount > 0)
        {
            AddMobileMenuItem(MobileMoreMenu, "Delete Selected", DeleteComponent_Click);

            if (selectedComponentCount > 1)
                AddMobileMenuItem(MobileMoreMenu, "Measure Distance", MeasureDistance_Click);
        }

        if (onboardingState.IsVisible)
            AddMobileMenuItem(MobileMoreMenu, "Dismiss Walkthrough", DismissWorkspaceOnboarding_Click);

        if (!hasFocusedWorkflow)
        {
            AddMobileSeparator(MobileMoreMenu);
            AddMobileMenuItem(MobileMoreMenu, "Open Project...", OpenProject_Click);
            AddMobileMenuItem(MobileMoreMenu, "Save Project", SaveProject_Click);
            AddMobileMenuItem(MobileMoreMenu, "Import PDF Underlay...", ImportPdf_Click);
        }

        AddMobileSeparator(MobileMoreMenu);
        AddMobileMenuItem(MobileMoreMenu, "Theme: iOS", MobileThemeIosMenuItem_Click, isCheckable: true, isChecked: _mobileTheme == MobileTheme.IOS);
        AddMobileMenuItem(MobileMoreMenu, "Theme: Android", MobileThemeAndroidMenuItem_Click, isCheckable: true, isChecked: _mobileTheme == MobileTheme.AndroidMaterial);
        AddMobileMenuItem(MobileMoreMenu, "Desktop View", MobileViewButton_Click);
    }

    private bool HasFocusedMobileWorkflow(int selectedComponentCount, bool hasSelectedMarkup)
    {
        return hasSelectedMarkup ||
               selectedComponentCount > 0 ||
               _isDrawingConduit ||
               _isEditingConduitPath ||
               _isSketchLineMode ||
               _isSketchRectangleMode ||
               _selectedSketchPrimitive != null ||
               _isPendingMarkupVertexInsertion ||
               _isFreehandDrawing;
    }

    private WorkspaceOnboardingState BuildMobileOnboardingState(int selectedComponentCount, bool hasSelectedMarkup)
    {
        if (HasFocusedMobileWorkflow(selectedComponentCount, hasSelectedMarkup))
            return WorkspaceOnboardingState.Hidden;

        var onboardingState = BuildWorkspaceOnboardingState(selectedComponentCount, hasSelectedMarkup);
        if (!onboardingState.IsVisible)
            return onboardingState;

        if (string.Equals(onboardingState.SecondaryActionKey, WorkspaceOnboardingActionShowModel, StringComparison.Ordinal))
        {
            return new WorkspaceOnboardingState(
                true,
                onboardingState.ProgressText,
                onboardingState.TitleText,
                onboardingState.SummaryText,
                onboardingState.ChecklistText,
                onboardingState.PrimaryActionLabel,
                onboardingState.PrimaryActionKey,
                "Show Inspector",
                WorkspaceOnboardingActionShowInspector);
        }

        return onboardingState;
    }

    private static bool IsCompactMobileOnboarding(WorkspaceOnboardingState onboardingState)
    {
        return onboardingState.IsVisible &&
               !string.Equals(onboardingState.ProgressText, "Step 1 of 3", StringComparison.Ordinal);
    }

    private void ShowMobileMarkupReview_Click(object sender, RoutedEventArgs e)
    {
        SetMobilePane(MobilePane.Properties);
        SelectInspectorTab(MarkupsInspectorTab);
    }

    private void CancelMobileActiveTool_Click(object sender, RoutedEventArgs e)
    {
        TryCancelActiveInteraction(sender, e);
    }

    private static void AddMobileMenuItem(ContextMenu menu, string header, RoutedEventHandler clickHandler, bool isCheckable = false, bool isChecked = false)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = isCheckable,
            IsChecked = isChecked
        };
        item.Click += clickHandler;
        menu.Items.Add(item);
    }

    private static void AddMobileSeparator(ContextMenu menu)
    {
        if (menu.Items.Count > 0 && menu.Items[menu.Items.Count - 1] is not Separator)
            menu.Items.Add(new Separator());
    }
}