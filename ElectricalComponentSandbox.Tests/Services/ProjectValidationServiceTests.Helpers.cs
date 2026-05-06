using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class ProjectValidationServiceTests
{
    private static ElectricalConnector CreatePowerConnector(
        string id,
        string componentId,
        string portName,
        double voltage,
        string phase)
    {
        return new ElectricalConnector
        {
            Id = id,
            ComponentId = componentId,
            PortName = portName,
            SystemType = ElectricalSystemType.PowerCircuit,
            Domain = ConnectorDomain.Electrical,
            Voltage = voltage,
            Phase = phase,
        };
    }

    private static PanelComponent CreatePanelWithConnector(
        string id,
        string name,
        ElectricalConnector connector)
    {
        return new PanelComponent
        {
            Id = id,
            Name = name,
            ElectricalConnectors = new ElectricalConnectorManager
            {
                Connectors = { connector }
            }
        };
    }

    private static BoxComponent CreateBoxWithConnector(
        string id,
        string name,
        ElectricalConnector connector)
    {
        return new BoxComponent
        {
            Id = id,
            Name = name,
            ElectricalConnectors = new ElectricalConnectorManager
            {
                Connectors = { connector }
            }
        };
    }

    private static Circuit CreatePhaseCircuit(string number, string phase, double loadVa)
    {
        return new Circuit
        {
            CircuitNumber = number,
            Voltage = 277,
            Poles = 1,
            Phase = phase,
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            ConnectedLoadVA = loadVa,
            DemandFactor = 1.0,
            Wire = new WireSpec { Size = "12", Conductors = 2, GroundSize = "12", Material = ConductorMaterial.Copper },
            SlotType = CircuitSlotType.Circuit,
            LoadClassification = LoadClassification.Lighting,
        };
    }
}