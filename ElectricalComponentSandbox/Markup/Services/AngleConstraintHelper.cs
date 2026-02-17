using System.Windows;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Helper for orthogonal and 45° angle constraints (Shift-key behavior).
/// Snaps the angle from an anchor point to the nearest multiple of 45°.
/// </summary>
public static class AngleConstraintHelper
{
    /// <summary>
    /// Constrains a free point to the nearest 45° increment from an anchor.
    /// Equivalent to Bluebeam Shift-click behavior.
    /// </summary>
    public static Point Constrain45(Point anchor, Point free)
    {
        return GeometryMath.ConstrainAngle45(anchor, free);
    }

    /// <summary>
    /// Constrains a free point to the nearest 90° increment (orthogonal only)
    /// </summary>
    public static Point ConstrainOrtho(Point anchor, Point free)
    {
        double dx = free.X - anchor.X;
        double dy = free.Y - anchor.Y;

        // Snap to nearest axis
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            // Horizontal
            return new Point(free.X, anchor.Y);
        }
        else
        {
            // Vertical
            return new Point(anchor.X, free.Y);
        }
    }

    /// <summary>
    /// Applies constraint based on whether Shift is held
    /// </summary>
    public static Point ApplyConstraint(Point anchor, Point free, bool shiftHeld)
    {
        return shiftHeld ? Constrain45(anchor, free) : free;
    }
}
