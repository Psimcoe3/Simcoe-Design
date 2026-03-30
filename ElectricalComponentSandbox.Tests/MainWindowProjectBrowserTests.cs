using System;
using System.Linq;
using System.Windows;
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