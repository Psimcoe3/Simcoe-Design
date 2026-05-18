namespace ElectricalComponentSandbox.Models;

using ElectricalComponentSandbox.Services;

/// <summary>
/// Classifies the panel equipment type, paralleling Revit's
/// IsSwitchboard / ElectricalAnalyticalNodeType distinctions.
/// </summary>
public enum PanelSubtype
{
    LoadCenter,
    Panelboard,
    Switchboard,
    MCCSection,
    TransferSwitch
}

public class PanelComponent : ElectricalComponent
{
    public int CircuitCount { get; set; } = 24;
    public double Amperage { get; set; } = 200.0;
    public string PanelType { get; set; } = "Distribution Panel";

    /// <summary>
    /// Nominal system voltage at the panel for validation checks that depend on voltage-to-ground.
    /// </summary>
    public double NominalVoltage { get; set; } = 208.0;

    /// <summary>
    /// Structured equipment subtype (LoadCenter, Switchboard, etc.).
    /// </summary>
    public PanelSubtype Subtype { get; set; } = PanelSubtype.LoadCenter;

    /// <summary>
    /// When true, the panel has feed-through lugs that allow downstream
    /// panels to be fed without consuming a breaker space.
    /// </summary>
    public bool HasFeedThruLugs { get; set; }

    /// <summary>
    /// When true, the panel has no main breaker — only main lugs.
    /// Replaces string-based "MLO" checks on <see cref="PanelType"/>.
    /// </summary>
    public bool MainLugOnly { get; set; }

    /// <summary>
    /// Available Interrupting Capacity rating in kA (kilo-Amperes).
    /// Standard values: 10, 14, 22, 25, 42, 65, 100 kA.
    /// </summary>
    public double AICRatingKA { get; set; } = 10.0;

    /// <summary>
    /// Bus bar ampacity, separate from the main breaker trip rating.
    /// Typical values: 100, 200, 225, 400, 600, 800, 1200, 1600, 2000, 3000, 4000 A.
    /// </summary>
    public double BusAmpacity { get; set; } = 200.0;

    /// <summary>
    /// ID of the upstream panel, bus, or transformer that feeds this panel.
    /// Null for panels fed directly from a power source or when unassigned.
    /// </summary>
    public string? FeederId { get; set; }

    /// <summary>
    /// NEC 110.26(A)(1) installation condition used for working clearance depth.
    /// </summary>
    public ElectricalRoomClearanceService.ClearanceCondition WorkingClearanceCondition { get; set; }
        = ElectricalRoomClearanceService.ClearanceCondition.Condition2;

    /// <summary>
    /// Provided working clearance depth (feet) in front of the panel.
    /// A value of 0 means the project has not supplied clearance data yet.
    /// </summary>
    public double ProvidedWorkingClearanceDepthFeet { get; set; }

    /// <summary>
    /// Provided working clearance width (inches) in front of the panel.
    /// A value of 0 means the project has not supplied clearance data yet.
    /// </summary>
    public double ProvidedWorkingClearanceWidthInches { get; set; }

    /// <summary>
    /// Provided working clearance headroom (feet) at the panel location.
    /// A value of 0 means the project has not supplied clearance data yet.
    /// </summary>
    public double ProvidedWorkingClearanceHeightFeet { get; set; }

    /// <summary>
    /// True when all three working-clearance dimensions are populated.
    /// </summary>
    public bool HasWorkingClearanceData =>
        ProvidedWorkingClearanceDepthFeet > 0 &&
        ProvidedWorkingClearanceWidthInches > 0 &&
        ProvidedWorkingClearanceHeightFeet > 0;

    /// <summary>
    /// Provided dedicated equipment-space width (inches) per NEC 110.26(E).
    /// A value of 0 means the project has not supplied dedicated-space data yet.
    /// </summary>
    public double ProvidedDedicatedSpaceWidthInches { get; set; }

    /// <summary>
    /// Provided dedicated equipment-space depth (inches) per NEC 110.26(E).
    /// A value of 0 means the project has not supplied dedicated-space data yet.
    /// </summary>
    public double ProvidedDedicatedSpaceDepthInches { get; set; }

    /// <summary>
    /// Provided dedicated equipment-space height (feet) per NEC 110.26(E).
    /// A value of 0 means the project has not supplied dedicated-space data yet.
    /// </summary>
    public double ProvidedDedicatedSpaceHeightFeet { get; set; }

    /// <summary>
    /// True when all three dedicated equipment-space dimensions are populated.
    /// </summary>
    public bool HasDedicatedSpaceData =>
        ProvidedDedicatedSpaceWidthInches > 0 &&
        ProvidedDedicatedSpaceDepthInches > 0 &&
        ProvidedDedicatedSpaceHeightFeet > 0;
    
    public PanelComponent()
    {
        Type = ComponentType.Panel;
        Name = "Electrical Panel";
        Parameters.Width = 20.0;
        Parameters.Height = 30.0;
        Parameters.Depth = 4.0;
    }
}
