using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public partial class MainWindowMarkupInteractionTests
{
    [Fact]
    public void ApplySketchLineSnapForTesting_WithDraftAnchor_SnapsPerpendicularToVisibleMarkupSegment()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Components.Add(new ConduitComponent { Length = 20.0 });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.RefreshCanvasForTesting();
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(920, 900) });
                return window.ApplySketchLineSnapForTesting(new Point(1002, 899));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new Point(1000, 900), snapped);
    }

    [Fact]
    public void ApplyConduitDrawingSnapForTesting_WithDraftAnchor_SnapsToVisibleCircleTangent()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var circleMarkup = new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(50, 50) },
                Radius = 20
            };
            circleMarkup.UpdateBoundingRect();

            viewModel.Markups.Add(circleMarkup);
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToTangent = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetDrawingCanvasPointsForTesting(new[] { new Point(0, 50) });
                return window.ApplyConduitDrawingSnapForTesting(new Point(43, 67));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(42.0, snapped.X, 1);
        Assert.Equal(68.3, snapped.Y, 1);
    }

    [Fact]
    public void ApplyFreehandSnapForTesting_WithPendingAnchor_SnapsPerpendicularToVisibleMarkupSegment()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Components.Add(new ConduitComponent { Length = 20.0 });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.RefreshCanvasForTesting();
                window.SetFreehandPendingCanvasPointsForTesting(new[] { new Point(920, 900) });
                return window.ApplyFreehandSnapForTesting(new Point(1002, 899));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new Point(1000, 900), snapped);
    }
}