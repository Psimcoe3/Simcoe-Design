using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

public sealed record ReferenceCatalogEntry(
    string DisplayName,
    string RelativePath,
    bool IsDirectory,
    IReadOnlyList<ReferenceCatalogEntry>? Children = null);

public sealed record ReferenceLaunchResolution(
    bool Success,
    string? LaunchTarget,
    string DisplayTarget,
    string ErrorMessage,
    bool IsDirectory);

public static class ReferenceCatalogService
{
    private const string SolutionFileName = "ElectricalComponentSandbox.slnx";

    public static IReadOnlyList<ReferenceCatalogEntry> GetCuratedEntries(string? workspaceRoot = null)
    {
        var resolvedWorkspaceRoot = workspaceRoot ?? TryFindWorkspaceRoot();
        const string estimatorRelativePath = "References\\docs\\2026_national_electrical_estimator_ebook.pdf";
        const string electricalMaterialRelativePath = "References\\docs\\Electrical Material";

        return new[]
        {
            new ReferenceCatalogEntry(
                "2026 National Electrical Estimator Ebook",
                estimatorRelativePath,
                IsDirectory: false),
            BuildElectricalMaterialEntry(electricalMaterialRelativePath, resolvedWorkspaceRoot)
        };
    }

    public static IReadOnlyList<ReferenceCatalogEntry> GetAssignableEntries(string? workspaceRoot = null)
    {
        var curatedEntries = GetCuratedEntries(workspaceRoot);
        var flattened = new List<ReferenceCatalogEntry>();

        foreach (var entry in curatedEntries)
        {
            flattened.Add(entry with { Children = null });

            if (entry.Children == null)
                continue;

            flattened.AddRange(entry.Children);
        }

        return flattened;
    }

    public static ReferenceLaunchResolution ResolveLaunchTarget(string reference, string? workspaceRoot = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return new ReferenceLaunchResolution(
                Success: false,
                LaunchTarget: null,
                DisplayTarget: string.Empty,
                ErrorMessage: "No reference target was provided.",
                IsDirectory: false);
        }

        var trimmedReference = reference.Trim();
        if (Uri.TryCreate(trimmedReference, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
                return ResolveFileSystemTarget(uri.LocalPath);

            if (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeMailto)
            {
                return new ReferenceLaunchResolution(
                    Success: true,
                    LaunchTarget: trimmedReference,
                    DisplayTarget: trimmedReference,
                    ErrorMessage: string.Empty,
                    IsDirectory: false);
            }
        }

        var candidatePath = Path.IsPathRooted(trimmedReference)
            ? trimmedReference
            : Path.Combine(workspaceRoot ?? TryFindWorkspaceRoot() ?? Directory.GetCurrentDirectory(), NormalizeRelativePath(trimmedReference));

        return ResolveFileSystemTarget(candidatePath);
    }

    public static string? TryFindWorkspaceRoot(params string[]? startDirectories)
    {
        var candidates = new List<string>();
        if (startDirectories != null)
        {
            candidates.AddRange(startDirectories.Where(path => !string.IsNullOrWhiteSpace(path))!);
        }

        candidates.Add(Environment.CurrentDirectory);
        candidates.Add(AppContext.BaseDirectory);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = File.Exists(candidate)
                ? Path.GetDirectoryName(candidate)
                : candidate;

            while (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                if (File.Exists(Path.Combine(directory, SolutionFileName)) &&
                    Directory.Exists(Path.Combine(directory, "References", "docs")))
                {
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }
        }

        return null;
    }

    private static ReferenceCatalogEntry BuildElectricalMaterialEntry(string relativePath, string? workspaceRoot)
    {
        var children = Array.Empty<ReferenceCatalogEntry>();
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
        {
            var absolutePath = Path.Combine(workspaceRoot, NormalizeRelativePath(relativePath));
            if (Directory.Exists(absolutePath))
            {
                children = Directory.GetDirectories(absolutePath)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .Select(path => new ReferenceCatalogEntry(
                        Path.GetFileName(path),
                        Path.Combine(relativePath, Path.GetFileName(path)),
                        IsDirectory: true))
                    .Concat(
                        Directory.GetFiles(absolutePath)
                            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                            .Select(path => new ReferenceCatalogEntry(
                                Path.GetFileName(path),
                                Path.Combine(relativePath, Path.GetFileName(path)),
                                IsDirectory: false)))
                    .ToArray();
            }
        }

        return new ReferenceCatalogEntry(
            "Electrical Material",
            relativePath,
            IsDirectory: true,
            Children: children);
    }

    private static ReferenceLaunchResolution ResolveFileSystemTarget(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            return new ReferenceLaunchResolution(
                Success: true,
                LaunchTarget: fullPath,
                DisplayTarget: fullPath,
                ErrorMessage: string.Empty,
                IsDirectory: false);
        }

        if (Directory.Exists(fullPath))
        {
            return new ReferenceLaunchResolution(
                Success: true,
                LaunchTarget: fullPath,
                DisplayTarget: fullPath,
                ErrorMessage: string.Empty,
                IsDirectory: true);
        }

        return new ReferenceLaunchResolution(
            Success: false,
            LaunchTarget: null,
            DisplayTarget: fullPath,
            ErrorMessage: $"Reference target was not found: {fullPath}",
            IsDirectory: false);
    }

    private static string NormalizeRelativePath(string relativePath)
        => relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}