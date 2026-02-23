using System.IO;
using ElectricalComponentSandbox.Services.RevitIntrospection;

namespace ElectricalComponentSandbox.Tests.Services.RevitIntrospection;

public sealed class RevitBinaryCatalogServiceTests : IDisposable
{
    private readonly string _tempDir;

    public RevitBinaryCatalogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ECS_RevitCatalog_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void BuildCatalog_MissingFiles_AreReportedAsMissing()
    {
        var service = new RevitBinaryCatalogService();
        var catalog = service.BuildCatalog(_tempDir, ["RevitDB.dll", "ASMAHL229A.dll"]);

        Assert.Equal(_tempDir, catalog.InstallPath);
        Assert.Equal(2, catalog.Entries.Count);
        Assert.All(catalog.Entries, entry => Assert.Equal(RevitBinaryClassification.Missing, entry.Classification));
    }

    [Fact]
    public void BuildCatalog_ManagedAndNativeFiles_AreClassified()
    {
        var managedSource = typeof(RevitBinaryCatalogServiceTests).Assembly.Location;
        var managedTarget = Path.Combine(_tempDir, "RevitDB.dll");
        File.Copy(managedSource, managedTarget, overwrite: true);

        var nativeTarget = Path.Combine(_tempDir, "ASMAHL229A.dll");
        File.WriteAllText(nativeTarget, "native-placeholder");

        var service = new RevitBinaryCatalogService();
        var catalog = service.BuildCatalog(_tempDir, ["RevitDB.dll", "ASMAHL229A.dll"]);

        var revitDb = Assert.Single(catalog.Entries.Where(entry => entry.FileName == "RevitDB.dll"));
        Assert.Equal(RevitBinaryClassification.ManagedAssembly, revitDb.Classification);
        Assert.True(revitDb.Exists);

        var asm = Assert.Single(catalog.Entries.Where(entry => entry.FileName == "ASMAHL229A.dll"));
        Assert.Equal(RevitBinaryClassification.NativeBinary, asm.Classification);
        Assert.True(asm.Exists);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
