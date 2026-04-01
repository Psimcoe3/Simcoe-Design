using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public class MainWindowProjectBrowserTests
{
    [Fact]
    public void ProjectBrowser_ListsNamedViewsUnderOwningSheets()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.NamedViews.Add(new NamedView { Name = "Sheet 1 View", Zoom = 1.1 });
            viewModel.AddSheet("Review");
            viewModel.NamedViews.Add(new NamedView { Name = "Review View", Zoom = 2.0 });

            var window = new MainWindow(viewModel);
            try
            {
                var labels = window.GetProjectBrowserLabelsForTesting();

                Assert.Contains($"Sheet:{viewModel.Sheets[0].DisplayName}", labels);
                Assert.Contains($"View:{viewModel.Sheets[0].DisplayName}:Sheet 1 View", labels);
                Assert.Contains($"Sheet:{viewModel.Sheets[1].DisplayName}", labels);
                Assert.Contains($"View:{viewModel.Sheets[1].DisplayName}:Review View", labels);
                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ProjectBrowser_SelectNamedView_SwitchesSheetAndRestoresView()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.NamedViews.Add(new NamedView { Name = "Sheet 1 View", Zoom = 1.5 });
            var firstSheet = viewModel.SelectedSheet;

            viewModel.AddSheet("Review");
            viewModel.NamedViews.Add(new NamedView { Name = "Review View", Zoom = 2.0 });

            var window = new MainWindow(viewModel);
            try
            {
                Assert.True(window.SelectProjectBrowserNamedViewForTesting(firstSheet!.DisplayName, "Sheet 1 View"));

                var scale = FindRequired<ScaleTransform>(window, "PlanCanvasScale");
                Assert.Equal(firstSheet.Id, viewModel.SelectedSheet?.Id);
                Assert.Equal(1.5, scale.ScaleX, 3);
                Assert.Equal(1.5, scale.ScaleY, 3);
                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SheetRevisionEditor_SaveAddsAndUpdatesRevisionInline()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow(viewModel);
            try
            {
                window.BeginNewSheetRevisionDraftForTesting();
                window.SetSheetRevisionDraftForTesting("A", "2026-03-31", "Initial issue", "Paul");

                Assert.True(window.SaveSheetRevisionEditorForTesting());
                var revision = Assert.Single(viewModel.SelectedSheet!.RevisionEntries);
                Assert.Equal("A", revision.RevisionNumber);
                Assert.Equal("Initial issue", revision.Description);

                Assert.True(window.SelectSheetRevisionForTesting("A"));
                window.SetSheetRevisionDraftForTesting("A", "2026-04-01", "Issued for permit", "Reviewer");

                Assert.True(window.SaveSheetRevisionEditorForTesting());
                Assert.Single(viewModel.SelectedSheet.RevisionEntries);
                Assert.Equal("Issued for permit", viewModel.SelectedSheet.RevisionEntries[0].Description);
                Assert.Equal("Reviewer", viewModel.SelectedSheet.RevisionEntries[0].Author);

                var state = window.GetSheetRevisionEditorStateForTesting();
                Assert.Equal("Save Changes", state.SaveCaption);
                Assert.True(state.DeleteEnabled);
                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SheetRevisionEditor_ShowsInlineValidationForDuplicateRevisionNumber()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.AddSheetRevision(viewModel.SelectedSheet!, "Initial issue", "Paul", "A", "2026-03-31");

            var window = new MainWindow(viewModel);
            try
            {
                window.BeginNewSheetRevisionDraftForTesting();
                window.SetSheetRevisionDraftForTesting("A", "2026-04-01", "Issued for permit", "Reviewer");

                var state = window.GetSheetRevisionEditorStateForTesting();

                Assert.False(state.SaveEnabled);
                Assert.Contains("unique", state.ValidationText, StringComparison.OrdinalIgnoreCase);
                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SheetRevisionEditor_DeleteRemovesSelectedRevision()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.AddSheetRevision(viewModel.SelectedSheet!, "Initial issue", "Paul", "A", "2026-03-31");

            var window = new MainWindow(viewModel);
            try
            {
                Assert.True(window.SelectSheetRevisionForTesting("A"));
                Assert.True(window.DeleteSelectedSheetRevisionForTesting());
                Assert.Empty(viewModel.SelectedSheet!.RevisionEntries);

                var state = window.GetSheetRevisionEditorStateForTesting();
                Assert.False(state.DeleteEnabled);
                Assert.Equal("Add Revision", state.SaveCaption);
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