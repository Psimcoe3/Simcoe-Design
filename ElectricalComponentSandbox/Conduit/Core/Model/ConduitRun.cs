namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// A fitting placed at a junction between conduit segments.
/// </summary>
public class ConduitFitting
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public FittingType Type { get; set; }

    /// <summary>Location of the fitting center.</summary>
    public XYZ Location { get; set; }

    /// <summary>Angle of the bend/fitting in degrees.</summary>
    public double AngleDegrees { get; set; }

    /// <summary>Trade size designation matching connected segments.</summary>
    public string TradeSize { get; set; } = "1/2";

    /// <summary>Bend radius in inches (for elbows).</summary>
    public double BendRadius { get; set; }

    /// <summary>Deduct length in inches (material saved by bending).</summary>
    public double DeductLength { get; set; }

    /// <summary>IDs of segments connected by this fitting.</summary>
    public List<string> ConnectedSegmentIds { get; set; } = new();

    /// <summary>Connector manager for this fitting.</summary>
    public ConnectorManager Connectors { get; set; } = new();
}

/// <summary>
/// A conduit run collecting multiple segments and fittings as a logical unit.
/// Analogous to Autodesk.Revit.DB.Electrical.ConduitRun.
/// </summary>
public class ConduitRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>User-visible run identifier (e.g. "CR-001").</summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>Ordered segment IDs in this run.</summary>
    public List<string> SegmentIds { get; set; } = new();

    /// <summary>Fitting IDs in this run.</summary>
    public List<string> FittingIds { get; set; } = new();

    /// <summary>Start equipment reference.</summary>
    public string StartEquipment { get; set; } = string.Empty;

    /// <summary>End equipment reference.</summary>
    public string EndEquipment { get; set; } = string.Empty;

    /// <summary>Voltage designation.</summary>
    public string Voltage { get; set; } = string.Empty;

    /// <summary>Conductor fill percentage (0-100).</summary>
    public double ConductorFillPercent { get; set; }

    /// <summary>Conduit type ID for this run.</summary>
    public string ConduitTypeId { get; set; } = string.Empty;

    /// <summary>Trade size for this run.</summary>
    public string TradeSize { get; set; } = "1/2";

    /// <summary>Material type.</summary>
    public ConduitMaterialType Material { get; set; } = ConduitMaterialType.EMT;

    /// <summary>Level ID this run belongs to.</summary>
    public string LevelId { get; set; } = "Level 1";

    /// <summary>Metadata for schedule export.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Computes total length from the segment store.
    /// </summary>
    public double ComputeTotalLength(ConduitModelStore store)
    {
        return SegmentIds.Sum(id =>
        {
            var seg = store.GetSegment(id);
            return seg?.Length ?? 0;
        });
    }

    /// <summary>
    /// Gets all segment objects from the store.
    /// </summary>
    public IEnumerable<ConduitSegment> GetSegments(ConduitModelStore store)
    {
        return SegmentIds
            .Select(id => store.GetSegment(id))
            .Where(s => s != null)!;
    }

    /// <summary>
    /// Gets all fitting objects from the store.
    /// </summary>
    public IEnumerable<ConduitFitting> GetFittings(ConduitModelStore store)
    {
        return FittingIds
            .Select(id => store.GetFitting(id))
            .Where(f => f != null)!;
    }
}
