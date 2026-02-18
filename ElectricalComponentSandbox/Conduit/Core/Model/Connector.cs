namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// A connector endpoint on a segment or fitting.
/// Stores position, direction, and connectivity.
/// </summary>
public class Connector
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>World-space location of this connector.</summary>
    public XYZ Origin { get; set; }

    /// <summary>Direction vector (outward from the element).</summary>
    public XYZ Direction { get; set; } = XYZ.BasisX;

    /// <summary>Up/basis vector for orientation.</summary>
    public XYZ BasisY { get; set; } = XYZ.BasisZ;

    /// <summary>Owner element ID (segment or fitting).</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Connected connector ID (null if open).</summary>
    public string? ConnectedToId { get; set; }

    /// <summary>Whether this connector is connected.</summary>
    public bool IsConnected => ConnectedToId != null;
}

/// <summary>
/// Manages connectors for a single element (segment, fitting, or equipment).
/// </summary>
public class ConnectorManager
{
    private readonly List<Connector> _connectors = new();
    public IReadOnlyList<Connector> Connectors => _connectors;

    public void AddConnector(Connector connector) => _connectors.Add(connector);

    public Connector? GetConnector(string id) =>
        _connectors.FirstOrDefault(c => c.Id == id);

    /// <summary>
    /// Finds the nearest unconnected connector to a given point within tolerance.
    /// </summary>
    public Connector? FindNearest(XYZ point, double tolerance = 0.01)
    {
        Connector? best = null;
        double bestDist = tolerance;
        foreach (var c in _connectors)
        {
            if (c.IsConnected) continue;
            double d = c.Origin.DistanceTo(point);
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }

    /// <summary>
    /// Gets all unconnected connectors.
    /// </summary>
    public IEnumerable<Connector> GetOpenConnectors() =>
        _connectors.Where(c => !c.IsConnected);
}

/// <summary>
/// Global connectivity graph that tracks connections across all elements.
/// </summary>
public class ConnectivityGraph
{
    private readonly Dictionary<string, Connector> _allConnectors = new();

    public void Register(Connector connector) =>
        _allConnectors[connector.Id] = connector;

    public void Unregister(string connectorId) =>
        _allConnectors.Remove(connectorId);

    /// <summary>
    /// Connects two connectors bidirectionally.
    /// </summary>
    public bool Connect(string connectorIdA, string connectorIdB)
    {
        if (!_allConnectors.TryGetValue(connectorIdA, out var a) ||
            !_allConnectors.TryGetValue(connectorIdB, out var b))
            return false;

        a.ConnectedToId = connectorIdB;
        b.ConnectedToId = connectorIdA;
        return true;
    }

    /// <summary>
    /// Disconnects two connectors.
    /// </summary>
    public void Disconnect(string connectorId)
    {
        if (!_allConnectors.TryGetValue(connectorId, out var c)) return;
        if (c.ConnectedToId != null && _allConnectors.TryGetValue(c.ConnectedToId, out var other))
        {
            other.ConnectedToId = null;
        }
        c.ConnectedToId = null;
    }

    /// <summary>
    /// Auto-connects any open connectors that are within tolerance.
    /// </summary>
    public int AutoConnect(double tolerance = 0.01)
    {
        int count = 0;
        var open = _allConnectors.Values.Where(c => !c.IsConnected).ToList();
        for (int i = 0; i < open.Count; i++)
        {
            for (int j = i + 1; j < open.Count; j++)
            {
                if (open[i].OwnerId == open[j].OwnerId) continue;
                if (open[i].Origin.DistanceTo(open[j].Origin) < tolerance)
                {
                    Connect(open[i].Id, open[j].Id);
                    count++;
                }
            }
        }
        return count;
    }

    public Connector? GetConnector(string id) =>
        _allConnectors.GetValueOrDefault(id);
}
