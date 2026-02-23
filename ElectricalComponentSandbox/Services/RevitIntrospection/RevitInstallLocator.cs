using System.IO;

namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public interface IRevitInstallLocator
{
    string? LocateInstallPath(string? overridePath = null);
    IReadOnlyList<string> DiscoverCandidateInstallPaths(string? overridePath = null);
}

public sealed class RevitInstallLocator : IRevitInstallLocator
{
    private static readonly string AutodeskRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Autodesk");

    public string? LocateInstallPath(string? overridePath = null)
    {
        var candidates = DiscoverCandidateInstallPaths(overridePath);
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public IReadOnlyList<string> DiscoverCandidateInstallPaths(string? overridePath = null)
    {
        var candidates = new List<string>();

        AddCandidate(candidates, overridePath);
        AddCandidate(candidates, Environment.GetEnvironmentVariable(RevitIntrospectionOptions.InstallPathEnvVar));
        AddCandidate(candidates, Path.Combine(AutodeskRoot, "Revit 2024"));

        if (Directory.Exists(AutodeskRoot))
        {
            var revitFolders = Directory.GetDirectories(AutodeskRoot, "Revit *", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase);
            foreach (var folder in revitFolders)
                AddCandidate(candidates, folder);
        }

        return candidates;
    }

    private static void AddCandidate(ICollection<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var normalized = path.Trim().Trim('"');
        if (normalized.Length == 0)
            return;

        if (candidates.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        candidates.Add(normalized);
    }
}
