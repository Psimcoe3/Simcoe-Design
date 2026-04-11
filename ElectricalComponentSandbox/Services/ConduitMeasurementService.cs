using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides conduit measurement formatting and live overlay label text.
/// Mirrors Revit's temporary dimension display during conduit placement.
/// </summary>
public static class ConduitMeasurementService
{
    /// <summary>
    /// Formats a distance in feet as the imperial feet-inches string "X'-Y\"".
    /// </summary>
    /// <param name="distanceFeet">Distance in decimal feet.</param>
    /// <returns>Formatted string, e.g. "12'-3\"".</returns>
    public static string FormatFeetInches(double distanceFeet)
    {
        if (distanceFeet < 0) distanceFeet = -distanceFeet;

        int feet = (int)distanceFeet;
        double remainingInches = (distanceFeet - feet) * 12.0;
        int inches = (int)Math.Round(remainingInches);

        // Handle rounding 11.5+ inches → next foot
        if (inches >= 12)
        {
            feet++;
            inches = 0;
        }

        if (feet == 0) return $"{inches}\"";
        if (inches == 0) return $"{feet}'-0\"";
        return $"{feet}'-{inches}\"";
    }

    /// <summary>
    /// Formats a distance in feet as a decimal-feet string with unit suffix.
    /// </summary>
    public static string FormatDecimalFeet(double distanceFeet, int decimalPlaces = 1)
    {
        return $"{distanceFeet.ToString($"F{decimalPlaces}")} ft";
    }

    /// <summary>
    /// Computes the distance between two 3D points and returns the formatted label.
    /// Intended for live rubber-band overlay during conduit drawing.
    /// </summary>
    public static string ComputeLabel(XYZ start, XYZ end)
    {
        double distance = start.DistanceTo(end);
        return FormatFeetInches(distance);
    }

    /// <summary>
    /// Computes the midpoint between two points (for label placement).
    /// </summary>
    public static XYZ ComputeMidpoint(XYZ start, XYZ end)
    {
        return new XYZ(
            (start.X + end.X) / 2.0,
            (start.Y + end.Y) / 2.0,
            (start.Z + end.Z) / 2.0);
    }

    /// <summary>
    /// Computes total run length and the formatted display string.
    /// </summary>
    public static (double totalFeet, string label) GetRunLengthLabel(
        ConduitModelStore store, string runId)
    {
        double total = ConduitRunService.GetTotalLength(store, runId);
        return (total, FormatFeetInches(total));
    }
}
