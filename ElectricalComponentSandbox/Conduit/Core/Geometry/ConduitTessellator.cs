using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Core.Geometry;

/// <summary>
/// Tessellation data for GPU rendering.
/// </summary>
public class TessellationResult
{
    public List<Point3D> Positions { get; set; } = new();
    public List<int> TriangleIndices { get; set; } = new();
    public List<Vector3D> Normals { get; set; } = new();
}

/// <summary>
/// Level of detail settings for 3D conduit tessellation.
/// </summary>
public enum LevelOfDetail
{
    /// <summary>4 sides – far distance</summary>
    Low = 4,
    /// <summary>8 sides – mid distance</summary>
    Medium = 8,
    /// <summary>16 sides – close</summary>
    High = 16,
    /// <summary>32 sides – very close / selection</summary>
    Ultra = 32
}

/// <summary>
/// Tessellates conduit segments into triangle meshes for 3D rendering.
/// Sweeps a circular cross-section along the segment centerline.
/// </summary>
public static class ConduitTessellator
{
    /// <summary>
    /// Determines LOD from camera distance to object center.
    /// </summary>
    public static LevelOfDetail ComputeLOD(double cameraDistance)
    {
        if (cameraDistance > 200) return LevelOfDetail.Low;
        if (cameraDistance > 50) return LevelOfDetail.Medium;
        if (cameraDistance > 10) return LevelOfDetail.High;
        return LevelOfDetail.Ultra;
    }

    /// <summary>
    /// Tessellates a single conduit segment as a cylinder.
    /// </summary>
    public static TessellationResult TessellateSegment(
        ConduitSegment segment, LevelOfDetail lod = LevelOfDetail.High)
    {
        int sides = (int)lod;
        double radius = segment.Diameter / 2.0 / 12.0; // inches to feet

        var start = ToPoint3D(segment.StartPoint);
        var end = ToPoint3D(segment.EndPoint);
        var dir = new Vector3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
        dir.Normalize();

        GetPerpendicularVectors(dir, out var right, out var up);

        var result = new TessellationResult();

        // Start ring
        AddRing(result, start, right, up, radius, sides);
        // End ring
        AddRing(result, end, right, up, radius, sides);

        // Side triangles
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int a = i;
            int b = next;
            int c = sides + i;
            int d = sides + next;

            result.TriangleIndices.AddRange(new[] { a, c, b, b, c, d });
        }

        // End caps
        AddCap(result, 0, sides, false);
        AddCap(result, sides, sides, true);

        return result;
    }

    /// <summary>
    /// Tessellates a full conduit run (all segments + fitting approximations).
    /// </summary>
    public static TessellationResult TessellateRun(
        ConduitRun run, ConduitModelStore store, LevelOfDetail lod = LevelOfDetail.High)
    {
        var combined = new TessellationResult();
        int indexOffset = 0;

        foreach (var segment in run.GetSegments(store))
        {
            var segMesh = TessellateSegment(segment, lod);
            foreach (var pos in segMesh.Positions)
                combined.Positions.Add(pos);
            foreach (var norm in segMesh.Normals)
                combined.Normals.Add(norm);
            foreach (var idx in segMesh.TriangleIndices)
                combined.TriangleIndices.Add(idx + indexOffset);
            indexOffset += segMesh.Positions.Count;
        }

        // Tessellate fittings as torus sections (simplified)
        foreach (var fitting in run.GetFittings(store))
        {
            var fitMesh = TessellateFitting(fitting, lod);
            foreach (var pos in fitMesh.Positions)
                combined.Positions.Add(pos);
            foreach (var norm in fitMesh.Normals)
                combined.Normals.Add(norm);
            foreach (var idx in fitMesh.TriangleIndices)
                combined.TriangleIndices.Add(idx + indexOffset);
            indexOffset += fitMesh.Positions.Count;
        }

        return combined;
    }

    /// <summary>
    /// Tessellates a fitting as a small sphere at the junction point.
    /// </summary>
    public static TessellationResult TessellateFitting(
        ConduitFitting fitting, LevelOfDetail lod = LevelOfDetail.High)
    {
        int sides = Math.Max(4, (int)lod / 2);
        var sizes = ConduitSizeSettings.CreateDefaultEMT();
        var sizeInfo = sizes.GetSize(fitting.TradeSize);
        double radius = (sizeInfo?.OuterDiameter ?? 0.706) / 2.0 / 12.0 * 1.2; // slightly larger

        var center = ToPoint3D(fitting.Location);
        var result = new TessellationResult();

        // Simplified sphere (octahedron-style for LOD)
        int stacks = sides / 2;
        for (int stack = 0; stack <= stacks; stack++)
        {
            double phi = Math.PI * stack / stacks;
            for (int slice = 0; slice < sides; slice++)
            {
                double theta = 2.0 * Math.PI * slice / sides;
                double x = radius * Math.Sin(phi) * Math.Cos(theta);
                double y = radius * Math.Sin(phi) * Math.Sin(theta);
                double z = radius * Math.Cos(phi);
                result.Positions.Add(new Point3D(center.X + x, center.Y + y, center.Z + z));
                result.Normals.Add(new Vector3D(x, y, z));
            }
        }

        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < sides; slice++)
            {
                int next = (slice + 1) % sides;
                int a = stack * sides + slice;
                int b = stack * sides + next;
                int c = (stack + 1) * sides + slice;
                int d = (stack + 1) * sides + next;

                result.TriangleIndices.AddRange(new[] { a, c, b, b, c, d });
            }
        }

        return result;
    }

    private static void AddRing(TessellationResult result, Point3D center,
        Vector3D right, Vector3D up, double radius, int sides)
    {
        for (int i = 0; i < sides; i++)
        {
            double angle = 2.0 * Math.PI * i / sides;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            var normal = new Vector3D(
                cos * right.X + sin * up.X,
                cos * right.Y + sin * up.Y,
                cos * right.Z + sin * up.Z);
            result.Positions.Add(new Point3D(
                center.X + radius * normal.X,
                center.Y + radius * normal.Y,
                center.Z + radius * normal.Z));
            result.Normals.Add(normal);
        }
    }

    private static void AddCap(TessellationResult result, int ringStart, int sides, bool flip)
    {
        // Fan triangulation for the cap
        for (int i = 1; i < sides - 1; i++)
        {
            if (flip)
            {
                result.TriangleIndices.Add(ringStart);
                result.TriangleIndices.Add(ringStart + i + 1);
                result.TriangleIndices.Add(ringStart + i);
            }
            else
            {
                result.TriangleIndices.Add(ringStart);
                result.TriangleIndices.Add(ringStart + i);
                result.TriangleIndices.Add(ringStart + i + 1);
            }
        }
    }

    private static void GetPerpendicularVectors(Vector3D dir,
        out Vector3D right, out Vector3D up)
    {
        var worldUp = Math.Abs(dir.Z) < 0.99
            ? new Vector3D(0, 0, 1)
            : new Vector3D(1, 0, 0);
        right = Vector3D.CrossProduct(dir, worldUp);
        right.Normalize();
        up = Vector3D.CrossProduct(right, dir);
        up.Normalize();
    }

    private static Point3D ToPoint3D(XYZ p) => new(p.X, p.Y, p.Z);
}
