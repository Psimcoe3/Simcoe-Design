using System.IO;
using System.Text;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SpoolSheetPdfExporterTests
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
    public void SaveToStream_NullGeometry_Throws()
    {
        var exporter = new SpoolSheetPdfExporter();
        using var ms = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => exporter.SaveToStream(null!, ms));
    }

    [Fact]
    public void SaveToStream_NullStream_Throws()
    {
        var exporter = new SpoolSheetPdfExporter();
        var geometry = BuildGeometry();
        Assert.Throws<ArgumentNullException>(() => exporter.SaveToStream(geometry, null!));
    }

    [Fact]
    public void SaveToFile_NullPath_Throws()
    {
        var exporter = new SpoolSheetPdfExporter();
        var geometry = BuildGeometry();
        Assert.Throws<ArgumentException>(() => exporter.SaveToFile(geometry, "  "));
    }

    [Fact]
    public void SaveToStream_WritesPdfMagicHeader()
    {
        var exporter = new SpoolSheetPdfExporter();
        var geometry = BuildGeometry();
        using var ms = new MemoryStream();

        exporter.SaveToStream(geometry, ms);

        Assert.True(ms.Length > 256, "expected a non-trivial PDF payload");
        var prefix = Encoding.ASCII.GetString(ms.GetBuffer(), 0, 4);
        Assert.Equal("%PDF", prefix);
    }

    [Fact]
    public void SaveToStream_ContainsEofMarker()
    {
        var exporter = new SpoolSheetPdfExporter();
        var geometry = BuildGeometry();
        using var ms = new MemoryStream();

        exporter.SaveToStream(geometry, ms);

        var bytes = ms.ToArray();
        // PDF trailer ends with "%%EOF" within the last 1024 bytes.
        int tailLen = Math.Min(bytes.Length, 1024);
        string tail = Encoding.ASCII.GetString(bytes, bytes.Length - tailLen, tailLen);
        Assert.Contains("%%EOF", tail);
    }

    [Fact]
    public void SaveToFile_WritesFileToDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spool-sheet-{Guid.NewGuid():N}.pdf");
        try
        {
            var exporter = new SpoolSheetPdfExporter();
            exporter.SaveToFile(BuildGeometry(), path);

            Assert.True(File.Exists(path));
            using var fs = File.OpenRead(path);
            var buf = new byte[4];
            int read = fs.Read(buf, 0, 4);
            Assert.Equal(4, read);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(buf));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveToFile_OverwritesExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spool-sheet-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllText(path, "placeholder");
            var exporter = new SpoolSheetPdfExporter();
            exporter.SaveToFile(BuildGeometry(), path);

            using var fs = File.OpenRead(path);
            var buf = new byte[4];
            fs.Read(buf, 0, 4);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(buf));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveToStream_ProducesByteIdenticalOutputForSameGeometry()
    {
        // Determinism: identical input geometry should yield identical PDFs
        // modulo a creation-date stamp embedded in the trailer. We strip
        // anything after the start-xref offset and compare the body.
        var exporter = new SpoolSheetPdfExporter();
        var geometry = BuildGeometry();

        using var first = new MemoryStream();
        using var second = new MemoryStream();
        exporter.SaveToStream(geometry, first);
        exporter.SaveToStream(geometry, second);

        // The body up to the first 'trailer' keyword should match.
        var a = first.ToArray();
        var b = second.ToArray();
        int aTrailer = IndexOfAscii(a, "trailer");
        int bTrailer = IndexOfAscii(b, "trailer");
        Assert.True(aTrailer > 0 && bTrailer > 0, "PDF must contain a trailer keyword");
        Assert.Equal(aTrailer, bTrailer);
        for (int i = 0; i < aTrailer; i++)
            Assert.Equal(a[i], b[i]);
    }

    private static int IndexOfAscii(byte[] haystack, string needle)
    {
        byte[] n = Encoding.ASCII.GetBytes(needle);
        for (int i = 0; i <= haystack.Length - n.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < n.Length; j++)
            {
                if (haystack[i + j] != n[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
