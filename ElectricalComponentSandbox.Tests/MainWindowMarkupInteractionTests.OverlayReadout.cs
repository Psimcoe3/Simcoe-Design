using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public partial class MainWindowMarkupInteractionTests
{
    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_RotationDrag_MarksRotationGripHot()
    {
        var hotGripPoints = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, _, markup) =>
            {
                markup.RotationDegrees = 90;
                var renderer = new OverlayRecordingRenderer();
                var handle = window.GetSelectedMarkupRotationHandlePointForTesting();

                var began = window.BeginSelectedMarkupRotationDragForTesting(handle);
                window.DrawSelectedMarkupOverlayForTesting(renderer);

                Assert.True(began);
                return renderer.HotGripPoints.ToList();
            });

        var hotGrip = Assert.Single(hotGripPoints);
        Assert.Equal(34, hotGrip.X, 6);
        Assert.Equal(5, hotGrip.Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_RotationDrag_ShowsRotationReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, viewModel, _) =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 15;
                var renderer = new OverlayRecordingRenderer();
                var handle = window.GetSelectedMarkupRotationHandlePointForTesting();

                var began = window.BeginSelectedMarkupRotationDragForTesting(handle);
                window.UpdateMarkupRotationPreviewForTesting(new Point(34, 5));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRotationDragForTesting();

                return (began, renderer.LastTextBoxText);
            });

        Assert.True(outcome.began);
        Assert.Equal("Rotation 90 deg  Snap 15 deg", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ResizeDrag_MarksActiveResizeGripHot()
    {
        var hotGripPoints = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();

                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(10, 10));
                window.DrawSelectedMarkupOverlayForTesting(renderer);

                Assert.True(began);
                return renderer.HotGripPoints.ToList();
            });

        var hotGrip = Assert.Single(hotGripPoints);
        Assert.Equal(10, hotGrip.X, 6);
        Assert.Equal(10, hotGrip.Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_TopResizeDrag_MarksActiveResizeGripHot()
    {
        var hotGripPoints = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();

                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(5, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);

                Assert.True(began);
                return renderer.HotGripPoints.ToList();
            });

        var hotGrip = Assert.Single(hotGripPoints);
        Assert.Equal(5, hotGrip.X, 6);
        Assert.Equal(0, hotGrip.Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ResizeDrag_ShowsWidthAndHeightReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, viewModel, _) =>
            {
                viewModel.SnapToGrid = false;
                var renderer = new OverlayRecordingRenderer();

                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(10, 10));
                window.UpdateMarkupResizePreviewForTesting(new Point(15, 20));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupResizeDragForTesting();

                return (began, renderer.LastTextBoxText);
            });

        Assert.True(outcome.began);
        Assert.Equal("Width 15  Height 20", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_TopResizeDrag_ShowsUpdatedHeightReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, viewModel, _) =>
            {
                viewModel.SnapToGrid = false;
                var renderer = new OverlayRecordingRenderer();

                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(5, 0));
                window.UpdateMarkupResizePreviewForTesting(new Point(5, -10));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupResizeDragForTesting();

                return (began, renderer.LastTextBoxText);
            });

        Assert.True(outcome.began);
        Assert.Equal("Width 10  Height 20", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ResizeDrag_WithAspectConstraint_ShowsConstraintHint()
    {
        var outcome = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, viewModel, _) =>
            {
                viewModel.SnapToGrid = false;
                window.SetMarkupResizeConstraintOverridesForTesting(preserveAspectRatio: true);
                var renderer = new OverlayRecordingRenderer();

                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(10, 10));
                window.UpdateMarkupResizePreviewForTesting(new Point(15, 20));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupResizeDragForTesting();

                return (began, renderer.LastTextBoxText);
            });

        Assert.True(outcome.began);
        Assert.Equal("Width 20  Height 20  Aspect locked", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ResizeDrag_WithCenterConstraint_ShowsConstraintHint()
    {
        var outcome = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, viewModel, _) =>
            {
                viewModel.SnapToGrid = false;
                window.SetMarkupResizeConstraintOverridesForTesting(resizeFromCenter: true);
                var renderer = new OverlayRecordingRenderer();

                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(10, 10));
                window.UpdateMarkupResizePreviewForTesting(new Point(13, 6));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupResizeDragForTesting();

                return (began, renderer.LastTextBoxText);
            });

        Assert.True(outcome.began);
        Assert.Equal("Width 16  Height 12  Centered", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_WithoutSelectedMarkup_RendersNothing()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow(viewModel);
            try
            {
                var renderer = new OverlayRecordingRenderer();
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                return (rectCount: renderer.DrawnRects.Count, gripCount: renderer.HotGripPoints.Count, renderer.LastTextBoxText);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(0, outcome.rectCount);
        Assert.Equal(0, outcome.gripCount);
        Assert.Equal(string.Empty, outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_EmptyBounds_RendersNothing()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.ConduitRun,
                BoundingRect = Rect.Empty
            };
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var renderer = new OverlayRecordingRenderer();
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                return (rectCount: renderer.DrawnRects.Count, gripCount: renderer.HotGripPoints.Count, renderer.LastTextBoxText);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(0, outcome.rectCount);
        Assert.Equal(0, outcome.gripCount);
        Assert.Equal(string.Empty, outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_NoHandleMode_DrawsHighlightOnly()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.ConduitRun,
                BoundingRect = new Rect(0, 0, 10, 10)
            };
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var renderer = new OverlayRecordingRenderer();
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                return (rects: renderer.DrawnRects.ToArray(), gripCount: renderer.HotGripPoints.Count, renderer.LastTextBoxText);
            }
            finally
            {
                window.Close();
            }
        });

        var rect = Assert.Single(outcome.rects);
        Assert.Equal(new Rect(0, 0, 10, 10), rect);
        Assert.Equal(0, outcome.gripCount);
        Assert.Equal(string.Empty, outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_PathVertexDrag_MarksHotGripWithoutReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(30, 0), new Point(30, 30) }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(30, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupVertexDragForTesting();
                return (began, grips: renderer.HotGripPoints.ToArray(), renderer.LastTextBoxText);
            });

        Assert.True(outcome.began);
        var hotGrip = Assert.Single(outcome.grips);
        Assert.Equal(30, hotGrip.X, 6);
        Assert.Equal(0, hotGrip.Y, 6);
        Assert.Equal(string.Empty, outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_LineStyleVertexDrag_RendersGeometryReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(30, 20) },
                Metadata = new MarkupMetadata { Subject = "Diameter" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupVertexDragForTesting(new Point(12, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupVertexDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.Equal("Diameter 18  Angle 0 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_DiameterMeasurementVertexDrag_RendersGeometryReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(30, 20) },
                Metadata = new MarkupMetadata { Subject = "Diameter" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(12, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupVertexDragForTesting();
                return (began, renderer.LastTextBoxText);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal("Diameter 18  Angle 0 deg", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_RadialDimensionVertexDrag_RendersGeometryReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
                Metadata = new MarkupMetadata { Subject = "Radial" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, -5));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupVertexDragForTesting();
                return (began, renderer.LastTextBoxText);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal("Radius 18  Angle 0 deg", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_RadialMeasurementVertexDrag_RendersGeometryReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
                Metadata = new MarkupMetadata { Subject = "Radial" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, -5));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupVertexDragForTesting();
                return (began, renderer.LastTextBoxText);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal("Radius 18  Angle 0 deg", outcome.LastTextBoxText);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_CircleRadiusDrag_RendersRadiusReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(0, 0) },
                Radius = 10
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupRadiusDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.Equal("Radius 28.28", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcStartAngleDrag_RendersSnapReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(0, 0) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(8, 6));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.Equal("Start 30 deg  End 90 deg  Sweep 60 deg  Snap 30 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcRadiusDrag_RendersRadiusReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(0, 0) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.Equal("Radius 28.28", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcEndAngleDrag_RendersSnapReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(0, 0) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.Equal("Start 0 deg  End 60 deg  Sweep 60 deg  Snap 30 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcEndAngleDrag_UsesGridSnapInReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(0, 0) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(11, 29));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
                viewModel.IsPolarActive = false;
            });

        Assert.Equal("Start 0 deg  End 45 deg  Sweep 45 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthAngleDrag_RendersArcLengthReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.Equal("Arc Length 31.42  Sweep 180 deg  Radius 10", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthMeasurementAngleDrag_RendersArcLengthReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.Equal("Arc Length 31.42  Sweep 180 deg  Radius 10", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularAngleDrag_RendersSnapReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.Equal("Angle 60 deg  Radius 8  Snap 30 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularMeasurementAngleDrag_RendersSnapReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.Equal("Angle 60 deg  Radius 8  Snap 30 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthAngleDrag_RendersSnapReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.Equal("Arc Length 10.47  Sweep 60 deg  Radius 10  Snap 30 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthMeasurementAngleDrag_RendersSnapReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.Equal("Arc Length 10.47  Sweep 60 deg  Radius 10  Snap 30 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularRadiusDrag_RendersRadiusReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupRadiusDragForTesting(new Point(5.656854249492381, 5.65685424949238));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.Equal("Radius 28.28", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularMeasurementRadiusDrag_RendersRadiusReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupRadiusDragForTesting(new Point(5.656854249492381, 5.65685424949238));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.Equal("Radius 28.28", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthRadiusDrag_RendersRadiusReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.Equal("Radius 28.28", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthMeasurementRadiusDrag_RendersRadiusReadout()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return renderer.LastTextBoxText;
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.Equal("Radius 28.28", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularRadiusDrag_MarksRadiusGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(5.656854249492381, 5.65685424949238));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(5.656854249492381, outcome.Item2[0].X, 6);
        Assert.Equal(5.65685424949238, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularMeasurementRadiusDrag_MarksRadiusGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(5.656854249492381, 5.65685424949238));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(5.656854249492381, outcome.Item2[0].X, 6);
        Assert.Equal(5.65685424949238, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularAngleDrag_MarksAngleGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(0, outcome.Item2[0].X, 6);
        Assert.Equal(10, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_AngularMeasurementAngleDrag_MarksAngleGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(0, outcome.Item2[0].X, 6);
        Assert.Equal(10, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_CircleRadiusDrag_MarksRadiusGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(0, 0) },
                Radius = 10
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(10, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(10, outcome.Item2[0].X, 6);
        Assert.Equal(0, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcStartAngleDrag_MarksStartGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(0, 0) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(10, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Contains(outcome.Item2, point => Math.Abs(point.X - 10) < 1e-6 && Math.Abs(point.Y) < 1e-6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcRadiusDrag_MarksRadiusGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(0, 0) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(7.0710678118654755, outcome.Item2[0].X, 6);
        Assert.Equal(7.0710678118654755, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcEndAngleDrag_MarksEndGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Arc,
                Vertices = { new Point(0, 0) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(0, outcome.Item2[0].X, 6);
        Assert.Equal(10, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthRadiusDrag_MarksRadiusGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(7.0710678118654755, outcome.Item2[0].X, 6);
        Assert.Equal(7.0710678118654755, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthMeasurementRadiusDrag_MarksRadiusGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupRadiusDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(7.0710678118654755, outcome.Item2[0].X, 6);
        Assert.Equal(7.0710678118654755, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthAngleDrag_MarksAngleGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(0, outcome.Item2[0].X, 6);
        Assert.Equal(10, outcome.Item2[0].Y, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthMeasurementAngleDrag_MarksAngleGripHot()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            },
            (window, _, _) =>
            {
                var renderer = new OverlayRecordingRenderer();
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return (began, renderer.HotGripPoints.ToArray());
            });

        Assert.True(outcome.began);
        Assert.Single(outcome.Item2);
        Assert.Equal(0, outcome.Item2[0].X, 6);
        Assert.Equal(10, outcome.Item2[0].Y, 6);
    }

    private sealed class OverlayRecordingRenderer : ICanvas2DRenderer
    {
        public string LastTextBoxText { get; private set; } = string.Empty;
        public List<Point> HotGripPoints { get; } = new();
        public List<Rect> DrawnRects { get; } = new();

        public Rect ViewportDocRect => Rect.Empty;
        public double Zoom => 1.0;

        public void Clear(string backgroundColor = "#FF1E1E1E") { }
        public void RequestRedraw() { }
        public void InvalidateRegion(Rect docRect) { }
        public void PushTransform(double translateX, double translateY, double rotateDeg = 0, double scale = 1.0) { }
        public void PopTransform() { }
        public void DrawLine(Point start, Point end, RenderStyle style) { }
        public void DrawPolyline(IReadOnlyList<Point> points, RenderStyle style) { }
        public void DrawPolygon(IReadOnlyList<Point> points, RenderStyle style) { }
        public void DrawRect(Rect rect, RenderStyle style) => DrawnRects.Add(rect);
        public void DrawEllipse(Point center, double radiusX, double radiusY, RenderStyle style) { }
        public void DrawArc(Point center, double radiusX, double radiusY, double startAngleDeg, double sweepAngleDeg, RenderStyle style) { }
        public void DrawBezier(IReadOnlyList<Point> controlPoints, RenderStyle style) { }
        public void DrawHatch(IReadOnlyList<Point> boundary, HatchPattern pattern, RenderStyle style) { }
        public void DrawText(Point anchor, string text, RenderStyle style, TextAlign align = TextAlign.Left) { }
        public void DrawTextBox(Point anchor, string text, RenderStyle textStyle, string boxFill = "#CCFFFFFF", double padding = 3.0)
            => LastTextBoxText = text;
        public void DrawDimension(Point p1, Point p2, double offset, string valueText, DimensionStyle dimStyle) { }
        public void DrawSnapGlyph(Point docPos, SnapGlyphType glyphType) { }
        public void DrawTrackingLine(Point docOrigin, double angleDeg) { }
        public void DrawSelectionRect(Rect docRect, bool crossing) { }
        public void DrawGrip(Point docPos, bool hot = false)
        {
            if (hot)
                HotGripPoints.Add(docPos);
        }
        public void DrawRevisionCloud(IReadOnlyList<Point> points, RenderStyle style, double arcRadius = 0.5) { }
        public void DrawLeader(IReadOnlyList<Point> points, string? calloutText, RenderStyle style) { }
    }

}
