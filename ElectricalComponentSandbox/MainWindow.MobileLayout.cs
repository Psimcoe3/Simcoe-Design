using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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
            OpenContextMenuFromButton(button, PlacementMode.Bottom);
        }
    }

    private void MobileAddTopButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            OpenContextMenuFromButton(button, PlacementMode.Bottom);
        }
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

        MobileTopBarGrid.Height = isIOS ? 52 : 56;
        MobileSectionTitleText.FontSize = isIOS ? 17 : 18;
        MobileSectionTitleText.FontWeight = isIOS ? FontWeights.SemiBold : FontWeights.Medium;

        MobileUndoButton.Foreground = primary;
        MobileRedoButton.Foreground = primary;
        MobileAddTopButton.Foreground = primary;
        MobileMoreButton.Foreground = primary;
        MobileUndoButton.Content = isIOS ? "Undo" : "Undo";
        MobileRedoButton.Content = isIOS ? "Redo" : "Redo";
        MobileAddTopButton.Content = isIOS ? "Add" : "+ Add";
        MobileMoreButton.Content = isIOS ? "More" : "Menu";

        MobileCanvasButton.Content = isIOS ? "Canvas\nPlan" : "Canvas";
        MobileLibraryButton.Content = isIOS ? "Library\nParts" : "Library";
        MobilePropertiesButton.Content = isIOS ? "Properties\nEdit" : "Properties";

        UpdateMobileNavigationVisuals();
        UpdateWorkspaceOverview();
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
                MobileSectionTitleText.Text = "Plan";
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
                MobileSectionTitleText.Text = "Library";
                break;

            case MobilePane.Properties:
                LibraryPanelContainer.Visibility = Visibility.Collapsed;
                ViewportPanelContainer.Visibility = Visibility.Collapsed;
                PropertiesPanelContainer.Visibility = Visibility.Visible;
                LibraryColumn.Width = new GridLength(0);
                ViewportColumn.Width = new GridLength(0);
                PropertiesColumn.Width = new GridLength(1, GridUnitType.Star);
                MobileAddTopButton.Visibility = Visibility.Collapsed;
                MobileSectionTitleText.Text = "Properties";
                break;
        }

        UpdateMobileNavigationVisuals();
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
}