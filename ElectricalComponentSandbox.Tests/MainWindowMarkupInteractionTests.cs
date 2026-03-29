using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public class MainWindowMarkupInteractionTests
{
    private static T RunOnSta<T>(Func<T> action)
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

    [Fact]
    public void DirectVertexDragForTesting_LineStyleDimension_UsesPolarSnapAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel
            {
                SnapToGrid = false,
                IsPolarActive = true,
                PolarIncrementDeg = 30
            };
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(30, 20) },
                Metadata = new MarkupMetadata { Subject = "Diameter" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 6));
                window.FinishMarkupVertexDragForTesting();
                var editedState = (markup.Vertices[1], markup.Vertices[2]);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Vertices[2]);
                return (began, editedState, undoneState);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.began);
        Assert.Equal(16.431676725154983, outcome.editedState.Item1.X, 3);
        Assert.Equal(9.486832980505136, outcome.editedState.Item1.Y, 3);
        Assert.NotEqual(new Point(30, 20), outcome.editedState.Item2);
        Assert.Equal(new Point(10, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(30, 20), outcome.undoneState.Item2);
    }

    [Fact]
    public void BeginSelectedMarkupVertexInsertionForTesting_InsertableSelection_EntersPendingMode()
    {
        var pending = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                return window.BeginSelectedMarkupVertexInsertionForTesting() &&
                       window.IsPendingMarkupVertexInsertionForTesting;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(pending);
    }

    [Fact]
    public void BeginSelectedMarkupVertexInsertionForTesting_NonInsertableSelection_DoesNotEnterPendingMode()
    {
        var pending = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                return began || window.IsPendingMarkupVertexInsertionForTesting;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(pending);
    }

    [Fact]
    public void HandlePendingMarkupVertexInsertionClickForTesting_SegmentHit_InsertsVertexAndClearsPendingMode()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(30, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var inserted = window.HandlePendingMarkupVertexInsertionClickForTesting(new Point(15, 0));
                return (began, inserted, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.began);
        Assert.True(outcome.inserted);
        Assert.False(outcome.Item3);
        Assert.Equal(3, outcome.Count);
        Assert.Equal(1, outcome.ActiveMarkupVertexIndexForTesting);
        Assert.Equal(new Point(15, 0), outcome.Item6);
    }

    [Fact]
    public void HandlePendingMarkupVertexInsertionClickForTesting_MissAndCancel_KeepThenClearPendingMode()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var inserted = window.HandlePendingMarkupVertexInsertionClickForTesting(new Point(200, 200));
                var pendingAfterMiss = window.IsPendingMarkupVertexInsertionForTesting;
                window.CancelPendingMarkupVertexInsertionForTesting();
                return (began, inserted, pendingAfterMiss, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.began);
        Assert.False(outcome.inserted);
        Assert.True(outcome.pendingAfterMiss);
        Assert.False(outcome.Item4);
        Assert.Equal(2, outcome.Count);
    }

    [Fact]
    public void SelectMarkupOnCanvasForTesting_NewSelection_ClearsPendingInsertion()
    {
        var cleared = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var first = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            };
            var second = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(20, 0), new Point(30, 0) }
            };
            first.UpdateBoundingRect();
            second.UpdateBoundingRect();
            viewModel.Markups.Add(first);
            viewModel.Markups.Add(second);
            viewModel.MarkupTool.SelectedMarkup = first;

            var window = new MainWindow(viewModel);
            try
            {
                window.BeginSelectedMarkupVertexInsertionForTesting();
                window.SelectMarkupOnCanvasForTesting(second);
                return !window.IsPendingMarkupVertexInsertionForTesting && ReferenceEquals(viewModel.MarkupTool.SelectedMarkup, second);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(cleared);
    }

    [Fact]
    public void ExecuteInsertVertexCommandForTesting_InsertableSelection_EntersPendingMode()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var enteredPendingMode = window.ExecuteInsertVertexCommandForTesting();
                return (enteredPendingMode, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.enteredPendingMode);
        Assert.True(outcome.Item2);
        Assert.Equal(3, outcome.Item3);
        Assert.Equal(-1, outcome.Item4);
    }

    [Fact]
    public void ExecuteEditMarkupAppearanceCommandForTesting_TextMarkup_UpdatesAppearanceAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Text,
                TextContent = "NOTE",
                BoundingRect = new Rect(10, 20, 30, 12),
                Appearance = new MarkupAppearance
                {
                    StrokeColor = "#FF0000",
                    StrokeWidth = 2,
                    FillColor = "#20FF0000",
                    Opacity = 1,
                    FontFamily = "Arial",
                    FontSize = 10
                }
            };
            markup.Vertices.Add(new Point(10, 32));
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var edited = window.ExecuteEditMarkupAppearanceCommandForTesting("stroke=#112233\nwidth=3.5\nfill=none\nopacity=60\nfont=Consolas\nfontsize=14");
                var editedState = (
                    markup.Appearance.StrokeColor,
                    markup.Appearance.StrokeWidth,
                    markup.Appearance.FillColor,
                    markup.Appearance.Opacity,
                    markup.Appearance.FontFamily,
                    markup.Appearance.FontSize,
                    markup.BoundingRect);

                viewModel.Undo();
                var undoneState = (
                    markup.Appearance.StrokeColor,
                    markup.Appearance.StrokeWidth,
                    markup.Appearance.FillColor,
                    markup.Appearance.Opacity,
                    markup.Appearance.FontFamily,
                    markup.Appearance.FontSize,
                    markup.BoundingRect);

                return (edited, editedState, undoneState);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.edited);
        Assert.Equal("#FF112233", outcome.editedState.Item1);
        Assert.Equal(3.5, outcome.editedState.Item2, 3);
        Assert.Equal("#00000000", outcome.editedState.Item3);
        Assert.Equal(0.6, outcome.editedState.Item4, 3);
        Assert.Equal("Consolas", outcome.editedState.Item5);
        Assert.Equal(14, outcome.editedState.Item6, 3);
        Assert.NotEqual(new Rect(10, 20, 30, 12), outcome.editedState.Item7);

        Assert.Equal("#FF0000", outcome.undoneState.Item1);
        Assert.Equal(2, outcome.undoneState.Item2, 3);
        Assert.Equal("#20FF0000", outcome.undoneState.Item3);
        Assert.Equal(1, outcome.undoneState.Item4, 3);
        Assert.Equal("Arial", outcome.undoneState.Item5);
        Assert.Equal(10, outcome.undoneState.Item6, 3);
        Assert.Equal(new Rect(10, 20, 30, 12), outcome.undoneState.Item7);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_AngularDimension_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 12), new Point(10, 10) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var edited = window.ExecuteEditMarkupGeometryCommandForTesting("angle=60\nradius=14");
                var editedState = (
                    markup.Vertices[2],
                    markup.Vertices[3],
                    markup.Radius,
                    markup.ArcStartDeg,
                    markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (
                    markup.Vertices[2],
                    markup.Vertices[3],
                    markup.Radius,
                    markup.ArcStartDeg,
                    markup.ArcSweepDeg);

                return (edited, editedState, undoneState);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.edited);
        Assert.Equal(6, outcome.editedState.Item1.X, 6);
        Assert.Equal(10.392304845413264, outcome.editedState.Item1.Y, 6);
        Assert.Equal(17.443601136622522, outcome.editedState.Item2.X, 6);
        Assert.Equal(10.071067811865476, outcome.editedState.Item2.Y, 6);
        Assert.Equal(14, outcome.editedState.Item3, 6);
        Assert.Equal(0, outcome.editedState.Item4, 6);
        Assert.Equal(60, outcome.editedState.Item5, 6);

        Assert.Equal(new Point(0, 12), outcome.undoneState.Item1);
        Assert.Equal(new Point(10, 10), outcome.undoneState.Item2);
        Assert.Equal(8, outcome.undoneState.Item3, 6);
        Assert.Equal(0, outcome.undoneState.Item4, 6);
        Assert.Equal(90, outcome.undoneState.Item5, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_AngularDimension_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 10), new Point(8, 8) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(10, 10));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[2], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[2], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.began);
        Assert.Equal(7.0710678118654755, outcome.editedState.Item1.X, 6);
        Assert.Equal(7.0710678118654755, outcome.editedState.Item1.Y, 6);
        Assert.Equal(45, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_ArcLengthDimension_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var edited = window.ExecuteEditMarkupGeometryCommandForTesting("arclength=6.283185307179586\nradius=12");
                var editedState = (
                    markup.Vertices[0],
                    markup.Vertices[1],
                    markup.Vertices[2],
                    markup.Radius,
                    markup.ArcStartDeg,
                    markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (
                    markup.Vertices[0],
                    markup.Vertices[1],
                    markup.Vertices[2],
                    markup.Radius,
                    markup.ArcStartDeg,
                    markup.ArcSweepDeg);

                return (edited, editedState, undoneState);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.edited);
        Assert.Equal(new Point(12, 0), outcome.editedState.Item1);
        Assert.Equal(10.392304845413264, outcome.editedState.Item2.X, 6);
        Assert.Equal(6, outcome.editedState.Item2.Y, 6);
        Assert.Equal(11.59110991546882, outcome.editedState.Item3.X, 6);
        Assert.Equal(3.105828541230249, outcome.editedState.Item3.Y, 6);
        Assert.Equal(12, outcome.editedState.Item4, 6);
        Assert.Equal(0, outcome.editedState.Item5, 6);
        Assert.Equal(30, outcome.editedState.Item6, 6);

        Assert.Equal(new Point(10, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item2);
        Assert.Equal(new Point(7.0710678118654755, 7.0710678118654755), outcome.undoneState.Item3);
        Assert.Equal(10, outcome.undoneState.Item4, 6);
        Assert.Equal(0, outcome.undoneState.Item5, 6);
        Assert.Equal(90, outcome.undoneState.Item6, 6);
    }

    [Fact]
    public void DirectRadiusDragForTesting_ArcLengthDimension_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(8.48528137423857, 8.48528137423857));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Vertices[1], markup.Radius, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Radius, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.began);
        Assert.Equal(3.105828541230249, outcome.editedState.Item1.X, 6);
        Assert.Equal(11.59110991546882, outcome.editedState.Item1.Y, 6);
        Assert.Equal(12, outcome.editedState.Item2, 6);
        Assert.Equal(75, outcome.editedState.Item3, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(10, outcome.undoneState.Item2, 6);
        Assert.Equal(90, outcome.undoneState.Item3, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_ArcLengthDimension_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[1], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.began);
        Assert.Equal(-10, outcome.editedState.Item1.X, 6);
        Assert.Equal(0, outcome.editedState.Item1.Y, 6);
        Assert.Equal(180, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_LineStyleVertexDrag_RendersGeometryReadout()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel { SnapToGrid = false };
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(30, 20) },
                Metadata = new MarkupMetadata { Subject = "Diameter" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var renderer = new OverlayRecordingRenderer();
            var window = new MainWindow(viewModel);
            try
            {
                window.BeginSelectedMarkupVertexDragForTesting(new Point(12, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupVertexDragForTesting();
                return renderer.LastTextBoxText;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("Diameter 18  Angle 0 deg", outcome);
    }

    [Fact]
    public void DrawSelectedMarkupOverlayForTesting_ArcLengthAngleDrag_RendersArcLengthReadout()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(10, 0), new Point(0, 10), new Point(7.0710678118654755, 7.0710678118654755) },
                Radius = 10,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "ArcLength" }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var renderer = new OverlayRecordingRenderer();
            var window = new MainWindow(viewModel);
            try
            {
                window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.DrawSelectedMarkupOverlayForTesting(renderer);
                window.FinishMarkupArcAngleDragForTesting();
                return renderer.LastTextBoxText;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("Arc Length 31.42  Sweep 180 deg  Radius 10", outcome);
    }

    private sealed class OverlayRecordingRenderer : ICanvas2DRenderer
    {
        public string LastTextBoxText { get; private set; } = string.Empty;

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
        public void DrawRect(Rect rect, RenderStyle style) { }
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
        public void DrawGrip(Point docPos, bool hot = false) { }
        public void DrawRevisionCloud(IReadOnlyList<Point> points, RenderStyle style, double arcRadius = 0.5) { }
        public void DrawLeader(IReadOnlyList<Point> points, string? calloutText, RenderStyle style) { }
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_PendingInsertMode_CancelsPendingInsertion()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                return (began, cancelled, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, ReferenceEquals(viewModel.MarkupTool.SelectedMarkup, markup));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.began);
        Assert.True(outcome.cancelled);
        Assert.False(outcome.Item3);
        Assert.Equal(2, outcome.Count);
        Assert.True(outcome.Item5);
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_NoActiveInteraction_ReturnsFalse()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                return (cancelled, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, ReferenceEquals(viewModel.MarkupTool.SelectedMarkup, markup));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(outcome.cancelled);
        Assert.False(outcome.Item2);
        Assert.Equal(2, outcome.Count);
        Assert.True(outcome.Item4);
    }

    [Fact]
    public void ExecuteDeleteVertexCommandForTesting_WithActiveGrip_DeletesVertex()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                var deleted = window.ExecuteDeleteVertexCommandForTesting();
                return (deleted, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.deleted);
        Assert.Equal(2, outcome.Item2);
        Assert.Equal(1, outcome.Item3);
        Assert.Equal(new Point(20, 0), outcome.Item4);
    }

    [Fact]
    public void TryDeleteSelectedMarkupVertexForTesting_WithActiveGrip_DeletesVertexAndClampsSelection()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                var deleted = window.TryDeleteSelectedMarkupVertexForTesting();
                return (deleted, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.deleted);
        Assert.Equal(2, outcome.Count);
        Assert.Equal(1, outcome.ActiveMarkupVertexIndexForTesting);
        Assert.Equal(new Point(20, 0), outcome.Item4);
    }

    [Fact]
    public void TryDeleteSelectedMarkupVertexForTesting_WithoutActiveGrip_ReturnsFalse()
    {
        var deleted = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                return window.TryDeleteSelectedMarkupVertexForTesting();
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(deleted);
    }

    [Fact]
    public void TryDeleteSelectedMarkupVertexForTesting_MinimumVertexSelection_ReturnsFalse()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                return (window.TryDeleteSelectedMarkupVertexForTesting(), markup.Vertices.Count);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(outcome.Item1);
        Assert.Equal(2, outcome.Count);
    }

    [Fact]
    public void TryDeleteSelectedMarkupVertexForTesting_GroupedSelection_ReturnsFalse()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            const string groupId = "group-1";
            var selected = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            var peer = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(30, 0), new Point(40, 0), new Point(50, 0) }
            };
            selected.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = groupId;
            peer.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = groupId;
            selected.UpdateBoundingRect();
            peer.UpdateBoundingRect();
            viewModel.Markups.Add(selected);
            viewModel.Markups.Add(peer);
            viewModel.MarkupTool.SelectedMarkup = selected;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                return (window.TryDeleteSelectedMarkupVertexForTesting(), selected.Vertices.Count, peer.Vertices.Count);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(outcome.Item1);
        Assert.Equal(3, outcome.Item2);
        Assert.Equal(3, outcome.Item3);
    }

    [Fact]
    public void ExecuteDeleteShortcutForTesting_WithActiveGrip_PrefersVertexDeletionOverMarkupDeletion()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                var usedVertexDelete = window.ExecuteDeleteShortcutForTesting();
                return (usedVertexDelete, viewModel.Markups.Count, markup.Vertices.Count, ReferenceEquals(viewModel.MarkupTool.SelectedMarkup, markup));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(outcome.usedVertexDelete);
        Assert.Equal(1, outcome.Item2);
        Assert.Equal(2, outcome.Item3);
        Assert.True(outcome.Item4);
    }

    [Fact]
    public void ExecuteDeleteShortcutForTesting_WithoutActiveGrip_FallsBackToMarkupDeletion()
    {
        var outcome = RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var window = new MainWindow(viewModel);
            try
            {
                var usedVertexDelete = window.ExecuteDeleteShortcutForTesting();
                return (usedVertexDelete, viewModel.Markups.Count, viewModel.MarkupTool.SelectedMarkup is null);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(outcome.usedVertexDelete);
        Assert.Equal(0, outcome.Count);
        Assert.True(outcome.Item3);
    }
}