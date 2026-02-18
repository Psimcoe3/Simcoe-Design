using System.Windows.Media;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Conduit.Core.Geometry;
using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.UI;

/// <summary>
/// Generates WPF 3D visual elements for conduit runs using HelixToolkit-compatible geometry.
/// Manages LOD, override graphics, and background tessellation.
/// </summary>
public class Conduit3DViewManager
{
    private readonly ConduitModelStore _store;
    private readonly Dictionary<string, MeshGeometry3D> _meshCache = new();
    private readonly Dictionary<string, OverrideGraphicSettings> _overrides = new();

    /// <summary>Default conduit color.</summary>
    public Color DefaultColor { get; set; } = Colors.Silver;

    /// <summary>Default opacity.</summary>
    public double DefaultOpacity { get; set; } = 1.0;

    /// <summary>Camera distance for LOD computation.</summary>
    public double CameraDistance { get; set; } = 50.0;

    public Conduit3DViewManager(ConduitModelStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Sets graphic overrides for an element in this 3D view.
    /// </summary>
    public void SetOverride(string elementId, OverrideGraphicSettings settings) =>
        _overrides[elementId] = settings;

    /// <summary>
    /// Generates a MeshGeometry3D for a single conduit segment.
    /// Uses cached geometry if available and not dirty.
    /// </summary>
    public MeshGeometry3D GenerateSegmentMesh(ConduitSegment segment, bool forceRefresh = false)
    {
        if (!forceRefresh && _meshCache.TryGetValue(segment.Id, out var cached))
            return cached;

        var lod = ConduitTessellator.ComputeLOD(CameraDistance);
        var tessResult = ConduitTessellator.TessellateSegment(segment, lod);
        var mesh = ConvertToMeshGeometry3D(tessResult);

        _meshCache[segment.Id] = mesh;
        return mesh;
    }

    /// <summary>
    /// Generates a MeshGeometry3D for an entire conduit run (all segments + fittings).
    /// </summary>
    public MeshGeometry3D GenerateRunMesh(ConduitRun run, bool forceRefresh = false)
    {
        if (!forceRefresh && _meshCache.TryGetValue(run.Id, out var cached))
            return cached;

        var lod = ConduitTessellator.ComputeLOD(CameraDistance);
        var tessResult = ConduitTessellator.TessellateRun(run, _store, lod);
        var mesh = ConvertToMeshGeometry3D(tessResult);

        _meshCache[run.Id] = mesh;
        return mesh;
    }

    /// <summary>
    /// Gets the material for an element, considering overrides.
    /// </summary>
    public Material GetMaterial(string elementId)
    {
        var color = DefaultColor;
        double opacity = DefaultOpacity;

        if (_overrides.TryGetValue(elementId, out var ovr))
        {
            if (ovr.ColorOverride != null)
            {
                try
                {
                    color = (Color)ColorConverter.ConvertFromString(ovr.ColorOverride);
                }
                catch { /* keep default */ }
            }
            if (ovr.TransparencyOverride.HasValue)
                opacity = 1.0 - ovr.TransparencyOverride.Value;
        }

        var brush = new SolidColorBrush(color) { Opacity = opacity };
        return new DiffuseMaterial(brush);
    }

    /// <summary>
    /// Generates GeometryModel3D objects for all runs in the store.
    /// </summary>
    public List<GeometryModel3D> GenerateAllModels()
    {
        var models = new List<GeometryModel3D>();

        foreach (var run in _store.GetAllRuns())
        {
            if (_overrides.TryGetValue(run.Id, out var ovr) && ovr.IsHidden)
                continue;

            var mesh = GenerateRunMesh(run);
            var material = GetMaterial(run.Id);
            models.Add(new GeometryModel3D(mesh, material));
        }

        return models;
    }

    /// <summary>
    /// Invalidates cached geometry for a specific element.
    /// </summary>
    public void InvalidateCache(string elementId) => _meshCache.Remove(elementId);

    /// <summary>
    /// Invalidates all cached geometry.
    /// </summary>
    public void InvalidateAll() => _meshCache.Clear();

    /// <summary>
    /// Updates LOD based on new camera distance and refreshes if needed.
    /// </summary>
    public bool UpdateCameraDistance(double newDistance)
    {
        var oldLod = ConduitTessellator.ComputeLOD(CameraDistance);
        var newLod = ConduitTessellator.ComputeLOD(newDistance);
        CameraDistance = newDistance;

        if (oldLod != newLod)
        {
            InvalidateAll();
            return true; // Caller should refresh
        }
        return false;
    }

    private static MeshGeometry3D ConvertToMeshGeometry3D(TessellationResult tess)
    {
        var mesh = new MeshGeometry3D();

        foreach (var pos in tess.Positions)
            mesh.Positions.Add(pos);

        foreach (var idx in tess.TriangleIndices)
            mesh.TriangleIndices.Add(idx);

        foreach (var norm in tess.Normals)
            mesh.Normals.Add(norm);

        return mesh;
    }
}
