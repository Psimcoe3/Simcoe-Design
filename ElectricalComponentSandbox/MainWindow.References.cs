using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    internal string GetReferenceDocsRootForTesting()
        => ReferenceCatalogService.GetReferenceDocsRoot() ?? string.Empty;

    internal IReadOnlyList<ReferenceCatalogEntry> GetAssignableReferenceEntriesForTesting()
        => ReferenceCatalogService.GetAssignableEntries();

    internal static ReferenceLaunchResolution ResolveReferenceForTesting(string reference, string? workspaceRoot = null)
        => ReferenceCatalogService.ResolveLaunchTarget(reference, workspaceRoot);

    private void InitializeReferenceMenu()
    {
        RefreshReferenceUi();
    }

    private void RefreshReferenceUi()
    {
        ReferencesMenuItem.Items.Clear();

        var currentDocsRoot = ReferenceCatalogService.GetReferenceDocsRoot();
        ReferencesMenuItem.Items.Add(new MenuItem
        {
            Header = $"Reference Root: {FormatReferenceRootDisplay(currentDocsRoot)}",
            IsEnabled = false,
            ToolTip = currentDocsRoot ?? "Automatic workspace-relative discovery"
        });
        ReferencesMenuItem.Items.Add(new Separator());
        ReferencesMenuItem.Items.Add(new MenuItem { Header = "Set Reference Root...", Tag = "set-root" });
        ((MenuItem)ReferencesMenuItem.Items[^1]).Click += SetReferenceRoot_Click;
        ReferencesMenuItem.Items.Add(new MenuItem
        {
            Header = "Reset Reference Root",
            Tag = "reset-root",
            IsEnabled = !string.IsNullOrWhiteSpace(ReferenceCatalogService.GetReferenceDocsRootOverride())
        });
        ((MenuItem)ReferencesMenuItem.Items[^1]).Click += ResetReferenceRoot_Click;
        ReferencesMenuItem.Items.Add(new Separator());

        foreach (var entry in ReferenceCatalogService.GetCuratedEntries())
        {
            ReferencesMenuItem.Items.Add(BuildReferenceMenuItem(entry));
        }

        ReferenceCatalogComboBox.ItemsSource = ReferenceCatalogService.GetAssignableEntries();
        ReferenceCatalogComboBox.SelectedIndex = -1;
        UpdateReferenceAssignmentUi(Array.Empty<ElectricalComponent>());
    }

    private void SetReferenceRoot_Click(object sender, RoutedEventArgs e)
    {
        var currentRoot = ReferenceCatalogService.GetReferenceDocsRootOverride() ?? ReferenceCatalogService.GetReferenceDocsRoot() ?? string.Empty;
        var input = PromptInput(
            "Reference Root",
            $"Enter the reference docs root or a workspace root containing References\\docs.\n\nYou can also set the {ReferenceCatalogService.ReferenceRootEnvVar} environment variable.",
            currentRoot);
        if (input == null)
            return;

        if (!ReferenceCatalogService.TrySetReferenceDocsRootOverride(input, out var normalizedPath, out var errorMessage))
        {
            MessageBox.Show(errorMessage, "Reference Root", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshReferenceUi();
        UpdateReferenceAssignmentUi(GetSelectedComponents());
        ActionLogService.Instance.Log(LogCategory.Application, "Reference root updated", normalizedPath);
        MessageBox.Show($"Reference root set to:\n{normalizedPath}", "Reference Root", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetReferenceRoot_Click(object sender, RoutedEventArgs e)
    {
        ReferenceCatalogService.ClearReferenceDocsRootOverride();
        RefreshReferenceUi();
        UpdateReferenceAssignmentUi(GetSelectedComponents());
        ActionLogService.Instance.Log(LogCategory.Application, "Reference root reset", "Using automatic discovery");
    }

    internal bool SelectReferenceCatalogEntryForTesting(string displayName)
    {
        var match = ReferenceCatalogComboBox.Items
            .OfType<ReferenceCatalogEntry>()
            .FirstOrDefault(entry => string.Equals(entry.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            return false;

        ReferenceCatalogComboBox.SelectedItem = match;
        return true;
    }

    internal bool AssignSelectedReferenceForTesting()
        => TryAssignSelectedCatalogReferenceToTextBox();

    internal bool ApplySuggestedReferenceForTesting()
        => TryApplySuggestedReferenceToTextBox(GetSelectedComponents());

    internal string? GetSuggestedReferencePathForTesting()
        => ElectricalComponentCatalog.GetSuggestedLocalReference(GetSelectedComponents());

    internal string? GetSelectedReferenceCatalogPathForTesting()
        => (ReferenceCatalogComboBox.SelectedItem as ReferenceCatalogEntry)?.RelativePath;

    private object BuildReferenceMenuItem(ReferenceCatalogEntry entry)
    {
        if (entry.Children == null || entry.Children.Count == 0)
            return CreateLaunchableReferenceMenuItem(entry.DisplayName, entry.RelativePath);

        var menuItem = new MenuItem
        {
            Header = entry.DisplayName,
            ToolTip = entry.RelativePath
        };

        menuItem.Items.Add(CreateLaunchableReferenceMenuItem("Open Folder", entry.RelativePath));

        if (entry.Children.Count > 0)
        {
            menuItem.Items.Add(new Separator());
            foreach (var child in entry.Children)
            {
                menuItem.Items.Add(BuildReferenceMenuItem(child));
            }
        }

        return menuItem;
    }

    private MenuItem CreateLaunchableReferenceMenuItem(string header, string referencePath)
    {
        var menuItem = new MenuItem
        {
            Header = header,
            Tag = referencePath,
            ToolTip = referencePath
        };
        menuItem.Click += OpenCuratedReference_Click;
        return menuItem;
    }

    private void OpenCuratedReference_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string referencePath })
            return;

        TryOpenReferenceTarget(referencePath, missingReferenceMessage: "Reference target is missing from the local workspace.");
    }

    private void AssignSelectedReference_Click(object sender, RoutedEventArgs e)
    {
        TryAssignSelectedCatalogReferenceToTextBox();
    }

    private void ApplySuggestedReference_Click(object sender, RoutedEventArgs e)
    {
        TryApplySuggestedReferenceToTextBox(GetSelectedComponents());
    }

    private bool TryAssignSelectedCatalogReferenceToTextBox()
    {
        if (ReferenceCatalogComboBox.SelectedItem is not ReferenceCatalogEntry selectedEntry)
        {
            MessageBox.Show("Select a curated reference first.", "Reference", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        ReferenceUrlTextBox.Text = selectedEntry.RelativePath;
        return true;
    }

    private bool TryApplySuggestedReferenceToTextBox(IReadOnlyList<ElectricalComponent> components)
    {
        var suggestedReference = ElectricalComponentCatalog.GetSuggestedLocalReference(components);
        if (string.IsNullOrWhiteSpace(suggestedReference))
        {
            MessageBox.Show("No single suggested local reference is available for the current selection.", "Reference", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        ReferenceUrlTextBox.Text = suggestedReference;
        SyncReferenceCatalogSelectionWithText(suggestedReference);
        return true;
    }

    private void UpdateReferenceAssignmentUi(IReadOnlyList<ElectricalComponent> selectedComponents)
    {
        var hasSelection = selectedComponents.Count > 0;
        ReferenceCatalogComboBox.IsEnabled = hasSelection;
        AssignReferenceButton.IsEnabled = hasSelection;

        var suggestedReference = ElectricalComponentCatalog.GetSuggestedLocalReference(selectedComponents);
        SuggestedReferenceButton.IsEnabled = hasSelection && !string.IsNullOrWhiteSpace(suggestedReference);
        SuggestedReferenceButton.ToolTip = string.IsNullOrWhiteSpace(suggestedReference)
            ? "No single suggested local reference is available for the current selection."
            : suggestedReference;

        ReferenceRootStatusTextBlock.Text = $"Reference root: {FormatReferenceRootDisplay(ReferenceCatalogService.GetReferenceDocsRoot())}";
        ReferenceRootStatusTextBlock.ToolTip = ReferenceCatalogService.GetReferenceDocsRoot() ?? "Automatic workspace-relative discovery";

        SyncReferenceCatalogSelectionWithText(ReferenceUrlTextBox.Text);
    }

    private void SyncReferenceCatalogSelectionWithText(string? reference)
    {
        if (ReferenceCatalogComboBox.ItemsSource == null)
            return;

        var normalizedReference = NormalizeReferenceValue(reference);
        var match = ReferenceCatalogComboBox.Items
            .OfType<ReferenceCatalogEntry>()
            .FirstOrDefault(entry => string.Equals(NormalizeReferenceValue(entry.RelativePath), normalizedReference, StringComparison.OrdinalIgnoreCase));

        ReferenceCatalogComboBox.SelectedItem = match;
    }

    private static string NormalizeReferenceValue(string? reference)
        => string.IsNullOrWhiteSpace(reference)
            ? string.Empty
            : reference.Trim().Replace('/', '\\');

    private static string FormatReferenceRootDisplay(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "(auto)";

        return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } leafName
            ? leafName
            : path;
    }

    private bool TryOpenReferenceTarget(string? reference, string missingReferenceMessage)
    {
        var resolution = ReferenceCatalogService.ResolveLaunchTarget(reference ?? string.Empty);
        if (!resolution.Success)
        {
            MessageBox.Show(
                string.IsNullOrWhiteSpace(reference)
                    ? missingReferenceMessage
                    : resolution.ErrorMessage,
                "Reference",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resolution.LaunchTarget!,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open reference: {ex.Message}", "Reference", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}