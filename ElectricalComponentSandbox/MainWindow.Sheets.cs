using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private void InitializeSheetBrowser()
    {
        _viewModel.RefreshProjectBrowserItems();
        SyncSheetBrowserSelection();
        UpdateSheetBrowserSummary();
        UpdateSheetBrowserCommandState();
    }

    private void SyncSheetBrowserSelection()
    {
        foreach (var sheetItem in _viewModel.ProjectBrowserItems)
        {
            sheetItem.IsSelected = ReferenceEquals(sheetItem.Sheet, _viewModel.SelectedSheet);
            foreach (var namedViewItem in sheetItem.Children.OfType<ProjectBrowserNamedViewItemViewModel>())
                namedViewItem.IsSelected = false;
        }

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
        var selectedSheet = GetSelectedBrowserSheet() ?? _viewModel.SelectedSheet;
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
        RebuildNamedViewMenuItems();
        UpdateViewport();
        Update2DCanvas();
        UpdateStatusBar();
    }

    private void ProjectBrowserTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not ProjectBrowserItemViewModel browserItem)
        {
            UpdateSheetBrowserSummary();
            UpdateSheetBrowserCommandState();
            return;
        }

        HandleProjectBrowserSelection(browserItem);
    }

    internal string[] GetProjectBrowserLabelsForTesting()
    {
        return _viewModel.ProjectBrowserItems
            .SelectMany(sheetItem => new[] { $"Sheet:{sheetItem.DisplayName}" }
                .Concat(sheetItem.Children.OfType<ProjectBrowserNamedViewItemViewModel>().Select(viewItem => $"View:{viewItem.Sheet.DisplayName}:{viewItem.DisplayName}")))
            .ToArray();
    }

    internal bool SelectProjectBrowserNamedViewForTesting(string sheetDisplayName, string namedViewName)
    {
        var item = _viewModel.ProjectBrowserItems
            .SelectMany(sheetItem => sheetItem.Children.OfType<ProjectBrowserNamedViewItemViewModel>())
            .FirstOrDefault(candidate => string.Equals(candidate.Sheet.DisplayName, sheetDisplayName, StringComparison.Ordinal) &&
                                         string.Equals(candidate.DisplayName, namedViewName, StringComparison.Ordinal));

        if (item == null)
            return false;

        HandleProjectBrowserSelection(item);
        return true;
    }

    private void HandleProjectBrowserSelection(ProjectBrowserItemViewModel browserItem)
    {
        if (browserItem is ProjectBrowserNamedViewItemViewModel namedViewItem)
        {
            if (_viewModel.SelectSheet(namedViewItem.Sheet))
            {
                ClearMarkupSelection();
                RebuildNamedViewMenuItems();
            }

            RestoreNamedView(namedViewItem.NamedView);
            UpdateSheetBrowserSummary();
            UpdateSheetBrowserCommandState();
            ActionLogService.Instance.Log(LogCategory.View, "Project browser named view restored",
                $"Sheet: {namedViewItem.Sheet.DisplayName}, View: {namedViewItem.NamedView.Name}");
            UpdateViewport();
            Update2DCanvas();
            UpdateStatusBar();
            return;
        }

        if (browserItem is not ProjectBrowserSheetItemViewModel sheetItem)
            return;

        var sheet = sheetItem.Sheet;

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
        if ((GetSelectedBrowserSheet() ?? _viewModel.SelectedSheet) is not { } sheet)
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
        if ((GetSelectedBrowserSheet() ?? _viewModel.SelectedSheet) is not { } sheet)
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
        if ((GetSelectedBrowserSheet() ?? _viewModel.SelectedSheet) is not { } sheet)
            return;

        if (!_viewModel.MoveSheet(sheet, direction))
        {
            UpdateSheetBrowserCommandState();
            return;
        }

        SyncSheetBrowserSelection();
        UpdateStatusBar();
    }

    private DrawingSheet? GetSelectedBrowserSheet()
    {
        return ProjectBrowserTreeView.SelectedItem switch
        {
            ProjectBrowserSheetItemViewModel sheetItem => sheetItem.Sheet,
            ProjectBrowserNamedViewItemViewModel namedViewItem => namedViewItem.Sheet,
            _ => null
        };
    }
}