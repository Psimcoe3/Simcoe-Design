namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// Global conduit settings for the project.
/// </summary>
public class ConduitSettings
{
    /// <summary>Prefix for trade size display, e.g. "TS".</summary>
    public string SizePrefix { get; set; } = "";

    /// <summary>Separator between connector labels.</summary>
    public string ConnectorSeparator { get; set; } = " - ";

    /// <summary>Default connection tolerance in feet.</summary>
    public double ConnectionTolerance { get; set; } = 0.01;

    /// <summary>Default conduit type ID used for new segments.</summary>
    public string DefaultConduitTypeId { get; set; } = string.Empty;

    /// <summary>Default trade size for new segments.</summary>
    public string DefaultTradeSize { get; set; } = "1/2";

    /// <summary>Default elevation for conduit runs (in feet).</summary>
    public double DefaultElevation { get; set; } = 10.0;

    /// <summary>Whether to automatically insert fittings at bends.</summary>
    public bool AutoInsertFittings { get; set; } = true;

    /// <summary>Hidden line gap settings.</summary>
    public MEPHiddenLineSettings HiddenLineSettings { get; set; } = new();
}

/// <summary>
/// Controls hidden line display for conduit crossings in plan view.
/// Analogous to Revit's MEPHiddenLineSettings.
/// </summary>
public class MEPHiddenLineSettings
{
    /// <summary>Whether to show hidden line gaps at crossings.</summary>
    public bool ShowGaps { get; set; } = true;

    /// <summary>Gap distance in document units at crossing points.</summary>
    public double GapDistance { get; set; } = 4.0;

    /// <summary>Whether higher-elevation conduit hides lower.</summary>
    public bool HigherElementHides { get; set; } = true;
}
