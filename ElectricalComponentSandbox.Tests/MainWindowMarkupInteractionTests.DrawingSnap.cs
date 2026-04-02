using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public partial class MainWindowMarkupInteractionTests
{
    [Fact]
    public void ApplySketchLineSnapForTesting_WithDraftAnchor_SnapsPerpendicularToVisibleComponentSegment()
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
    public void ApplySketchLineSnapForTesting_WithVisibleArcMarkup_SnapsToArcEndpoint()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(920, 900) });
                return window.ApplySketchLineSnapForTesting(new Point(1002, 1098));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new Point(1000, 1100), snapped);
    }

    [Fact]
    public void ApplySketchLineSnapForTesting_WithDraftAnchor_SnapsToVisibleArcTangent()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToTangent = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(1000, 1200) });
                return window.ApplySketchLineSnapForTesting(new Point(1088, 1048));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1086.6, snapped.X, 1);
        Assert.Equal(1050.0, snapped.Y, 1);
    }

    [Fact]
    public void PreviewSketchLineSnapIndicatorForTesting_WithDraftAnchor_UsesPerpendicularIndicatorForComponentGeometry()
    {
        var outcome = RunOnSta(() =>
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
                window.PreviewSketchLineSnapIndicatorForTesting(new Point(1002, 899));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Perpendicular, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void PreviewSketchLineSnapIndicatorForTesting_WithVisibleArcMarkup_UsesEndpointIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(920, 900) });
                window.PreviewSketchLineSnapIndicatorForTesting(new Point(1002, 1098));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Endpoint, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void PreviewSketchLineSnapIndicatorForTesting_WithVisibleArcMarkup_UsesTangentIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToTangent = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(1000, 1200) });
                window.PreviewSketchLineSnapIndicatorForTesting(new Point(1088, 1048));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Tangent, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void ApplySketchLineSnapForTesting_WithDraftAnchor_SnapsPerpendicularToVisibleArcMarkup()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(800, 800) });
                return window.ApplySketchLineSnapForTesting(new Point(1074, 1072));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1070.7, snapped.X, 1);
        Assert.Equal(1070.7, snapped.Y, 1);
    }

    [Fact]
    public void PreviewSketchLineSnapIndicatorForTesting_WithVisibleArcMarkup_UsesPerpendicularIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(800, 800) });
                window.PreviewSketchLineSnapIndicatorForTesting(new Point(1074, 1072));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Perpendicular, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void PreviewSketchLineSnapIndicatorForTesting_WithVisibleArcAndPolylineGeometry_UsesIntersectionIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(1080, 900), new Point(1080, 1100) }
            });
            viewModel.SnapToGrid = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(920, 900) });
                window.PreviewSketchLineSnapIndicatorForTesting(new Point(1079, 1059));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Intersection, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void ApplySketchLineSnapForTesting_WithVisibleArcMarkup_NearArcCurve_SnapsToNearest()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToNearest = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(920, 900) });
                return window.ApplySketchLineSnapForTesting(new Point(1074, 1074));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1070.7, snapped.X, 1);
        Assert.Equal(1070.7, snapped.Y, 1);
    }

    [Fact]
    public void ApplySketchLineSnapForTesting_WithVisibleArcAndPolylineGeometry_SnapsToIntersection()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(1080, 900), new Point(1080, 1100) }
            });
            viewModel.SnapToGrid = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetSketchDraftLinePointsForTesting(new[] { new Point(920, 900) });
                return window.ApplySketchLineSnapForTesting(new Point(1079, 1059));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1080.0, snapped.X, 1);
        Assert.Equal(1060.0, snapped.Y, 1);
    }

    [Fact]
    public void PreviewConduitDrawingSnapIndicatorForTesting_WithDraftAnchor_UsesTangentIndicator()
    {
        var outcome = RunOnSta(() =>
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
                window.PreviewConduitDrawingSnapIndicatorForTesting(new Point(43, 67));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Tangent, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void ApplyConduitDrawingSnapForTesting_WithDraftAnchor_SnapsToVisibleArcTangent()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToTangent = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetDrawingCanvasPointsForTesting(new[] { new Point(1000, 1200) });
                return window.ApplyConduitDrawingSnapForTesting(new Point(1088, 1048));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1086.6, snapped.X, 1);
        Assert.Equal(1050.0, snapped.Y, 1);
    }

    [Fact]
    public void ApplyConduitDrawingSnapForTesting_WithDraftAnchor_SnapsPerpendicularToVisibleArcMarkup()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetDrawingCanvasPointsForTesting(new[] { new Point(800, 800) });
                return window.ApplyConduitDrawingSnapForTesting(new Point(1074, 1072));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1070.7, snapped.X, 1);
        Assert.Equal(1070.7, snapped.Y, 1);
    }

    [Fact]
    public void PreviewConduitDrawingSnapIndicatorForTesting_WithVisibleArcMarkup_UsesTangentIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToTangent = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetDrawingCanvasPointsForTesting(new[] { new Point(1000, 1200) });
                window.PreviewConduitDrawingSnapIndicatorForTesting(new Point(1088, 1048));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Tangent, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void PreviewConduitDrawingSnapIndicatorForTesting_WithVisibleArcMarkup_UsesPerpendicularIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetDrawingCanvasPointsForTesting(new[] { new Point(800, 800) });
                window.PreviewConduitDrawingSnapIndicatorForTesting(new Point(1074, 1072));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Perpendicular, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void ApplyFreehandSnapForTesting_WithPendingAnchor_SnapsPerpendicularToVisibleComponentSegment()
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

    [Fact]
    public void ApplyFreehandSnapForTesting_WithPendingAnchor_SnapsToVisibleArcTangent()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToTangent = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetFreehandPendingCanvasPointsForTesting(new[] { new Point(1000, 1200) });
                return window.ApplyFreehandSnapForTesting(new Point(1088, 1048));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1086.6, snapped.X, 1);
        Assert.Equal(1050.0, snapped.Y, 1);
    }

    [Fact]
    public void PreviewFreehandSnapIndicatorForTesting_WithPendingAnchor_UsesPerpendicularIndicatorForComponentGeometry()
    {
        var outcome = RunOnSta(() =>
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
                window.PreviewFreehandSnapIndicatorForTesting(new Point(1002, 899));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Perpendicular, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void PreviewFreehandSnapIndicatorForTesting_WithVisibleArcMarkup_UsesTangentIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;
            viewModel.SnapService.SnapToTangent = true;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetFreehandPendingCanvasPointsForTesting(new[] { new Point(1000, 1200) });
                window.PreviewFreehandSnapIndicatorForTesting(new Point(1088, 1048));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Tangent, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void ApplyFreehandSnapForTesting_WithPendingAnchor_SnapsPerpendicularToVisibleArcMarkup()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetFreehandPendingCanvasPointsForTesting(new[] { new Point(800, 800) });
                return window.ApplyFreehandSnapForTesting(new Point(1074, 1072));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(1070.7, snapped.X, 1);
        Assert.Equal(1070.7, snapped.Y, 1);
    }

    [Fact]
    public void PreviewFreehandSnapIndicatorForTesting_WithVisibleArcMarkup_UsesPerpendicularIndicator()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(1000, 1000) },
                Radius = 100,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;
            viewModel.SnapService.SnapToCenter = false;
            viewModel.SnapService.SnapToQuadrant = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetFreehandPendingCanvasPointsForTesting(new[] { new Point(800, 800) });
                window.PreviewFreehandSnapIndicatorForTesting(new Point(1074, 1072));
                return (window.HasSnapIndicatorForTesting, window.SnapIndicatorTypeForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.HasSnapIndicatorForTesting);
        Assert.Equal(SnapService.SnapType.Perpendicular, outcome.SnapIndicatorTypeForTesting);
    }

    [Fact]
    public void ApplySketchLineSnapForTesting_WithDraftAnchor_SnapsPerpendicularToVisiblePolylineMarkupSegment()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(1000, 800), new Point(1000, 1000) }
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;

            var window = new MainWindow(viewModel);
            try
            {
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
    public void ApplyConduitDrawingSnapForTesting_WithDraftAnchor_SnapsPerpendicularToVisiblePolylineMarkupSegment()
    {
        var snapped = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.Markups.Add(new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(1000, 800), new Point(1000, 1000) }
            });
            viewModel.SnapToGrid = false;
            viewModel.SnapService.SnapToEndpoints = false;
            viewModel.SnapService.SnapToMidpoints = false;
            viewModel.SnapService.SnapToIntersections = false;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetDrawingCanvasPointsForTesting(new[] { new Point(920, 900) });
                return window.ApplyConduitDrawingSnapForTesting(new Point(1002, 899));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new Point(1000, 900), snapped);
    }
}