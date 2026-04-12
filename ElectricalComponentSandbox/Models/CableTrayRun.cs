using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Material / construction types for cable trays per NEC 392.
/// </summary>
public enum CableTrayType
{
    Ladder,
    VentilatedTrough,
    SolidBottom,
    Channel,
    Wire,
    SingleRail
}

/// <summary>
/// Fitting types used in cable tray runs at direction changes.
/// </summary>
public enum CableTrayFittingType
{
    Elbow90,
    Elbow45,
    Tee,
    Cross,
    Reducer,
    Splice,
    EndCap
}

/// <summary>
/// A straight cable tray segment between two endpoints.
/// Parallels <see cref="ConduitSegment"/> for the conduit subsystem.
/// </summary>
public class CableTraySegment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public XYZ StartPoint { get; set; }
    public XYZ EndPoint { get; set; }

    /// <summary>Tray width in inches.</summary>
    public double Width { get; set; } = 12.0;

    /// <summary>Tray depth (loading depth) in inches.</summary>
    public double Depth { get; set; } = 4.0;

    public CableTrayType TrayType { get; set; } = CableTrayType.Ladder;

    /// <summary>Segment length in feet (computed from endpoints).</summary>
    public double Length => StartPoint.DistanceTo(EndPoint);

    /// <summary>Level/floor identifier.</summary>
    public string LevelId { get; set; } = "Level 1";

    /// <summary>Direction vector from start to end.</summary>
    public XYZ Direction => Length > 0
        ? (EndPoint - StartPoint).Normalize()
        : XYZ.BasisX;
}

/// <summary>
/// A fitting placed at a direction change between two cable tray segments.
/// </summary>
public class CableTrayFitting
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public CableTrayFittingType Type { get; set; }
    public XYZ Location { get; set; }
    public double AngleDegrees { get; set; }

    /// <summary>Tray width at fitting (matches connected segments).</summary>
    public double Width { get; set; } = 12.0;

    /// <summary>Tray depth at fitting.</summary>
    public double Depth { get; set; } = 4.0;

    /// <summary>IDs of the two segments connected at this fitting.</summary>
    public List<string> ConnectedSegmentIds { get; set; } = new();
}

/// <summary>
/// A cable tray run linking segments and fittings end-to-end.
/// Parallels <see cref="Conduit.Core.Model.ConduitRun"/>.
/// </summary>
public class CableTrayRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>User-visible tag (e.g. "CT-001").</summary>
    public string RunId { get; set; } = string.Empty;

    public List<string> SegmentIds { get; set; } = new();
    public List<string> FittingIds { get; set; } = new();

    /// <summary>Source equipment label.</summary>
    public string StartEquipment { get; set; } = string.Empty;

    /// <summary>Destination equipment label.</summary>
    public string EndEquipment { get; set; } = string.Empty;

    /// <summary>Tray width in inches (inherited by segments).</summary>
    public double Width { get; set; } = 12.0;

    /// <summary>Tray depth in inches.</summary>
    public double Depth { get; set; } = 4.0;

    public CableTrayType TrayType { get; set; } = CableTrayType.Ladder;
    public string LevelId { get; set; } = "Level 1";

    /// <summary>Cached fill percent from last calculation.</summary>
    public double FillPercent { get; set; }

    /// <summary>Arbitrary metadata key-value pairs.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Specification for a cable placed in a tray, used for fill calculations.
/// </summary>
public class CableSpec
{
    /// <summary>Cable outer diameter in inches.</summary>
    public double OuterDiameterInches { get; set; }

    /// <summary>Number of identical cables of this spec.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Cable type label (e.g. "MC 12/2", "SO 10/3").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Cross-sectional area of a single cable in sq inches.</summary>
    public double AreaSqIn => Math.PI * Math.Pow(OuterDiameterInches / 2, 2);

    /// <summary>Total area for all cables of this spec.</summary>
    public double TotalAreaSqIn => AreaSqIn * Quantity;
}
