namespace ElectricalComponentSandbox.Models;

/// <summary>
/// One row of the wire specification library — describes the conductor count,
/// sizes, and minimum conduit for a feeder at a given amperage. Schema mirrors
/// the eVolve <c>WireSpecificationData</c> element in
/// <c>ParameterPushData.xml</c> so libraries roundtrip without lossy
/// translation.
/// </summary>
public sealed class WireSpecification
{
    /// <summary>Configuration name, e.g. "1P3W-CU" (1-phase, 3-wire, copper).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Conductor material — typically "Copper" or "Aluminum".</summary>
    public string MaterialName { get; set; } = "Copper";

    /// <summary>Stable feeder identifier (e.g. "1P3W-CU-100A"). Used to bind a conduit run to a spec.</summary>
    public string FeederId { get; set; } = string.Empty;

    /// <summary>Nameplate amperage of the feeder.</summary>
    public double Amperage { get; set; }

    /// <summary>Phase conductor AWG / kcmil size as a string ("1", "1/0", "250", "500", "750").</summary>
    public string PhaseSize { get; set; } = string.Empty;

    /// <summary>Number of phase conductors per parallel set.</summary>
    public int PhaseQuantity { get; set; }

    /// <summary>Neutral conductor size; empty string when no neutral is included.</summary>
    public string NeutralSize { get; set; } = string.Empty;

    /// <summary>Number of neutral conductors per parallel set.</summary>
    public int NeutralQuantity { get; set; }

    /// <summary>Ground conductor size; empty string when no ground is included.</summary>
    public string GroundSize { get; set; } = string.Empty;

    /// <summary>Number of ground conductors.</summary>
    public int GroundQuantity { get; set; }

    /// <summary>Isolated ground size; empty string when no isolated ground is included.</summary>
    public string IsoGroundSize { get; set; } = string.Empty;

    /// <summary>Number of isolated ground conductors.</summary>
    public int IsoGroundQuantity { get; set; }

    /// <summary>Number of parallel sets (1 = single run, 2 = parallel, etc.).</summary>
    public int ParallelQuantity { get; set; } = 1;

    /// <summary>
    /// Minimum conduit size in feet, matching the eVolve <c>ConduitSize</c>
    /// encoding (0.125 ft = 1-1/2", 0.16666... ft = 2", 0.25 ft = 3", etc.).
    /// </summary>
    public double ConduitSizeFeet { get; set; }
}

/// <summary>
/// One row of the wire-size table — describes the ampacity and outer diameter
/// of a specific gauge / insulation / material combination. Mirrors the eVolve
/// <c>WireSizeData</c> element.
/// </summary>
public sealed class WireSizeEntry
{
    /// <summary>Conductor material ("Copper" / "Aluminum").</summary>
    public string MaterialName { get; set; } = "Copper";

    /// <summary>Insulation type ("THHN", "XHHW", etc.).</summary>
    public string Insulation { get; set; } = "THHN";

    /// <summary>NEC ampacity at 75°C termination (table 310.16).</summary>
    public double Ampacity { get; set; }

    /// <summary>AWG / kcmil gauge as the eVolve string (e.g. "14", "1/0", "500", "750").</summary>
    public string Gauge { get; set; } = string.Empty;

    /// <summary>Outer diameter in feet (eVolve encoding).</summary>
    public double DiameterFeet { get; set; }
}

/// <summary>
/// Highlight-rule for a single mismatch category. The renderer / QA pass
/// colors flagged runs with <see cref="HighlightColorValue"/> when
/// <see cref="Enabled"/> is true.
/// </summary>
public sealed class RunScheduleHighlightRule
{
    public bool Enabled { get; set; }
    public string HighlightColorValue { get; set; } = "Red";
}

/// <summary>
/// Wire-description rendering format — controls how the wire description on
/// a conduit run is composed from the spec parts. Mirrors the eVolve
/// <c>WireDescriptionFormat</c> enum.
/// </summary>
public enum WireDescriptionFormat
{
    Hyphen,
    Comma,
    Plus,
}

/// <summary>
/// Full project-scoped wire-spec library: feeder catalog + wire-size table +
/// highlight rules + status / format settings. Mirrors the eVolve
/// <c>ParameterPushData</c> XML root so an exported library is immediately
/// importable into eVolve-equipped Revit.
/// </summary>
public sealed class RunScheduleConfiguration
{
    public bool SystemColorsEnabled { get; set; } = true;
    public List<string> SystemColors { get; set; } = new();

    public RunScheduleHighlightRule RunIdNotDefined { get; set; } = new() { Enabled = true, HighlightColorValue = "Red" };
    public RunScheduleHighlightRule RunIdNotAssigned { get; set; } = new() { Enabled = true, HighlightColorValue = "Orange" };
    public RunScheduleHighlightRule SizeMismatch { get; set; } = new() { Enabled = true, HighlightColorValue = "Green" };
    public RunScheduleHighlightRule StartMismatch { get; set; } = new() { Enabled = false, HighlightColorValue = "Blue" };
    public RunScheduleHighlightRule FinishMismatch { get; set; } = new() { Enabled = false, HighlightColorValue = "Purple" };
    public RunScheduleHighlightRule TypeMismatch { get; set; } = new() { Enabled = false, HighlightColorValue = "Cyan" };
    public RunScheduleHighlightRule SystemMismatch { get; set; } = new() { Enabled = false, HighlightColorValue = "Fuchsia" };

    public bool AutomaticallyPushRunInfo { get; set; } = true;

    /// <summary>Maximum allowed wire fill (NEC chapter 9 — 0.4 = 40% three-or-more conductors).</summary>
    public double MaximumWireFill { get; set; } = 0.4;

    public WireDescriptionFormat WireDescriptionFormat { get; set; } = WireDescriptionFormat.Hyphen;

    public List<string> RunStatuses { get; set; } = new();

    public List<WireSpecification> WireSpecifications { get; set; } = new();
    public List<WireSizeEntry> WireSizes { get; set; } = new();

    /// <summary>Default status assigned to a run when it's bound to a feeder.</summary>
    public string StatusSetOnAssignRun { get; set; } = "Not Used";
}
