namespace ElectricalComponentSandbox.Models;

public class ConduitComponent : ElectricalComponent
{
    public double Diameter { get; set; } = 0.5;
    public double Length { get; set; } = 10.0;
    public string ConduitType { get; set; } = "EMT";
    
    public ConduitComponent()
    {
        Type = ComponentType.Conduit;
        Name = "Conduit";
    }
}
