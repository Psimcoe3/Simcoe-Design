namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Fitting category used in takeoff output, independent of the routing
/// <see cref="Conduit.Core.Model.FittingType"/> enum.
/// </summary>
public enum TakeoffFittingCategory
{
    Elbow90,
    Elbow45,
    OtherBend,
    Coupling,
    Offset,
    Unknown,
}

/// <summary>
/// Takeoff detail for a single fitting on a conduit run.
/// </summary>
public sealed record ConduitFittingTakeoff(
    string FittingId,
    TakeoffFittingCategory Category,
    string TradeSize,
    double AngleDegrees,
    double DeductInches);

/// <summary>
/// Complete takeoff result for a conduit run including adjusted footage,
/// per-fitting deducts, and recommended support count.
/// </summary>
public sealed record ConduitRunTakeoff(
    string RunId,
    double GrossLengthFeet,
    double AdjustedLengthFeet,
    double TotalDeductInches,
    IReadOnlyList<ConduitFittingTakeoff> Fittings,
    int RecommendedSupportCount,
    double SupportSpacingFeet);
