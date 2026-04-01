using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private bool _isUpdatingSheetRevisionPanel;
    private string? _selectedSheetRevisionId;
    private string? _sheetRevisionValidationMessage;

    internal void BeginNewSheetRevisionDraftForTesting()
        => BeginNewSheetRevisionDraft();

    internal void SetSheetRevisionDraftForTesting(string revisionNumber, string revisionDate, string description, string author)
    {
        if (SheetRevisionNumberTextBox != null)
            SheetRevisionNumberTextBox.Text = revisionNumber;
        if (SheetRevisionDateTextBox != null)
            SheetRevisionDateTextBox.Text = revisionDate;
        if (SheetRevisionDescriptionTextBox != null)
            SheetRevisionDescriptionTextBox.Text = description;
        if (SheetRevisionAuthorTextBox != null)
            SheetRevisionAuthorTextBox.Text = author;
    }

    internal bool SelectSheetRevisionForTesting(string revisionNumber)
    {
        if (SheetRevisionListBox == null)
            return false;

        var match = SheetRevisionListBox.Items
            .OfType<RevisionEntry>()
            .FirstOrDefault(candidate => string.Equals(candidate.RevisionNumber, revisionNumber, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            return false;

        SheetRevisionListBox.SelectedItem = match;
        return true;
    }

    internal bool SaveSheetRevisionEditorForTesting()
        => SaveSheetRevisionEditor() != null;

    internal bool DeleteSelectedSheetRevisionForTesting()
        => DeleteSelectedSheetRevision(confirmDelete: false, showFeedbackIfNone: false);

    internal (string SaveCaption, bool SaveEnabled, bool DeleteEnabled, string RevisionNumber, string RevisionDate, string Description, string Author, string ValidationText) GetSheetRevisionEditorStateForTesting()
    {
        return (
            SaveSheetRevisionButton?.Content?.ToString() ?? string.Empty,
            SaveSheetRevisionButton?.IsEnabled == true,
            RemoveSheetRevisionButton?.IsEnabled == true,
            SheetRevisionNumberTextBox?.Text ?? string.Empty,
            SheetRevisionDateTextBox?.Text ?? string.Empty,
            SheetRevisionDescriptionTextBox?.Text ?? string.Empty,
            SheetRevisionAuthorTextBox?.Text ?? string.Empty,
            SheetRevisionValidationTextBlock?.Text ?? string.Empty);
    }

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

        UpdateSheetRevisionPanel();
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

    private void AddSheetRevision_Click(object sender, RoutedEventArgs e)
    {
        BeginNewSheetRevisionDraft();
    }

    private void SaveSheetRevision_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSheetRevisionEditor();
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Edit, "Failed to save sheet revision", ex);
            MessageBox.Show($"Unable to save the sheet revision: {ex.Message}", "Sheet Revision", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveSheetRevision_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DeleteSelectedSheetRevision(confirmDelete: true, showFeedbackIfNone: true);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Edit, "Failed to delete sheet revision", ex);
            MessageBox.Show($"Unable to delete the sheet revision: {ex.Message}", "Sheet Revision", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SheetStatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSheetRevisionPanel || _viewModel is not { } viewModel)
            return;

        var sheet = GetSelectedBrowserSheet() ?? viewModel.SelectedSheet;
        if (sheet == null ||
            SheetStatusComboBox.SelectedItem is not ComboBoxItem statusItem ||
            statusItem.Tag is not string statusTag ||
            !Enum.TryParse<DrawingSheetStatus>(statusTag, out var status))
        {
            return;
        }

        if (!viewModel.SetSheetStatus(sheet, status, Environment.UserName))
            return;

        UpdateSheetRevisionPanel();
        UpdateStatusBar();
        ActionLogService.Instance.Log(LogCategory.Edit, "Sheet status updated",
            $"Sheet: {sheet.DisplayName}, Status: {status}");
    }

    private void SheetRevisionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSheetRevisionPanel)
            return;

        if (SheetRevisionListBox?.SelectedItem is RevisionEntry revision)
        {
            LoadSheetRevisionEditorSelection(revision.Id);
        }
        else if (!HasSheetRevisionDraft())
        {
            LoadNewSheetRevisionDraft();
        }

        UpdateSheetRevisionCommandState();
    }

    private void SheetRevisionEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSheetRevisionPanel)
            return;

        RefreshSheetRevisionEditorDraftFeedback();
    }

    private void UpdateSheetRevisionPanel()
    {
        if (_viewModel is not { } viewModel ||
            SheetRevisionSummaryTextBlock == null ||
            SheetStatusComboBox == null ||
            SheetRevisionListBox == null ||
            SheetRevisionNumberTextBox == null ||
            SheetRevisionDateTextBox == null ||
            SheetRevisionDescriptionTextBox == null ||
            SheetRevisionAuthorTextBox == null)
        {
            return;
        }

        var sheet = GetSelectedBrowserSheet() ?? viewModel.SelectedSheet;
        var preserveDraft = _selectedSheetRevisionId == null && HasSheetRevisionDraft();
        RevisionEntry? selectedRevision = null;
        if (!string.IsNullOrWhiteSpace(_selectedSheetRevisionId))
        {
            selectedRevision = sheet?.RevisionEntries.FirstOrDefault(candidate => string.Equals(candidate.Id, _selectedSheetRevisionId, StringComparison.Ordinal));
        }

        _isUpdatingSheetRevisionPanel = true;
        try
        {
            if (sheet == null)
            {
                SheetRevisionSummaryTextBlock.Text = "Select a sheet to manage revisions and approval state.";
                SheetStatusComboBox.SelectedIndex = -1;
                SheetRevisionListBox.ItemsSource = null;
                ClearSheetRevisionEditorControls();
                return;
            }

            SheetRevisionSummaryTextBlock.Text = BuildSheetRevisionSummary(sheet);
            SheetStatusComboBox.SelectedItem = SheetStatusComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), sheet.Status.ToString(), StringComparison.Ordinal));
            SheetRevisionListBox.ItemsSource = null;
            SheetRevisionListBox.ItemsSource = sheet.RevisionEntries;
            SheetRevisionListBox.SelectedItem = selectedRevision;

            if (selectedRevision == null && !preserveDraft)
                SheetRevisionListBox.SelectedIndex = sheet.RevisionEntries.Count > 0 ? 0 : -1;
        }
        finally
        {
            _isUpdatingSheetRevisionPanel = false;
        }

        if (sheet == null)
        {
            UpdateSheetRevisionCommandState();
            return;
        }

        if (selectedRevision != null)
        {
            LoadSheetRevisionEditorSelection(selectedRevision.Id);
        }
        else if (SheetRevisionListBox.SelectedItem is RevisionEntry selectedFromList)
        {
            LoadSheetRevisionEditorSelection(selectedFromList.Id);
        }
        else if (!preserveDraft)
        {
            LoadNewSheetRevisionDraft();
        }
        else
        {
            RefreshSheetRevisionEditorDraftFeedback();
        }

        UpdateSheetRevisionCommandState();
    }

    private void UpdateSheetRevisionCommandState()
    {
        if (SheetStatusComboBox == null ||
            AddSheetRevisionButton == null ||
            SaveSheetRevisionButton == null ||
            RemoveSheetRevisionButton == null ||
            SheetRevisionListBox == null ||
            SheetRevisionNumberTextBox == null ||
            SheetRevisionDateTextBox == null ||
            SheetRevisionDescriptionTextBox == null)
        {
            return;
        }

        var sheet = _viewModel == null ? null : GetSelectedBrowserSheet() ?? _viewModel.SelectedSheet;
        var hasSheet = sheet != null;
        var hasSelection = !string.IsNullOrWhiteSpace(_selectedSheetRevisionId);
        var hasDraft = HasSheetRevisionDraft();

        SheetStatusComboBox.IsEnabled = hasSheet;
        AddSheetRevisionButton.IsEnabled = hasSheet;
        SaveSheetRevisionButton.Content = hasSelection ? "Save Changes" : "Add Revision";
        SaveSheetRevisionButton.IsEnabled = hasSheet && hasDraft && string.IsNullOrWhiteSpace(_sheetRevisionValidationMessage);
        RemoveSheetRevisionButton.IsEnabled = hasSheet && hasSelection && SheetRevisionListBox.SelectedItem is RevisionEntry;
    }

    private void BeginNewSheetRevisionDraft()
    {
        _selectedSheetRevisionId = null;
        if (SheetRevisionListBox != null)
        {
            _isUpdatingSheetRevisionPanel = true;
            SheetRevisionListBox.SelectedItem = null;
            _isUpdatingSheetRevisionPanel = false;
        }

        LoadNewSheetRevisionDraft();
    }

    private bool HasSheetRevisionDraft()
    {
        return !string.IsNullOrWhiteSpace(SheetRevisionNumberTextBox?.Text) ||
               !string.IsNullOrWhiteSpace(SheetRevisionDateTextBox?.Text) ||
               !string.IsNullOrWhiteSpace(SheetRevisionDescriptionTextBox?.Text) ||
               !string.IsNullOrWhiteSpace(SheetRevisionAuthorTextBox?.Text);
    }

    private void LoadSheetRevisionEditorSelection(string revisionId)
    {
        if (_viewModel is not { } viewModel)
            return;

        var sheet = GetSelectedBrowserSheet() ?? viewModel.SelectedSheet;
        var revision = sheet?.RevisionEntries.FirstOrDefault(candidate => string.Equals(candidate.Id, revisionId, StringComparison.Ordinal));
        if (revision == null)
        {
            LoadNewSheetRevisionDraft();
            return;
        }

        _selectedSheetRevisionId = revision.Id;
        _isUpdatingSheetRevisionPanel = true;
        SheetRevisionNumberTextBox!.Text = revision.RevisionNumber;
        SheetRevisionDateTextBox!.Text = revision.Date;
        SheetRevisionDescriptionTextBox!.Text = revision.Description;
        SheetRevisionAuthorTextBox!.Text = revision.Author;
        _isUpdatingSheetRevisionPanel = false;
        RefreshSheetRevisionEditorDraftFeedback();
    }

    private void LoadNewSheetRevisionDraft()
    {
        if (_viewModel is not { } viewModel)
            return;

        var sheet = GetSelectedBrowserSheet() ?? viewModel.SelectedSheet;
        _selectedSheetRevisionId = null;
        _isUpdatingSheetRevisionPanel = true;
        SheetRevisionNumberTextBox!.Text = sheet == null ? string.Empty : viewModel.GetNextSheetRevisionNumber(sheet);
        SheetRevisionDateTextBox!.Text = DateTime.Now.ToString("yyyy-MM-dd");
        SheetRevisionDescriptionTextBox!.Text = string.Empty;
        SheetRevisionAuthorTextBox!.Text = Environment.UserName;
        _isUpdatingSheetRevisionPanel = false;
        RefreshSheetRevisionEditorDraftFeedback();
    }

    private void ClearSheetRevisionEditorControls()
    {
        _selectedSheetRevisionId = null;
        SheetRevisionNumberTextBox!.Text = string.Empty;
        SheetRevisionDateTextBox!.Text = string.Empty;
        SheetRevisionDescriptionTextBox!.Text = string.Empty;
        SheetRevisionAuthorTextBox!.Text = string.Empty;
        _sheetRevisionValidationMessage = null;
        if (SheetRevisionValidationTextBlock != null)
        {
            SheetRevisionValidationTextBlock.Text = string.Empty;
            SheetRevisionValidationTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshSheetRevisionEditorDraftFeedback()
    {
        if (_viewModel is not { } viewModel ||
            SheetRevisionNumberTextBox == null ||
            SheetRevisionDateTextBox == null ||
            SheetRevisionDescriptionTextBox == null ||
            SheetRevisionValidationTextBlock == null)
        {
            return;
        }

        var sheet = GetSelectedBrowserSheet() ?? viewModel.SelectedSheet;
        var revisionNumber = SheetRevisionNumberTextBox.Text.Trim();
        var revisionDate = SheetRevisionDateTextBox.Text.Trim();
        var description = SheetRevisionDescriptionTextBox.Text.Trim();
        string? validationMessage = null;

        if (!string.IsNullOrWhiteSpace(_selectedSheetRevisionId) || HasSheetRevisionDraft())
        {
            if (string.IsNullOrWhiteSpace(revisionNumber))
                validationMessage = "Revision number is required.";
            else if (string.IsNullOrWhiteSpace(revisionDate))
                validationMessage = "Revision date is required.";
            else if (string.IsNullOrWhiteSpace(description))
                validationMessage = "Revision description is required.";
            else if (sheet != null && sheet.RevisionEntries.Any(candidate =>
                         !string.Equals(candidate.Id, _selectedSheetRevisionId, StringComparison.Ordinal) &&
                         string.Equals(candidate.RevisionNumber, revisionNumber, StringComparison.OrdinalIgnoreCase)))
                validationMessage = "Revision number must be unique within the selected sheet.";
        }

        _sheetRevisionValidationMessage = validationMessage;
        SheetRevisionValidationTextBlock.Text = validationMessage ?? string.Empty;
        SheetRevisionValidationTextBlock.Visibility = string.IsNullOrWhiteSpace(validationMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateSheetRevisionCommandState();
    }

    private RevisionEntry? SaveSheetRevisionEditor()
    {
        if (_viewModel is not { } viewModel ||
            SheetRevisionNumberTextBox == null ||
            SheetRevisionDateTextBox == null ||
            SheetRevisionDescriptionTextBox == null ||
            SheetRevisionAuthorTextBox == null)
        {
            throw new InvalidOperationException("Sheet revision editor controls are not available.");
        }

        var sheet = GetSelectedBrowserSheet() ?? viewModel.SelectedSheet
            ?? throw new InvalidOperationException("A sheet must be selected before saving revisions.");

        RefreshSheetRevisionEditorDraftFeedback();
        if (!string.IsNullOrWhiteSpace(_sheetRevisionValidationMessage))
            throw new InvalidOperationException(_sheetRevisionValidationMessage);

        var revisionNumber = SheetRevisionNumberTextBox.Text.Trim();
        var revisionDate = SheetRevisionDateTextBox.Text.Trim();
        var description = SheetRevisionDescriptionTextBox.Text.Trim();
        var author = SheetRevisionAuthorTextBox.Text.Trim();

        RevisionEntry revision;
        if (string.IsNullOrWhiteSpace(_selectedSheetRevisionId))
        {
            revision = viewModel.AddSheetRevision(sheet, description, author, revisionNumber, revisionDate);
            ActionLogService.Instance.Log(LogCategory.Edit, "Sheet revision added",
                $"Sheet: {sheet.DisplayName}, Revision: {revision.RevisionNumber}, Description: {revision.Description}");
        }
        else
        {
            if (!viewModel.UpdateSheetRevision(sheet, _selectedSheetRevisionId, revisionNumber, revisionDate, description, author))
                throw new InvalidOperationException("The selected revision could not be updated.");

            revision = sheet.RevisionEntries.First(candidate => string.Equals(candidate.Id, _selectedSheetRevisionId, StringComparison.Ordinal));
            ActionLogService.Instance.Log(LogCategory.Edit, "Sheet revision updated",
                $"Sheet: {sheet.DisplayName}, Revision: {revision.RevisionNumber}, Description: {revision.Description}");
        }

        _selectedSheetRevisionId = revision.Id;
        UpdateSheetRevisionPanel();
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        UpdateStatusBar();
        return revision;
    }

    private bool DeleteSelectedSheetRevision(bool confirmDelete, bool showFeedbackIfNone)
    {
        if (_viewModel is not { } viewModel)
            return false;

        var sheet = GetSelectedBrowserSheet() ?? viewModel.SelectedSheet;
        var revision = sheet?.RevisionEntries.FirstOrDefault(candidate => string.Equals(candidate.Id, _selectedSheetRevisionId, StringComparison.Ordinal));
        if (sheet == null || revision == null)
        {
            if (showFeedbackIfNone)
            {
                MessageBox.Show("Select a revision to remove.", "Sheet Revision", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        if (confirmDelete && MessageBox.Show($"Remove revision {revision.RevisionNumber} from {sheet.DisplayName}?", "Remove Revision",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return false;
        }

        if (!viewModel.RemoveSheetRevision(sheet, revision.Id))
            return false;

        _selectedSheetRevisionId = null;
        UpdateSheetRevisionPanel();
        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        UpdateStatusBar();
        ActionLogService.Instance.Log(LogCategory.Edit, "Sheet revision removed",
            $"Sheet: {sheet.DisplayName}, Revision: {revision.RevisionNumber}");
        return true;
    }

    private static string BuildSheetRevisionSummary(DrawingSheet sheet)
    {
        var summary = $"Status: {FormatSheetStatus(sheet.Status)}\nRevisions: {sheet.RevisionEntries.Count}\nModified: {sheet.ModifiedUtc.ToLocalTime():g} by {sheet.ModifiedBy}";
        if (sheet.ApprovedUtc is { } approvedUtc && !string.IsNullOrWhiteSpace(sheet.ApprovedBy))
            summary += $"\nApproved: {approvedUtc.ToLocalTime():g} by {sheet.ApprovedBy}";

        return summary;
    }

    private static string FormatSheetStatus(DrawingSheetStatus status)
    {
        return status switch
        {
            DrawingSheetStatus.InReview => "In Review",
            _ => status.ToString()
        };
    }
}