namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// A single conduit segment with a centerline curve, type, and parameters.
/// Analogous to Autodesk.Revit.DB.Electrical.Conduit.
/// </summary>
public class ConduitSegment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Start point of the segment centerline.</summary>
    public XYZ StartPoint { get; set; }

    /// <summary>End point of the segment centerline.</summary>
    public XYZ EndPoint { get; set; }

    /// <summary>The centerline of this segment.</summary>
    public Line LocationCurve => new(StartPoint, EndPoint);

    /// <summary>Length of the segment in feet.</summary>
    public double Length => StartPoint.DistanceTo(EndPoint);

    /// <summary>Level ID this segment belongs to.</summary>
    public string LevelId { get; set; } = "Level 1";

    /// <summary>Conduit type ID reference.</summary>
    public string ConduitTypeId { get; set; } = string.Empty;

    /// <summary>Elevation offset from level.</summary>
    public double Offset { get; set; }

    // ?? Parameters ??

    /// <summary>Outer diameter in inches.</summary>
    public double Diameter { get; set; } = 0.706;

    /// <summary>Trade size designation.</summary>
    public string TradeSize { get; set; } = "1/2";

    /// <summary>Material type.</summary>
    public ConduitMaterialType Material { get; set; } = ConduitMaterialType.EMT;

    /// <summary>Associated connector manager for this segment.</summary>
    public ConnectorManager Connectors { get; set; } = new();

    /// <summary>
    /// Direction vector of this segment.
    /// </summary>
    public XYZ Direction => LocationCurve.Direction;

    /// <summary>
    /// Initializes connectors at both ends of this segment.
    /// </summary>
    public void InitializeConnectors()
    {
        var dir = Direction;
        Connectors = new ConnectorManager();
        Connectors.AddConnector(new Connector
        {
            Id = $"{Id}-start",
            Origin = StartPoint,
            Direction = -dir,
            OwnerId = Id
        });
        Connectors.AddConnector(new Connector
        {
            Id = $"{Id}-end",
            Origin = EndPoint,
            Direction = dir,
            OwnerId = Id
        });
    }
}
