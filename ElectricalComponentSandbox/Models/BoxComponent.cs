namespace ElectricalComponentSandbox.Models;

public class BoxComponent : ElectricalComponent
{
    public int KnockoutCount { get; set; } = 4;
    public string BoxType { get; set; } = "Junction Box";
    
    public BoxComponent()
    {
        Type = ComponentType.Box;
        Name = "Electrical Box";
        Parameters.Width = 4.0;
        Parameters.Height = 4.0;
        Parameters.Depth = 2.0;
    }
}
