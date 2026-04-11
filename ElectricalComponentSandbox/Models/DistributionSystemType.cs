namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Phase configuration for a distribution system.
/// Maps to Revit's <c>DistributionSysType</c> wiring types.
/// </summary>
public enum PhaseConfiguration
{
    /// <summary>Single-phase, two-wire or three-wire (e.g. 120/240V residential).</summary>
    SinglePhase,
    /// <summary>Wye (star) three-phase, four-wire (e.g. 120/208V, 277/480V).</summary>
    Wye,
    /// <summary>Delta three-phase, three-wire (e.g. 240V Δ).</summary>
    Delta
}

/// <summary>
/// A named distribution system type that defines voltage, phase configuration, and wiring layout.
/// Mirrors Revit's <c>ElectricalSetting.DistributionSystemTypes</c>.
/// Stored in <see cref="ProjectModel.DistributionSystems"/>; referenced by
/// <see cref="PanelSchedule.DistributionSystemId"/>.
/// </summary>
public class DistributionSystemType
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name (e.g. "120/208V Wye").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Phase topology.</summary>
    public PhaseConfiguration Phase { get; set; } = PhaseConfiguration.Wye;

    /// <summary>Line-to-line voltage in volts.</summary>
    public double LineVoltage { get; set; } = 208;

    /// <summary>Line-to-neutral (phase) voltage in volts. Zero for delta (no neutral).</summary>
    public double PhaseVoltage { get; set; } = 120;

    /// <summary>Number of wires in the system (e.g. 3W for 1-phase, 4W for wye 3-phase).</summary>
    public int Wires { get; set; } = 4;

    /// <summary>Whether this is a built-in system type that cannot be deleted.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>True when the system is three-phase (Wye or Delta).</summary>
    public bool IsThreePhase => Phase != PhaseConfiguration.SinglePhase;

    /// <summary>
    /// Produces a standard voltage label such as "120/208V 3×3W" or "120/240V 1×3W".
    /// </summary>
    public string VoltageLabel
    {
        get
        {
            string phaseCount = IsThreePhase ? "3" : "1";
            if (PhaseVoltage > 0 && Math.Abs(PhaseVoltage - LineVoltage) > 0.1)
                return $"{PhaseVoltage:F0}/{LineVoltage:F0}V {phaseCount}×{Wires}W";
            return $"{LineVoltage:F0}V {phaseCount}×{Wires}W";
        }
    }

    /// <summary>
    /// Computes total current from apparent power (VA) using the system's voltage and phase config.
    /// </summary>
    public double CalculateCurrent(double totalVA)
    {
        if (LineVoltage <= 0) return 0;
        return IsThreePhase
            ? totalVA / (LineVoltage * Math.Sqrt(3))
            : totalVA / LineVoltage;
    }

    // ── Built-In Defaults ────────────────────────────────────────────────────

    public static readonly string BuiltIn_120_208_3Ph_Id   = "builtin-120-208-3ph";
    public static readonly string BuiltIn_277_480_3Ph_Id   = "builtin-277-480-3ph";
    public static readonly string BuiltIn_120_240_1Ph_Id   = "builtin-120-240-1ph";
    public static readonly string BuiltIn_240_Delta_3Ph_Id = "builtin-240-delta-3ph";

    /// <summary>
    /// Returns the four standard built-in distribution system types.
    /// </summary>
    public static IReadOnlyList<DistributionSystemType> GetBuiltInDefaults() => new[]
    {
        new DistributionSystemType
        {
            Id = BuiltIn_120_208_3Ph_Id, Name = "120/208V Wye",
            Phase = PhaseConfiguration.Wye, LineVoltage = 208, PhaseVoltage = 120, Wires = 4,
            IsBuiltIn = true
        },
        new DistributionSystemType
        {
            Id = BuiltIn_277_480_3Ph_Id, Name = "277/480V Wye",
            Phase = PhaseConfiguration.Wye, LineVoltage = 480, PhaseVoltage = 277, Wires = 4,
            IsBuiltIn = true
        },
        new DistributionSystemType
        {
            Id = BuiltIn_120_240_1Ph_Id, Name = "120/240V 1-Phase",
            Phase = PhaseConfiguration.SinglePhase, LineVoltage = 240, PhaseVoltage = 120, Wires = 3,
            IsBuiltIn = true
        },
        new DistributionSystemType
        {
            Id = BuiltIn_240_Delta_3Ph_Id, Name = "240V Delta",
            Phase = PhaseConfiguration.Delta, LineVoltage = 240, PhaseVoltage = 0, Wires = 3,
            IsBuiltIn = true
        }
    };

    /// <summary>
    /// Maps a legacy <see cref="PanelVoltageConfig"/> to the corresponding built-in distribution system ID.
    /// </summary>
    public static string MigrateFromVoltageConfig(PanelVoltageConfig config) => config switch
    {
        PanelVoltageConfig.V120_208_3Ph => BuiltIn_120_208_3Ph_Id,
        PanelVoltageConfig.V277_480_3Ph => BuiltIn_277_480_3Ph_Id,
        PanelVoltageConfig.V120_240_1Ph => BuiltIn_120_240_1Ph_Id,
        PanelVoltageConfig.V240_3Ph     => BuiltIn_240_Delta_3Ph_Id,
        _ => BuiltIn_120_208_3Ph_Id
    };
}
