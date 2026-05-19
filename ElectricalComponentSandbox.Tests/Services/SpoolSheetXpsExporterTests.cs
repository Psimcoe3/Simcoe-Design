using System.IO;
using System.Threading;
using System.Windows.Documents;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SpoolSheetXpsExporterTests
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
    public void BuildFixedDocument_NullGeometry_Throws()
    {
        RunOnSta(() =>
        {
            var exporter = new SpoolSheetXpsExporter();
            Assert.Throws<ArgumentNullException>(() => exporter.BuildFixedDocument(null!));
            return true;
        });
    }

    [Fact]
    public void BuildFixedDocument_HasOnePageAtAnsiBDipSize()
    {
        RunOnSta(() =>
        {
            var exporter = new SpoolSheetXpsExporter();
            var geometry = BuildGeometry();

            var document = exporter.BuildFixedDocument(geometry);

            Assert.Equal(1, document.Pages.Count);
            // ANSI B is 17 × 11 inches → 1632 × 1056 DIP at 96 DPI.
            Assert.Equal(17.0 * SpoolSheetXpsExporter.DipPerInch, document.DocumentPaginator.PageSize.Width, 1);
            Assert.Equal(11.0 * SpoolSheetXpsExporter.DipPerInch, document.DocumentPaginator.PageSize.Height, 1);
            return true;
        });
    }

    [Fact]
    public void BuildFixedDocument_PopulatesFixedPageChildren()
    {
        RunOnSta(() =>
        {
            var exporter = new SpoolSheetXpsExporter();
            var geometry = BuildGeometry();

            var document = exporter.BuildFixedDocument(geometry);
            var page = (FixedPage)document.Pages[0].Child;

            // Each geometry rect/text becomes at least one FixedPage child.
            int expectedMin =
                geometry.Rects.Count +
                geometry.Texts.Count(t => !string.IsNullOrEmpty(t.Value));
            Assert.True(page.Children.Count >= expectedMin,
                $"expected at least {expectedMin} children, got {page.Children.Count}");
            return true;
        });
    }

    [Fact]
    public void SaveToFile_WritesValidXpsFile()
    {
        RunOnSta(() =>
        {
            var path = Path.Combine(Path.GetTempPath(),
                $"spool-sheet-test-{Guid.NewGuid():N}.xps");
            try
            {
                var exporter = new SpoolSheetXpsExporter();
                var geometry = BuildGeometry();
                exporter.SaveToFile(geometry, path);

                Assert.True(File.Exists(path));
                var info = new FileInfo(path);
                Assert.True(info.Length > 1024, "XPS file should contain non-trivial content");
                return true;
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });
    }

    [Fact]
    public void SaveToFile_OverwritesExistingFile()
    {
        RunOnSta(() =>
        {
            var path = Path.Combine(Path.GetTempPath(),
                $"spool-sheet-test-{Guid.NewGuid():N}.xps");
            try
            {
                File.WriteAllText(path, "placeholder");
                var exporter = new SpoolSheetXpsExporter();
                var geometry = BuildGeometry();
                exporter.SaveToFile(geometry, path);

                Assert.True(File.Exists(path));
                // Should now be a real XPS file (much bigger than the placeholder)
                Assert.True(new FileInfo(path).Length > 1024);
                return true;
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });
    }

    [Fact]
    public void SaveToFile_NullPath_Throws()
    {
        RunOnSta(() =>
        {
            var exporter = new SpoolSheetXpsExporter();
            var geometry = BuildGeometry();
            Assert.Throws<ArgumentException>(() => exporter.SaveToFile(geometry, "   "));
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
