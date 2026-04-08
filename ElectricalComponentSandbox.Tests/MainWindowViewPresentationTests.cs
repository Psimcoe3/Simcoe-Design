using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    [Fact]
    public void BuildPrintPreviewInfoTextForTesting_IncludesPageSetupPaperSpaceAndExtents()
    {
        var infoText = MainWindow.BuildPrintPreviewInfoTextForTesting(
            new PlotLayout
            {
                Name = "Permit Set",
                PaperSize = PaperSize.ANSI_D,
                PlotScale = 24.0,
                PlotStyleTableName = "permit.ctb"
            },
            new Rect(0, 0, 144, 96),
            componentCount: 12,
            outputDpi: 300);

        Assert.Contains("Page setup: Permit Set", infoText);
        Assert.Contains("Paper space: ANSI_D", infoText);
        Assert.Contains("Model extents: 144.00 × 96.00", infoText);
        Assert.Contains("Components: 12", infoText);
        Assert.Contains("DPI: 300", infoText);
    }

    [Fact]
    public void BuildPrintPreviewWorkspaceForTesting_AddsPaperSpaceBadgeAndLayoutCaption()
    {
        RunOnSta(() =>
        {
            var workspace = MainWindow.BuildPrintPreviewWorkspaceForTesting(
                new WriteableBitmap(640, 480, 96, 96, PixelFormats.Bgra32, null),
                new PlotLayout
                {
                    Name = "Permit Set",
                    PaperSize = PaperSize.ANSI_C,
                    PlotScale = 12.0,
                    PlotStyleTableName = "permit.ctb"
                },
                new Rect(0, 0, 96, 48));

            var allText = GetDescendants<TextBlock>(workspace)
                .Select(textBlock => textBlock.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            var workspaceBorder = Assert.IsType<Border>(workspace);
            var background = Assert.IsType<SolidColorBrush>(workspaceBorder.Background);

            Assert.Equal(Color.FromRgb(229, 231, 235), background.Color);
            Assert.Contains("PAPER SPACE", allText);
            Assert.Contains(allText, text => text.Contains("Permit Set", StringComparison.Ordinal));
            Assert.Contains(allText, text => text.Contains("Model: 96.00 × 48.00", StringComparison.Ordinal));
        });
    }

    private static List<T> GetDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var results = new List<T>();
        CollectDescendants(root, results);
        return results;
    }

    private static void CollectDescendants<T>(DependencyObject root, ICollection<T> results) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                results.Add(match);

            CollectDescendants(child, results);
        }
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