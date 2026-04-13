using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Automatic Transfer Switch sizing per NEC 700/701/702 and NFPA 110.
/// Covers ATS rating, transfer time classification, load priority,
/// and withstand rating verification.
/// </summary>
public static class AutoTransferSwitchService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum SystemClass
    {
        Emergency,     // NEC 700 — life safety, 10s max transfer
        LegallyRequired, // NEC 701 — legally required standby
        Optional,      // NEC 702 — optional standby
    }

    public enum TransferType
    {
        Open,           // Break-before-make — momentary outage (~100ms)
        Closed,         // Make-before-break — no outage, requires sync
        DelayedOpen,    // Timed open transition with adjustable delay
        SoftLoad,       // Ramped transfer for motor loads
    }

    public enum AtsClass
    {
        Class1,  // Total system, 0.5s max transfer (emergency)
        Class2,  // Total system, 2s max transfer
        Class3,  // Total system, up to 10s transfer
        Class4,  // Manual transfer allowed
    }

    public record AtsLoad
    {
        public string Name { get; init; } = "";
        public double AmpsAtVoltage { get; init; }
        public SystemClass Priority { get; init; } = SystemClass.Emergency;
        public bool HasMotorLoads { get; init; }
        public double MotorInrushMultiple { get; init; } = 6.0;
    }

    public record AtsSizingResult
    {
        public double ContinuousAmps { get; init; }
        public double WithstandAmps { get; init; }
        public double SelectedFrameAmps { get; init; }
        public AtsClass Class { get; init; }
        public TransferType RecommendedTransfer { get; init; }
        public double MaxTransferTimeSec { get; init; }
        public bool RequiresClosedTransition { get; init; }
    }

    public record WithstandResult
    {
        public double AvailableFaultKA { get; init; }
        public double RequiredWithstandKA { get; init; }
        public double SelectedWithstandKA { get; init; }
        public bool IsAdequate { get; init; }
    }

    // ── Standard ATS Frame Sizes ─────────────────────────────────────────────

    private static readonly double[] FrameSizes =
        { 30, 40, 60, 100, 150, 200, 225, 260, 400, 600, 800, 1000, 1200, 1600, 2000, 3000, 4000 };

    // ── Standard Withstand Ratings (kA) ──────────────────────────────────────

    private static readonly double[] WithstandRatings =
        { 10, 14, 18, 22, 25, 35, 42, 50, 65, 85, 100, 150, 200 };

    // ── Transfer Time by System Class ────────────────────────────────────────

    /// <summary>
    /// Gets maximum allowable transfer time per NEC article.
    /// </summary>
    public static double GetMaxTransferTimeSec(SystemClass systemClass) => systemClass switch
    {
        SystemClass.Emergency => 10.0,        // NEC 700.12 — 10 seconds
        SystemClass.LegallyRequired => 60.0,  // NEC 701.12 — 60 seconds
        SystemClass.Optional => 120.0,        // NEC 702 — no strict limit, 2 min typical
        _ => 10.0,
    };

    // ── ATS Sizing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sizes an ATS for the given load profile.
    /// Considers continuous current, motor inrush, and system class.
    /// </summary>
    public static AtsSizingResult SizeAts(
        double continuousAmps,
        SystemClass systemClass,
        bool hasMotorLoads = false,
        double motorInrushMultiple = 6.0,
        bool requiresClosedTransition = false)
    {
        if (continuousAmps <= 0)
            throw new ArgumentException("Continuous amps must be positive.");

        // Withstand must handle motor inrush current
        double withstandAmps = hasMotorLoads
            ? continuousAmps * motorInrushMultiple
            : continuousAmps * 1.0;

        // Select frame size ≥ continuous amps (NEC 700.5 — rated not less than load)
        double selectedFrame = 0;
        foreach (double frame in FrameSizes)
        {
            if (frame >= continuousAmps)
            {
                selectedFrame = frame;
                break;
            }
        }
        if (selectedFrame == 0) selectedFrame = FrameSizes[^1];

        // ATS class per transfer time requirements
        double maxTransfer = GetMaxTransferTimeSec(systemClass);
        AtsClass atsClass = maxTransfer switch
        {
            <= 0.5 => AtsClass.Class1,
            <= 2.0 => AtsClass.Class2,
            <= 10.0 => AtsClass.Class3,
            _ => AtsClass.Class4,
        };

        // Transfer type recommendation
        TransferType transferType;
        if (requiresClosedTransition)
            transferType = TransferType.Closed;
        else if (hasMotorLoads)
            transferType = TransferType.SoftLoad;
        else
            transferType = TransferType.Open;

        return new AtsSizingResult
        {
            ContinuousAmps = Math.Round(continuousAmps, 1),
            WithstandAmps = Math.Round(withstandAmps, 1),
            SelectedFrameAmps = selectedFrame,
            Class = atsClass,
            RecommendedTransfer = transferType,
            MaxTransferTimeSec = maxTransfer,
            RequiresClosedTransition = requiresClosedTransition,
        };
    }

    // ── Withstand Rating ─────────────────────────────────────────────────────

    /// <summary>
    /// Selects ATS withstand/close-on rating for available fault current.
    /// Per UL 1008, ATS must withstand available fault at its line terminals.
    /// </summary>
    public static WithstandResult SelectWithstandRating(double availableFaultKA)
    {
        if (availableFaultKA <= 0)
            throw new ArgumentException("Fault current must be positive.");

        // Select next standard withstand rating above available fault
        double selectedKA = 0;
        foreach (double rating in WithstandRatings)
        {
            if (rating >= availableFaultKA)
            {
                selectedKA = rating;
                break;
            }
        }

        bool isAdequate = selectedKA >= availableFaultKA;
        if (selectedKA == 0) selectedKA = WithstandRatings[^1];

        return new WithstandResult
        {
            AvailableFaultKA = Math.Round(availableFaultKA, 2),
            RequiredWithstandKA = Math.Round(availableFaultKA, 2),
            SelectedWithstandKA = selectedKA,
            IsAdequate = isAdequate,
        };
    }

    // ── Exercise Interval ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the recommended exercise/test interval per NFPA 110.
    /// </summary>
    public static int GetExerciseIntervalDays(SystemClass systemClass) => systemClass switch
    {
        SystemClass.Emergency => 7,          // NFPA 110 — weekly for Level 1
        SystemClass.LegallyRequired => 30,   // Monthly
        SystemClass.Optional => 30,          // Monthly recommended
        _ => 30,
    };
}
