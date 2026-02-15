namespace ElectricalComponentSandbox.Models;

public class SupportComponent : ElectricalComponent
{
    public double LoadCapacity { get; set; } = 100.0;
    public string SupportType { get; set; } = "Bracket";
    
    public SupportComponent()
    {
        Type = ComponentType.Support;
        Name = "Support Bracket";
        Parameters.Width = 2.0;
        Parameters.Height = 2.0;
        Parameters.Depth = 1.0;
    }
}
