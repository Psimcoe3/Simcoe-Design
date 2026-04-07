using System.Windows;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public class MainWindowViewPresentationTests
{
    [Fact]
    public void BuildPlotLayoutSummaryForTesting_IncludesLayoutNameScaleAndCtb()
    {
        var summary = MainWindow.BuildPlotLayoutSummaryForTesting(new PlotLayout
        {
            Name = "Permit Set",
            PaperSize = PaperSize.ANSI_D,
            PlotScale = 24.0,
            PlotStyleTableName = "permit.ctb"
        });

        Assert.Contains("Permit Set", summary);
        Assert.Contains("ANSI_D", summary);
        Assert.Contains("24", summary);
        Assert.Contains("permit.ctb", summary);
    }

    [Fact]
    public void SavedPageSetupCommandsForTesting_SaveApplyAndDelete()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.ActivePlotLayout = new PlotLayout
            {
                Name = "Current",
                PaperSize = PaperSize.ANSI_C,
                PlotScale = 12.0,
                PlotStyleTableName = "permit.ctb"
            };

            var window = new MainWindow(viewModel);
            try
            {
                Assert.True(window.SaveCurrentPageSetupForTesting("Permit Set"));
                Assert.Contains("Permit Set", window.GetSavedPageSetupNamesForTesting());

                viewModel.ActivePlotLayout = new PlotLayout
                {
                    Name = "Working",
                    PaperSize = PaperSize.ANSI_E,
                    PlotScale = 48.0,
                    PlotStyleTableName = "working.ctb"
                };

                Assert.True(window.ApplySavedPageSetupForTesting("Permit Set"));
                Assert.Equal(PaperSize.ANSI_C, viewModel.ActivePlotLayout?.PaperSize);
                Assert.Equal(12.0, viewModel.ActivePlotLayout?.PlotScale);
                Assert.Equal("permit.ctb", viewModel.ActivePlotLayout?.PlotStyleTableName);

                Assert.True(window.DeleteSavedPageSetupForTesting("Permit Set"));
                Assert.Empty(window.GetSavedPageSetupNamesForTesting());
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        lock (WpfStaTestSynchronization.MainWindowLock)
        {
            Exception? exception = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
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
        }
    }
}