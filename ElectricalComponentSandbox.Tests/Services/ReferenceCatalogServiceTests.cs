using System;
using System.IO;
using System.Linq;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class ReferenceCatalogServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ReferenceCatalogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ecs-reference-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        ReferenceCatalogService.ClearReferenceDocsRootOverride();
        Environment.SetEnvironmentVariable(ReferenceCatalogService.ReferenceRootEnvVar, null);
    }

    [Fact]
    public void ResolveLaunchTarget_HttpUrl_PreservesExternalTarget()
    {
        var resolution = ReferenceCatalogService.ResolveLaunchTarget("https://example.com/reference.pdf", _tempDir);

        Assert.True(resolution.Success);
        Assert.Equal("https://example.com/reference.pdf", resolution.LaunchTarget);
        Assert.False(resolution.IsDirectory);
    }

    [Fact]
    public void ResolveLaunchTarget_RepoRelativePath_ResolvesAgainstWorkspaceRoot()
    {
        var workspaceRoot = CreateWorkspace();
        var relativePath = Path.Combine("References", "docs", "2026_national_electrical_estimator_ebook.pdf");
        var absolutePath = Path.Combine(workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, "estimator");

        var resolution = ReferenceCatalogService.ResolveLaunchTarget(relativePath, workspaceRoot);

        Assert.True(resolution.Success);
        Assert.Equal(Path.GetFullPath(absolutePath), resolution.LaunchTarget);
    }

    [Fact]
    public void ResolveLaunchTarget_UsesConfiguredReferenceDocsRootOverride()
    {
        var workspaceRoot = CreateWorkspace();
        var docsRoot = Path.Combine(workspaceRoot, "References", "docs");
        File.WriteAllText(Path.Combine(docsRoot, "2026_national_electrical_estimator_ebook.pdf"), "estimator");

        var set = ReferenceCatalogService.TrySetReferenceDocsRootOverride(docsRoot, out var normalizedRoot, out var errorMessage);

        Assert.True(set, errorMessage);
        Assert.Equal(docsRoot, normalizedRoot);

        var resolution = ReferenceCatalogService.ResolveLaunchTarget(@"References\docs\2026_national_electrical_estimator_ebook.pdf");

        Assert.True(resolution.Success);
        Assert.Equal(Path.Combine(docsRoot, "2026_national_electrical_estimator_ebook.pdf"), resolution.LaunchTarget);
    }

    [Fact]
    public void ResolveLaunchTarget_ExplicitWorkspaceRoot_WinsOverConfiguredOverride()
    {
        var workspaceRoot = CreateWorkspace();
        var overrideWorkspaceRoot = Path.Combine(_tempDir, "override-workspace");
        var overrideDocsRoot = Path.Combine(overrideWorkspaceRoot, "References", "docs");
        Directory.CreateDirectory(overrideDocsRoot);

        var relativePath = Path.Combine("References", "docs", "2026_national_electrical_estimator_ebook.pdf");
        var expectedPath = Path.Combine(workspaceRoot, relativePath);
        File.WriteAllText(expectedPath, "workspace-root");
        File.WriteAllText(Path.Combine(overrideDocsRoot, "2026_national_electrical_estimator_ebook.pdf"), "override-root");

        var set = ReferenceCatalogService.TrySetReferenceDocsRootOverride(overrideDocsRoot, out _, out var errorMessage);
        Assert.True(set, errorMessage);

        var resolution = ReferenceCatalogService.ResolveLaunchTarget(relativePath, workspaceRoot);

        Assert.True(resolution.Success);
        Assert.Equal(Path.GetFullPath(expectedPath), resolution.LaunchTarget);
    }

    [Fact]
    public void ResolveLaunchTarget_MissingRepoRelativePath_ReturnsClearError()
    {
        var workspaceRoot = CreateWorkspace();

        var resolution = ReferenceCatalogService.ResolveLaunchTarget(Path.Combine("References", "docs", "missing.pdf"), workspaceRoot);

        Assert.False(resolution.Success);
        Assert.Contains("Reference target was not found", resolution.ErrorMessage);
        Assert.EndsWith("missing.pdf", resolution.DisplayTarget, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCuratedEntries_IncludesEstimatorAndTopLevelElectricalMaterialItems()
    {
        var workspaceRoot = CreateWorkspace();
        var docsRoot = Path.Combine(workspaceRoot, "References", "docs");
        File.WriteAllText(Path.Combine(docsRoot, "2026_national_electrical_estimator_ebook.pdf"), "estimator");

        var electricalMaterialRoot = Path.Combine(docsRoot, "Electrical Material");
        Directory.CreateDirectory(electricalMaterialRoot);
        Directory.CreateDirectory(Path.Combine(electricalMaterialRoot, "NCCER Electrical Guides"));
        File.WriteAllText(Path.Combine(electricalMaterialRoot, "NFPA 70 2023.pdf"), "code");
        File.WriteAllText(Path.Combine(electricalMaterialRoot, "NFPA 70E 2024.pdf"), "safety");

        var entries = ReferenceCatalogService.GetCuratedEntries(workspaceRoot);

        Assert.Collection(entries,
            estimator =>
            {
                Assert.Equal("2026 National Electrical Estimator Ebook", estimator.DisplayName);
                Assert.False(estimator.IsDirectory);
            },
            electricalMaterial =>
            {
                Assert.Equal("Electrical Material", electricalMaterial.DisplayName);
                Assert.True(electricalMaterial.IsDirectory);
                Assert.NotNull(electricalMaterial.Children);
                Assert.Contains(electricalMaterial.Children!, child => child.DisplayName == "NCCER Electrical Guides" && child.IsDirectory);
                Assert.Contains(electricalMaterial.Children!, child => child.DisplayName == "NFPA 70 2023.pdf" && !child.IsDirectory);
                Assert.Contains(electricalMaterial.Children!, child => child.DisplayName == "NFPA 70E 2024.pdf" && !child.IsDirectory);
            });
    }

    [Fact]
    public void GetAssignableEntries_FlattensCuratedRootsAndTopLevelChildren()
    {
        var workspaceRoot = CreateWorkspace();
        var docsRoot = Path.Combine(workspaceRoot, "References", "docs");
        File.WriteAllText(Path.Combine(docsRoot, "2026_national_electrical_estimator_ebook.pdf"), "estimator");

        var electricalMaterialRoot = Path.Combine(docsRoot, "Electrical Material");
        Directory.CreateDirectory(electricalMaterialRoot);
        Directory.CreateDirectory(Path.Combine(electricalMaterialRoot, "NJATC School Materials"));
        File.WriteAllText(Path.Combine(electricalMaterialRoot, "NFPA 70 2023.pdf"), "code");

        var entries = ReferenceCatalogService.GetAssignableEntries(workspaceRoot);

        Assert.Contains(entries, entry => entry.DisplayName == "2026 National Electrical Estimator Ebook" && !entry.IsDirectory);
        Assert.Contains(entries, entry => entry.DisplayName == "Electrical Material" && entry.IsDirectory);
        Assert.Contains(entries, entry => entry.DisplayName == "NJATC School Materials" && entry.IsDirectory);
        Assert.Contains(entries, entry => entry.DisplayName == "NFPA 70 2023.pdf" && !entry.IsDirectory);
    }

    [Fact]
    public void TryFindWorkspaceRoot_WalksUpFromNestedDirectory()
    {
        var workspaceRoot = CreateWorkspace();
        var nestedDirectory = Path.Combine(workspaceRoot, "ElectricalComponentSandbox", "bin", "Debug", "net8.0-windows");
        Directory.CreateDirectory(nestedDirectory);

        var resolved = ReferenceCatalogService.TryFindWorkspaceRoot(nestedDirectory);

        Assert.Equal(workspaceRoot, resolved);
    }

    [Fact]
    public void TrySetReferenceDocsRootOverride_AcceptsWorkspaceRootAndNormalizesToDocsFolder()
    {
        var workspaceRoot = CreateWorkspace();

        var set = ReferenceCatalogService.TrySetReferenceDocsRootOverride(workspaceRoot, out var normalizedRoot, out var errorMessage);

        Assert.True(set, errorMessage);
        Assert.Equal(Path.Combine(workspaceRoot, "References", "docs"), normalizedRoot);
        Assert.Equal(normalizedRoot, ReferenceCatalogService.GetReferenceDocsRoot());
    }

    [Fact]
    public void GetReferenceDocsRoot_UsesEnvironmentVariableWhenOverrideIsUnset()
    {
        var workspaceRoot = CreateWorkspace();
        var docsRoot = Path.Combine(workspaceRoot, "References", "docs");
        Environment.SetEnvironmentVariable(ReferenceCatalogService.ReferenceRootEnvVar, docsRoot);

        var resolvedRoot = ReferenceCatalogService.GetReferenceDocsRoot();

        Assert.Equal(docsRoot, resolvedRoot);
    }

    private string CreateWorkspace()
    {
        var workspaceRoot = Path.Combine(_tempDir, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "References", "docs"));
        File.WriteAllText(Path.Combine(workspaceRoot, "ElectricalComponentSandbox.slnx"), string.Empty);
        return workspaceRoot;
    }

    public void Dispose()
    {
        ReferenceCatalogService.ClearReferenceDocsRootOverride();
        Environment.SetEnvironmentVariable(ReferenceCatalogService.ReferenceRootEnvVar, null);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}