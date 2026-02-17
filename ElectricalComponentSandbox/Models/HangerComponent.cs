namespace ElectricalComponentSandbox.Models;

public class HangerComponent : ElectricalComponent
{
    public double RodDiameter { get; set; } = 0.375;
    public double RodLength { get; set; } = 12.0;
    public string HangerType { get; set; } = "Threaded Rod";
    public double LoadCapacity { get; set; } = 150.0;
    
    public HangerComponent()
    {
        Type = ComponentType.Hanger;
        Name = "Hanger";
        Parameters.Width = 0.375;
        Parameters.Height = 12.0;
        Parameters.Depth = 0.375;
        Parameters.Color = "#A0A0A0";
    }
}
