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
    public void BeginSelectedMarkupVertexInsertionForTesting_InsertableSelection_EntersPendingMode()
    {
        var pending = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, _, _) =>
            {
                return window.BeginSelectedMarkupVertexInsertionForTesting() &&
                       window.IsPendingMarkupVertexInsertionForTesting;
            });

        Assert.True(pending);
    }

    [Fact]
    public void BeginSelectedMarkupVertexInsertionForTesting_NonInsertableSelection_DoesNotEnterPendingMode()
    {
        var pending = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, _) =>
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                return began || window.IsPendingMarkupVertexInsertionForTesting;
            });

        Assert.False(pending);
    }

    [Fact]
    public void HandlePendingMarkupVertexInsertionClickForTesting_SegmentHit_InsertsVertexAndClearsPendingMode()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(30, 0) }
            },
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var inserted = window.HandlePendingMarkupVertexInsertionClickForTesting(new Point(15, 0));
                return (began, inserted, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.inserted);
        Assert.False(outcome.Item3);
        Assert.Equal(3, outcome.Count);
        Assert.Equal(1, outcome.ActiveMarkupVertexIndexForTesting);
        Assert.Equal(new Point(15, 0), outcome.Item6);
    }

    [Fact]
    public void HandlePendingMarkupVertexInsertionClickForTesting_WithGridSnap_InsertsSnappedVertexAndClearsPendingMode()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(30, 0) }
            },
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var inserted = window.HandlePendingMarkupVertexInsertionClickForTesting(new Point(13, 6));
                return (began, inserted, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = true;
                viewModel.GridSize = 1.0;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.inserted);
        Assert.False(outcome.Item3);
        Assert.Equal(3, outcome.Count);
        Assert.Equal(1, outcome.ActiveMarkupVertexIndexForTesting);
        Assert.Equal(new Point(15, 0), outcome.Item6);
    }

    [Fact]
    public void HandlePendingMarkupVertexInsertionClickForTesting_NearExistingVertexSnap_DoesNotInsertAndKeepsPendingMode()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(30, 0) }
            },
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var inserted = window.HandlePendingMarkupVertexInsertionClickForTesting(new Point(28, 1));
                return (began, inserted, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.False(outcome.inserted);
        Assert.True(outcome.Item3);
        Assert.Equal(2, outcome.Count);
        Assert.Equal(-1, outcome.ActiveMarkupVertexIndexForTesting);
        Assert.Equal(new Point(30, 0), outcome.Item6);
    }

    [Fact]
    public void UpdateCanvasHoverSnapForTesting_WithPendingVertexInsertion_SnapsToSelectedMarkupEndpoint()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(30, 0) }
            },
            (window, _, _) =>
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var snap = window.UpdateCanvasHoverSnapForTesting(new Point(28, 1));
                return (began, snap, window.IsPendingMarkupVertexInsertionForTesting);
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
            });

        Assert.True(outcome.began);
        Assert.True(outcome.Item3);
        Assert.NotNull(outcome.snap);
        Assert.True(outcome.snap!.Snapped);
        Assert.Equal(SnapService.SnapType.Endpoint, outcome.snap.Type);
        Assert.Equal(new Point(30, 0), outcome.snap.SnappedPoint);
    }

    [Fact]
    public void UpdateCanvasHoverSnapForTesting_WithVisibleCircleMarkup_SnapsToCircleCenter()
    {
        var peerCircle = new MarkupRecord
        {
            Type = MarkupType.Circle,
            Vertices = { new Point(40, 40) },
            Radius = 10
        };

        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, _) => window.UpdateCanvasHoverSnapForTesting(new Point(42, 39)),
            viewModel =>
            {
                viewModel.Markups.Add(peerCircle);
                viewModel.SnapToGrid = false;
            });

        Assert.NotNull(outcome);
        Assert.True(outcome!.Snapped);
        Assert.Equal(SnapService.SnapType.Center, outcome.Type);
        Assert.Equal(new Point(40, 40), outcome.SnappedPoint);
    }

    [Fact]
    public void UpdateCanvasHoverSnapForTesting_WithVisibleArcMarkup_SnapsToArcEndpoint()
    {
        var peerArc = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(40, 40) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90
        };

        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, _) => window.UpdateCanvasHoverSnapForTesting(new Point(41, 49)),
            viewModel =>
            {
                viewModel.Markups.Add(peerArc);
                viewModel.SnapToGrid = false;
            });

        Assert.NotNull(outcome);
        Assert.True(outcome!.Snapped);
        Assert.Equal(SnapService.SnapType.Endpoint, outcome.Type);
        Assert.Equal(new Point(40, 50), outcome.SnappedPoint);
    }

    [Fact]
    public void UpdateCanvasHoverSnapForTesting_WithVisibleArcMarkup_DoesNotSnapToOffSweepQuadrant()
    {
        var peerArc = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(40, 40) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90
        };

        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, _) => window.UpdateCanvasHoverSnapForTesting(new Point(29, 39)),
            viewModel =>
            {
                viewModel.Markups.Add(peerArc);
                viewModel.SnapToGrid = false;
                viewModel.SnapService.SnapToCenter = false;
            });

        Assert.NotNull(outcome);
        Assert.False(outcome!.Snapped);
        Assert.Equal(SnapService.SnapType.None, outcome.Type);
    }

    [Fact]
    public void UpdateCanvasHoverSnapForTesting_WithVisibleArcMarkup_NearArcCurve_SnapsToNearest()
    {
        var peerArc = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(40, 40) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90
        };

        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, _) => window.UpdateCanvasHoverSnapForTesting(new Point(48, 48)),
            viewModel =>
            {
                viewModel.Markups.Add(peerArc);
                viewModel.SnapToGrid = false;
                viewModel.SnapService.SnapToEndpoints = false;
                viewModel.SnapService.SnapToMidpoints = false;
                viewModel.SnapService.SnapToCenter = false;
                viewModel.SnapService.SnapToQuadrant = false;
                viewModel.SnapService.SnapToNearest = true;
            });

        Assert.NotNull(outcome);
        Assert.True(outcome!.Snapped);
        Assert.Equal(SnapService.SnapType.Nearest, outcome.Type);
        Assert.Equal(47.1, outcome.SnappedPoint.X, 1);
        Assert.Equal(47.1, outcome.SnappedPoint.Y, 1);
    }

    [Fact]
    public void UpdateCanvasHoverSnapForTesting_WithVisibleArcMarkup_SnapsToArcMidpoint()
    {
        var peerArc = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(40, 40) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 90
        };

        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, _) => window.UpdateCanvasHoverSnapForTesting(new Point(48, 48)),
            viewModel =>
            {
                viewModel.Markups.Add(peerArc);
                viewModel.SnapToGrid = false;
            });

        Assert.NotNull(outcome);
        Assert.True(outcome!.Snapped);
        Assert.Equal(SnapService.SnapType.Midpoint, outcome.Type);
        Assert.Equal(47.1, outcome.SnappedPoint.X, 1);
        Assert.Equal(47.1, outcome.SnappedPoint.Y, 1);
    }

    [Fact]
    public void UpdateCanvasHoverSnapForTesting_WithVisibleArcMarkups_SnapsToIntersection()
    {
        var firstArc = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(40, 40) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 180
        };
        var secondArc = new MarkupRecord
        {
            Type = MarkupType.Arc,
            Vertices = { new Point(50, 40) },
            Radius = 10,
            ArcStartDeg = 0,
            ArcSweepDeg = 180
        };

        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, _) => window.UpdateCanvasHoverSnapForTesting(new Point(45, 49)),
            viewModel =>
            {
                viewModel.Markups.Add(firstArc);
                viewModel.Markups.Add(secondArc);
                viewModel.SnapToGrid = false;
            });

        Assert.NotNull(outcome);
        Assert.True(outcome!.Snapped);
        Assert.Equal(SnapService.SnapType.Intersection, outcome.Type);
        Assert.Equal(45.0, outcome.SnappedPoint.X, 1);
        Assert.Equal(48.7, outcome.SnappedPoint.Y, 1);
    }

    [Fact]
    public void HandlePendingMarkupVertexInsertionClickForTesting_MissAndCancel_KeepThenClearPendingMode()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var inserted = window.HandlePendingMarkupVertexInsertionClickForTesting(new Point(200, 200));
                var pendingAfterMiss = window.IsPendingMarkupVertexInsertionForTesting;
                window.CancelPendingMarkupVertexInsertionForTesting();
                return (began, inserted, pendingAfterMiss, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count);
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
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, _, markup) =>
            {
                var enteredPendingMode = window.ExecuteInsertVertexCommandForTesting();
                return (enteredPendingMode, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting);
            });

        Assert.True(outcome.enteredPendingMode);
        Assert.True(outcome.Item2);
        Assert.Equal(3, outcome.Item3);
        Assert.Equal(-1, outcome.Item4);
    }

    [Fact]
    public void ExecuteEscapeShortcutForTesting_PendingInsertMode_CancelsPendingInsertion()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, viewModel, markup) =>
            {
                var began = window.BeginSelectedMarkupVertexInsertionForTesting();
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                return (began, cancelled, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, ReferenceEquals(viewModel.MarkupTool.SelectedMarkup, markup));
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
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, viewModel, markup) =>
            {
                var cancelled = window.ExecuteEscapeShortcutForTesting();
                return (cancelled, window.IsPendingMarkupVertexInsertionForTesting, markup.Vertices.Count, ReferenceEquals(viewModel.MarkupTool.SelectedMarkup, markup));
            });

        Assert.False(outcome.cancelled);
        Assert.False(outcome.Item2);
        Assert.Equal(2, outcome.Count);
        Assert.True(outcome.Item4);
    }

    [Fact]
    public void ExecuteDeleteVertexCommandForTesting_WithActiveGrip_DeletesVertex()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, _, markup) =>
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                var deleted = window.ExecuteDeleteVertexCommandForTesting();
                return (deleted, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            });

        Assert.True(outcome.deleted);
        Assert.Equal(2, outcome.Item2);
        Assert.Equal(1, outcome.Item3);
        Assert.Equal(new Point(20, 0), outcome.Item4);
    }

    [Fact]
    public void TryDeleteSelectedMarkupVertexForTesting_WithActiveGrip_DeletesVertexAndClampsSelection()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, _, markup) =>
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                var deleted = window.TryDeleteSelectedMarkupVertexForTesting();
                return (deleted, markup.Vertices.Count, window.ActiveMarkupVertexIndexForTesting, markup.Vertices[1]);
            });

        Assert.True(outcome.deleted);
        Assert.Equal(2, outcome.Count);
        Assert.Equal(1, outcome.ActiveMarkupVertexIndexForTesting);
        Assert.Equal(new Point(20, 0), outcome.Item4);
    }

    [Fact]
    public void TryDeleteSelectedMarkupVertexForTesting_WithoutActiveGrip_ReturnsFalse()
    {
        var deleted = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, _, _) =>
            {
                return window.TryDeleteSelectedMarkupVertexForTesting();
            });

        Assert.False(deleted);
    }

    [Fact]
    public void TryDeleteSelectedMarkupVertexForTesting_MinimumVertexSelection_ReturnsFalse()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, _, markup) =>
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                return (window.TryDeleteSelectedMarkupVertexForTesting(), markup.Vertices.Count);
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
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, viewModel, markup) =>
            {
                window.SetActiveMarkupVertexIndexForTesting(1);
                var usedVertexDelete = window.ExecuteDeleteShortcutForTesting();
                return (usedVertexDelete, viewModel.Markups.Count, markup.Vertices.Count, ReferenceEquals(viewModel.MarkupTool.SelectedMarkup, markup));
            });

        Assert.True(outcome.usedVertexDelete);
        Assert.Equal(1, outcome.Item2);
        Assert.Equal(2, outcome.Item3);
        Assert.True(outcome.Item4);
    }

    [Fact]
    public void ExecuteDeleteShortcutForTesting_WithoutActiveGrip_FallsBackToMarkupDeletion()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 0) }
            },
            (window, viewModel, _) =>
            {
                var usedVertexDelete = window.ExecuteDeleteShortcutForTesting();
                return (usedVertexDelete, viewModel.Markups.Count, viewModel.MarkupTool.SelectedMarkup is null);
            });

        Assert.False(outcome.usedVertexDelete);
        Assert.Equal(0, outcome.Count);
        Assert.True(outcome.Item3);
    }
}
