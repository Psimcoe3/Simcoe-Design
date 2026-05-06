using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class ElectricalCircuitServiceTests
{
    // ── Create compatibility guards ─────────────────────────────────────

    [Fact]
    public void Create_SystemTypeMismatch_ThrowsAndLeavesConnectorsUnstamped()
    {
        var panelConn = MakeConnector("p1", "Main", ElectricalSystemType.PowerCircuit);
        var deviceConn = MakeConnector("d1", "Data", ElectricalSystemType.Data);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, deviceConn, ElectricalSystemType.PowerCircuit));

        Assert.Contains("requested circuit type", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(panelConn.CircuitId);
        Assert.Null(deviceConn.CircuitId);
    }

    [Fact]
    public void Create_NonElectricalConnector_ThrowsAndLeavesConnectorsUnstamped()
    {
        var panelConn = MakeConnector("p1", "Main");
        var deviceConn = MakeConnector("d1", "Raceway");
        deviceConn.Domain = ConnectorDomain.Conduit;

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, deviceConn, ElectricalSystemType.PowerCircuit));

        Assert.Null(panelConn.CircuitId);
        Assert.Null(deviceConn.CircuitId);
    }

    [Fact]
    public void Create_VoltageMismatch_ThrowsAndLeavesConnectorsUnstamped()
    {
        var panelConn = MakeConnector("p1", "Main");
        panelConn.Voltage = 277;
        var deviceConn = MakeConnector("d1", "Line");
        deviceConn.Voltage = 120;

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, deviceConn, ElectricalSystemType.PowerCircuit));

        Assert.Null(panelConn.CircuitId);
        Assert.Null(deviceConn.CircuitId);
    }

    [Fact]
    public void Create_PhaseMismatch_ThrowsAndLeavesConnectorsUnstamped()
    {
        var panelConn = MakeConnector("p1", "Main");
        panelConn.Phase = "A";
        var deviceConn = MakeConnector("d1", "Line");
        deviceConn.Phase = "B";

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, deviceConn, ElectricalSystemType.PowerCircuit));

        Assert.Null(panelConn.CircuitId);
        Assert.Null(deviceConn.CircuitId);
    }

    [Fact]
    public void Create_MultipleDevicesWithIncompatibleConnector_DoesNotStampAnyConnector()
    {
        var panelConn = MakeConnector("p1", "Main");
        var d1 = MakeConnector("d1", "Line");
        var d2 = MakeConnector("d2", "Data", ElectricalSystemType.Data);

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.Create(panelConn, new[] { d1, d2 }, ElectricalSystemType.PowerCircuit));

        Assert.Null(panelConn.CircuitId);
        Assert.Null(d1.CircuitId);
        Assert.Null(d2.CircuitId);
    }

    [Fact]
    public void AddDevice_SystemTypeMismatch_ThrowsAndLeavesCircuitUnchanged()
    {
        var panelConn = MakeConnector("p1", "Main");
        var d1 = MakeConnector("d1", "Line");
        var circuit = ElectricalCircuitService.Create(panelConn, d1, ElectricalSystemType.PowerCircuit);
        var dataConnector = MakeConnector("d2", "Data", ElectricalSystemType.Data);

        Assert.Throws<InvalidOperationException>(() =>
            ElectricalCircuitService.AddDevice(circuit, dataConnector));

        Assert.DoesNotContain(dataConnector.Id, circuit.ConnectorIds);
        Assert.Null(dataConnector.CircuitId);
    }

    [Fact]
    public void ValidateNewCircuitConnection_ValidLowVoltageConnectors_ReturnsNoFindings()
    {
        var source = MakeConnector("p1", "Data Source", ElectricalSystemType.Data);
        source.Phase = string.Empty;
        source.Voltage = 0;
        var device = MakeConnector("d1", "Data", ElectricalSystemType.Data);
        device.Phase = string.Empty;
        device.Voltage = 0;

        var findings = ElectricalCircuitService.ValidateNewCircuitConnection(
            source,
            [device],
            ElectricalSystemType.Data);

        Assert.Empty(findings);
    }

    // ── ValidateCircuit ─────────────────────────────────────────────────

    [Fact]
    public void ValidateCircuit_ValidStampedPanelAndDevice_ReturnsNoFindings()
    {
        var panelConn = MakeConnector("p1", "Main");
        panelConn.Phase = "ABC";
        var panel = MakePanel(panelConn);

        var deviceConn = MakeConnector("d1", "Line");
        deviceConn.Phase = "A";
        var device = MakeDevice(deviceConn);

        var circuit = ElectricalCircuitService.Create(
            panelConn,
            deviceConn,
            ElectricalSystemType.PowerCircuit);

        var findings = ElectricalCircuitService.ValidateCircuit(
            circuit,
            new ElectricalComponent[] { panel, device });

        Assert.Empty(findings);
    }

    [Fact]
    public void ValidateCircuit_MissingDeviceConnector_ReturnsTopologyError()
    {
        var panelConn = MakeConnector("p1", "Main");
        var panel = MakePanel(panelConn);
        var circuit = new ElectricalCircuit
        {
            Id = "CKT-1",
            SystemType = ElectricalSystemType.PowerCircuit,
            ConnectorIds = { panelConn.Id }
        };
        panelConn.CircuitId = circuit.Id;

        var findings = ElectricalCircuitService.ValidateCircuit(
            circuit,
            new ElectricalComponent[] { panel });

        Assert.Contains(findings, f =>
            f.Category == ElectricalCircuitValidationCategory.Topology &&
            f.Severity == ElectricalCircuitValidationSeverity.Error);
    }

    [Fact]
    public void ValidateCircuit_SystemTypeMismatch_ReturnsError()
    {
        var panelConn = MakeConnector("p1", "Main", ElectricalSystemType.PowerCircuit);
        var panel = MakePanel(panelConn);

        var deviceConn = MakeConnector("d1", "Data", ElectricalSystemType.Data);
        var device = MakeDevice(deviceConn);

        var circuit = new ElectricalCircuit
        {
            Id = "CKT-SYS",
            SystemType = ElectricalSystemType.PowerCircuit,
            ConnectorIds = { panelConn.Id, deviceConn.Id }
        };
        panelConn.CircuitId = circuit.Id;
        deviceConn.CircuitId = circuit.Id;

        var findings = ElectricalCircuitService.ValidateCircuit(
            circuit,
            new ElectricalComponent[] { panel, device });

        Assert.Contains(findings, f =>
            f.Category == ElectricalCircuitValidationCategory.SystemType &&
            f.ConnectorId == deviceConn.Id);
    }

    [Fact]
    public void ValidateCircuit_ConduitDomainConnector_ReturnsDomainError()
    {
        var panelConn = MakeConnector("p1", "Main");
        var panel = MakePanel(panelConn);

        var conduitConnector = MakeConnector("d1", "Raceway");
        conduitConnector.Domain = ConnectorDomain.Conduit;
        var device = MakeDevice(conduitConnector);

        var circuit = new ElectricalCircuit
        {
            Id = "CKT-DOMAIN",
            SystemType = ElectricalSystemType.PowerCircuit,
            ConnectorIds = { panelConn.Id, conduitConnector.Id }
        };
        panelConn.CircuitId = circuit.Id;
        conduitConnector.CircuitId = circuit.Id;

        var findings = ElectricalCircuitService.ValidateCircuit(
            circuit,
            new ElectricalComponent[] { panel, device });

        Assert.Contains(findings, f =>
            f.Category == ElectricalCircuitValidationCategory.ConnectorDomain &&
            f.ConnectorId == conduitConnector.Id);
    }

    [Fact]
    public void ValidateCircuit_VoltageMismatch_ReturnsError()
    {
        var panelConn = MakeConnector("p1", "Main");
        panelConn.Voltage = 277;
        var panel = MakePanel(panelConn);

        var deviceConn = MakeConnector("d1", "Line");
        deviceConn.Voltage = 120;
        var device = MakeDevice(deviceConn);

        var circuit = new ElectricalCircuit
        {
            Id = "CKT-VOLT",
            SystemType = ElectricalSystemType.PowerCircuit,
            ConnectorIds = { panelConn.Id, deviceConn.Id }
        };
        panelConn.CircuitId = circuit.Id;
        deviceConn.CircuitId = circuit.Id;

        var findings = ElectricalCircuitService.ValidateCircuit(
            circuit,
            new ElectricalComponent[] { panel, device });

        Assert.Contains(findings, f =>
            f.Category == ElectricalCircuitValidationCategory.Voltage &&
            f.ConnectorId == deviceConn.Id);
    }

    [Fact]
    public void ValidateCircuit_PhaseMismatch_ReturnsError()
    {
        var panelConn = MakeConnector("p1", "Main");
        panelConn.Phase = "A";
        var panel = MakePanel(panelConn);

        var deviceConn = MakeConnector("d1", "Line");
        deviceConn.Phase = "B";
        var device = MakeDevice(deviceConn);

        var circuit = new ElectricalCircuit
        {
            Id = "CKT-PHASE",
            SystemType = ElectricalSystemType.PowerCircuit,
            ConnectorIds = { panelConn.Id, deviceConn.Id }
        };
        panelConn.CircuitId = circuit.Id;
        deviceConn.CircuitId = circuit.Id;

        var findings = ElectricalCircuitService.ValidateCircuit(
            circuit,
            new ElectricalComponent[] { panel, device });

        Assert.Contains(findings, f =>
            f.Category == ElectricalCircuitValidationCategory.Phase &&
            f.ConnectorId == deviceConn.Id);
    }

    [Fact]
    public void ValidateCircuitSet_SharedConnectorAcrossCircuits_ReturnsDuplicateErrors()
    {
        var panelConn1 = MakeConnector("p1", "Main");
        var panel = MakePanel(panelConn1);

        var deviceConn = MakeConnector("d1", "Line");
        var device = MakeDevice(deviceConn);

        var circuit1 = new ElectricalCircuit
        {
            Id = "CKT-1",
            SystemType = ElectricalSystemType.PowerCircuit,
            ConnectorIds = { panelConn1.Id, deviceConn.Id }
        };
        var circuit2 = new ElectricalCircuit
        {
            Id = "CKT-2",
            SystemType = ElectricalSystemType.PowerCircuit,
            ConnectorIds = { panelConn1.Id, deviceConn.Id }
        };
        panelConn1.CircuitId = circuit1.Id;
        deviceConn.CircuitId = circuit1.Id;

        var findings = ElectricalCircuitService.ValidateCircuitSet(
            new[] { circuit1, circuit2 },
            new ElectricalComponent[] { panel, device });

        Assert.Contains(findings, f =>
            f.Category == ElectricalCircuitValidationCategory.DuplicateConnector &&
            f.ConnectorId == deviceConn.Id);
    }
}
