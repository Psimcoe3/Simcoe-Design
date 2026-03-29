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
