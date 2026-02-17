using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Generates 3D mesh geometry from 2D markup records for the HelixToolkit preview viewport.
/// Polylines → swept conduit tubes; Polygons/Rectangles → extruded slabs; Circles → cylinders.
/// </summary>
public class Markup3DGenerator
{
    /// <summary>Default conduit diameter for polyline/conduit-run extrusion (in real-world units)</summary>
    public double DefaultConduitDiameter { get; set; } = 0.5; // 6 inches = 0.5 ft

    /// <summary>Default extrusion height for polygon/rectangle/box markups</summary>
    public double DefaultExtrusionHeight { get; set; } = 1.0;

    /// <summary>Number of sides for tube cross-section approximation</summary>
    public int TubeSegments { get; set; } = 12;

    private readonly CoordinateTransformService _transform;

    public Markup3DGenerator(CoordinateTransformService transform)
    {
        _transform = transform;
    }

    /// <summary>
    /// Generates 3D mesh data for a markup record.
    /// Returns vertices and triangle indices suitable for MeshGeometry3D.
    /// </summary>
    public MeshData Generate3DMesh(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polyline or MarkupType.ConduitRun =>
                GenerateConduitMesh(markup),
            MarkupType.Polygon or MarkupType.Box or MarkupType.Panel =>
                GenerateExtrudedPolygonMesh(markup),
            MarkupType.Rectangle =>
                GenerateExtrudedRectangleMesh(markup),
            MarkupType.Circle =>
                GenerateCylinderMesh(markup),
            _ => new MeshData()
        };
    }

    /// <summary>
    /// Sweeps a circular cross-section along a polyline to create a conduit tube
    /// </summary>
    private MeshData GenerateConduitMesh(MarkupRecord markup)
    {
        var mesh = new MeshData();
        if (markup.Vertices.Count < 2) return mesh;

        double radius = DefaultConduitDiameter / 2.0;

        // Convert vertices to 3D real-world coordinates (Z=0 plane)
        var rwPoints = markup.Vertices
            .Select(v => _transform.DocumentToRealWorld(v))
            .Select(p => new Point3D(p.X, p.Y, 0))
            .ToList();

        for (int seg = 0; seg < rwPoints.Count - 1; seg++)
        {
            var start = rwPoints[seg];
            var end = rwPoints[seg + 1];
            var dir = new Vector3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            dir.Normalize();

            // Find a perpendicular vector
            var up = Math.Abs(dir.Z) < 0.99
                ? new Vector3D(0, 0, 1)
                : new Vector3D(1, 0, 0);
            var right = Vector3D.CrossProduct(dir, up);
            right.Normalize();
            up = Vector3D.CrossProduct(right, dir);
            up.Normalize();

            int baseIndex = mesh.Positions.Count;

            // Create ring at start and end
            for (int ring = 0; ring < 2; ring++)
            {
                var center = ring == 0 ? start : end;
                for (int i = 0; i < TubeSegments; i++)
                {
                    double angle = 2.0 * Math.PI * i / TubeSegments;
                    double cos = Math.Cos(angle);
                    double sin = Math.Sin(angle);
                    var point = new Point3D(
                        center.X + radius * (cos * right.X + sin * up.X),
                        center.Y + radius * (cos * right.Y + sin * up.Y),
                        center.Z + radius * (cos * right.Z + sin * up.Z));
                    mesh.Positions.Add(point);
                }
            }

            // Create triangles between rings
            for (int i = 0; i < TubeSegments; i++)
            {
                int next = (i + 1) % TubeSegments;
                int a = baseIndex + i;
                int b = baseIndex + next;
                int c = baseIndex + TubeSegments + i;
                int d = baseIndex + TubeSegments + next;

                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(b);

                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(d);
            }
        }

        return mesh;
    }

    /// <summary>
    /// Extrudes a polygon upward by depth or DefaultExtrusionHeight
    /// </summary>
    private MeshData GenerateExtrudedPolygonMesh(MarkupRecord markup)
    {
        var mesh = new MeshData();
        if (markup.Vertices.Count < 3) return mesh;

        double height = markup.Metadata.Depth > 0
            ? markup.Metadata.Depth
            : DefaultExtrusionHeight;

        var rwVerts = markup.Vertices
            .Select(v => _transform.DocumentToRealWorld(v))
            .ToList();

        int n = rwVerts.Count;

        // Bottom face vertices
        for (int i = 0; i < n; i++)
            mesh.Positions.Add(new Point3D(rwVerts[i].X, rwVerts[i].Y, 0));

        // Top face vertices
        for (int i = 0; i < n; i++)
            mesh.Positions.Add(new Point3D(rwVerts[i].X, rwVerts[i].Y, height));

        // Bottom face triangles (fan)
        for (int i = 1; i < n - 1; i++)
        {
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(i + 1);
            mesh.TriangleIndices.Add(i);
        }

        // Top face triangles (fan)
        for (int i = 1; i < n - 1; i++)
        {
            mesh.TriangleIndices.Add(n);
            mesh.TriangleIndices.Add(n + i);
            mesh.TriangleIndices.Add(n + i + 1);
        }

        // Side faces
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int a = i;
            int b = next;
            int c = n + i;
            int d = n + next;

            mesh.TriangleIndices.Add(a);
            mesh.TriangleIndices.Add(c);
            mesh.TriangleIndices.Add(b);

            mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(c);
            mesh.TriangleIndices.Add(d);
        }

        return mesh;
    }

    /// <summary>
    /// Extrudes a rectangle upward
    /// </summary>
    private MeshData GenerateExtrudedRectangleMesh(MarkupRecord markup)
    {
        if (markup.Vertices.Count < 2) return new MeshData();

        // Convert rectangle to 4-vertex polygon
        var rw1 = _transform.DocumentToRealWorld(markup.Vertices[0]);
        var rw2 = _transform.DocumentToRealWorld(markup.Vertices[1]);

        var polyMarkup = new MarkupRecord
        {
            Type = MarkupType.Polygon,
            Vertices = new List<Point>
            {
                markup.Vertices[0],
                new Point(markup.Vertices[1].X, markup.Vertices[0].Y),
                markup.Vertices[1],
                new Point(markup.Vertices[0].X, markup.Vertices[1].Y)
            },
            Metadata = markup.Metadata
        };

        return GenerateExtrudedPolygonMesh(polyMarkup);
    }

    /// <summary>
    /// Generates a cylinder mesh for a circle markup
    /// </summary>
    private MeshData GenerateCylinderMesh(MarkupRecord markup)
    {
        var mesh = new MeshData();
        if (markup.Vertices.Count < 1) return mesh;

        double height = markup.Metadata.Depth > 0
            ? markup.Metadata.Depth
            : DefaultExtrusionHeight;

        var center = _transform.DocumentToRealWorld(markup.Vertices[0]);
        double radius = _transform.DocumentToRealWorldDistance(markup.Radius);

        int segments = TubeSegments * 2;

        // Bottom ring
        mesh.Positions.Add(new Point3D(center.X, center.Y, 0)); // center bottom
        for (int i = 0; i < segments; i++)
        {
            double angle = 2.0 * Math.PI * i / segments;
            mesh.Positions.Add(new Point3D(
                center.X + radius * Math.Cos(angle),
                center.Y + radius * Math.Sin(angle), 0));
        }

        // Top ring
        mesh.Positions.Add(new Point3D(center.X, center.Y, height)); // center top
        for (int i = 0; i < segments; i++)
        {
            double angle = 2.0 * Math.PI * i / segments;
            mesh.Positions.Add(new Point3D(
                center.X + radius * Math.Cos(angle),
                center.Y + radius * Math.Sin(angle), height));
        }

        int bottomCenter = 0;
        int topCenter = segments + 1;

        // Bottom face
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            mesh.TriangleIndices.Add(bottomCenter);
            mesh.TriangleIndices.Add(1 + next);
            mesh.TriangleIndices.Add(1 + i);
        }

        // Top face
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            mesh.TriangleIndices.Add(topCenter);
            mesh.TriangleIndices.Add(topCenter + 1 + i);
            mesh.TriangleIndices.Add(topCenter + 1 + next);
        }

        // Side faces
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int a = 1 + i;
            int b = 1 + next;
            int c = topCenter + 1 + i;
            int d = topCenter + 1 + next;

            mesh.TriangleIndices.Add(a);
            mesh.TriangleIndices.Add(c);
            mesh.TriangleIndices.Add(b);

            mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(c);
            mesh.TriangleIndices.Add(d);
        }

        return mesh;
    }
}

/// <summary>
/// Raw mesh data: vertex positions and triangle indices.
/// Compatible with WPF MeshGeometry3D.
/// </summary>
public class MeshData
{
    public List<Point3D> Positions { get; set; } = new();
    public List<int> TriangleIndices { get; set; } = new();

    public bool IsEmpty => Positions.Count == 0;
}
