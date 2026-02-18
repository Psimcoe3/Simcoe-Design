using System.Text.Json.Serialization;

namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// Immutable 3D point/vector, analogous to Autodesk.Revit.DB.XYZ.
/// Uses feet as the default unit consistent with Revit conventions.
/// </summary>
public readonly struct XYZ : IEquatable<XYZ>
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    [JsonConstructor]
    public XYZ(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static XYZ Zero => new(0, 0, 0);
    public static XYZ BasisX => new(1, 0, 0);
    public static XYZ BasisY => new(0, 1, 0);
    public static XYZ BasisZ => new(0, 0, 1);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared => X * X + Y * Y + Z * Z;

    public XYZ Normalize()
    {
        double len = Length;
        if (len < 1e-12) return Zero;
        return new XYZ(X / len, Y / len, Z / len);
    }

    public double DistanceTo(XYZ other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public double DotProduct(XYZ other) => X * other.X + Y * other.Y + Z * other.Z;

    public XYZ CrossProduct(XYZ other) =>
        new(Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X);

    public static XYZ operator +(XYZ a, XYZ b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static XYZ operator -(XYZ a, XYZ b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static XYZ operator *(XYZ v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static XYZ operator *(double s, XYZ v) => v * s;
    public static XYZ operator -(XYZ v) => new(-v.X, -v.Y, -v.Z);

    public static double AngleBetween(XYZ a, XYZ b)
    {
        double dot = a.Normalize().DotProduct(b.Normalize());
        dot = Math.Clamp(dot, -1.0, 1.0);
        return Math.Acos(dot);
    }

    public bool IsAlmostEqualTo(XYZ other, double tolerance = 1e-9)
    {
        return Math.Abs(X - other.X) < tolerance &&
               Math.Abs(Y - other.Y) < tolerance &&
               Math.Abs(Z - other.Z) < tolerance;
    }

    public bool Equals(XYZ other) => IsAlmostEqualTo(other);
    public override bool Equals(object? obj) => obj is XYZ other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        Math.Round(X, 6), Math.Round(Y, 6), Math.Round(Z, 6));
    public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
}
