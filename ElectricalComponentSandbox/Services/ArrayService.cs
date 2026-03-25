using System.Windows.Media.Media3D;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Parameters for a rectangular (grid) array
/// </summary>
public class RectangularArrayParams
{
    public int Rows { get; set; } = 1;
    public int Columns { get; set; } = 1;
    public double RowSpacing { get; set; } = 5.0;      // spacing along Z axis (depth)
    public double ColumnSpacing { get; set; } = 5.0;    // spacing along X axis (width)
    public double LevelSpacing { get; set; } = 0.0;     // spacing along Y axis (elevation)
    public int Levels { get; set; } = 1;
}

/// <summary>
/// Parameters for a polar (circular) array
/// </summary>
public class PolarArrayParams
{
    public Point3D Center { get; set; }
    public int Count { get; set; } = 6;
    public double TotalAngleDegrees { get; set; } = 360.0;  // full circle by default
    public bool RotateItems { get; set; } = true;            // rotate each item to face center
}

/// <summary>
/// A computed placement for one array copy
/// </summary>
public class ArrayPlacement
{
    public Point3D Position { get; set; }
    public Vector3D Rotation { get; set; }
}

/// <summary>
/// Generates array positions/rotations for component arrays.
/// Does not directly modify the component collection — returns the positions
/// so the caller can create copies with undo support.
/// </summary>
public class ArrayService
{
    /// <summary>
    /// Computes positions for a rectangular array around a source position.
    /// The source position is at row=0, col=0, level=0.
    /// Returns positions for all OTHER cells (excludes the source).
    /// </summary>
    public List<ArrayPlacement> ComputeRectangularArray(
        Point3D sourcePosition,
        Vector3D sourceRotation,
        RectangularArrayParams p)
    {
        var placements = new List<ArrayPlacement>();

        for (int level = 0; level < p.Levels; level++)
        {
            for (int row = 0; row < p.Rows; row++)
            {
                for (int col = 0; col < p.Columns; col++)
                {
                    // Skip the source cell
                    if (row == 0 && col == 0 && level == 0)
                        continue;

                    var position = new Point3D(
                        sourcePosition.X + col * p.ColumnSpacing,
                        sourcePosition.Y + level * p.LevelSpacing,
                        sourcePosition.Z + row * p.RowSpacing);

                    placements.Add(new ArrayPlacement
                    {
                        Position = position,
                        Rotation = sourceRotation
                    });
                }
            }
        }

        return placements;
    }

    /// <summary>
    /// Computes positions for a polar array around a center point.
    /// Returns positions for all items EXCEPT the source (which is item 0 at angle 0).
    /// </summary>
    public List<ArrayPlacement> ComputePolarArray(
        Point3D sourcePosition,
        Vector3D sourceRotation,
        PolarArrayParams p)
    {
        var placements = new List<ArrayPlacement>();

        if (p.Count <= 1)
            return placements;

        double angleIncrement = p.TotalAngleDegrees / p.Count;

        // Vector from center to source on the XZ plane
        double dx = sourcePosition.X - p.Center.X;
        double dz = sourcePosition.Z - p.Center.Z;

        for (int i = 1; i < p.Count; i++)
        {
            double angleDeg = angleIncrement * i;
            double angleRad = angleDeg * Math.PI / 180.0;

            // Rotate (dx, dz) around origin by angleRad on the XZ plane
            double cosA = Math.Cos(angleRad);
            double sinA = Math.Sin(angleRad);

            double rotatedDx = dx * cosA - dz * sinA;
            double rotatedDz = dx * sinA + dz * cosA;

            var position = new Point3D(
                p.Center.X + rotatedDx,
                sourcePosition.Y,   // Y (elevation) stays unchanged
                p.Center.Z + rotatedDz);

            var rotation = sourceRotation;
            if (p.RotateItems)
            {
                rotation = new Vector3D(
                    sourceRotation.X,
                    sourceRotation.Y + angleDeg,
                    sourceRotation.Z);
            }

            placements.Add(new ArrayPlacement
            {
                Position = position,
                Rotation = rotation
            });
        }

        return placements;
    }
}
