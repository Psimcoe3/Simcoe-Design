using System.IO;
using ElectricalComponentSandbox.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElectricalComponentSandbox.Services;

public class ComponentFileService
{
    private const string FileFilter = "Component Files (*.ecomp)|*.ecomp|All Files (*.*)|*.*";
    
    public async Task<ElectricalComponent?> LoadComponentAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var components = DeserializeComponentsFromJson(json);
            return components.Count > 0 ? components[0] : null;
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

    public async Task ExportToJsonAsync(IReadOnlyList<ElectricalComponent> components, string filePath)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                Formatting = Formatting.Indented
            };

            var json = JsonConvert.SerializeObject(components, settings);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save component: {ex.Message}", ex);
        }
    }

    public async Task<IReadOnlyList<ElectricalComponent>> LoadComponentsFromJsonAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return DeserializeComponentsFromJson(json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load components: {ex.Message}", ex);
        }
    }

    internal static IReadOnlyList<ElectricalComponent> DeserializeComponentsFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ElectricalComponent>();

        var token = JToken.Parse(json);
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
        };

        return token.Type switch
        {
            JTokenType.Array => DeserializeComponentArray((JArray)token, settings),
            JTokenType.Object => DeserializeComponentSingle((JObject)token, settings),
            _ => throw new InvalidOperationException("Component JSON must be an object or array."),
        };
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

    private static ElectricalComponent DeserializeComponentObject(JObject jsonObject, JsonSerializerSettings settings)
    {
        var typeToken = jsonObject[nameof(ElectricalComponent.Type)];
        if (typeToken == null)
            throw new InvalidOperationException("Component JSON does not include a Type field.");

        var componentType = ParseComponentType(typeToken);
        return componentType switch
        {
            ComponentType.Conduit => DeserializeTyped<ConduitComponent>(jsonObject, settings),
            ComponentType.Box => DeserializeTyped<BoxComponent>(jsonObject, settings),
            ComponentType.Panel => DeserializeTyped<PanelComponent>(jsonObject, settings),
            ComponentType.Support => DeserializeTyped<SupportComponent>(jsonObject, settings),
            ComponentType.CableTray => DeserializeTyped<CableTrayComponent>(jsonObject, settings),
            ComponentType.Hanger => DeserializeTyped<HangerComponent>(jsonObject, settings),
            ComponentType.Transformer => DeserializeTyped<TransformerComponent>(jsonObject, settings),
            ComponentType.Bus => DeserializeTyped<BusComponent>(jsonObject, settings),
            ComponentType.PowerSource => DeserializeTyped<PowerSourceComponent>(jsonObject, settings),
            ComponentType.TransferSwitch => DeserializeTyped<TransferSwitchComponent>(jsonObject, settings),
            _ => throw new InvalidOperationException($"Unsupported component type '{componentType}'."),
        };
    }

    private static IReadOnlyList<ElectricalComponent> DeserializeComponentArray(JArray array, JsonSerializerSettings settings)
    {
        try
        {
            var fromTypeInfo = JsonConvert.DeserializeObject<List<ElectricalComponent>>(array.ToString(), settings);
            if (fromTypeInfo != null && fromTypeInfo.Count > 0)
                return fromTypeInfo;
        }
        catch (JsonSerializationException)
        {
            // Fallback to explicit type discrimination below.
        }

        return array
            .OfType<JObject>()
            .Select(obj => DeserializeComponentObject(obj, settings))
            .ToList();
    }

    private static IReadOnlyList<ElectricalComponent> DeserializeComponentSingle(JObject obj, JsonSerializerSettings settings)
    {
        try
        {
            var fromTypeInfo = JsonConvert.DeserializeObject<ElectricalComponent>(obj.ToString(), settings);
            if (fromTypeInfo != null)
                return new[] { fromTypeInfo };
        }
        catch (JsonSerializationException)
        {
            // Fallback to explicit type discrimination below.
        }

        return new[] { DeserializeComponentObject(obj, settings) };
    }

    private static T DeserializeTyped<T>(JObject jsonObject, JsonSerializerSettings settings)
        where T : ElectricalComponent
    {
        return JsonConvert.DeserializeObject<T>(jsonObject.ToString(), settings)
            ?? throw new InvalidOperationException($"Failed to deserialize component type '{typeof(T).Name}'.");
    }

    private static ComponentType ParseComponentType(JToken typeToken)
    {
        if (typeToken.Type == JTokenType.Integer)
            return (ComponentType)typeToken.Value<int>();

        var text = typeToken.Value<string>();
        if (Enum.TryParse<ComponentType>(text, true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Unsupported component type value '{text}'.");
    }
    
    public static string GetFileFilter() => FileFilter;
}
