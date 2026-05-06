namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides slope calculations for conduit drainage and elevation routing.
/// Standard practice: ≥¼" per 10 ft (0.2%) minimum slope for drainage.
/// </summary>
public static class ConduitSlopeService
{
    /// <summary>Minimum recommended drainage slope percent (0.2% = ¼″ per 10 ft).</summary>
    public const double MinDrainageSlopePercent = 0.2;

    // ── Conversion ───────────────────────────────────────────────────────

    /// <summary>
    /// Converts a slope expressed as a rise-over-run ratio to percent.
    /// </summary>
    public static double RatioToPercent(double rise, double run)
    {
        if (run == 0) return 0;
        return rise / run * 100.0;
    }

    /// <summary>
    /// Converts a slope percent to rise-over-run ratio.
    /// </summary>
    public static double PercentToRatio(double slopePercent) => slopePercent / 100.0;

    /// <summary>
    /// Converts a slope percent to degrees.
    /// </summary>
    public static double PercentToDegrees(double slopePercent)
    {
        return Math.Atan(slopePercent / 100.0) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Converts a slope in degrees to percent.
    /// </summary>
    public static double DegreesToPercent(double slopeDegrees)
    {
        return Math.Tan(slopeDegrees * Math.PI / 180.0) * 100.0;
    }

    // ── Elevation change ─────────────────────────────────────────────────

    /// <summary>
    /// Computes the elevation change (in feet) for a given horizontal run
    /// length and slope percent.
    /// </summary>
    /// <param name="horizontalLengthFeet">Horizontal run length in feet.</param>
    /// <param name="slopePercent">Slope in percent (e.g. 1.0 for 1%).</param>
    /// <returns>Elevation change in feet.</returns>
    public static double ComputeElevationChange(double horizontalLengthFeet, double slopePercent)
    {
        return horizontalLengthFeet * slopePercent / 100.0;
    }

    /// <summary>
    /// Computes the slope percent from a known elevation change and
    /// horizontal run length.
    /// </summary>
    public static double ComputeSlopePercent(double elevationChangeFeet, double horizontalLengthFeet)
    {
        if (horizontalLengthFeet <= 0) return 0;
        return elevationChangeFeet / horizontalLengthFeet * 100.0;
    }

    /// <summary>
    /// Computes the actual pipe length (hypotenuse) given horizontal run and
    /// slope percent.  For typical conduit slopes the difference is negligible,
    /// but this is used for precise prefab cut lengths.
    /// </summary>
    public static double ComputeActualLength(double horizontalLengthFeet, double slopePercent)
    {
        var rise = ComputeElevationChange(horizontalLengthFeet, slopePercent);
        return Math.Sqrt(horizontalLengthFeet * horizontalLengthFeet + rise * rise);
    }

    // ── Validation ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the slope percent meets the minimum drainage
    /// requirement (≥¼″ per 10 ft = 0.2%).
    /// </summary>
    public static bool MeetsDrainageMinimum(double slopePercent)
        => Math.Abs(slopePercent) >= MinDrainageSlopePercent;

    /// <summary>
    /// Validates slope for a conduit segment and returns an optional
    /// diagnostic message.  Returns <c>null</c> when the slope is acceptable.
    /// </summary>
    /// <param name="slopePercent">Slope in percent (positive = uphill).</param>
    /// <param name="requiresDrainage">
    /// When <c>true</c>, enforces the minimum drainage slope.
    /// </param>
    public static string? Validate(double slopePercent, bool requiresDrainage = false)
    {
        if (requiresDrainage && !MeetsDrainageMinimum(slopePercent))
        {
            return $"Slope {slopePercent:F2}% is below the minimum drainage slope of " +
                   $"{MinDrainageSlopePercent}% (¼″ per 10 ft).";
        }

        return null;
    }
}
