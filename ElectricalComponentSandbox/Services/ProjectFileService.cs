using System.IO;
using ElectricalComponentSandbox.Models;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Handles saving and loading full project files (JSON)
/// </summary>
public class ProjectFileService
{
    private const string FileFilter = "Project Files (*.ecproj)|*.ecproj|All Files (*.*)|*.*";
    
    /// <summary>
    /// Saves a project to JSON file
    /// </summary>
    public async Task SaveProjectAsync(ProjectModel project, string filePath)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented
            };
            var json = JsonConvert.SerializeObject(project, settings);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save project: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Loads a project from a JSON file
    /// </summary>
    public async Task<ProjectModel?> LoadProjectAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            };
            return JsonConvert.DeserializeObject<ProjectModel>(json, settings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load project: {ex.Message}", ex);
        }
    }
    
    public static string GetFileFilter() => FileFilter;
}
