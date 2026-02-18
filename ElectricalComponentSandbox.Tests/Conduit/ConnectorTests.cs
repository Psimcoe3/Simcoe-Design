using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Tests.Conduit;

/// <summary>
/// Tests for ConnectorManager, ConnectivityGraph, and auto-connect.
/// </summary>
public class ConnectorTests
{
    [Fact]
    public void ConnectorManager_FindNearest_ReturnsClosestOpen()
    {
        var mgr = new ConnectorManager();
        mgr.AddConnector(new Connector { Id = "c1", Origin = new XYZ(0, 0, 0), OwnerId = "s1" });
        mgr.AddConnector(new Connector { Id = "c2", Origin = new XYZ(1, 0, 0), OwnerId = "s1" });

        var nearest = mgr.FindNearest(new XYZ(0.005, 0, 0), tolerance: 0.01);
        Assert.NotNull(nearest);
        Assert.Equal("c1", nearest!.Id);
    }

    [Fact]
    public void ConnectorManager_FindNearest_SkipsConnected()
    {
        var mgr = new ConnectorManager();
        mgr.AddConnector(new Connector { Id = "c1", Origin = new XYZ(0, 0, 0), OwnerId = "s1", ConnectedToId = "other" });
        mgr.AddConnector(new Connector { Id = "c2", Origin = new XYZ(0.5, 0, 0), OwnerId = "s1" });

        var nearest = mgr.FindNearest(new XYZ(0, 0, 0), tolerance: 1.0);
        Assert.NotNull(nearest);
        Assert.Equal("c2", nearest!.Id);
    }

    [Fact]
    public void ConnectivityGraph_Connect_BidirectionalLink()
    {
        var graph = new ConnectivityGraph();
        var c1 = new Connector { Id = "c1", OwnerId = "s1" };
        var c2 = new Connector { Id = "c2", OwnerId = "s2" };
        graph.Register(c1);
        graph.Register(c2);

        bool result = graph.Connect("c1", "c2");
        Assert.True(result);
        Assert.Equal("c2", c1.ConnectedToId);
        Assert.Equal("c1", c2.ConnectedToId);
    }

    [Fact]
    public void ConnectivityGraph_Disconnect_ClearsBothEnds()
    {
        var graph = new ConnectivityGraph();
        var c1 = new Connector { Id = "c1", OwnerId = "s1" };
        var c2 = new Connector { Id = "c2", OwnerId = "s2" };
        graph.Register(c1);
        graph.Register(c2);
        graph.Connect("c1", "c2");

        graph.Disconnect("c1");
        Assert.Null(c1.ConnectedToId);
        Assert.Null(c2.ConnectedToId);
    }

    [Fact]
    public void ConnectivityGraph_AutoConnect_ConnectsNearbyEndpoints()
    {
        var graph = new ConnectivityGraph();
        var c1 = new Connector { Id = "c1", Origin = new XYZ(5, 0, 0), OwnerId = "s1" };
        var c2 = new Connector { Id = "c2", Origin = new XYZ(5.005, 0, 0), OwnerId = "s2" };
        graph.Register(c1);
        graph.Register(c2);

        int count = graph.AutoConnect(tolerance: 0.01);
        Assert.Equal(1, count);
        Assert.True(c1.IsConnected);
        Assert.True(c2.IsConnected);
    }

    [Fact]
    public void ConnectivityGraph_AutoConnect_SkipsSameOwner()
    {
        var graph = new ConnectivityGraph();
        var c1 = new Connector { Id = "c1", Origin = new XYZ(0, 0, 0), OwnerId = "s1" };
        var c2 = new Connector { Id = "c2", Origin = new XYZ(0, 0, 0), OwnerId = "s1" };
        graph.Register(c1);
        graph.Register(c2);

        int count = graph.AutoConnect(tolerance: 0.01);
        Assert.Equal(0, count);
    }
}
