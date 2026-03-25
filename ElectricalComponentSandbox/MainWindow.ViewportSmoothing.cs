using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private List<Point3D> GenerateSmoothPath(List<Point3D> controlPoints, double bendRadius)
    {
        if (controlPoints.Count < 3 || bendRadius <= 0)
            return new List<Point3D>(controlPoints);

        var smoothPath = new List<Point3D>();
        int resolution = Math.Max(MinSegmentResolution, Math.Min(MaxSegmentResolution, (int)(bendRadius * ResolutionScaleFactor)));

        smoothPath.Add(controlPoints[0]);

        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            if (i == 0 || i == controlPoints.Count - 2)
            {
                smoothPath.Add(controlPoints[i + 1]);
                continue;
            }

            Point3D p0 = controlPoints[Math.Max(0, i - 1)];
            Point3D p1 = controlPoints[i];
            Point3D p2 = controlPoints[i + 1];
            Point3D p3 = controlPoints[Math.Min(controlPoints.Count - 1, i + 2)];

            for (int j = 1; j <= resolution; j++)
            {
                double t = (double)j / resolution;
                smoothPath.Add(CatmullRomInterpolate(p0, p1, p2, p3, t));
            }
        }

        return smoothPath;
    }

    private Point3D CatmullRomInterpolate(Point3D p0, Point3D p1, Point3D p2, Point3D p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        double x = 0.5 * ((2 * p1.X) +
            (-p0.X + p2.X) * t +
            (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
            (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);

        double y = 0.5 * ((2 * p1.Y) +
            (-p0.Y + p2.Y) * t +
            (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

        double z = 0.5 * ((2 * p1.Z) +
            (-p0.Z + p2.Z) * t +
            (2 * p0.Z - 5 * p1.Z + 4 * p2.Z - p3.Z) * t2 +
            (-p0.Z + 3 * p1.Z - 3 * p2.Z + p3.Z) * t3);

        return new Point3D(x, y, z);
    }
}
