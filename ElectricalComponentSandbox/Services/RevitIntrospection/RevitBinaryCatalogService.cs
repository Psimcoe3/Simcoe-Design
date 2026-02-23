using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public interface IRevitBinaryCatalogService
{
    RevitBinaryCatalog BuildCatalog(string installPath, IReadOnlyList<string> targetFileNames);
}

public sealed class RevitBinaryCatalogService : IRevitBinaryCatalogService
{
    public RevitBinaryCatalog BuildCatalog(string installPath, IReadOnlyList<string> targetFileNames)
    {
        var root = installPath ?? string.Empty;
        var entries = new List<RevitBinaryInfo>();

        foreach (var fileName in targetFileNames)
        {
            var path = Path.Combine(root, fileName);
            if (!File.Exists(path))
            {
                entries.Add(new RevitBinaryInfo(
                    fileName,
                    path,
                    Exists: false,
                    Classification: RevitBinaryClassification.Missing,
                    FileSizeBytes: null,
                    FileVersion: null));
                continue;
            }

            var fileInfo = new FileInfo(path);
            entries.Add(new RevitBinaryInfo(
                fileName,
                path,
                Exists: true,
                Classification: ClassifyBinary(path),
                FileSizeBytes: fileInfo.Length,
                FileVersion: SafeReadFileVersion(path)));
        }

        return new RevitBinaryCatalog
        {
            InstallPath = root,
            Entries = entries
        };
    }

    private static RevitBinaryClassification ClassifyBinary(string path)
    {
        try
        {
            _ = AssemblyName.GetAssemblyName(path);
            return RevitBinaryClassification.ManagedAssembly;
        }
        catch (BadImageFormatException)
        {
            return RevitBinaryClassification.NativeBinary;
        }
        catch (FileLoadException)
        {
            // Metadata exists but load context/security prevented loading.
            return RevitBinaryClassification.ManagedAssembly;
        }
        catch
        {
            return RevitBinaryClassification.NativeBinary;
        }
    }

    private static string? SafeReadFileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch
        {
            return null;
        }
    }
}
