using System.Windows;
using System.Windows.Controls.Primitives;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private GridLength _desktopLibraryColumnWidth = new GridLength(200);
    private GridLength _desktopViewportColumnWidth = new GridLength(1, GridUnitType.Star);
    private GridLength _desktopPropertiesColumnWidth = new GridLength(300);
    private bool _showDesktopLibraryPanel = true;
    private bool _showDesktopPropertiesPanel = true;

    private void ShowLibraryPanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _showDesktopLibraryPanel = ShowLibraryPanelMenuItem.IsChecked;
        if (_isMobileView)
            return;

        ApplyDesktopPaneLayout();
        ActionLogService.Instance.Log(LogCategory.View, "Left panel visibility changed",
            $"Visible: {_showDesktopLibraryPanel}");
    }

    private void ShowPropertiesPanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _showDesktopPropertiesPanel = ShowPropertiesPanelMenuItem.IsChecked;
        if (_isMobileView)
            return;

        ApplyDesktopPaneLayout();
        ActionLogService.Instance.Log(LogCategory.View, "Right panel visibility changed",
            $"Visible: {_showDesktopPropertiesPanel}");
    }

    private void LibraryGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isMobileView || !_showDesktopLibraryPanel)
            return;

        if (LibraryColumn.Width.Value <= 0)
            return;

        _desktopLibraryColumnWidth = LibraryColumn.Width;
        ActionLogService.Instance.Log(LogCategory.View, "Left panel resized",
            $"Width: {_desktopLibraryColumnWidth.Value:F0}");
    }

    private void PropertiesGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isMobileView || !_showDesktopPropertiesPanel)
            return;

        if (PropertiesColumn.Width.Value <= 0)
            return;

        _desktopPropertiesColumnWidth = PropertiesColumn.Width;
        ActionLogService.Instance.Log(LogCategory.View, "Right panel resized",
            $"Width: {_desktopPropertiesColumnWidth.Value:F0}");
    }

    private void ApplyDesktopPaneLayout()
    {
        if (_isMobileView)
            return;

        ViewportPanelContainer.Visibility = Visibility.Visible;
        ViewportColumn.Width = EnsureVisibleColumnWidth(_desktopViewportColumnWidth, 1, GridUnitType.Star);

        if (_showDesktopLibraryPanel)
        {
            LibraryPanelContainer.Visibility = Visibility.Visible;
            LibraryColumn.Width = EnsureVisibleColumnWidth(_desktopLibraryColumnWidth, 200);
        }
        else
        {
            if (LibraryColumn.Width.Value > 0)
                _desktopLibraryColumnWidth = LibraryColumn.Width;

            LibraryPanelContainer.Visibility = Visibility.Collapsed;
            LibraryColumn.Width = new GridLength(0);
        }

        if (_showDesktopPropertiesPanel)
        {
            PropertiesPanelContainer.Visibility = Visibility.Visible;
            PropertiesColumn.Width = EnsureVisibleColumnWidth(_desktopPropertiesColumnWidth, 300);
        }
        else
        {
            if (PropertiesColumn.Width.Value > 0)
                _desktopPropertiesColumnWidth = PropertiesColumn.Width;

            PropertiesPanelContainer.Visibility = Visibility.Collapsed;
            PropertiesColumn.Width = new GridLength(0);
        }

        LibraryGridSplitter.Visibility = _showDesktopLibraryPanel ? Visibility.Visible : Visibility.Collapsed;
        PropertiesGridSplitter.Visibility = _showDesktopPropertiesPanel ? Visibility.Visible : Visibility.Collapsed;
        ShowLibraryPanelMenuItem.IsChecked = _showDesktopLibraryPanel;
        ShowPropertiesPanelMenuItem.IsChecked = _showDesktopPropertiesPanel;
    }

    private static GridLength EnsureVisibleColumnWidth(GridLength width, double fallbackValue, GridUnitType fallbackUnit = GridUnitType.Pixel)
    {
        if (width.Value > 0)
            return width;

        return new GridLength(fallbackValue, fallbackUnit);
    }
}