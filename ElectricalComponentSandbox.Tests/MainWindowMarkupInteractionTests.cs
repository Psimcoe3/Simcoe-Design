using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public partial class MainWindowMarkupInteractionTests
{
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

    private static T RunWithSelectedMarkupWindow<T>(
        MarkupRecord markup,
        Func<MainWindow, MainViewModel, MarkupRecord, T> action,
        Action<MainViewModel>? configureViewModel = null)
    {
        return RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            configureViewModel?.Invoke(viewModel);

            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                return action(window, viewModel, markup);
            }
            finally
            {
                window.Close();
            }
        });
    }

        private static MarkupRecord CreateGroupedRectangle(Rect bounds, string? groupId)
        {
            var markup = new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                BoundingRect = bounds,
                Appearance = new MarkupAppearance
                {
                    StrokeWidth = 1.0,
                    FontSize = 12.0
                }
            };
            markup.Vertices.Add(bounds.TopLeft);
            markup.Vertices.Add(bounds.BottomRight);

            if (!string.IsNullOrWhiteSpace(groupId))
                markup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = groupId;

            return markup;
        }

    [Fact]
    public void GetMarkupHandleOverlayMode_PrefersDirectGeometryOverVerticesAndResize()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: true,
            canEditVertices: true,
            canResize: true);

        Assert.Equal(MarkupHandleOverlayMode.DirectGeometry, mode);
    }

    [Fact]
    public void GetMarkupHandleOverlayMode_PrefersVerticesOverResizeWhenNoDirectGeometry()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: false,
            canEditVertices: true,
            canResize: true);

        Assert.Equal(MarkupHandleOverlayMode.Vertices, mode);
    }

    [Fact]
    public void GetMarkupHandleOverlayMode_UsesResizeWhenItIsTheOnlyAvailableMode()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: false,
            canEditVertices: false,
            canResize: true);

        Assert.Equal(MarkupHandleOverlayMode.Resize, mode);
    }

    [Fact]
    public void GetMarkupHandleOverlayMode_ReturnsNoneWhenNoHandlesAreAvailable()
    {
        var mode = MainWindow.GetMarkupHandleOverlayMode(
            canEditArcAngles: false,
            canEditRadius: false,
            canEditVertices: false,
            canResize: false);

        Assert.Equal(MarkupHandleOverlayMode.None, mode);
    }

    [Fact]
    public void IsLineGeometryReadoutEligibleForTesting_RequiresEndpointDragOnLineStyleGeometry()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };

        Assert.True(MainWindow.IsLineGeometryReadoutEligibleForTesting(markup, activeVertexIndex: 1));
        Assert.False(MainWindow.IsLineGeometryReadoutEligibleForTesting(markup, activeVertexIndex: 2));
    }

    [Fact]
    public void BuildLineGeometryReadoutForTesting_UsesSemanticLengthLabel()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(-6, 0), new Point(6, 0), new Point(9, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };

        Assert.Equal("Diameter 12  Angle 0 deg", MainWindow.BuildLineGeometryReadoutForTesting(markup));
    }
}
