namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Represents an electrical circuit connecting a panel breaker to downstream loads.
/// Models the home-run path: panel → breaker → wire/conduit → loads.
/// </summary>
public class Circuit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Circuit number as labeled on the panel (e.g. "1", "3", "1,3" for multi-pole)</summary>
    public string CircuitNumber { get; set; } = string.Empty;

    /// <summary>Human-readable description (e.g. "Lighting – 2nd Floor East")</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>ID of the panel component this circuit belongs to</summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>Breaker protecting this circuit</summary>
    public CircuitBreaker Breaker { get; set; } = new();

    /// <summary>Wire specification for the home run</summary>
    public WireSpec Wire { get; set; } = new();

    /// <summary>IDs of conduit components carrying this circuit's wiring</summary>
    public List<string> ConduitIds { get; set; } = new();

    /// <summary>IDs of downstream load components (boxes, receptacles, fixtures)</summary>
    public List<string> LoadComponentIds { get; set; } = new();

    /// <summary>Phase assignment: A, B, C, or AB/BC/AC for multi-pole</summary>
    public string Phase { get; set; } = "A";

    /// <summary>System voltage (120, 208, 240, 277, 480)</summary>
    public double Voltage { get; set; } = 120.0;

    /// <summary>Number of poles (1, 2, or 3)</summary>
    public int Poles { get; set; } = 1;

    /// <summary>Connected load in VA</summary>
    public double ConnectedLoadVA { get; set; }

    /// <summary>Demand factor (0.0–1.0, typically 1.0 for continuous, 0.8 for non-continuous)</summary>
    public double DemandFactor { get; set; } = 1.0;

    /// <summary>Computed demand load = ConnectedLoadVA × DemandFactor</summary>
    public double DemandLoadVA => ConnectedLoadVA * DemandFactor;

    /// <summary>One-way wire length in feet from panel to farthest load</summary>
    public double WireLengthFeet { get; set; }

    /// <summary>NEC load classification used to apply demand factors and produce per-category schedule summaries.</summary>
    public LoadClassification LoadClassification { get; set; } = LoadClassification.Power;

    /// <summary>
    /// Electrical system type distinguishing power circuits from low-voltage signal systems.
    /// Power circuits participate in load calculations; signal circuits are excluded.
    /// </summary>
    public ElectricalSystemType SystemType { get; set; } = ElectricalSystemType.PowerCircuit;

    /// <summary>True when this is a power circuit that participates in panel load calculations.</summary>
    public bool IsPowerCircuit => SystemType == ElectricalSystemType.PowerCircuit;

    /// <summary>Whether this slot holds an active circuit, a spare breaker, or an open space.</summary>
    public CircuitSlotType SlotType { get; set; } = CircuitSlotType.Circuit;

    /// <summary>True power in watts. When zero, derived from ConnectedLoadVA × PowerFactor.</summary>
    public double TrueLoadW { get; set; }

    /// <summary>
    /// Displacement power factor (0.0–1.0). Defaults to 1.0 (unity / purely resistive).
    /// Used to derive TrueLoadW when it is not set explicitly.
    /// </summary>
    public double PowerFactor { get; set; } = 1.0;

    /// <summary>Whether current leads or lags voltage. Relevant for inductive (motors) vs
    /// capacitive (PFC-corrected) loads.</summary>
    public PowerFactorState PowerFactorState { get; set; } = PowerFactorState.Lagging;

    /// <summary>
    /// Effective true power in watts: uses the explicit TrueLoadW value when non-zero,
    /// otherwise derives it as ConnectedLoadVA × PowerFactor.
    /// </summary>
    public double EffectiveTrueLoadW => TrueLoadW > 0 ? TrueLoadW : ConnectedLoadVA * PowerFactor;
    /// <summary>
    /// Physical slot position in the panel (1-based). Odd = left column, even = right column.
    /// 0 means unassigned — ScheduleTableService.AssignSlotNumbers will fill this in.
    /// Multi-pole circuits own consecutive slots starting at this number.
    /// </summary>
    public int SlotNumber { get; set; }}

/// <summary>
/// Breaker configuration for a circuit.
/// </summary>
public class CircuitBreaker
{
    /// <summary>Trip rating in amps (15, 20, 30, 40, 50, 60, 70, 100, etc.)</summary>
    public int TripAmps { get; set; } = 20;

    /// <summary>Frame size in amps (may differ from trip for adjustable breakers)</summary>
    public int FrameAmps { get; set; } = 20;

    /// <summary>Number of poles (1, 2, or 3)</summary>
    public int Poles { get; set; } = 1;

    /// <summary>Breaker type: Standard, GFCI, AFCI, Dual (AFCI+GFCI), Shunt Trip</summary>
    public BreakerType BreakerType { get; set; } = BreakerType.Standard;

    /// <summary>Interrupting rating in kAIC (typically 10, 14, 22, 25, 42, 65)</summary>
    public double InterruptingRatingKAIC { get; set; } = 10.0;
}

public enum BreakerType
{
    Standard,
    GFCI,
    AFCI,
    DualFunction,
    ShuntTrip
}

/// <summary>
/// Wire specification for a circuit.
/// </summary>
public class WireSpec
{
    /// <summary>Wire size in AWG (14, 12, 10, 8, 6, 4, 3, 2, 1) or kcmil for larger (250, 300, etc.)</summary>
    public string Size { get; set; } = "12";

    /// <summary>Number of current-carrying conductors</summary>
    public int Conductors { get; set; } = 2;

    /// <summary>Ground wire size</summary>
    public string GroundSize { get; set; } = "12";

    /// <summary>Insulation type (THHN, THWN-2, XHHW, etc.)</summary>
    public string InsulationType { get; set; } = "THHN";

    /// <summary>Conductor material</summary>
    public ConductorMaterial Material { get; set; } = ConductorMaterial.Copper;
}

public enum ConductorMaterial
{
    Copper,
    Aluminum
}

/// <summary>
/// Panel schedule: aggregates all circuits for a given panel and computes totals.
/// </summary>
public class PanelSchedule
{
    public string PanelId { get; set; } = string.Empty;
    public string PanelName { get; set; } = string.Empty;

    /// <summary>Main breaker or MLO</summary>
    public int MainBreakerAmps { get; set; } = 200;
    public bool IsMainLugsOnly { get; set; }

    /// <summary>Panel voltage configuration (legacy — prefer DistributionSystemId)</summary>
    public PanelVoltageConfig VoltageConfig { get; set; } = PanelVoltageConfig.V120_208_3Ph;

    /// <summary>
    /// ID of the named distribution system type (from <see cref="ProjectModel.DistributionSystems"/>).
    /// When set, overrides <see cref="VoltageConfig"/> for voltage/phase resolution.
    /// </summary>
    public string? DistributionSystemId { get; set; }

    /// <summary>Bus rating in amps</summary>
    public int BusAmps { get; set; } = 200;

    /// <summary>Available fault current at the panel in kA (from upstream study)</summary>
    public double AvailableFaultCurrentKA { get; set; } = 10.0;

    /// <summary>Controls the order in which circuits are numbered and laid out in the schedule.</summary>
    public CircuitSequence CircuitSequence { get; set; } = CircuitSequence.OddThenEven;

    /// <summary>All circuits in this panel</summary>
    public List<Circuit> Circuits { get; set; } = new();

    /// <summary>Total connected load in VA across all circuits</summary>
    public double TotalConnectedVA => Circuits.Sum(c => c.ConnectedLoadVA);

    /// <summary>Total demand load in VA across all circuits</summary>
    public double TotalDemandVA => Circuits.Sum(c => c.DemandLoadVA);

    /// <summary>Per-phase demand load in VA</summary>
    public (double PhaseA, double PhaseB, double PhaseC) PhaseDemandVA
    {
        get
        {
            double a = 0, b = 0, c = 0;
            foreach (var circuit in Circuits)
            {
                var load = circuit.DemandLoadVA / circuit.Poles;
                foreach (var ch in circuit.Phase)
                {
                    switch (ch)
                    {
                        case 'A': a += load; break;
                        case 'B': b += load; break;
                        case 'C': c += load; break;
                    }
                }
            }
            return (a, b, c);
        }
    }
}

public enum PanelVoltageConfig
{
    V120_240_1Ph,
    V120_208_3Ph,
    V277_480_3Ph,
    V240_3Ph
}

/// <summary>
/// NEC load classification used to apply demand factors and categorize panel load.
/// Maps to Revit's ElectricalSystemType load classification concepts.
/// </summary>
public enum LoadClassification
{
    Power,
    Lighting,
    HVAC,
    Other
}

/// <summary>
/// Determines whether a panel slot is occupied by an active circuit, reserved as spare, or held as open space.
/// Maps to Revit's CircuitType enum.
/// </summary>
public enum CircuitSlotType
{
    Circuit,
    Spare,
    Space
}

/// <summary>
/// Power factor state indicating whether current leads or lags voltage.
/// </summary>
public enum PowerFactorState
{
    Lagging,
    Leading
}

/// <summary>
/// Electrical system type mirroring Revit's ElectricalSystemType.
/// Power circuits carry load and participate in demand calculations.
/// Signal/low-voltage types are excluded from load calcs but appear in schedule summaries.
/// </summary>
public enum ElectricalSystemType
{
    PowerCircuit,
    Data,
    Telephone,
    FireAlarm,
    Security,
    NurseCall,
    Controls,
    Communication
}

/// <summary>
/// Controls how circuits are ordered and numbered in the panel schedule.
/// </summary>
public enum CircuitSequence
{
    /// <summary>Circuits ordered by circuit number ascending.</summary>
    Numerical,
    /// <summary>Circuits grouped by phase (A-group, B-group, C-group).</summary>
    GroupByPhase,
    /// <summary>Odd-numbered slots on the left column, even on the right — standard US panel layout.</summary>
    OddThenEven
}
