using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Per-classification load result for a single <see cref="LoadZone"/>.
/// </summary>
public class ZoneClassificationLoad
{
    public LoadClassification Classification { get; init; }
    public double DensityWPerSqFt { get; init; }
    public double AreaSqFt { get; init; }
    public double LoadVA { get; init; }
}

/// <summary>
/// Result of computing load for a single <see cref="LoadZone"/>.
/// </summary>
public class ZoneLoadResult
{
    public string ZoneId { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public double AreaSqFt { get; init; }
    public double TotalLoadVA { get; init; }
    public List<ZoneClassificationLoad> ClassificationLoads { get; init; } = new();
}

/// <summary>
/// Aggregate result from summing loads across multiple zones.
/// </summary>
public class ZoneLoadSummary
{
    public double TotalAreaSqFt { get; init; }
    public double TotalLoadVA { get; init; }
    public Dictionary<LoadClassification, double> ClassificationTotals { get; init; } = new();
    public List<ZoneLoadResult> ZoneResults { get; init; } = new();
}

/// <summary>
/// Service for creating and calculating area-based preliminary load zones,
/// analogous to Revit's <c>Zone.CreateAreaBasedLoad</c> API.
/// </summary>
public static class LoadZoneService
{
    /// <summary>
    /// Creates a new <see cref="LoadZone"/> from boundary points with a default
    /// load density for a single classification.
    /// </summary>
    public static LoadZone CreateZone(
        IEnumerable<XYZ> boundary,
        string? level = null,
        LoadClassification classification = LoadClassification.Power,
        double densityWPerSqFt = 0)
    {
        return new LoadZone
        {
            BoundaryPoints = boundary.ToList(),
            Level = level,
            LoadDensities = densityWPerSqFt > 0
                ? new() { [classification] = densityWPerSqFt }
                : new()
        };
    }

    /// <summary>
    /// Calculates the polygon area of a <see cref="LoadZone"/> in square feet
    /// using the shoelace formula, then multiplies by each density.
    /// </summary>
    public static ZoneLoadResult CalculateZoneLoad(LoadZone zone)
    {
        double area = CalculatePolygonArea(zone.BoundaryPoints);

        var classLoads = new List<ZoneClassificationLoad>();
        double total = 0;

        foreach (var (classification, density) in zone.LoadDensities)
        {
            double va = area * density;
            total += va;
            classLoads.Add(new ZoneClassificationLoad
            {
                Classification = classification,
                DensityWPerSqFt = density,
                AreaSqFt = area,
                LoadVA = va
            });
        }

        return new ZoneLoadResult
        {
            ZoneId = zone.Id,
            ZoneName = zone.Name,
            AreaSqFt = area,
            TotalLoadVA = total,
            ClassificationLoads = classLoads
        };
    }

    /// <summary>
    /// Sums the loads across multiple <see cref="LoadZone"/>s, returning a
    /// per-classification and overall total.
    /// </summary>
    public static ZoneLoadSummary SumZoneLoads(IEnumerable<LoadZone> zones)
    {
        var results = new List<ZoneLoadResult>();
        var totals = new Dictionary<LoadClassification, double>();
        double totalArea = 0;
        double totalVA = 0;

        foreach (var zone in zones)
        {
            var result = CalculateZoneLoad(zone);
            results.Add(result);
            totalArea += result.AreaSqFt;
            totalVA += result.TotalLoadVA;

            foreach (var cl in result.ClassificationLoads)
            {
                if (totals.ContainsKey(cl.Classification))
                    totals[cl.Classification] += cl.LoadVA;
                else
                    totals[cl.Classification] = cl.LoadVA;
            }
        }

        return new ZoneLoadSummary
        {
            TotalAreaSqFt = totalArea,
            TotalLoadVA = totalVA,
            ClassificationTotals = totals,
            ZoneResults = results
        };
    }

    /// <summary>
    /// Computes the area of a 2D polygon using the shoelace formula.
    /// Uses X and Y components of <see cref="XYZ"/> (Z is ignored).
    /// Returns 0 for fewer than 3 vertices.
    /// </summary>
    public static double CalculatePolygonArea(IReadOnlyList<XYZ> vertices)
    {
        if (vertices.Count < 3)
            return 0;

        double sum = 0;
        int n = vertices.Count;

        for (int i = 0; i < n; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % n];
            sum += current.X * next.Y - next.X * current.Y;
        }

        return Math.Abs(sum) / 2.0;
    }
}
