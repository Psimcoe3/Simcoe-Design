using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Persistence;

/// <summary>
/// Serializable snapshot of the entire conduit model for JSON persistence.
/// </summary>
public class ConduitProjectData
{
    public string Version { get; set; } = "1.0";
    public DateTime SavedUtc { get; set; } = DateTime.UtcNow;

    public ConduitSettings Settings { get; set; } = new();
    public List<ConduitTypeData> ConduitTypes { get; set; } = new();
    public List<ConduitSegmentData> Segments { get; set; } = new();
    public List<ConduitFittingData> Fittings { get; set; } = new();
    public List<ConduitRunData> Runs { get; set; } = new();
}

public class ConduitTypeData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Standard { get; set; } = "EMT";
    public bool IsWithFitting { get; set; } = true;
}

public class ConduitSegmentData
{
    public string Id { get; set; } = string.Empty;
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    public string LevelId { get; set; } = string.Empty;
    public string ConduitTypeId { get; set; } = string.Empty;
    public double Diameter { get; set; }
    public string TradeSize { get; set; } = string.Empty;
    public string Material { get; set; } = "EMT";
}

public class ConduitFittingData
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double LocationX { get; set; }
    public double LocationY { get; set; }
    public double LocationZ { get; set; }
    public double AngleDegrees { get; set; }
    public string TradeSize { get; set; } = string.Empty;
    public double BendRadius { get; set; }
    public double DeductLength { get; set; }
    public List<string> ConnectedSegmentIds { get; set; } = new();
}

public class ConduitRunData
{
    public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public List<string> SegmentIds { get; set; } = new();
    public List<string> FittingIds { get; set; } = new();
    public string StartEquipment { get; set; } = string.Empty;
    public string EndEquipment { get; set; } = string.Empty;
    public string Voltage { get; set; } = string.Empty;
    public double ConductorFillPercent { get; set; }
    public string ConduitTypeId { get; set; } = string.Empty;
    public string TradeSize { get; set; } = string.Empty;
    public string Material { get; set; } = "EMT";
    public string LevelId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Serializes and deserializes the conduit model to/from JSON.
/// </summary>
public static class ConduitPersistence
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes the conduit model store to JSON.
    /// </summary>
    public static string SerializeToJson(ConduitModelStore store)
    {
        var data = new ConduitProjectData
        {
            Settings = store.Settings,
            ConduitTypes = store.GetAllTypes().Select(t => new ConduitTypeData
            {
                Id = t.Id,
                Name = t.Name,
                Standard = t.Standard.ToString(),
                IsWithFitting = t.IsWithFitting
            }).ToList(),
            Segments = store.GetAllSegments().Select(s => new ConduitSegmentData
            {
                Id = s.Id,
                StartX = s.StartPoint.X, StartY = s.StartPoint.Y, StartZ = s.StartPoint.Z,
                EndX = s.EndPoint.X, EndY = s.EndPoint.Y, EndZ = s.EndPoint.Z,
                LevelId = s.LevelId,
                ConduitTypeId = s.ConduitTypeId,
                Diameter = s.Diameter,
                TradeSize = s.TradeSize,
                Material = s.Material.ToString()
            }).ToList(),
            Fittings = store.GetAllFittings().Select(f => new ConduitFittingData
            {
                Id = f.Id,
                Type = f.Type.ToString(),
                LocationX = f.Location.X, LocationY = f.Location.Y, LocationZ = f.Location.Z,
                AngleDegrees = f.AngleDegrees,
                TradeSize = f.TradeSize,
                BendRadius = f.BendRadius,
                DeductLength = f.DeductLength,
                ConnectedSegmentIds = f.ConnectedSegmentIds
            }).ToList(),
            Runs = store.GetAllRuns().Select(r => new ConduitRunData
            {
                Id = r.Id,
                RunId = r.RunId,
                SegmentIds = r.SegmentIds,
                FittingIds = r.FittingIds,
                StartEquipment = r.StartEquipment,
                EndEquipment = r.EndEquipment,
                Voltage = r.Voltage,
                ConductorFillPercent = r.ConductorFillPercent,
                ConduitTypeId = r.ConduitTypeId,
                TradeSize = r.TradeSize,
                Material = r.Material.ToString(),
                LevelId = r.LevelId,
                Metadata = r.Metadata
            }).ToList()
        };

        return JsonSerializer.Serialize(data, Options);
    }

    /// <summary>
    /// Deserializes JSON into a conduit model store.
    /// </summary>
    public static ConduitModelStore DeserializeFromJson(string json)
    {
        var data = JsonSerializer.Deserialize<ConduitProjectData>(json, Options)
                   ?? throw new InvalidOperationException("Failed to deserialize conduit data.");

        var store = new ConduitModelStore { Settings = data.Settings };

        foreach (var td in data.ConduitTypes)
        {
            Enum.TryParse<ConduitMaterialType>(td.Standard, out var standard);
            store.AddType(new ConduitType
            {
                Id = td.Id,
                Name = td.Name,
                Standard = standard,
                IsWithFitting = td.IsWithFitting
            });
        }

        foreach (var sd in data.Segments)
        {
            Enum.TryParse<ConduitMaterialType>(sd.Material, out var material);
            store.AddSegment(new ConduitSegment
            {
                Id = sd.Id,
                StartPoint = new XYZ(sd.StartX, sd.StartY, sd.StartZ),
                EndPoint = new XYZ(sd.EndX, sd.EndY, sd.EndZ),
                LevelId = sd.LevelId,
                ConduitTypeId = sd.ConduitTypeId,
                Diameter = sd.Diameter,
                TradeSize = sd.TradeSize,
                Material = material
            });
        }

        foreach (var fd in data.Fittings)
        {
            Enum.TryParse<FittingType>(fd.Type, out var fittingType);
            store.AddFitting(new ConduitFitting
            {
                Id = fd.Id,
                Type = fittingType,
                Location = new XYZ(fd.LocationX, fd.LocationY, fd.LocationZ),
                AngleDegrees = fd.AngleDegrees,
                TradeSize = fd.TradeSize,
                BendRadius = fd.BendRadius,
                DeductLength = fd.DeductLength,
                ConnectedSegmentIds = fd.ConnectedSegmentIds
            });
        }

        foreach (var rd in data.Runs)
        {
            Enum.TryParse<ConduitMaterialType>(rd.Material, out var material);
            store.AddRun(new ConduitRun
            {
                Id = rd.Id,
                RunId = rd.RunId,
                SegmentIds = rd.SegmentIds,
                FittingIds = rd.FittingIds,
                StartEquipment = rd.StartEquipment,
                EndEquipment = rd.EndEquipment,
                Voltage = rd.Voltage,
                ConductorFillPercent = rd.ConductorFillPercent,
                ConduitTypeId = rd.ConduitTypeId,
                TradeSize = rd.TradeSize,
                Material = material,
                LevelId = rd.LevelId,
                Metadata = rd.Metadata
            });
        }

        store.Connectivity.AutoConnect(store.Settings.ConnectionTolerance);
        return store;
    }

    /// <summary>
    /// Saves the conduit model store to a file.
    /// </summary>
    public static async Task SaveToFileAsync(ConduitModelStore store, string filePath)
    {
        var json = SerializeToJson(store);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads a conduit model store from a file.
    /// </summary>
    public static async Task<ConduitModelStore> LoadFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return DeserializeFromJson(json);
    }
}
