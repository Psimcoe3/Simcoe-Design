namespace ElectricalComponentSandbox.Models;

public class PanelComponent : ElectricalComponent
{
    public int CircuitCount { get; set; } = 24;
    public double Amperage { get; set; } = 200.0;
    public string PanelType { get; set; } = "Distribution Panel";
    
    public PanelComponent()
    {
        Type = ComponentType.Panel;
        Name = "Electrical Panel";
        Parameters.Width = 20.0;
        Parameters.Height = 30.0;
        Parameters.Depth = 4.0;
    }
}
