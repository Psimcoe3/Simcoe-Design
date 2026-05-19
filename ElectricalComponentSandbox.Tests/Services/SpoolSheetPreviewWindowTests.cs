using System.Threading;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SpoolSheetPreviewWindowTests
{
    private static SpoolSheetRenderGeometry BuildGeometry()
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "emt", Name = "EMT", Standard = ConduitMaterialType.EMT });
        store.Settings.DefaultConduitTypeId = "emt";

        var seg = new ConduitSegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0),
            TradeSize = "3/4",
        };
        store.AddSegment(seg);
        var run = new ConduitRun { RunId = "CR-001", TradeSize = "3/4", Material = ConduitMaterialType.EMT };
        run.SegmentIds.Add(seg.Id);
        store.AddRun(run);

        var hanger = new HangerComponent { Trapeze = TrapezeAssembly.CreateSingleTierDefault() };
        var sheet = new SpoolSheetBuilder(store).Build(run.Id, new[] { hanger });
        return new SpoolSheetRenderer().Render(sheet);
    }

    [Fact]
    public void Constructor_NullGeometry_Throws()
    {
        RunOnSta(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new SpoolSheetPreviewWindow(null!));
            return true;
        });
    }

    [Fact]
    public void Constructor_SetsTitleAndSuggestedFileName()
    {
        RunOnSta(() =>
        {
            var window = new SpoolSheetPreviewWindow(BuildGeometry(), "SP-PILOT-01");

            Assert.Contains("SP-PILOT-01", window.Title);
            Assert.Equal("SP-PILOT-01", window.SuggestedFileName);
            return true;
        });
    }

    [Fact]
    public void Constructor_BlankFileName_FallsBackToDefault()
    {
        RunOnSta(() =>
        {
            var window = new SpoolSheetPreviewWindow(BuildGeometry(), "   ");

            Assert.Equal("spool-sheet", window.SuggestedFileName);
            return true;
        });
    }

    [Fact]
    public void Constructor_BuildsToolbarAndCanvas()
    {
        RunOnSta(() =>
        {
            var window = new SpoolSheetPreviewWindow(BuildGeometry(), "SP-01");

            // Window has a DockPanel root with a toolbar + canvas.
            Assert.NotNull(window.Content);
            var dock = Assert.IsType<System.Windows.Controls.DockPanel>(window.Content);
            Assert.Equal(2, dock.Children.Count);
            Assert.IsType<System.Windows.Controls.ToolBar>(dock.Children[0]);
            Assert.IsType<ElectricalComponentSandbox.Rendering.SkiaCanvasHost>(dock.Children[1]);
            return true;
        });
    }

    [Fact]
    public void Constructor_ToolbarHasAllExpectedCommands()
    {
        RunOnSta(() =>
        {
            var window = new SpoolSheetPreviewWindow(BuildGeometry(), "SP-01");

            var dock = (System.Windows.Controls.DockPanel)window.Content;
            var toolbar = (System.Windows.Controls.ToolBar)dock.Children[0];

            var labels = new HashSet<string>();
            foreach (var item in toolbar.Items)
            {
                if (item is System.Windows.Controls.Button b && b.Content is string s)
                    labels.Add(s);
            }
            Assert.Contains("Fit", labels);
            Assert.Contains("Print…", labels);
            Assert.Contains("Export XPS…", labels);
            Assert.Contains("Export PDF…", labels);
            return true;
        });
    }

    [Fact]
    public void ZoomBy_ChangesDrawingContextZoom()
    {
        RunOnSta(() =>
        {
            var window = new SpoolSheetPreviewWindow(BuildGeometry(), "SP-01");
            var dock = (System.Windows.Controls.DockPanel)window.Content;
            var host = (ElectricalComponentSandbox.Rendering.SkiaCanvasHost)dock.Children[1];

            // Force a layout pass so ActualWidth/ActualHeight are non-zero
            host.Measure(new System.Windows.Size(800, 600));
            host.Arrange(new System.Windows.Rect(0, 0, 800, 600));

            double initialZoom = host.DrawingContext.Zoom;
            window.ZoomBy(2.0);

            Assert.NotEqual(initialZoom, host.DrawingContext.Zoom);
            return true;
        });
    }

    [Fact]
    public void FitToView_NoLayoutYet_DoesNotCrash()
    {
        RunOnSta(() =>
        {
            var window = new SpoolSheetPreviewWindow(BuildGeometry(), "SP-01");

            // No layout — ActualWidth/ActualHeight are 0; FitToView should bail.
            window.FitToView();
            return true;
        });
    }

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
}
