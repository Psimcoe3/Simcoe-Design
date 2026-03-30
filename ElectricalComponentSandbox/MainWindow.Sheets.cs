using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private void InitializeSheetBrowser()
    {
        SyncSheetBrowserSelection();
        UpdateSheetBrowserSummary();
        UpdateSheetBrowserCommandState();
    }

    private void SyncSheetBrowserSelection()
    {
        SheetListBox.SelectedItem = _viewModel.SelectedSheet;
        UpdateSheetBrowserSummary();
        UpdateSheetBrowserCommandState();
    }

    private void UpdateSheetBrowserSummary()
    {
        var sheetCount = _viewModel.Sheets.Count;
        var active = _viewModel.SelectedSheet;
        var activeIndex = active == null ? 0 : _viewModel.Sheets.IndexOf(active) + 1;
        ActiveSheetSummaryTextBlock.Text = active == null
            ? $"{sheetCount} sheet{(sheetCount == 1 ? string.Empty : "s")}"
            : $"{activeIndex}/{sheetCount} | {active.DisplayName}";
    }

    private void UpdateSheetBrowserCommandState()
    {
        var selectedSheet = SheetListBox.SelectedItem as DrawingSheet ?? _viewModel.SelectedSheet;
        var selectedIndex = selectedSheet == null ? -1 : _viewModel.Sheets.IndexOf(selectedSheet);
        var canModifySelection = selectedIndex >= 0;

        RenameSheetButton.IsEnabled = canModifySelection;
        DeleteSheetButton.IsEnabled = canModifySelection && _viewModel.Sheets.Count > 1;
        MoveSheetUpButton.IsEnabled = selectedIndex > 0;
        MoveSheetDownButton.IsEnabled = selectedIndex >= 0 && selectedIndex < _viewModel.Sheets.Count - 1;
    }

    private void AddSheet_Click(object sender, RoutedEventArgs e)
    {
        var nextDefault = $"Sheet {_viewModel.Sheets.Count + 1}";
        var name = PromptInput("Add Sheet", "Enter a sheet name:", nextDefault);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var sheet = _viewModel.AddSheet(name);
        SyncSheetBrowserSelection();
        SheetListBox.SelectedItem = sheet;
        RebuildNamedViewMenuItems();
        UpdateViewport();
        Update2DCanvas();
        UpdateStatusBar();
    }

    private void SheetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SheetListBox.SelectedItem is not DrawingSheet sheet)
        {
            UpdateSheetBrowserSummary();
            UpdateSheetBrowserCommandState();
            return;
        }

        if (!_viewModel.SelectSheet(sheet))
        {
            UpdateSheetBrowserSummary();
            UpdateSheetBrowserCommandState();
            return;
        }

        ClearMarkupSelection();
        RebuildNamedViewMenuItems();
        UpdateSheetBrowserSummary();
        UpdateSheetBrowserCommandState();
        ActionLogService.Instance.Log(LogCategory.View, "Sheet browser selection changed", $"Sheet: {sheet.DisplayName}");
        UpdateViewport();
        Update2DCanvas();
        UpdateStatusBar();
    }

    private void RenameSheet_Click(object sender, RoutedEventArgs e)
    {
        if ((SheetListBox.SelectedItem as DrawingSheet ?? _viewModel.SelectedSheet) is not { } sheet)
            return;

        var number = PromptInput("Rename Sheet", "Enter a sheet number:", sheet.Number);
        if (number == null)
            return;

        var name = PromptInput("Rename Sheet", "Enter a sheet name:", sheet.Name);
        if (name == null)
            return;

        if (!_viewModel.RenameSheet(sheet, number, name))
        {
            UpdateSheetBrowserSummary();
            UpdateSheetBrowserCommandState();
            return;
        }

        SyncSheetBrowserSelection();
        RebuildNamedViewMenuItems();
        UpdateStatusBar();
    }

    private void DeleteSheet_Click(object sender, RoutedEventArgs e)
    {
        if ((SheetListBox.SelectedItem as DrawingSheet ?? _viewModel.SelectedSheet) is not { } sheet)
            return;

        if (_viewModel.Sheets.Count <= 1)
        {
            MessageBox.Show("At least one sheet must remain in the project.", "Delete Sheet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Delete {sheet.DisplayName}?", "Delete Sheet", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (!_viewModel.DeleteSheet(sheet))
            return;

        SyncSheetBrowserSelection();
        RebuildNamedViewMenuItems();
        UpdateViewport();
        Update2DCanvas();
        UpdateStatusBar();
    }

    private void MoveSheetUp_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedSheet(-1);
    }

    private void MoveSheetDown_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedSheet(1);
    }

    private void MoveSelectedSheet(int direction)
    {
        if ((SheetListBox.SelectedItem as DrawingSheet ?? _viewModel.SelectedSheet) is not { } sheet)
            return;

        if (!_viewModel.MoveSheet(sheet, direction))
        {
            UpdateSheetBrowserCommandState();
            return;
        }

        SyncSheetBrowserSelection();
        SheetListBox.SelectedItem = sheet;
        UpdateStatusBar();
    }
}