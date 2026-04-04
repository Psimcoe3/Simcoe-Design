using System.Windows;
using System.Windows.Controls;
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
    public void IsLineGeometryReadoutEligibleForTesting_RequiresEndpointDragOnLineStyleMeasurement()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
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
    public void BuildLineGeometryReadoutForTesting_RadialMeasurement_UsesRadiusLabel()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
        };

        Assert.Equal("Radius 12  Angle 0 deg", MainWindow.BuildLineGeometryReadoutForTesting(markup));
    }

    [Fact]
    public void BuildLineGeometryReadoutForTesting_RadialDimension_UsesRadiusLabel()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Dimension,
            Vertices = { new Point(0, 0), new Point(12, 0), new Point(15, 3) },
            Metadata = new MarkupMetadata { Subject = "Radial" }
        };

        Assert.Equal("Radius 12  Angle 0 deg", MainWindow.BuildLineGeometryReadoutForTesting(markup));
    }

    [Fact]
    public void BuildLineGeometryReadoutForTesting_DiameterMeasurement_UsesDiameterLabel()
    {
        var markup = new MarkupRecord
        {
            Type = MarkupType.Measurement,
            Vertices = { new Point(-6, 0), new Point(6, 0), new Point(9, 3) },
            Metadata = new MarkupMetadata { Subject = "Diameter" }
        };

        Assert.Equal("Diameter 12  Angle 0 deg", MainWindow.BuildLineGeometryReadoutForTesting(markup));
    }

    [Fact]
    public void UpdateContextualInspectorForTesting_WithSelectedMarkup_ShowsMarkupInspectorMode()
    {
        var outcome = RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Panel issue"
                }
            },
            (window, _, _) =>
            {
                window.UpdateContextualInspectorForTesting();

                var title = (TextBlock?)window.FindName("InspectorTitleTextBlock");
                var summary = (TextBlock?)window.FindName("InspectorSummaryTextBlock");
                var hint = (TextBlock?)window.FindName("InspectorHintTextBlock");
                var tabs = (TabControl?)window.FindName("RightPanelTabs");
                var selectedTab = tabs?.SelectedItem as TabItem;

                return (
                    Title: title?.Text,
                    Summary: summary?.Text,
                    Hint: hint?.Text,
                    SelectedHeader: selectedTab?.Header?.ToString());
            });

        Assert.Equal("Markup Inspector", outcome.Title);
        Assert.Contains("Panel issue", outcome.Summary);
        Assert.Contains("Review & Markups", outcome.SelectedHeader);
        Assert.Contains("threaded replies", outcome.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateContextualInspectorForTesting_WithSelectedMarkup_ShowsReviewTaskActionsAndResolveButtonWorks()
    {
        RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(10, 0), new Point(20, 10) },
                Metadata = new MarkupMetadata
                {
                    Label = "Routing conflict"
                }
            },
            (window, _, markup) =>
            {
                window.UpdateContextualInspectorForTesting();

                var taskTitle = FindRequired<TextBlock>(window, "InspectorTaskTitleTextBlock");
                var primaryButton = FindRequired<Button>(window, "InspectorPrimaryActionButton");
                var secondaryButton = FindRequired<Button>(window, "InspectorSecondaryActionButton");
                var tertiaryButton = FindRequired<Button>(window, "InspectorTertiaryActionButton");

                Assert.Equal("Current Review Action", taskTitle.Text);
                Assert.Equal("Add Reply", primaryButton.Content?.ToString());
                Assert.Equal("Resolve", secondaryButton.Content?.ToString());
                Assert.Equal("Assign", tertiaryButton.Content?.ToString());

                secondaryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, secondaryButton));

                Assert.Equal(MarkupStatus.Resolved, markup.Status);
                return 0;
            });
    }

    [Fact]
    public void UpdateCanvasGuidanceForTesting_WithPendingMarkupVertexInsertion_ShowsCancelGuidanceAndPrimaryActionClearsMode()
    {
        RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(18, 8) }
            },
            (window, _, _) =>
            {
                Assert.True(window.BeginSelectedMarkupVertexInsertionForTesting());

                window.UpdateCanvasGuidanceForTesting();

                var planCard = FindRequired<Border>(window, "PlanCanvasGuidanceCard");
                var planTitle = FindRequired<TextBlock>(window, "PlanCanvasGuidanceTitleTextBlock");
                var primaryButton = FindRequired<Button>(window, "PlanCanvasGuidancePrimaryActionButton");
                var secondaryButton = FindRequired<Button>(window, "PlanCanvasGuidanceSecondaryActionButton");

                Assert.Equal(Visibility.Visible, planCard.Visibility);
                Assert.Equal("Markup vertex insertion", planTitle.Text);
                Assert.Equal("Cancel Insert", primaryButton.Content?.ToString());
                Assert.Equal(Visibility.Collapsed, secondaryButton.Visibility);

                primaryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, primaryButton));

                Assert.False(window.IsPendingMarkupVertexInsertionForTesting);
                Assert.Equal(Visibility.Collapsed, planCard.Visibility);
                return 0;
            });
    }

    [Fact]
    public void UpdateMobileTopBarForTesting_WithSelectedMarkup_ShowsReviewMenuAndResolveWorks()
    {
        RunWithSelectedMarkupWindow(
            new MarkupRecord
            {
                Type = MarkupType.Polyline,
                Status = MarkupStatus.Open,
                Vertices = { new Point(0, 0), new Point(12, 0), new Point(18, 8) },
                Metadata = new MarkupMetadata
                {
                    Label = "Review clash"
                }
            },
            (window, _, markup) =>
            {
                window.EnableMobileViewForTesting();
                window.UpdateContextualInspectorForTesting();

                var state = window.GetMobileTopBarStateForTesting();
                var navigation = window.GetMobileNavigationStateForTesting();
                var primaryMenuHeaders = window.GetMobileMenuHeadersForTesting(primaryMenu: true);
                var overflowMenuHeaders = window.GetMobileMenuHeadersForTesting(primaryMenu: false);

                Assert.Equal("Review", state.SectionTitle);
                Assert.Equal("Review", state.AddLabel);
                Assert.Contains("resolve", state.Summary, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("Review\nIssue", navigation.PropertiesLabel);
                Assert.Contains("Add Reply", primaryMenuHeaders);
                Assert.Contains("Resolve", primaryMenuHeaders);
                Assert.Contains("Assign", primaryMenuHeaders);
                Assert.Contains("Edit Geometry...", overflowMenuHeaders);
                Assert.Contains("Edit Appearance...", overflowMenuHeaders);
                Assert.Contains("Insert Vertex", overflowMenuHeaders);
                Assert.DoesNotContain("Open Project...", overflowMenuHeaders);
                Assert.DoesNotContain("Save Project", overflowMenuHeaders);
                Assert.DoesNotContain("Import PDF Underlay...", overflowMenuHeaders);

                Assert.True(window.ExecuteMobileMenuItemForTesting(primaryMenu: true, header: "Resolve"));
                Assert.Equal(MarkupStatus.Resolved, markup.Status);
                return 0;
            });
    }

    private static T FindRequired<T>(FrameworkElement root, string name) where T : class
    {
        return root.FindName(name) as T
            ?? throw new Xunit.Sdk.XunitException($"Expected control '{name}' of type {typeof(T).Name}.");
    }
}
