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
    public void ExecuteEditMarkupGeometryCommandForTesting_Circle_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(20, 20) },
                Radius = 5
            },
            (window, viewModel, markup) =>
            {
                var edited = window.ExecuteEditMarkupGeometryCommandForTesting("12");
                var editedState = (markup.Radius, markup.BoundingRect);

                viewModel.Undo();
                var undoneState = (markup.Radius, markup.BoundingRect);
                return (edited, editedState, undoneState);
            });

        Assert.True(outcome.edited);
        Assert.Equal(12, outcome.editedState.Item1, 6);
        Assert.Equal(new Rect(8, 8, 24, 24), outcome.editedState.Item2);
        Assert.Equal(5, outcome.undoneState.Item1, 6);
        Assert.Equal(new Rect(15, 15, 10, 10), outcome.undoneState.Item2);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_Arc_UpdatesGeometryAndSupportsUndo()
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
                var edited = window.ExecuteEditMarkupGeometryCommandForTesting("radius=12\nstart=30\nend=150");
                var editedState = (markup.Radius, markup.ArcStartDeg, markup.ArcSweepDeg);

                viewModel.Undo();
                var undoneState = (markup.Radius, markup.ArcStartDeg, markup.ArcSweepDeg);
                return (edited, editedState, undoneState);
            });

        Assert.True(outcome.edited);
        Assert.Equal(12, outcome.editedState.Item1, 6);
        Assert.Equal(30, outcome.editedState.Item2, 6);
        Assert.Equal(120, outcome.editedState.Item3, 6);
        Assert.Equal(10, outcome.undoneState.Item1, 6);
        Assert.Equal(0, outcome.undoneState.Item2, 6);
        Assert.Equal(90, outcome.undoneState.Item3, 6);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_Circle_InvalidRadius_DoesNotChangeGeometry()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = { new Point(20, 20) },
                Radius = 5
            },
            (window, viewModel, markup) =>
            {
                var handled = window.ExecuteEditMarkupGeometryCommandForTesting("radius=0");
                var stateAfterCommand = (markup.Radius, markup.BoundingRect);

                viewModel.Undo();
                var stateAfterUndo = (markup.Radius, markup.BoundingRect);
                return (handled, stateAfterCommand, stateAfterUndo);
            });

        Assert.True(outcome.handled);
        Assert.Equal(5, outcome.stateAfterCommand.Item1, 6);
        Assert.Equal(new Rect(15, 15, 10, 10), outcome.stateAfterCommand.Item2);
        Assert.Equal(outcome.stateAfterCommand, outcome.stateAfterUndo);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_Arc_InvalidRadius_DoesNotChangeGeometry()
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
                var handled = window.ExecuteEditMarkupGeometryCommandForTesting("radius=0\nstart=30");
                var stateAfterCommand = (markup.Radius, markup.ArcStartDeg, markup.ArcSweepDeg);

                viewModel.Undo();
                var stateAfterUndo = (markup.Radius, markup.ArcStartDeg, markup.ArcSweepDeg);
                return (handled, stateAfterCommand, stateAfterUndo);
            });

        Assert.True(outcome.handled);
        Assert.Equal(10, outcome.stateAfterCommand.Item1, 6);
        Assert.Equal(0, outcome.stateAfterCommand.Item2, 6);
        Assert.Equal(90, outcome.stateAfterCommand.Item3, 6);
        Assert.Equal(outcome.stateAfterCommand, outcome.stateAfterUndo);
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
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 12), new Point(10, 10) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, viewModel, markup) =>
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
    public void ExecuteEditMarkupGeometryCommandForTesting_LineMeasurement_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(5, 3) }
            },
            (window, viewModel, markup) =>
            {
                var edited = window.ExecuteEditMarkupGeometryCommandForTesting("length=24\nangle=45");
                var editedState = (
                    markup.Vertices[1],
                    markup.Vertices[2]);

                viewModel.Undo();
                var undoneState = (
                    markup.Vertices[1],
                    markup.Vertices[2]);

                return (edited, editedState, undoneState);
            });

        Assert.True(outcome.edited);
        Assert.Equal(16.970562748477143, outcome.editedState.Item1.X, 6);
        Assert.Equal(16.97056274847714, outcome.editedState.Item1.Y, 6);
        Assert.Equal(3.3941125496954285, outcome.editedState.Item2.X, 6);
        Assert.Equal(13.57645019878171, outcome.editedState.Item2.Y, 6);

        Assert.Equal(new Point(10, 0), outcome.undoneState.Item1);
        Assert.Equal(new Point(5, 3), outcome.undoneState.Item2);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_AngularMeasurement_UpdatesGeometryAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Measurement,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(0, 12), new Point(10, 10) },
                Radius = 8,
                ArcStartDeg = 0,
                ArcSweepDeg = 90,
                Metadata = new MarkupMetadata { Subject = "Angular" }
            },
            (window, viewModel, markup) =>
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
    public void ExecuteEditMarkupGeometryCommandForTesting_ArcLengthMeasurement_UpdatesGeometryAndSupportsUndo()
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
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
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
    public void ExecuteEditMarkupGeometryCommandForTesting_ArcLengthDimension_UpdatesGeometryAndSupportsUndo()
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
            },
            viewModel =>
            {
                viewModel.SnapToGrid = false;
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
    public void ExecuteEditMarkupGeometryCommandForTesting_Polyline_UpdatesVerticesAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10) }
            },
            (window, viewModel, markup) =>
            {
                var edited = window.ExecuteEditMarkupGeometryCommandForTesting("x1=5\ny1=5\nx2=15\ny2=5\nx3=15\ny3=15");
                var editedVertices = markup.Vertices.ToList();

                viewModel.Undo();
                var undoneVertices = markup.Vertices.ToList();
                return (edited, editedVertices, undoneVertices);
            });

        Assert.True(outcome.edited);
        Assert.Equal(3, outcome.editedVertices.Count);
        Assert.Equal(new Point(5, 5), outcome.editedVertices[0]);
        Assert.Equal(new Point(15, 5), outcome.editedVertices[1]);
        Assert.Equal(new Point(15, 15), outcome.editedVertices[2]);

        Assert.Equal(3, outcome.undoneVertices.Count);
        Assert.Equal(new Point(0, 0), outcome.undoneVertices[0]);
        Assert.Equal(new Point(10, 0), outcome.undoneVertices[1]);
        Assert.Equal(new Point(10, 10), outcome.undoneVertices[2]);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_Polygon_UpdatesVerticesAndSupportsUndo()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polygon,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10) }
            },
            (window, viewModel, markup) =>
            {
                var edited = window.ExecuteEditMarkupGeometryCommandForTesting("x1=1\ny1=1\nx2=11\ny2=1\nx3=11\ny3=11");
                var editedVertices = markup.Vertices.ToList();

                viewModel.Undo();
                var undoneVertices = markup.Vertices.ToList();
                return (edited, editedVertices, undoneVertices);
            });

        Assert.True(outcome.edited);
        Assert.Equal(3, outcome.editedVertices.Count);
        Assert.Equal(new Point(1, 1), outcome.editedVertices[0]);
        Assert.Equal(new Point(11, 1), outcome.editedVertices[1]);
        Assert.Equal(new Point(11, 11), outcome.editedVertices[2]);

        Assert.Equal(4, outcome.undoneVertices.Count);
        Assert.Equal(new Point(0, 0), outcome.undoneVertices[0]);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_Polyline_TooFewVertices_DoesNotChangeGeometry()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10) }
            },
            (window, viewModel, markup) =>
            {
                var handled = window.ExecuteEditMarkupGeometryCommandForTesting("x1=5\ny1=5");
                var vertices = markup.Vertices.ToList();
                return (handled, vertices);
            });

        Assert.True(outcome.handled);
        Assert.Equal(3, outcome.vertices.Count);
        Assert.Equal(new Point(0, 0), outcome.vertices[0]);
    }

    [Fact]
    public void ExecuteEditMarkupGeometryCommandForTesting_Polyline_IncompleteVertex_DoesNotChangeGeometry()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Vertices = { new Point(0, 0), new Point(10, 0) }
            },
            (window, viewModel, markup) =>
            {
                var handled = window.ExecuteEditMarkupGeometryCommandForTesting("x1=5\ny1=5\nx2=15");
                var vertices = markup.Vertices.ToList();
                return (handled, vertices);
            });

        Assert.True(outcome.handled);
        Assert.Equal(2, outcome.vertices.Count);
        Assert.Equal(new Point(0, 0), outcome.vertices[0]);
    }

}
