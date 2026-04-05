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
    public void ResizeDragForTesting_GroupedRectangles_ResizesGroupAndSupportsUndo()
    {
        var selectedMarkup = CreateGroupedRectangle(new Rect(0, 0, 10, 10), "group-1");
        var peerMarkup = CreateGroupedRectangle(new Rect(20, 20, 10, 10), "group-1");

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(30, 30));
                window.UpdateMarkupResizePreviewForTesting(new Point(40, 50));
                window.FinishMarkupResizeDragForTesting();

                var editedPrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var editedPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);

                viewModel.Undo();

                var undonePrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var undonePeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);
                return (began, editedPrimary, editedPeer, undonePrimary, undonePeer);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(0, 0), outcome.editedPrimary.Item1);
        Assert.Equal(13.333333333333334, outcome.editedPrimary.Item2.X, 6);
        Assert.Equal(16.666666666666668, outcome.editedPrimary.Item2.Y, 6);
        Assert.Equal(26.666666666666668, outcome.editedPeer.Item1.X, 6);
        Assert.Equal(33.333333333333336, outcome.editedPeer.Item1.Y, 6);
        Assert.Equal(40, outcome.editedPeer.Item2.X, 6);
        Assert.Equal(50, outcome.editedPeer.Item2.Y, 6);
        Assert.True(outcome.editedPrimary.Item3 > 1.0);
        Assert.True(outcome.editedPeer.Item3 > 1.0);

        Assert.Equal(new Point(0, 0), outcome.undonePrimary.Item1);
        Assert.Equal(new Point(10, 10), outcome.undonePrimary.Item2);
        Assert.Equal(new Point(20, 20), outcome.undonePeer.Item1);
        Assert.Equal(new Point(30, 30), outcome.undonePeer.Item2);
        Assert.Equal(1.0, outcome.undonePrimary.Item3, 6);
        Assert.Equal(1.0, outcome.undonePeer.Item3, 6);
    }

    [Fact]
    public void ResizeDragForTesting_GroupedRectangles_FromTopHandle_ResizesHeightOnlyAndSupportsUndo()
    {
        var selectedMarkup = CreateGroupedRectangle(new Rect(0, 0, 10, 10), "group-1");
        var peerMarkup = CreateGroupedRectangle(new Rect(20, 20, 10, 10), "group-1");

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(15, 0));
                window.UpdateMarkupResizePreviewForTesting(new Point(15, -10));
                window.FinishMarkupResizeDragForTesting();

                var editedPrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var editedPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);

                viewModel.Undo();

                var undonePrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var undonePeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);
                return (began, editedPrimary, editedPeer, undonePrimary, undonePeer);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(0, -10), outcome.editedPrimary.Item1);
        Assert.Equal(10, outcome.editedPrimary.Item2.X, 6);
        Assert.Equal(3.333333333333334, outcome.editedPrimary.Item2.Y, 6);
        Assert.Equal(20, outcome.editedPeer.Item1.X, 6);
        Assert.Equal(16.666666666666668, outcome.editedPeer.Item1.Y, 6);
        Assert.Equal(30, outcome.editedPeer.Item2.X, 6);
        Assert.Equal(30, outcome.editedPeer.Item2.Y, 6);
        Assert.Equal(1.0, outcome.editedPrimary.Item3, 6);
        Assert.Equal(1.0, outcome.editedPeer.Item3, 6);

        Assert.Equal(new Point(0, 0), outcome.undonePrimary.Item1);
        Assert.Equal(new Point(10, 10), outcome.undonePrimary.Item2);
        Assert.Equal(new Point(20, 20), outcome.undonePeer.Item1);
        Assert.Equal(new Point(30, 30), outcome.undonePeer.Item2);
        Assert.Equal(1.0, outcome.undonePrimary.Item3, 6);
        Assert.Equal(1.0, outcome.undonePeer.Item3, 6);
    }

    [Fact]
    public void ResizeDragForTesting_GroupedRectangles_UsesGridSnapAndSupportsUndo()
    {
        var selectedMarkup = CreateGroupedRectangle(new Rect(0, 0, 10, 10), "group-1");
        var peerMarkup = CreateGroupedRectangle(new Rect(20, 20, 10, 10), "group-1");

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(30, 30));
                window.UpdateMarkupResizePreviewForTesting(new Point(31, 49));
                window.FinishMarkupResizeDragForTesting();

                var editedPrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var editedPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);

                viewModel.Undo();

                var undonePrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var undonePeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);
                return (began, editedPrimary, editedPeer, undonePrimary, undonePeer);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(0, 0), outcome.editedPrimary.Item1);
        Assert.Equal(13.333333333333334, outcome.editedPrimary.Item2.X, 6);
        Assert.Equal(13.333333333333334, outcome.editedPrimary.Item2.Y, 6);
        Assert.Equal(26.666666666666668, outcome.editedPeer.Item1.X, 6);
        Assert.Equal(26.666666666666668, outcome.editedPeer.Item1.Y, 6);
        Assert.Equal(40, outcome.editedPeer.Item2.X, 6);
        Assert.Equal(40, outcome.editedPeer.Item2.Y, 6);
        Assert.True(outcome.editedPrimary.Item3 > 1.0);
        Assert.True(outcome.editedPeer.Item3 > 1.0);

        Assert.Equal(new Point(0, 0), outcome.undonePrimary.Item1);
        Assert.Equal(new Point(10, 10), outcome.undonePrimary.Item2);
        Assert.Equal(new Point(20, 20), outcome.undonePeer.Item1);
        Assert.Equal(new Point(30, 30), outcome.undonePeer.Item2);
        Assert.Equal(1.0, outcome.undonePrimary.Item3, 6);
        Assert.Equal(1.0, outcome.undonePeer.Item3, 6);
    }

    [Fact]
    public void ResizeDragForTesting_GroupedRectangles_FromTopHandle_UsesGridSnapAndSupportsUndo()
    {
        var selectedMarkup = CreateGroupedRectangle(new Rect(0, 0, 10, 10), "group-1");
        var peerMarkup = CreateGroupedRectangle(new Rect(20, 20, 10, 10), "group-1");

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(15, 0));
                window.UpdateMarkupResizePreviewForTesting(new Point(11, -11));
                window.FinishMarkupResizeDragForTesting();

                var editedPrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var editedPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);

                viewModel.Undo();

                var undonePrimary = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth);
                var undonePeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1], peerMarkup.Appearance.StrokeWidth);
                return (began, editedPrimary, editedPeer, undonePrimary, undonePeer);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(0, -20), outcome.editedPrimary.Item1);
        Assert.Equal(10, outcome.editedPrimary.Item2.X, 6);
        Assert.Equal(-3.333333333333332, outcome.editedPrimary.Item2.Y, 6);
        Assert.Equal(20, outcome.editedPeer.Item1.X, 6);
        Assert.Equal(13.333333333333336, outcome.editedPeer.Item1.Y, 6);
        Assert.Equal(30, outcome.editedPeer.Item2.X, 6);
        Assert.Equal(30, outcome.editedPeer.Item2.Y, 6);
        Assert.Equal(1.0, outcome.editedPrimary.Item3, 6);
        Assert.Equal(1.0, outcome.editedPeer.Item3, 6);

        Assert.Equal(new Point(0, 0), outcome.undonePrimary.Item1);
        Assert.Equal(new Point(10, 10), outcome.undonePrimary.Item2);
        Assert.Equal(new Point(20, 20), outcome.undonePeer.Item1);
        Assert.Equal(new Point(30, 30), outcome.undonePeer.Item2);
        Assert.Equal(1.0, outcome.undonePrimary.Item3, 6);
        Assert.Equal(1.0, outcome.undonePeer.Item3, 6);
    }

    [Fact]
    public void BeginSelectedMarkupResizeDragForTesting_WithoutHandleHit_ReturnsFalseAndLeavesGeometryUnchanged()
    {
        var outcome = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 100, 100), null),
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(50, 50));
                window.UpdateMarkupResizePreviewForTesting(new Point(140, 160));
                window.FinishMarkupResizeDragForTesting();

                return (began, bounds: markup.BoundingRect, canUndo: viewModel.UndoRedo.CanUndo);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.False(outcome.began);
        Assert.Equal(new Rect(0, 0, 100, 100), outcome.bounds);
        Assert.False(outcome.canUndo);
    }

    [Fact]
    public void BeginSelectedMarkupResizeDragForTesting_GroupContainingNonResizableMarkup_ReturnsFalse()
    {
        var selectedMarkup = CreateGroupedRectangle(new Rect(0, 0, 10, 10), "group-1");
        var peerMarkup = new MarkupRecord
        {
            Type = MarkupType.RevisionCloud,
            Vertices = { new Point(20, 20), new Point(30, 20), new Point(30, 30) }
        };
        peerMarkup.UpdateBoundingRect();
        peerMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(10, 10));
                window.UpdateMarkupResizePreviewForTesting(new Point(20, 20));
                window.FinishMarkupResizeDragForTesting();

                return (began, primaryBounds: markup.BoundingRect, peerBounds: peerMarkup.BoundingRect, canUndo: viewModel.UndoRedo.CanUndo);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = false;
            });

        Assert.False(outcome.began);
        Assert.Equal(new Rect(0, 0, 10, 10), outcome.primaryBounds);
        Assert.Equal(new Rect(20, 20, 10, 10), outcome.peerBounds);
        Assert.False(outcome.canUndo);
    }

    [Fact]
    public void ResizeDragForTesting_WithoutPreviewChange_DoesNotCreateUndoAction()
    {
        var outcome = RunWithSelectedMarkupWindow(
            CreateGroupedRectangle(new Rect(0, 0, 10, 10), null),
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(10, 10));
                var beforeFinish = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth, viewModel.UndoRedo.CanUndo);
                window.FinishMarkupResizeDragForTesting();
                var afterFinish = (markup.Vertices[0], markup.Vertices[1], markup.Appearance.StrokeWidth, viewModel.UndoRedo.CanUndo);
                return (began, beforeFinish, afterFinish);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(0, 0), outcome.beforeFinish.Item1);
        Assert.Equal(new Point(10, 10), outcome.beforeFinish.Item2);
        Assert.Equal(1.0, outcome.beforeFinish.Item3, 6);
        Assert.False(outcome.beforeFinish.Item4);
        Assert.Equal(new Point(0, 0), outcome.afterFinish.Item1);
        Assert.Equal(new Point(10, 10), outcome.afterFinish.Item2);
        Assert.Equal(1.0, outcome.afterFinish.Item3, 6);
        Assert.False(outcome.afterFinish.Item4);
    }

    [Fact]
    public void DirectVertexDragForTesting_LineStyleDimension_UsesPolarSnapAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(30, 20) },
                Metadata = new MarkupMetadata { Subject = "Diameter" }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 6));
                window.FinishMarkupVertexDragForTesting();
                var editedState = (markup.Vertices[1], markup.Vertices[2]);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Vertices[2]);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(16.431676725154983, outcome.editedState.Item1.X, 3);
        Assert.Equal(9.486832980505136, outcome.editedState.Item1.Y, 3);
        Assert.NotEqual(new Point(30, 20), outcome.editedState.Item2);
        Assert.Equal(new Point(10, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(30, 20), outcome.undoneState.Item2);
    }

    [Fact]
    public void DirectVertexDragForTesting_LineStyleMeasurement_UsesPolarSnapAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(30, 20) },
                Metadata = new MarkupMetadata { Subject = "Diameter" }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 6));
                window.FinishMarkupVertexDragForTesting();
                var editedState = (markup.Vertices[1], markup.Vertices[2]);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Vertices[2]);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(16.431676725154983, outcome.editedState.Item1.X, 3);
        Assert.Equal(9.486832980505136, outcome.editedState.Item1.Y, 3);
        Assert.NotEqual(new Point(30, 20), outcome.editedState.Item2);
        Assert.Equal(new Point(10, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(30, 20), outcome.undoneState.Item2);
    }

    [Fact]
    public void DirectVertexDragForTesting_RadialDimension_UsesPolarSnapAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
                Metadata = new MarkupMetadata { Subject = "Radial" }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, -5));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 6));
                window.FinishMarkupVertexDragForTesting();
                var editedState = (markup.Vertices[1], markup.Vertices[2]);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Vertices[2]);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(16.431676725154983, outcome.editedState.Item1.X, 3);
        Assert.Equal(9.486832980505136, outcome.editedState.Item1.Y, 3);
        Assert.NotEqual(new Point(15, 3), outcome.editedState.Item2);
        Assert.Equal(new Point(12, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(15, 3), outcome.undoneState.Item2);
    }

    [Fact]
    public void DirectVertexDragForTesting_RadialMeasurement_UsesPolarSnapAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
                Metadata = new MarkupMetadata { Subject = "Radial" }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, -5));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 6));
                window.FinishMarkupVertexDragForTesting();
                var editedState = (markup.Vertices[1], markup.Vertices[2]);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Vertices[2]);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(16.431676725154983, outcome.editedState.Item1.X, 3);
        Assert.Equal(9.486832980505136, outcome.editedState.Item1.Y, 3);
        Assert.NotEqual(new Point(15, 3), outcome.editedState.Item2);
        Assert.Equal(new Point(12, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(15, 3), outcome.undoneState.Item2);
    }

    [Fact]
    public void DirectVertexDragForTesting_PathMarkup_SnapsToSiblingVertexAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(30, 0), new Point(30, 30) }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(30, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(28, 28));
                window.FinishMarkupVertexDragForTesting();
                var editedVertex = markup.Vertices[1];

                viewModel.Undo();
                var undoneVertex = markup.Vertices[1];
                return (began, editedVertex, undoneVertex);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(30, 30), outcome.editedVertex);
        Assert.Equal(new Point(30, 0), outcome.undoneVertex);
    }

    [Fact]
    public void DirectSelectionDragForTesting_Polyline_UsesGridSnapAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupSelectionDragForTesting(new Point(5, 0));
                window.UpdateDraggedMarkupPreviewForTesting(new Point(11, 11));
                window.FinishMarkupSelectionDragForTesting();
                var editedState = (markup.Vertices[0], markup.Vertices[1]);

                viewModel.Undo();
                var undoneState = (markup.Vertices[0], markup.Vertices[1]);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(15, 20), outcome.editedState.Item1);
        Assert.Equal(new Point(25, 20), outcome.editedState.Item2);
        Assert.Equal(new Point(0, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(10, 0), outcome.undoneState.Item2);
    }

    [Fact]
    public void DirectSelectionDragForTesting_Polyline_SnapsToPeerCircleCenterAndSupportsUndo()
    {
        var peerCircle = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = { new Point(40, 20) },
            Radius = 10
        };

        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupSelectionDragForTesting(new Point(5, 0));
                window.UpdateDraggedMarkupPreviewForTesting(new Point(42, 18));
                window.FinishMarkupSelectionDragForTesting();
                var editedState = (markup.Vertices[0], markup.Vertices[1]);

                viewModel.Undo();
                var undoneState = (markup.Vertices[0], markup.Vertices[1]);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerCircle);
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(new Point(35, 20), outcome.editedState.Item1);
        Assert.Equal(new Point(45, 20), outcome.editedState.Item2);
        Assert.Equal(new Point(0, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(10, 0), outcome.undoneState.Item2);
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_ActiveResizeDrag_RestoresPreviewGeometry()
    {
        var selectedMarkup = CreateGroupedRectangle(new Rect(0, 0, 10, 10), "group-1");
        var peerMarkup = CreateGroupedRectangle(new Rect(20, 20, 10, 10), "group-1");

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupResizeDragForTesting(new Point(30, 30));
                window.UpdateMarkupResizePreviewForTesting(new Point(40, 50));
                var previewPrimary = (markup.Vertices[0], markup.Vertices[1]);
                var previewPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1]);
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                var cancelledPrimary = (markup.Vertices[0], markup.Vertices[1]);
                var cancelledPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1]);
                window.UpdateMarkupResizePreviewForTesting(new Point(60, 70));
                var postCancelPrimary = (markup.Vertices[0], markup.Vertices[1]);
                var postCancelPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1]);
                window.FinishMarkupResizeDragForTesting();
                var finalPrimary = (markup.Vertices[0], markup.Vertices[1]);
                var finalPeer = (peerMarkup.Vertices[0], peerMarkup.Vertices[1]);
                return (began, cancelled, previewPrimary, previewPeer, cancelledPrimary, cancelledPeer, postCancelPrimary, postCancelPeer, finalPrimary, finalPeer);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.cancelled);
        Assert.NotEqual((new Point(0, 0), new Point(10, 10)), outcome.previewPrimary);
        Assert.NotEqual((new Point(20, 20), new Point(30, 30)), outcome.previewPeer);
        Assert.Equal((new Point(0, 0), new Point(10, 10)), outcome.cancelledPrimary);
        Assert.Equal((new Point(20, 20), new Point(30, 30)), outcome.cancelledPeer);
        Assert.Equal((new Point(0, 0), new Point(10, 10)), outcome.postCancelPrimary);
        Assert.Equal((new Point(20, 20), new Point(30, 30)), outcome.postCancelPeer);
        Assert.Equal((new Point(0, 0), new Point(10, 10)), outcome.finalPrimary);
        Assert.Equal((new Point(20, 20), new Point(30, 30)), outcome.finalPeer);
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_ActiveVertexDrag_RestoresPreviewGeometry()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(15, 5));
                var previewVertex = markup.Vertices[1];
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                var cancelledVertex = markup.Vertices[1];
                window.UpdateDraggedMarkupVertexPreviewForTesting(new Point(18, 8));
                var postCancelVertex = markup.Vertices[1];
                window.FinishMarkupVertexDragForTesting();
                var finalVertex = markup.Vertices[1];
                return (began, cancelled, previewVertex, cancelledVertex, postCancelVertex, finalVertex);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.cancelled);
        Assert.NotEqual(new Point(10, 0), outcome.previewVertex);
        Assert.Equal(new Point(10, 0), outcome.cancelledVertex);
        Assert.Equal(new Point(10, 0), outcome.postCancelVertex);
        Assert.Equal(new Point(10, 0), outcome.finalVertex);
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_ActiveSelectionDrag_RestoresPreviewGeometry()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupSelectionDragForTesting(new Point(5, 0));
                window.UpdateDraggedMarkupPreviewForTesting(new Point(12, 8));
                var previewState = (markup.Vertices[0], markup.Vertices[1]);
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                var cancelledState = (markup.Vertices[0], markup.Vertices[1]);
                window.UpdateDraggedMarkupPreviewForTesting(new Point(25, 25));
                var postCancelState = (markup.Vertices[0], markup.Vertices[1]);
                window.FinishMarkupSelectionDragForTesting();
                var finalState = (markup.Vertices[0], markup.Vertices[1]);
                return (began, cancelled, previewState, cancelledState, postCancelState, finalState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.cancelled);
        Assert.Equal((new Point(7, 8), new Point(17, 8)), outcome.previewState);
        Assert.Equal((new Point(0, 0), new Point(10, 0)), outcome.cancelledState);
        Assert.Equal((new Point(0, 0), new Point(10, 0)), outcome.postCancelState);
        Assert.Equal((new Point(0, 0), new Point(10, 0)), outcome.finalState);
    }

    [Fact]
    public void BeginSelectedMarkupRadiusDragForTesting_WithoutHandleHit_ReturnsFalseAndLeavesGeometryUnchanged()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(0, 0) },
                Radius = 10
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(0, 0));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(12, 0));
                window.FinishMarkupRadiusDragForTesting();
                return (began, radius: markup.Radius, canUndo: viewModel.UndoRedo.CanUndo);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.False(outcome.began);
        Assert.Equal(10, outcome.radius, 6);
        Assert.False(outcome.canUndo);
    }

    [Fact]
    public void BeginSelectedMarkupRadiusDragForTesting_GroupedSelection_ReturnsFalse()
    {
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = { new Point(0, 0) },
            Radius = 10
        };
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var peerMarkup = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = { new Point(20, 20) },
            Radius = 8
        };
        peerMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(12, 0));
                window.FinishMarkupRadiusDragForTesting();
                return (began, radius: markup.Radius, peerRadius: peerMarkup.Radius, canUndo: viewModel.UndoRedo.CanUndo);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = false;
            });

        Assert.False(outcome.began);
        Assert.Equal(10, outcome.radius, 6);
        Assert.Equal(8, outcome.peerRadius, 6);
        Assert.False(outcome.canUndo);
    }

    [Fact]
    public void DirectRadiusDragForTesting_WithoutPreviewChange_DoesNotCreateUndoAction()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(0, 0) },
                Radius = 10
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(10, 0));
                var beforeFinish = (markup.Radius, viewModel.UndoRedo.CanUndo);
                window.FinishMarkupRadiusDragForTesting();
                var afterFinish = (markup.Radius, viewModel.UndoRedo.CanUndo);
                return (began, beforeFinish, afterFinish);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(10, outcome.beforeFinish.Item1, 6);
        Assert.False(outcome.beforeFinish.Item2);
        Assert.Equal(10, outcome.afterFinish.Item1, 6);
        Assert.False(outcome.afterFinish.Item2);
    }

    [Fact]
    public void DirectRadiusDragForTesting_Circle_UsesGridSnapAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(0, 0) },
                Radius = 10
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.FinishMarkupRadiusDragForTesting();
                var editedRadius = markup.Radius;

                viewModel.Undo();
                var undoneRadius = markup.Radius;
                return (began, editedRadius, undoneRadius);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(28.284271247461902, outcome.editedRadius, 6);
        Assert.Equal(10, outcome.undoneRadius, 6);
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_ActiveRadiusDrag_RestoresPreviewGeometry()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(0, 0) },
                Radius = 10
            },
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(14, 0));
                var previewRadius = markup.Radius;
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                var cancelledRadius = markup.Radius;
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(18, 0));
                var postCancelRadius = markup.Radius;
                window.FinishMarkupRadiusDragForTesting();
                var finalRadius = markup.Radius;
                return (began, cancelled, previewRadius, cancelledRadius, postCancelRadius, finalRadius);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.cancelled);
        Assert.Equal(14, outcome.previewRadius, 6);
        Assert.Equal(10, outcome.cancelledRadius, 6);
        Assert.Equal(10, outcome.postCancelRadius, 6);
        Assert.Equal(10, outcome.finalRadius, 6);
    }

    [Fact]
    public void BeginSelectedMarkupArcAngleDragForTesting_WithoutHandleHit_ReturnsFalseAndLeavesGeometryUnchanged()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 0));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.FinishMarkupArcAngleDragForTesting();
                return (began, state: (markup.ArcStartDeg, markup.ArcSweepDeg), canUndo: viewModel.UndoRedo.CanUndo);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.False(outcome.began);
        Assert.Equal((0d, 90d), outcome.state);
        Assert.False(outcome.canUndo);
    }

    [Fact]
    public void BeginSelectedMarkupArcAngleDragForTesting_GroupedSelection_ReturnsFalse()
    {
        var selectedMarkup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(0, 0) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90
        };
        selectedMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var peerMarkup = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(20, 20) },
            Radius = 8,
            ArcStartDeg = 0,
            ArcSweepDeg = 90
        };
        peerMarkup.Metadata.CustomFields[DrawingAnnotationMarkupService.AnnotationGroupIdField] = "group-1";

        var outcome = RunWithSelectedMarkupWindow(
            selectedMarkup,
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.FinishMarkupArcAngleDragForTesting();
                return (began, state: (markup.ArcStartDeg, markup.ArcSweepDeg), peerState: (peerMarkup.ArcStartDeg, peerMarkup.ArcSweepDeg), canUndo: viewModel.UndoRedo.CanUndo);
            },
            viewModel =>
            {
                viewModel.Markups.Add(peerMarkup);
                viewModel.SnapToGrid = false;
            });

        Assert.False(outcome.began);
        Assert.Equal((0d, 90d), outcome.state);
        Assert.Equal((0d, 90d), outcome.peerState);
        Assert.False(outcome.canUndo);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_WithoutPreviewChange_DoesNotCreateUndoAction()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                var beforeFinish = ((markup.ArcStartDeg, markup.ArcSweepDeg), viewModel.UndoRedo.CanUndo);
                window.FinishMarkupArcAngleDragForTesting();
                var afterFinish = ((markup.ArcStartDeg, markup.ArcSweepDeg), viewModel.UndoRedo.CanUndo);
                return (began, beforeFinish, afterFinish);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal((0d, 90d), outcome.beforeFinish.Item1);
        Assert.False(outcome.beforeFinish.Item2);
        Assert.Equal((0d, 90d), outcome.afterFinish.Item1);
        Assert.False(outcome.afterFinish.Item2);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_ArcStartHandle_UsesPolarSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(10, 0));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(8, 6));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.ArcStartDeg, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.ArcStartDeg, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(30, outcome.editedState.Item1, 6);
        Assert.Equal(60, outcome.editedState.Item2, 6);
        Assert.Equal(0, outcome.undoneState.Item1, 6);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_ActiveArcAngleDrag_RestoresPreviewGeometry()
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
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                var previewState = (markup.ArcStartDeg, markup.ArcSweepDeg);
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                var cancelledState = (markup.ArcStartDeg, markup.ArcSweepDeg);
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(10, 10));
                var postCancelState = (markup.ArcStartDeg, markup.ArcSweepDeg);
                window.FinishMarkupArcAngleDragForTesting();
                var finalState = (markup.ArcStartDeg, markup.ArcSweepDeg);
                return (began, cancelled, previewState, cancelledState, postCancelState, finalState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.cancelled);
        Assert.Equal((0d, 180d), outcome.previewState);
        Assert.Equal((0d, 90d), outcome.cancelledState);
        Assert.Equal((0d, 90d), outcome.postCancelState);
        Assert.Equal((0d, 90d), outcome.finalState);
    }

    [Fact]
    public void DirectRadiusDragForTesting_Arc_UsesGridSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Radius, markup.ArcStartDeg, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Radius, markup.ArcStartDeg, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(28.284271247461902, outcome.editedState.Item1, 6);
        Assert.Equal(0, outcome.editedState.Item2, 6);
        Assert.Equal(90, outcome.editedState.Item3, 6);
        Assert.Equal(10, outcome.undoneState.Item1, 6);
        Assert.Equal(0, outcome.undoneState.Item2, 6);
        Assert.Equal(90, outcome.undoneState.Item3, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_ArcEndHandle_UsesPolarSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.ArcStartDeg, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.ArcStartDeg, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(0, outcome.editedState.Item1, 6);
        Assert.Equal(60, outcome.editedState.Item2, 6);
        Assert.Equal(0, outcome.undoneState.Item1, 6);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }


    [Fact]
    public void DirectArcAngleDragForTesting_AngularDimension_UpdatesGeometryAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(10, 10));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[2], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[2], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(7.0710678118654755, outcome.editedState.Item1.X, 6);
        Assert.Equal(7.0710678118654755, outcome.editedState.Item1.Y, 6);
        Assert.Equal(45, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_AngularMeasurement_UpdatesGeometryAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(10, 10));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[2], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[2], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(7.0710678118654755, outcome.editedState.Item1.X, 6);
        Assert.Equal(7.0710678118654755, outcome.editedState.Item1.Y, 6);
        Assert.Equal(45, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_AngularDimension_UsesPolarSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[2], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[2], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(5, outcome.editedState.Item1.X, 6);
        Assert.Equal(8.660254037844386, outcome.editedState.Item1.Y, 6);
        Assert.Equal(60, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_AngularMeasurement_UsesPolarSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[2], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[2], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(5, outcome.editedState.Item1.X, 6);
        Assert.Equal(8.660254037844386, outcome.editedState.Item1.Y, 6);
        Assert.Equal(60, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectRadiusDragForTesting_ArcLengthDimension_UpdatesGeometryAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(8.48528137423857, 8.48528137423857));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Vertices[1], markup.Radius, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Radius, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
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
    public void DirectRadiusDragForTesting_ArcLengthMeasurement_UpdatesGeometryAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(8.48528137423857, 8.48528137423857));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Vertices[1], markup.Radius, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.Radius, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
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
    public void DirectArcAngleDragForTesting_ArcEndHandle_UsesGridSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(11, 29));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.ArcStartDeg, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.ArcStartDeg, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
                viewModel.IsPolarActive = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(0, outcome.editedState.Item1, 6);
        Assert.Equal(45, outcome.editedState.Item2, 6);
        Assert.Equal(0, outcome.undoneState.Item1, 6);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectRadiusDragForTesting_AngularDimension_UsesGridSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(5.656854249492381, 5.65685424949238));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Radius, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Radius, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(28.284271247461902, outcome.editedState.Item1, 6);
        Assert.Equal(90, outcome.editedState.Item2, 6);
        Assert.Equal(8, outcome.undoneState.Item1, 6);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectRadiusDragForTesting_AngularMeasurement_UsesGridSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(5.656854249492381, 5.65685424949238));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Radius, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Radius, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(28.284271247461902, outcome.editedState.Item1, 6);
        Assert.Equal(90, outcome.editedState.Item2, 6);
        Assert.Equal(8, outcome.undoneState.Item1, 6);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectRadiusDragForTesting_ArcLengthDimension_UsesGridSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Radius, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Radius, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(28.284271247461902, outcome.editedState.Item1, 6);
        Assert.Equal(31.819805153394636, outcome.editedState.Item2, 6);
        Assert.Equal(10, outcome.undoneState.Item1, 6);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectRadiusDragForTesting_ArcLengthMeasurement_UsesGridSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupRadiusDragForTesting(new Point(7.0710678118654755, 7.0710678118654755));
                window.UpdateDraggedMarkupRadiusPreviewForTesting(new Point(11, 11));
                window.FinishMarkupRadiusDragForTesting();
                var editedState = (markup.Radius, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Radius, markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.Equal(28.284271247461902, outcome.editedState.Item1, 6);
        Assert.Equal(31.819805153394636, outcome.editedState.Item2, 6);
        Assert.Equal(10, outcome.undoneState.Item1, 6);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_ArcLengthDimension_UpdatesGeometryAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[1], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(-10, outcome.editedState.Item1.X, 6);
        Assert.Equal(0, outcome.editedState.Item1.Y, 6);
        Assert.Equal(180, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_ArcLengthMeasurement_UpdatesGeometryAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(-10, 0));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[1], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.Equal(-10, outcome.editedState.Item1.X, 6);
        Assert.Equal(0, outcome.editedState.Item1.Y, 6);
        Assert.Equal(180, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_ArcLengthDimension_UsesPolarSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[1], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(5, outcome.editedState.Item1.X, 6);
        Assert.Equal(8.660254037844386, outcome.editedState.Item1.Y, 6);
        Assert.Equal(60, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

    [Fact]
    public void DirectArcAngleDragForTesting_ArcLengthMeasurement_UsesPolarSnapAndSupportsUndo()
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
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupArcAngleDragForTesting(new Point(0, 10));
                window.UpdateDraggedMarkupArcAnglePreviewForTesting(new Point(6, 8));
                window.FinishMarkupArcAngleDragForTesting();
                var editedState = (markup.Vertices[1], markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Vertices[1], markup.ArcSweepDeg);
                return (began, editedState, undoneState);
            },
            viewModel =>
            {
                viewModel.IsPolarActive = true;
                viewModel.PolarIncrementDeg = 30;
            });

        Assert.True(outcome.began);
        Assert.Equal(5, outcome.editedState.Item1.X, 6);
        Assert.Equal(8.660254037844386, outcome.editedState.Item1.Y, 6);
        Assert.Equal(60, outcome.editedState.Item2, 6);
        Assert.Equal(new Point(0, 10), outcome.undoneState.Item1);
        Assert.Equal(90, outcome.undoneState.Item2, 6);
    }

}
