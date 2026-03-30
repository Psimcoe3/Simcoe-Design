using System;
using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public class MainWindowReferenceTests
{
    [Fact]
    public void ReferencesMenu_InitializesCuratedEntries()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow(new MainViewModel());
            try
            {
                var referencesMenu = FindRequired<MenuItem>(window, "ReferencesMenuItem");
                Assert.Equal(2, referencesMenu.Items.Count);

                var estimator = Assert.IsType<MenuItem>(referencesMenu.Items[0]);
                Assert.Equal("2026 National Electrical Estimator Ebook", estimator.Header?.ToString());

                var electricalMaterial = Assert.IsType<MenuItem>(referencesMenu.Items[1]);
                Assert.Equal("Electrical Material", electricalMaterial.Header?.ToString());
                Assert.True(electricalMaterial.Items.Count >= 1);

                var openFolderItem = Assert.IsType<MenuItem>(electricalMaterial.Items[0]);
                Assert.Equal("Open Folder", openFolderItem.Header?.ToString());
                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ResolveReferenceForTesting_RepoRelativePath_ResolvesWithinWorkspace()
    {
        var resolution = MainWindow.ResolveReferenceForTesting(@"References\docs\2026_national_electrical_estimator_ebook.pdf");

        Assert.True(resolution.Success);
        Assert.EndsWith(@"References\docs\2026_national_electrical_estimator_ebook.pdf", resolution.LaunchTarget);
    }

    [Fact]
    public void PropertiesPanel_AssignSelectedReference_FillsReferenceUrlText()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow(viewModel);
            try
            {
                viewModel.Components.Add(new BoxComponent { Name = "Box 1" });
                viewModel.SelectSingleComponent(viewModel.Components[0]);
                window.UpdatePropertiesPanelForTesting();

                Assert.True(window.SelectReferenceCatalogEntryForTesting("NFPA 70 2023.pdf"));
                Assert.True(window.AssignSelectedReferenceForTesting());

                var referenceTextBox = FindRequired<TextBox>(window, "ReferenceUrlTextBox");
                Assert.Equal(@"References\docs\Electrical Material\NFPA 70 2023.pdf", referenceTextBox.Text);
                Assert.Equal(@"References\docs\Electrical Material\NFPA 70 2023.pdf", window.GetSelectedReferenceCatalogPathForTesting());
                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PropertiesPanel_ApplySuggestedReference_UsesSharedSelectionSuggestion()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var first = new ConduitComponent { Name = "Conduit 1" };
            var second = new ConduitComponent { Name = "Conduit 2" };
            viewModel.Components.Add(first);
            viewModel.Components.Add(second);

            var window = new MainWindow(viewModel);
            try
            {
                viewModel.SetSelectedComponents(new ElectricalComponent[] { first, second }, first);
                window.UpdatePropertiesPanelForTesting();

                Assert.Equal(@"References\docs\Electrical Material\NFPA 70 2023.pdf", window.GetSuggestedReferencePathForTesting());
                Assert.True(window.ApplySuggestedReferenceForTesting());

                var referenceTextBox = FindRequired<TextBox>(window, "ReferenceUrlTextBox");
                Assert.Equal(@"References\docs\Electrical Material\NFPA 70 2023.pdf", referenceTextBox.Text);

                window.ApplyPropertiesForTesting();
                Assert.All(new[] { first, second }, component =>
                    Assert.Equal(@"References\docs\Electrical Material\NFPA 70 2023.pdf", component.Parameters.ReferenceUrl));
                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        lock (WpfStaTestSynchronization.MainWindowLock)
        {
            T? result = default;
            Exception? exception = null;

            var thread = new Thread(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
                throw new Xunit.Sdk.XunitException($"STA test failed: {exception}");

            return result!;
        }
    }

    private static T FindRequired<T>(FrameworkElement root, string name) where T : class
    {
        return root.FindName(name) as T
            ?? throw new Xunit.Sdk.XunitException($"Expected control '{name}' of type {typeof(T).Name}.");
    }
}