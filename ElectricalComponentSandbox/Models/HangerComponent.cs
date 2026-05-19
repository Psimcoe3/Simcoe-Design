namespace ElectricalComponentSandbox.Models;

public class HangerComponent : ElectricalComponent
{
    public double RodDiameter { get; set; } = 0.375;
    public double RodLength { get; set; } = 12.0;
    public string HangerType { get; set; } = "Threaded Rod";
    public double LoadCapacity { get; set; } = 150.0;

    /// <summary>
    /// Multi-tier trapeze configuration: tier struts, rods, attachments, and finish.
    /// Drives the trapeze BOM and the spool-sheet hanger schedule. Null on legacy
    /// hangers; consumers that need a structured assembly should call
    /// <see cref="EnsureTrapeze"/> to materialize a single-tier default.
    /// </summary>
    public TrapezeAssembly? Trapeze { get; set; }

    public HangerComponent()
    {
        Type = ComponentType.Hanger;
        Name = "Hanger";
        Parameters.Width = 0.375;
        Parameters.Height = 12.0;
        Parameters.Depth = 0.375;
        Parameters.Color = "#A0A0A0";
    }

    /// <summary>
    /// Returns the current trapeze assembly, creating a single-tier default
    /// the first time it is requested so callers don't have to null-check.
    /// </summary>
    public TrapezeAssembly EnsureTrapeze()
    {
        return Trapeze ??= TrapezeAssembly.CreateSingleTierDefault();
    }
}
