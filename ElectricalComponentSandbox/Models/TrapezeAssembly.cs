namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Standard strut section depths used for trapeze hangers. Names follow
/// industry convention (Unistrut P1000 = 1-5/8" deep, P3000 = 1-5/8x7/8",
/// P5500 = 2-7/16", P5000 = 3-1/4" deep back-to-back).
/// </summary>
public enum TrapezeStrutDepth
{
    SevenEighths,        // 7/8"
    OneFiveEighths,      // 1-5/8"
    TwoSevenSixteenths,  // 2-7/16"
    ThreeOneQuarter      // 3-1/4"
}

/// <summary>
/// Strut finish options, matching the eVolve <c>eV_Strut_Finish</c> /
/// <c>eVolve_Finish_*</c> parameter set.
/// </summary>
public enum TrapezeFinish
{
    PreGalvanized,
    HotDipGalvanized,
    Stainless,
    Copper,
    Painted
}

/// <summary>
/// Rod attachment used to anchor the trapeze top end to the deck or beam.
/// Maps to the eVolve <c>Trapeze_*</c> Yes/No flags.
/// </summary>
public enum TrapezeAttachmentType
{
    BeamClamp,
    BlueBanger,        // powder-actuated rod hanger for steel deck
    DeckInsert,        // cast-in concrete insert
    ConcreteAnchor,    // wedge/sleeve anchor
    WallAnchor,
    ThreadedToEmbed    // rod embedded in pour
}

/// <summary>
/// Rod diameter options, matching the eVolve <c>eVolve_Hardware_*</c> sizing.
/// </summary>
public enum TrapezeRodDiameter
{
    OneQuarter,      // 1/4"
    ThreeEighths,    // 3/8"
    OneHalf,         // 1/2"
    FiveEighths,     // 5/8"
    ThreeQuarters,   // 3/4"
    SevenEighths,    // 7/8"
    One,             // 1"
    OneOneQuarter,   // 1-1/4"
    OneOneHalf       // 1-1/2"
}

/// <summary>
/// Top-mounted hardware that completes the rod-to-strut connection. eVolve
/// captures these per-tier as Yes/No flags; we keep them as quantified
/// per-rod choices so the BOM can compute precise quantities.
/// </summary>
public enum TrapezeWasher
{
    None,
    Square,
    Fender
}

/// <summary>
/// A single trapeze tier: one length of strut suspended by rods (or attached
/// directly to a wall) supporting a configured set of conduit positions.
/// </summary>
public class TrapezeTier
{
    /// <summary>Strut depth selection.</summary>
    public TrapezeStrutDepth StrutDepth { get; set; } = TrapezeStrutDepth.OneFiveEighths;

    /// <summary>True when the tier uses two struts back-to-back welded together.</summary>
    public bool BackToBack { get; set; }

    /// <summary>True when the strut is the slotted style; false for solid.</summary>
    public bool Slotted { get; set; } = true;

    /// <summary>True when strut end caps are included on both ends.</summary>
    public bool IncludeEndCaps { get; set; } = true;

    /// <summary>Strut length in inches.</summary>
    public double StrutLengthInches { get; set; } = 24.0;

    /// <summary>Vertical offset (inches) below the previous tier centerline.</summary>
    public double OffsetBelowPreviousInches { get; set; } = 12.0;

    /// <summary>Number of conduits this tier supports. Drives channel-nut count.</summary>
    public int ConduitCount { get; set; }
}

/// <summary>
/// A rod within a trapeze assembly, dropping from the top attachment to all
/// tier struts below it.
/// </summary>
public class TrapezeRod
{
    /// <summary>Rod diameter.</summary>
    public TrapezeRodDiameter Diameter { get; set; } = TrapezeRodDiameter.ThreeEighths;

    /// <summary>Total rod length in inches measured from attachment to bottom tier.</summary>
    public double LengthInches { get; set; } = 24.0;

    /// <summary>True when the rod is embedded into a concrete pour at the top end.</summary>
    public bool EmbedTop { get; set; }

    /// <summary>True when the rod includes a coupling (two pieces threaded together).</summary>
    public bool HasCoupling { get; set; }

    /// <summary>Top attachment hardware for this rod.</summary>
    public TrapezeAttachmentType TopAttachment { get; set; } = TrapezeAttachmentType.BeamClamp;

    /// <summary>Washer type used at the top of each tier strut for this rod.</summary>
    public TrapezeWasher WasherTop { get; set; } = TrapezeWasher.Square;

    /// <summary>Washer type used at the bottom of each tier strut for this rod.</summary>
    public TrapezeWasher WasherBottom { get; set; } = TrapezeWasher.Square;
}

/// <summary>
/// Multi-tier trapeze hanger description that mirrors the eVolve T1–T4 tier
/// parameter group. Owned by a <see cref="HangerComponent"/>; the rest of the
/// hanger's legacy properties stay valid for single-tier defaults.
/// </summary>
public class TrapezeAssembly
{
    /// <summary>Tier strut runs from top to bottom (index 0 = T1 = top tier).</summary>
    public List<TrapezeTier> Tiers { get; set; } = new();

    /// <summary>Rods used to suspend the assembly. Typical trapezes use 2 rods.</summary>
    public List<TrapezeRod> Rods { get; set; } = new();

    /// <summary>Strut/rod finish for the assembly.</summary>
    public TrapezeFinish Finish { get; set; } = TrapezeFinish.PreGalvanized;

    /// <summary>True when this hanger is anchored to a wall rather than overhead.</summary>
    public bool IsWallMounted { get; set; }

    /// <summary>Number of tiers configured on this assembly.</summary>
    public int TierCount => Tiers.Count;

    /// <summary>
    /// Creates a single-tier overhead trapeze with two 3/8" rods, a 1-5/8" strut,
    /// and beam-clamp top attachments — the typical commercial default.
    /// </summary>
    public static TrapezeAssembly CreateSingleTierDefault()
    {
        return new TrapezeAssembly
        {
            Tiers =
            {
                new TrapezeTier
                {
                    StrutDepth = TrapezeStrutDepth.OneFiveEighths,
                    StrutLengthInches = 24.0,
                    OffsetBelowPreviousInches = 12.0,
                    ConduitCount = 1,
                }
            },
            Rods =
            {
                new TrapezeRod { Diameter = TrapezeRodDiameter.ThreeEighths, LengthInches = 24.0 },
                new TrapezeRod { Diameter = TrapezeRodDiameter.ThreeEighths, LengthInches = 24.0 },
            }
        };
    }

    /// <summary>
    /// Creates a multi-tier overhead trapeze with the given tier count, each
    /// tier offset 12 inches below the previous and supporting one conduit.
    /// </summary>
    public static TrapezeAssembly CreateMultiTier(
        int tierCount,
        TrapezeStrutDepth strutDepth = TrapezeStrutDepth.OneFiveEighths,
        TrapezeRodDiameter rodDiameter = TrapezeRodDiameter.ThreeEighths)
    {
        if (tierCount < 1 || tierCount > 4)
            throw new ArgumentOutOfRangeException(nameof(tierCount), "Tier count must be between 1 and 4.");

        var assembly = new TrapezeAssembly();
        double cumulativeOffset = 0;
        for (int i = 0; i < tierCount; i++)
        {
            assembly.Tiers.Add(new TrapezeTier
            {
                StrutDepth = strutDepth,
                StrutLengthInches = 24.0,
                OffsetBelowPreviousInches = i == 0 ? 6.0 : 12.0,
                ConduitCount = 1,
            });
            cumulativeOffset += i == 0 ? 6.0 : 12.0;
        }

        double rodLength = cumulativeOffset + 12.0;
        assembly.Rods.Add(new TrapezeRod { Diameter = rodDiameter, LengthInches = rodLength });
        assembly.Rods.Add(new TrapezeRod { Diameter = rodDiameter, LengthInches = rodLength });
        return assembly;
    }
}
