namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Represents a drawing layer for organizing components
/// </summary>
public class Layer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Layer";
    public string Color { get; set; } = "#808080";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    
    public static Layer CreateDefault()
    {
        return new Layer
        {
            Id = "default",
            Name = "Default",
            Color = "#808080",
            IsVisible = true,
            IsLocked = false
        };
    }
}
