using System.IO;
using ElectricalComponentSandbox.Models;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Services;

public class ComponentFileService
{
    private const string FileFilter = "Component Files (*.ecomp)|*.ecomp|All Files (*.*)|*.*";
    
    public async Task<ElectricalComponent?> LoadComponentAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            };
            return JsonConvert.DeserializeObject<ElectricalComponent>(json, settings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load component: {ex.Message}", ex);
        }
    }
    
    public async Task SaveComponentAsync(ElectricalComponent component, string filePath)
    {
        await SerializeComponentAsync(component, filePath, includeTypeInfo: true);
    }
    
    public async Task ExportToJsonAsync(ElectricalComponent component, string filePath)
    {
        await SerializeComponentAsync(component, filePath, includeTypeInfo: false);
    }
    
    private async Task SerializeComponentAsync(ElectricalComponent component, string filePath, bool includeTypeInfo)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = includeTypeInfo ? TypeNameHandling.Auto : TypeNameHandling.None,
                Formatting = Formatting.Indented
            };
            var json = includeTypeInfo
                ? JsonConvert.SerializeObject(component, typeof(ElectricalComponent), settings)
                : JsonConvert.SerializeObject(component, settings);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save component: {ex.Message}", ex);
        }
    }
    
    public static string GetFileFilter() => FileFilter;
}
