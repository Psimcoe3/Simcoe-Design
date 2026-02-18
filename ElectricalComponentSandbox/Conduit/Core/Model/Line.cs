namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// A line segment from Start to End in 3D space, serving as the centerline
/// (LocationCurve) of a conduit segment.
/// </summary>
public class Line
{
    public XYZ Start { get; }
    public XYZ End { get; }
    public XYZ Direction => (End - Start).Normalize();
    public double Length => Start.DistanceTo(End);

    public Line(XYZ start, XYZ end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Evaluates the point at parameter t ? [0,1].
    /// </summary>
    public XYZ Evaluate(double t)
    {
        return Start + (End - Start) * t;
    }

    /// <summary>
    /// Returns the closest point on this line to the given point.
    /// </summary>
    public XYZ ClosestPointTo(XYZ point)
    {
        var ab = End - Start;
        double lenSq = ab.LengthSquared;
        if (lenSq < 1e-12) return Start;
        double t = Math.Clamp((point - Start).DotProduct(ab) / lenSq, 0, 1);
        return Evaluate(t);
    }
}
