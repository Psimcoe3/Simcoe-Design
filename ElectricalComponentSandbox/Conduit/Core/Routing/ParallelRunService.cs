using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Core.Routing;

/// <summary>
/// Manages parallel conduit runs with consistent spacing, concentric bends,
/// and alignment.
/// </summary>
public static class ParallelRunService
{
    /// <summary>
    /// Creates parallel offset paths from a centerline path.
    /// </summary>
    /// <param name="centerPath">The reference centerline path (3D points).</param>
    /// <param name="count">Number of parallel runs.</param>
    /// <param name="spacing">Center-to-center spacing in feet.</param>
    /// <param name="offsetDirection">Direction to offset (perpendicular to path plane).</param>
    /// <returns>List of offset paths, one per run.</returns>
    public static List<List<XYZ>> GenerateParallelPaths(
        IReadOnlyList<XYZ> centerPath, int count, double spacing,
        XYZ? offsetDirection = null)
    {
        var paths = new List<List<XYZ>>();
        if (centerPath.Count < 2) return paths;

        // Default offset perpendicular in the XY plane
        var firstDir = (centerPath[1] - centerPath[0]).Normalize();
        var up = offsetDirection ?? XYZ.BasisZ;
        var perp = firstDir.CrossProduct(up).Normalize();

        double startOffset = -(count - 1) * spacing / 2.0;

        for (int r = 0; r < count; r++)
        {
            double offset = startOffset + r * spacing;
            var path = new List<XYZ>();

            for (int i = 0; i < centerPath.Count; i++)
            {
                // Compute local perpendicular at each vertex
                XYZ localPerp;
                if (i == 0)
                {
                    localPerp = ComputePerpendicular(centerPath[0], centerPath[1], up);
                }
                else if (i == centerPath.Count - 1)
                {
                    localPerp = ComputePerpendicular(centerPath[i - 1], centerPath[i], up);
                }
                else
                {
                    var p1 = ComputePerpendicular(centerPath[i - 1], centerPath[i], up);
                    var p2 = ComputePerpendicular(centerPath[i], centerPath[i + 1], up);
                    localPerp = (p1 + p2).Normalize();
                }

                path.Add(centerPath[i] + localPerp * offset);
            }

            paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// Adjusts bend radii for concentric bends on parallel runs.
    /// Inner runs get smaller radii, outer runs get larger.
    /// </summary>
    public static List<double> ComputeConcentricRadii(
        double baseBendRadius, int runCount, double spacing)
    {
        var radii = new List<double>();
        double startOffset = -(runCount - 1) * spacing / 2.0;

        for (int i = 0; i < runCount; i++)
        {
            double offset = startOffset + i * spacing;
            radii.Add(Math.Max(spacing / 2.0, baseBendRadius + offset));
        }

        return radii;
    }

    private static XYZ ComputePerpendicular(XYZ from, XYZ to, XYZ up)
    {
        var dir = (to - from).Normalize();
        return dir.CrossProduct(up).Normalize();
    }
}
