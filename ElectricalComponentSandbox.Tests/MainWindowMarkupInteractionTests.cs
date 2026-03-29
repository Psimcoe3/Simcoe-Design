using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
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