using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalCircuitServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static ElectricalConnector MakeConnector(
        string componentId, string portName = "Line",
        ElectricalSystemType systemType = ElectricalSystemType.PowerCircuit)
    {
        return new ElectricalConnector
        {
            ComponentId = componentId,
            PortName = portName,
            SystemType = systemType,
            Voltage = 120,
            Phase = "A"
        };
    }

    private static PanelComponent MakePanel(params ElectricalConnector[] connectors)
    {
        var panel = new PanelComponent { Name = "Panel A" };
        panel.ElectricalConnectors = new ElectricalConnectorManager();
        panel.ElectricalConnectors.Connectors.AddRange(connectors);
        foreach (var c in connectors) c.ComponentId = panel.Id;
        return panel;
    }

    private static BoxComponent MakeDevice(params ElectricalConnector[] connectors)
    {
        var box = new BoxComponent { Name = "Device" };
        box.ElectricalConnectors = new ElectricalConnectorManager();
        box.ElectricalConnectors.Connectors.AddRange(connectors);
        foreach (var c in connectors) c.ComponentId = box.Id;
        return box;
    }

    // ── Create (single device) ──────────────────────────────────────────

    [Fact]
    public void Create_PanelAndDevice_ReturnsCircuit()
    {
        var panelConn = MakeConnector("p1", "Main");
        var deviceConn = MakeConnector("d1", "Line");

        var circuit = ElectricalCircuitService.Create(
            panelConn, deviceConn, ElectricalSystemType.PowerCircuit);

        Assert.NotNull(circuit);
        Assert.Equal(ElectricalSystemType.PowerCircuit, circuit.SystemType);
        Assert.Equal(2, circuit.ConnectorIds.Count);
        Assert.Equal(panelConn.Id, circuit.PanelConnectorId);
        Assert.Single(circuit.DeviceConnectorIds);
    }

    [Fact]
    public void Create_StampsCircuitIdOnConnectors()
    {
        var panelConn = MakeConnector("p1");
        var deviceConn = MakeConnector("d1");

        var circuit = ElectricalCircuitService.Create(
            panelConn, deviceConn, ElectricalSystemType.PowerCircuit);

        Assert.Equal(circuit.Id, panelConn.CircuitId);
        Assert.Equal(circuit.Id, deviceConn.CircuitId);
        Assert.True(panelConn.IsConnected);
        Assert.True(deviceConn.IsConnected);
    }

    [Fact]
    public void Create_AlreadyConnectedPanel_Throws()
    {
        var panelConn = MakeConnector("p1");
        panelConn.CircuitId = "existing";
        var deviceConn = MakeConnector("d1");

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, deviceConn, ElectricalSystemType.PowerCircuit));
    }

    [Fact]
    public void Create_AlreadyConnectedDevice_Throws()
    {
        var panelConn = MakeConnector("p1");
        var deviceConn = MakeConnector("d1");
        deviceConn.CircuitId = "existing";

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, deviceConn, ElectricalSystemType.PowerCircuit));
    }

    // ── Create (connector set) ──────────────────────────────────────────

    [Fact]
    public void Create_MultipleDevices()
    {
        var panelConn = MakeConnector("p1");
        var devices = new[] { MakeConnector("d1"), MakeConnector("d2"), MakeConnector("d3") };

        var circuit = ElectricalCircuitService.Create(
            panelConn, devices, ElectricalSystemType.Data);

        Assert.Equal(4, circuit.ConnectorIds.Count);
        Assert.Equal(ElectricalSystemType.Data, circuit.SystemType);
        Assert.Equal(3, circuit.DeviceConnectorIds.Count);
        Assert.All(devices, d => Assert.Equal(circuit.Id, d.CircuitId));
    }

    [Fact]
    public void Create_EmptyDeviceList_Throws()
    {
        var panelConn = MakeConnector("p1");

        Assert.Throws<ArgumentException>(() =>
            ElectricalCircuitService.Create(
                panelConn, Array.Empty<ElectricalConnector>(), ElectricalSystemType.PowerCircuit));
    }

    [Fact]
    public void Create_MultipleDevices_OneAlreadyConnected_Throws()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var d2 = MakeConnector("d2");
        d2.CircuitId = "existing";

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, new[] { d1, d2 }, ElectricalSystemType.PowerCircuit));
    }

    // ── AddDevice ────────────────────────────────────────────────────────

    [Fact]
    public void AddDevice_AppendsToCircuit()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var circuit = ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);

        var d2 = MakeConnector("d2");
        ElectricalCircuitService.AddDevice(circuit, d2);

        Assert.Equal(3, circuit.ConnectorIds.Count);
        Assert.Equal(circuit.Id, d2.CircuitId);
    }

    [Fact]
    public void AddDevice_AlreadyConnected_Throws()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var circuit = ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.AddDevice(circuit, d1)); // already wired
    }

    // ── RemoveDevice ─────────────────────────────────────────────────────

    [Fact]
    public void RemoveDevice_RemovesFromCircuit()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var d2 = MakeConnector("d2");
        var circuit = ElectricalCircuitService.Create(
            panelConn, new[] { d1, d2 }, ElectricalSystemType.PowerCircuit);

        ElectricalCircuitService.RemoveDevice(circuit, d1);

        Assert.Equal(2, circuit.ConnectorIds.Count); // panel + d2
        Assert.Null(d1.CircuitId);
        Assert.False(d1.IsConnected);
    }

    [Fact]
    public void RemoveDevice_PanelConnector_Throws()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var circuit = ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.RemoveDevice(circuit, panelConn));
    }

    [Fact]
    public void RemoveDevice_NotInCircuit_Throws()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var circuit = ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);

        var stranger = MakeConnector("d99");
        Assert.Throws<ArgumentException>(() =>
            ElectricalCircuitService.RemoveDevice(circuit, stranger));
    }

    // ── GetOrphanConnectors ──────────────────────────────────────────────

    [Fact]
    public void GetOrphanConnectors_ReturnsUnwired()
    {
        var panelConn = MakeConnector("p1");
        var orphanConn = MakeConnector("p1", "Spare");
        var panel = MakePanel(panelConn, orphanConn);

        var d1 = MakeConnector("d1");
        var d2Orphan = MakeConnector("d2", "Spare");
        var device = MakeDevice(d1, d2Orphan);

        // Wire panel→d1
        ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);

        var orphans = ElectricalCircuitService.GetOrphanConnectors(new ElectricalComponent[] { panel, device });

        Assert.Equal(2, orphans.Count);
        Assert.Contains(orphans, o => o.Id == orphanConn.Id);
        Assert.Contains(orphans, o => o.Id == d2Orphan.Id);
    }

    [Fact]
    public void GetOrphanConnectors_NoConnectors_ReturnsEmpty()
    {
        var box = new BoxComponent(); // no ElectricalConnectors set
        var orphans = ElectricalCircuitService.GetOrphanConnectors(new[] { box });
        Assert.Empty(orphans);
    }

    // ── ResolveConnectors ────────────────────────────────────────────────

    [Fact]
    public void ResolveConnectors_ReturnsOrderedList()
    {
        var panelConn = MakeConnector("p1");
        var panel = MakePanel(panelConn);

        var d1 = MakeConnector("d1");
        var d2 = MakeConnector("d2");
        var device1 = MakeDevice(d1);
        var device2 = MakeDevice(d2);

        var circuit = ElectricalCircuitService.Create(
            panelConn, new[] { d1, d2 }, ElectricalSystemType.PowerCircuit);

        var resolved = ElectricalCircuitService.ResolveConnectors(
            circuit, new ElectricalComponent[] { panel, device1, device2 });

        Assert.Equal(3, resolved.Count);
        Assert.Equal(panelConn.Id, resolved[0].Id);
        Assert.Equal(d1.Id, resolved[1].Id);
        Assert.Equal(d2.Id, resolved[2].Id);
    }

    // ── CircuitPathMode ──────────────────────────────────────────────────

    [Fact]
    public void DefaultPathMode_IsAllDevices()
    {
        var circuit = new ElectricalCircuit();
        Assert.Equal(CircuitPathMode.AllDevices, circuit.PathMode);
    }

    [Fact]
    public void PathMode_CanBeSetToCustom()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var circuit = ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);

        circuit.PathMode = CircuitPathMode.Custom;
        Assert.Equal(CircuitPathMode.Custom, circuit.PathMode);
    }

    // ── ElectricalConnectorManager ───────────────────────────────────────

    [Fact]
    public void ConnectorManager_FindConnector_ByName()
    {
        var mgr = new ElectricalConnectorManager();
        var c1 = new ElectricalConnector { PortName = "Line" };
        var c2 = new ElectricalConnector { PortName = "Load" };
        mgr.Connectors.AddRange(new[] { c1, c2 });

        Assert.Same(c1, mgr.FindConnector("Line"));
        Assert.Same(c2, mgr.FindConnector("Load"));
        Assert.Null(mgr.FindConnector("Neutral"));
    }

    [Fact]
    public void ConnectorManager_FindConnector_CaseInsensitive()
    {
        var mgr = new ElectricalConnectorManager();
        var c1 = new ElectricalConnector { PortName = "Line" };
        mgr.Connectors.Add(c1);

        Assert.Same(c1, mgr.FindConnector("line"));
        Assert.Same(c1, mgr.FindConnector("LINE"));
    }

    [Fact]
    public void ConnectorManager_GetUnconnected_ReturnsOnlyOrphans()
    {
        var mgr = new ElectricalConnectorManager();
        var c1 = new ElectricalConnector { PortName = "Line", CircuitId = "ckt1" };
        var c2 = new ElectricalConnector { PortName = "Load" };
        mgr.Connectors.AddRange(new[] { c1, c2 });

        var unconnected = mgr.GetUnconnected();
        Assert.Single(unconnected);
        Assert.Equal("Load", unconnected[0].PortName);
    }

    [Fact]
    public void ConnectorManager_GetBySystemType_Filters()
    {
        var mgr = new ElectricalConnectorManager();
        var power = new ElectricalConnector { PortName = "L1", SystemType = ElectricalSystemType.PowerCircuit };
        var data = new ElectricalConnector { PortName = "D1", SystemType = ElectricalSystemType.Data };
        mgr.Connectors.AddRange(new[] { power, data });

        Assert.Single(mgr.GetBySystemType(ElectricalSystemType.Data));
        Assert.Single(mgr.GetBySystemType(ElectricalSystemType.PowerCircuit));
        Assert.Empty(mgr.GetBySystemType(ElectricalSystemType.FireAlarm));
    }

    // ── ElectricalCircuit model ──────────────────────────────────────────

    [Fact]
    public void ElectricalCircuit_PanelConnectorId_NullWhenEmpty()
    {
        var circuit = new ElectricalCircuit();
        Assert.Null(circuit.PanelConnectorId);
        Assert.Empty(circuit.DeviceConnectorIds);
    }

    [Fact]
    public void ElectricalCircuit_ScheduleCircuitId_Optional()
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");
        var circuit = ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);

        Assert.Null(circuit.ScheduleCircuitId);
        circuit.ScheduleCircuitId = "legacy-ckt-1";
        Assert.Equal("legacy-ckt-1", circuit.ScheduleCircuitId);
    }

    // ── System type variety ──────────────────────────────────────────────

    [Theory]
    [InlineData(ElectricalSystemType.Data)]
    [InlineData(ElectricalSystemType.FireAlarm)]
    [InlineData(ElectricalSystemType.Telephone)]
    [InlineData(ElectricalSystemType.Security)]
    public void Create_SupportsAllSystemTypes(ElectricalSystemType systemType)
    {
        var panelConn = MakeConnector("p1");
        var d1 = MakeConnector("d1");

        var circuit = ElectricalCircuitService.Create(panelConn, d1, systemType);
        Assert.Equal(systemType, circuit.SystemType);
    }

    // ── ProjectModel integration ─────────────────────────────────────────

    [Fact]
    public void ProjectModel_HasElectricalCircuitsCollection()
    {
        var project = new ElectricalComponentSandbox.Models.ProjectModel();
        Assert.NotNull(project.ElectricalCircuits);
        Assert.Empty(project.ElectricalCircuits);
    }

    // ── Component ElectricalConnectors property ──────────────────────────

    [Fact]
    public void Component_ElectricalConnectors_NullByDefault()
    {
        var box = new BoxComponent();
        Assert.Null(box.ElectricalConnectors);
    }

    [Fact]
    public void Component_ElectricalConnectors_CanBeSet()
    {
        var box = new BoxComponent();
        box.ElectricalConnectors = new ElectricalConnectorManager();
        box.ElectricalConnectors.Connectors.Add(
            new ElectricalConnector { PortName = "Line" });

        Assert.Single(box.ElectricalConnectors.Connectors);
    }
}
