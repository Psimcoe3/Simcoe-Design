namespace ElectricalComponentSandbox.Models;

/// <summary>
/// A transformer in the distribution hierarchy.
/// Maps to Revit's ElectricalAnalyticalNodeType.Transformer.
/// </summary>
public class TransformerComponent : ElectricalComponent
{
    /// <summary>Primary (line-side) voltage in volts.</summary>
    public double PrimaryVoltage { get; set; } = 480;

    /// <summary>Secondary (load-side) voltage in volts.</summary>
    public double SecondaryVoltage { get; set; } = 208;

    /// <summary>Transformer capacity in kVA.</summary>
    public double KVA { get; set; } = 75;

    /// <summary>Impedance as a percentage (e.g. 5.75 means 5.75%).</summary>
    public double ImpedancePercent { get; set; } = 5.75;

    /// <summary>
    /// ID of the upstream component (panel, bus, or power source) feeding the primary side.
    /// Null when unassigned.
    /// </summary>
    public string? FeederId { get; set; }

    public TransformerComponent()
    {
        Type = ComponentType.Transformer;
        Name = "Transformer";
    }
}

/// <summary>
/// A bus duct / busway node in the distribution hierarchy.
/// Maps to Revit's ElectricalAnalyticalNodeType.Bus.
/// </summary>
public class BusComponent : ElectricalComponent
{
    /// <summary>Bus rating in amps.</summary>
    public int BusAmps { get; set; } = 800;

    /// <summary>Bus voltage in volts.</summary>
    public double Voltage { get; set; } = 480;

    /// <summary>
    /// ID of the upstream component feeding this bus.
    /// Null when unassigned.
    /// </summary>
    public string? FeederId { get; set; }

    public BusComponent()
    {
        Type = ComponentType.Bus;
        Name = "Bus";
    }
}

/// <summary>
/// The utility / generator power source at the root of a distribution tree.
/// Maps to Revit's ElectricalAnalyticalNodeType.PowerSource.
/// </summary>
public class PowerSourceComponent : ElectricalComponent
{
    /// <summary>Available fault current at the source in kA.</summary>
    public double AvailableFaultCurrentKA { get; set; } = 65;

    /// <summary>Source voltage in volts.</summary>
    public double Voltage { get; set; } = 480;

    /// <summary>Source capacity in kVA.</summary>
    public double KVA { get; set; } = 1500;

    public PowerSourceComponent()
    {
        Type = ComponentType.PowerSource;
        Name = "Utility";
    }
}

/// <summary>
/// A transfer switch (ATS/MTS) in the distribution hierarchy.
/// Maps to Revit's ElectricalAnalyticalNodeType.TransferSwitch.
/// Has two upstream feeds (normal and alternate) and one downstream output.
/// </summary>
public class TransferSwitchComponent : ElectricalComponent
{
    /// <summary>ID of the normal (primary) upstream feed.</summary>
    public string? NormalFeederId { get; set; }

    /// <summary>ID of the alternate (emergency/generator) upstream feed.</summary>
    public string? AlternateFeederId { get; set; }

    /// <summary>Switch rating in amps.</summary>
    public int AmpsRating { get; set; } = 400;

    public TransferSwitchComponent()
    {
        Type = ComponentType.TransferSwitch;
        Name = "Transfer Switch";
    }
}
