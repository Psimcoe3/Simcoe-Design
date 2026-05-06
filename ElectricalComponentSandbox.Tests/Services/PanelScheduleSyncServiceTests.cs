using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class PanelScheduleSyncServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static PanelComponent MakePanel(string id, string name = "LP-1")
    {
        var panel = new PanelComponent { Name = name };
        panel.Id = id;
        panel.ElectricalConnectors = new ElectricalConnectorManager();
        return panel;
    }

    private static ElectricalConnector MakePanelConnector(
        string id, string panelId, string phase = "A", double voltage = 120.0,
        ElectricalSystemType systemType = ElectricalSystemType.PowerCircuit)
    {
        return new ElectricalConnector
        {
            Id = id,
            ComponentId = panelId,
            Phase = phase,
            Voltage = voltage,
            SystemType = systemType,
            Domain = ConnectorDomain.Electrical
        };
    }

    private static ElectricalComponent MakeDevice(string id, string name)
    {
        var dev = new ElectricalComponent_Stub { Name = name };
        dev.Id = id;
        dev.ElectricalConnectors = new ElectricalConnectorManager();
        return dev;
    }

    private static ElectricalConnector MakeDeviceConnector(
        string id, string deviceId, string phase = "A", double voltage = 120.0)
    {
        return new ElectricalConnector
        {
            Id = id,
            ComponentId = deviceId,
            Phase = phase,
            Voltage = voltage,
            Domain = ConnectorDomain.Electrical
        };
    }

    private static ElectricalCircuit MakeCircuit(
        string panelConnectorId,
        IEnumerable<string> deviceConnectorIds,
        string? scheduleCircuitId = null)
    {
        var ec = new ElectricalCircuit
        {
            ScheduleCircuitId = scheduleCircuitId
        };
        ec.ConnectorIds.Add(panelConnectorId);
        foreach (var id in deviceConnectorIds) ec.ConnectorIds.Add(id);
        return ec;
    }

    // Concrete stub so we can instantiate ElectricalComponent (abstract)
    private class ElectricalComponent_Stub : ElectricalComponent
    {
        public ElectricalComponent_Stub() { Type = ComponentType.Box; }
    }

    // ── PhaseToPoles ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("A",   1)]
    [InlineData("B",   1)]
    [InlineData("C",   1)]
    [InlineData("AB",  2)]
    [InlineData("BC",  2)]
    [InlineData("CA",  2)]
    [InlineData("ABC", 3)]
    [InlineData("",    1)]
    public void PhaseToPoles_ReturnsExpectedPoles(string phase, int expected)
    {
        Assert.Equal(expected, PanelScheduleSyncService.PhaseToPoles(phase));
    }

    // ── SyncPanel – no circuits ───────────────────────────────────────────────

    [Fact]
    public void SyncPanel_NoPanelCircuits_ReturnsEmptySynced()
    {
        var panel = MakePanel("P1");
        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel],
            [],
            []);

        Assert.Empty(result.SyncedCircuits);
        Assert.Empty(result.StaleCircuitIds);
        Assert.Equal("P1", result.PanelId);
        Assert.Equal("LP-1", result.PanelName);
    }

    // ── SyncPanel – new circuit creates schedule row ──────────────────────────

    [Fact]
    public void SyncPanel_NewCircuit_AddsScheduleRow()
    {
        var panel = MakePanel("P1", "LP-1");
        var panelConn = MakePanelConnector("PC1", "P1", "A", 120.0);
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var device = MakeDevice("D1", "Receptacle 1");
        var devConn = MakeDeviceConnector("DC1", "D1", "A", 120.0);
        device.ElectricalConnectors!.Connectors.Add(devConn);

        var ec = MakeCircuit("PC1", ["DC1"]);

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, device],
            [ec],
            []);

        Assert.Single(result.SyncedCircuits);
        var entry = result.SyncedCircuits[0];
        Assert.Equal(CircuitSyncAction.Added, entry.Action);
        Assert.Equal(ec.Id, entry.ElectricalCircuitId);
        Assert.Equal("P1", entry.Circuit.PanelId);
        Assert.Equal("A", entry.Circuit.Phase);
        Assert.Equal(120.0, entry.Circuit.Voltage);
        Assert.Equal(1, entry.Circuit.Poles);
        Assert.Equal("Receptacle 1", entry.Circuit.Description);
        Assert.Equal(CircuitSlotType.Circuit, entry.Circuit.SlotType);
        Assert.Empty(result.StaleCircuitIds);
    }

    // ── SyncPanel – circuit number assignment ─────────────────────────────────

    [Fact]
    public void SyncPanel_TwoNewCircuits_AssignsOddNumbers()
    {
        var panel = MakePanel("P1");
        var panelConnA = MakePanelConnector("PCA", "P1", "A");
        var panelConnB = MakePanelConnector("PCB", "P1", "B");
        panel.ElectricalConnectors!.Connectors.AddRange([panelConnA, panelConnB]);

        var devA = MakeDevice("DA", "Device A");
        var dcA = MakeDeviceConnector("DCA", "DA");
        devA.ElectricalConnectors!.Connectors.Add(dcA);

        var devB = MakeDevice("DB", "Device B");
        var dcB = MakeDeviceConnector("DCB", "DB");
        devB.ElectricalConnectors!.Connectors.Add(dcB);

        var ec1 = MakeCircuit("PCA", ["DCA"]);
        var ec2 = MakeCircuit("PCB", ["DCB"]);

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, devA, devB],
            [ec1, ec2],
            []);

        var numbers = result.SyncedCircuits.Select(e => e.Circuit.CircuitNumber).ToList();
        Assert.Contains("1", numbers);
        Assert.Contains("3", numbers);
    }

    [Fact]
    public void SyncPanel_ExistingCircuitNumberOccupied_SkipsToNextOdd()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1");
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev = MakeDevice("D1", "Dev");
        var dc = MakeDeviceConnector("DC1", "D1");
        dev.ElectricalConnectors!.Connectors.Add(dc);

        var ec = MakeCircuit("PC1", ["DC1"]);

        // Pre-occupy circuit number "1"
        var existing = new Circuit { PanelId = "P1", CircuitNumber = "1", Id = "E1" };

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, dev],
            [ec],
            [existing]);

        // "1" is occupied by the stale existing row, new circuit gets "3"
        Assert.Single(result.SyncedCircuits);
        Assert.Equal("3", result.SyncedCircuits[0].Circuit.CircuitNumber);
    }

    // ── SyncPanel – existing circuit with matching ScheduleCircuitId ──────────

    [Fact]
    public void SyncPanel_ExistingCircuit_Unchanged_WhenDataMatches()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1", "A", 120.0);
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev = MakeDevice("D1", "Light 1");
        var dc = MakeDeviceConnector("DC1", "D1", "A", 120.0);
        dev.ElectricalConnectors!.Connectors.Add(dc);

        var existingCircuit = new Circuit
        {
            Id = "SC-1",
            PanelId = "P1",
            CircuitNumber = "1",
            Phase = "A",
            Voltage = 120.0,
            Poles = 1,
            SystemType = ElectricalSystemType.PowerCircuit,
            Description = "Light 1",
            Breaker = new CircuitBreaker { Poles = 1 },
        };

        var ec = MakeCircuit("PC1", ["DC1"], scheduleCircuitId: "SC-1");

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, dev],
            [ec],
            [existingCircuit]);

        Assert.Single(result.SyncedCircuits);
        Assert.Equal(CircuitSyncAction.Unchanged, result.SyncedCircuits[0].Action);
        Assert.Empty(result.StaleCircuitIds);
    }

    [Fact]
    public void SyncPanel_ExistingCircuit_Updated_WhenDescriptionChanges()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1", "A", 120.0);
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev = MakeDevice("D1", "New Device Name");
        var dc = MakeDeviceConnector("DC1", "D1", "A", 120.0);
        dev.ElectricalConnectors!.Connectors.Add(dc);

        var existingCircuit = new Circuit
        {
            Id = "SC-1",
            PanelId = "P1",
            CircuitNumber = "1",
            Phase = "A",
            Voltage = 120.0,
            Poles = 1,
            SystemType = ElectricalSystemType.PowerCircuit,
            Description = "Old Device Name",
            Breaker = new CircuitBreaker { Poles = 1 },
        };

        var ec = MakeCircuit("PC1", ["DC1"], scheduleCircuitId: "SC-1");

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, dev],
            [ec],
            [existingCircuit]);

        Assert.Single(result.SyncedCircuits);
        Assert.Equal(CircuitSyncAction.Updated, result.SyncedCircuits[0].Action);
        Assert.Equal("New Device Name", result.SyncedCircuits[0].Circuit.Description);
    }

    // ── SyncPanel – stale circuit detection ───────────────────────────────────

    [Fact]
    public void SyncPanel_ExistingCircuit_WithNoMatchingElectricalCircuit_IsStale()
    {
        var panel = MakePanel("P1");

        var staleRow = new Circuit { Id = "STALE-1", PanelId = "P1", CircuitNumber = "1" };

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel],
            [],
            [staleRow]);

        Assert.Contains("STALE-1", result.StaleCircuitIds);
        Assert.Empty(result.SyncedCircuits);
    }

    // ── SyncPanel – multi-phase circuit poles ─────────────────────────────────

    [Fact]
    public void SyncPanel_ThreePhaseCircuit_ThreePoles()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1", "ABC", 208.0);
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev = MakeDevice("D1", "Motor");
        var dc = MakeDeviceConnector("DC1", "D1", "ABC", 208.0);
        dev.ElectricalConnectors!.Connectors.Add(dc);

        var ec = MakeCircuit("PC1", ["DC1"]);

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, dev],
            [ec],
            []);

        Assert.Single(result.SyncedCircuits);
        Assert.Equal(3, result.SyncedCircuits[0].Circuit.Poles);
        Assert.Equal(30, result.SyncedCircuits[0].Circuit.Breaker.TripAmps);
    }

    [Fact]
    public void SyncPanel_TwoPoleCircuit_TwoPoles()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1", "AB", 240.0);
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev = MakeDevice("D1", "Dryer");
        var dc = MakeDeviceConnector("DC1", "D1", "AB", 240.0);
        dev.ElectricalConnectors!.Connectors.Add(dc);

        var ec = MakeCircuit("PC1", ["DC1"]);

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, dev],
            [ec],
            []);

        Assert.Single(result.SyncedCircuits);
        Assert.Equal(2, result.SyncedCircuits[0].Circuit.Poles);
    }

    // ── SyncPanel – multiple devices ─────────────────────────────────────────

    [Fact]
    public void SyncPanel_MultipleDevices_JoinsDescriptionDistinct()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1");
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev1 = MakeDevice("D1", "Receptacle");
        var dc1 = MakeDeviceConnector("DC1", "D1");
        dev1.ElectricalConnectors!.Connectors.Add(dc1);

        var dev2 = MakeDevice("D2", "Receptacle");
        var dc2 = MakeDeviceConnector("DC2", "D2");
        dev2.ElectricalConnectors!.Connectors.Add(dc2);

        var dev3 = MakeDevice("D3", "Switch");
        var dc3 = MakeDeviceConnector("DC3", "D3");
        dev3.ElectricalConnectors!.Connectors.Add(dc3);

        var ec = MakeCircuit("PC1", ["DC1", "DC2", "DC3"]);

        var result = PanelScheduleSyncService.SyncPanel(
            "P1",
            [panel, dev1, dev2, dev3],
            [ec],
            []);

        // "Receptacle" appears twice but should be listed once; "Switch" once
        string desc = result.SyncedCircuits[0].Circuit.Description;
        Assert.Contains("Receptacle", desc);
        Assert.Contains("Switch", desc);
        // Count occurrences of "Receptacle" — should be exactly 1
        Assert.Equal(1, desc.Split("Receptacle").Length - 1);
    }

    // ── SyncAllPanels ─────────────────────────────────────────────────────────

    [Fact]
    public void SyncAllPanels_TwoPanels_ReturnsTwoResults()
    {
        var panelA = MakePanel("PA", "LP-A");
        var panelB = MakePanel("PB", "LP-B");

        var connA = MakePanelConnector("PCA", "PA");
        panelA.ElectricalConnectors!.Connectors.Add(connA);
        var connB = MakePanelConnector("PCB", "PB");
        panelB.ElectricalConnectors!.Connectors.Add(connB);

        var devA = MakeDevice("DA", "Dev A");
        var dcA = MakeDeviceConnector("DCA", "DA");
        devA.ElectricalConnectors!.Connectors.Add(dcA);

        var devB = MakeDevice("DB", "Dev B");
        var dcB = MakeDeviceConnector("DCB", "DB");
        devB.ElectricalConnectors!.Connectors.Add(dcB);

        var ecA = MakeCircuit("PCA", ["DCA"]);
        var ecB = MakeCircuit("PCB", ["DCB"]);

        var results = PanelScheduleSyncService.SyncAllPanels(
            [panelA, panelB, devA, devB],
            [ecA, ecB],
            []);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.PanelId == "PA");
        Assert.Contains(results, r => r.PanelId == "PB");
        Assert.All(results, r => Assert.Single(r.SyncedCircuits));
    }

    [Fact]
    public void SyncAllPanels_NoCircuits_AllPanelsReturnEmpty()
    {
        var panelA = MakePanel("PA");
        var panelB = MakePanel("PB");

        var results = PanelScheduleSyncService.SyncAllPanels(
            [panelA, panelB],
            [],
            []);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Empty(r.SyncedCircuits));
    }

    // ── SyncPanel – panel not found ───────────────────────────────────────────

    [Fact]
    public void SyncPanel_UnknownPanelId_ReturnsEmptyWithIdAsFallbackName()
    {
        var result = PanelScheduleSyncService.SyncPanel(
            "UNKNOWN",
            [],
            [],
            []);

        Assert.Equal("UNKNOWN", result.PanelId);
        Assert.Equal("UNKNOWN", result.PanelName);
        Assert.Empty(result.SyncedCircuits);
    }

    // ── SyncPanel – voltage/systemType propagation ────────────────────────────

    [Fact]
    public void SyncPanel_480VCircuit_SetsVoltageCorrectly()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1", "ABC", 480.0);
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev = MakeDevice("D1", "Motor");
        var dc = MakeDeviceConnector("DC1", "D1", "ABC", 480.0);
        dev.ElectricalConnectors!.Connectors.Add(dc);

        var ec = MakeCircuit("PC1", ["DC1"]);

        var result = PanelScheduleSyncService.SyncPanel(
            "P1", [panel, dev], [ec], []);

        Assert.Equal(480.0, result.SyncedCircuits[0].Circuit.Voltage);
    }

    [Fact]
    public void SyncPanel_ControlsCircuit_SystemTypePropagated()
    {
        var panel = MakePanel("P1");
        var panelConn = MakePanelConnector("PC1", "P1", "A", 24.0,
            ElectricalSystemType.Controls);
        panel.ElectricalConnectors!.Connectors.Add(panelConn);

        var dev = MakeDevice("D1", "Control Panel");
        var dc = MakeDeviceConnector("DC1", "D1", "A", 24.0);
        dev.ElectricalConnectors!.Connectors.Add(dc);

        var ec = MakeCircuit("PC1", ["DC1"]);

        var result = PanelScheduleSyncService.SyncPanel(
            "P1", [panel, dev], [ec], []);

        Assert.Equal(ElectricalSystemType.Controls,
            result.SyncedCircuits[0].Circuit.SystemType);
    }
}
