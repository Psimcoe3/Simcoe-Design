using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// A single line on a trapeze bill-of-materials.
/// </summary>
public sealed record TrapezeBomLine(
    string ItemCode,
    string Description,
    int Quantity,
    string Unit,
    double TotalLengthInches);

/// <summary>
/// Aggregated BOM for one or more trapeze hangers. Mirrors the line categories
/// produced by the eVolve <c>eV_TrapezeReport</c>.
/// </summary>
public sealed class TrapezeBom
{
    public List<TrapezeBomLine> Lines { get; } = new();

    public IEnumerable<TrapezeBomLine> Struts => Lines.Where(l => l.ItemCode.StartsWith("STR-"));
    public IEnumerable<TrapezeBomLine> Rods => Lines.Where(l => l.ItemCode.StartsWith("ROD-"));
    public IEnumerable<TrapezeBomLine> Hardware => Lines.Where(l => l.ItemCode.StartsWith("HW-"));

    public int TotalLineCount => Lines.Count;
}

/// <summary>
/// Generates a manufactured bill of materials for trapeze hangers. The output
/// itemizes struts, rods, and hardware (washers, nuts, anchors, couplings)
/// using stable item codes the fab shop can pick by.
/// </summary>
public static class TrapezeBomService
{
    /// <summary>
    /// Generates a BOM for a single trapeze hanger.
    /// </summary>
    public static TrapezeBom GenerateBom(HangerComponent hanger)
    {
        ArgumentNullException.ThrowIfNull(hanger);
        return GenerateBom(new[] { hanger });
    }

    /// <summary>
    /// Generates an aggregated BOM for a collection of trapeze hangers.
    /// Quantities and total lengths are summed across hangers.
    /// </summary>
    public static TrapezeBom GenerateBom(IEnumerable<HangerComponent> hangers)
    {
        ArgumentNullException.ThrowIfNull(hangers);

        var accum = new Dictionary<string, (string Description, int Qty, string Unit, double TotalLengthIn)>(StringComparer.Ordinal);

        foreach (var hanger in hangers)
        {
            var trapeze = hanger.Trapeze;
            if (trapeze == null) continue;

            AccumulateStruts(accum, trapeze);
            AccumulateRods(accum, trapeze);
            AccumulateHardware(accum, trapeze);
        }

        var bom = new TrapezeBom();
        foreach (var (code, value) in accum.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            bom.Lines.Add(new TrapezeBomLine(code, value.Description, value.Qty, value.Unit, value.TotalLengthIn));
        }
        return bom;
    }

    // ── Strut lines ──────────────────────────────────────────────────────

    private static void AccumulateStruts(
        Dictionary<string, (string Description, int Qty, string Unit, double TotalLengthIn)> accum,
        TrapezeAssembly trapeze)
    {
        foreach (var tier in trapeze.Tiers)
        {
            int strutCount = tier.BackToBack ? 2 : 1;
            string depthCode = DepthCode(tier.StrutDepth);
            string slotCode = tier.Slotted ? "SL" : "SO";
            string finishCode = FinishCode(trapeze.Finish);
            string code = $"STR-{depthCode}-{slotCode}-{finishCode}";
            string desc = $"{StrutDepthDisplay(tier.StrutDepth)} {(tier.Slotted ? "slotted" : "solid")} strut, {FinishDisplay(trapeze.Finish)}";
            AddLine(accum, code, desc, strutCount, "ea", tier.StrutLengthInches * strutCount);

            if (tier.IncludeEndCaps)
            {
                string capCode = $"HW-CAP-{depthCode}";
                string capDesc = $"{StrutDepthDisplay(tier.StrutDepth)} strut end cap";
                AddLine(accum, capCode, capDesc, strutCount * 2, "ea", 0);
            }
        }
    }

    // ── Rod lines ────────────────────────────────────────────────────────

    private static void AccumulateRods(
        Dictionary<string, (string Description, int Qty, string Unit, double TotalLengthIn)> accum,
        TrapezeAssembly trapeze)
    {
        foreach (var rod in trapeze.Rods)
        {
            string sizeCode = RodSizeCode(rod.Diameter);
            string finishCode = FinishCode(trapeze.Finish);
            string code = $"ROD-{sizeCode}-{finishCode}";
            string desc = $"{RodSizeDisplay(rod.Diameter)} threaded rod, {FinishDisplay(trapeze.Finish)}";
            AddLine(accum, code, desc, 1, "ea", rod.LengthInches);

            if (rod.HasCoupling)
            {
                string couplingCode = $"HW-CPL-{sizeCode}";
                string couplingDesc = $"{RodSizeDisplay(rod.Diameter)} rod coupling";
                AddLine(accum, couplingCode, couplingDesc, 1, "ea", 0);
            }
        }
    }

    // ── Hardware lines ───────────────────────────────────────────────────

    private static void AccumulateHardware(
        Dictionary<string, (string Description, int Qty, string Unit, double TotalLengthIn)> accum,
        TrapezeAssembly trapeze)
    {
        // Top attachments — one per rod
        foreach (var rod in trapeze.Rods)
        {
            string sizeCode = RodSizeCode(rod.Diameter);
            (string code, string desc) = rod.TopAttachment switch
            {
                TrapezeAttachmentType.BeamClamp => ($"HW-BMC-{sizeCode}", $"{RodSizeDisplay(rod.Diameter)} beam clamp"),
                TrapezeAttachmentType.BlueBanger => ($"HW-BLB-{sizeCode}", $"{RodSizeDisplay(rod.Diameter)} blue banger rod hanger"),
                TrapezeAttachmentType.DeckInsert => ($"HW-DKI-{sizeCode}", $"{RodSizeDisplay(rod.Diameter)} concrete deck insert"),
                TrapezeAttachmentType.ConcreteAnchor => ($"HW-ANC-{sizeCode}", $"{RodSizeDisplay(rod.Diameter)} concrete wedge anchor"),
                TrapezeAttachmentType.WallAnchor => ($"HW-WAL-{sizeCode}", $"{RodSizeDisplay(rod.Diameter)} wall anchor"),
                TrapezeAttachmentType.ThreadedToEmbed => ($"HW-EMB-{sizeCode}", $"{RodSizeDisplay(rod.Diameter)} cast-in embedded rod"),
                _ => ($"HW-ATT-{sizeCode}", $"{RodSizeDisplay(rod.Diameter)} attachment"),
            };
            AddLine(accum, code, desc, 1, "ea", 0);
        }

        // Per tier × per rod hardware (washers, nuts)
        int tierCount = trapeze.Tiers.Count;
        foreach (var rod in trapeze.Rods)
        {
            string sizeCode = RodSizeCode(rod.Diameter);
            string sizeDisp = RodSizeDisplay(rod.Diameter);

            // Two hex nuts per rod per tier (one above strut, one below)
            AddLine(accum, $"HW-HXN-{sizeCode}", $"{sizeDisp} hex nut", tierCount * 2, "ea", 0);

            // Washers per rod per tier
            if (rod.WasherTop != TrapezeWasher.None)
            {
                string topCode = rod.WasherTop == TrapezeWasher.Square ? $"HW-SQW-{sizeCode}" : $"HW-FNW-{sizeCode}";
                string topDesc = $"{sizeDisp} {(rod.WasherTop == TrapezeWasher.Square ? "square" : "fender")} washer";
                AddLine(accum, topCode, topDesc, tierCount, "ea", 0);
            }
            if (rod.WasherBottom != TrapezeWasher.None)
            {
                string botCode = rod.WasherBottom == TrapezeWasher.Square ? $"HW-SQW-{sizeCode}" : $"HW-FNW-{sizeCode}";
                string botDesc = $"{sizeDisp} {(rod.WasherBottom == TrapezeWasher.Square ? "square" : "fender")} washer";
                AddLine(accum, botCode, botDesc, tierCount, "ea", 0);
            }
        }

        // Channel nuts per conduit per tier (one each side of conduit)
        foreach (var tier in trapeze.Tiers)
        {
            if (tier.ConduitCount <= 0) continue;
            // Channel nuts are sized by strut family; we use a single SKU per finish.
            string finishCode = FinishCode(trapeze.Finish);
            string code = $"HW-CHN-{finishCode}";
            string desc = $"Strut channel nut, {FinishDisplay(trapeze.Finish)}";
            AddLine(accum, code, desc, tier.ConduitCount * 2, "ea", 0);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void AddLine(
        Dictionary<string, (string Description, int Qty, string Unit, double TotalLengthIn)> accum,
        string code, string description, int qty, string unit, double totalLengthIn)
    {
        if (accum.TryGetValue(code, out var existing))
        {
            accum[code] = (existing.Description, existing.Qty + qty, existing.Unit, existing.TotalLengthIn + totalLengthIn);
        }
        else
        {
            accum[code] = (description, qty, unit, totalLengthIn);
        }
    }

    private static string DepthCode(TrapezeStrutDepth depth) => depth switch
    {
        TrapezeStrutDepth.SevenEighths => "078",
        TrapezeStrutDepth.OneFiveEighths => "158",
        TrapezeStrutDepth.TwoSevenSixteenths => "247",
        TrapezeStrutDepth.ThreeOneQuarter => "314",
        _ => "158",
    };

    private static string StrutDepthDisplay(TrapezeStrutDepth depth) => depth switch
    {
        TrapezeStrutDepth.SevenEighths => "7/8\"",
        TrapezeStrutDepth.OneFiveEighths => "1-5/8\"",
        TrapezeStrutDepth.TwoSevenSixteenths => "2-7/16\"",
        TrapezeStrutDepth.ThreeOneQuarter => "3-1/4\"",
        _ => "1-5/8\"",
    };

    private static string RodSizeCode(TrapezeRodDiameter d) => d switch
    {
        TrapezeRodDiameter.OneQuarter => "025",
        TrapezeRodDiameter.ThreeEighths => "038",
        TrapezeRodDiameter.OneHalf => "050",
        TrapezeRodDiameter.FiveEighths => "063",
        TrapezeRodDiameter.ThreeQuarters => "075",
        TrapezeRodDiameter.SevenEighths => "088",
        TrapezeRodDiameter.One => "100",
        TrapezeRodDiameter.OneOneQuarter => "125",
        TrapezeRodDiameter.OneOneHalf => "150",
        _ => "038",
    };

    private static string RodSizeDisplay(TrapezeRodDiameter d) => d switch
    {
        TrapezeRodDiameter.OneQuarter => "1/4\"",
        TrapezeRodDiameter.ThreeEighths => "3/8\"",
        TrapezeRodDiameter.OneHalf => "1/2\"",
        TrapezeRodDiameter.FiveEighths => "5/8\"",
        TrapezeRodDiameter.ThreeQuarters => "3/4\"",
        TrapezeRodDiameter.SevenEighths => "7/8\"",
        TrapezeRodDiameter.One => "1\"",
        TrapezeRodDiameter.OneOneQuarter => "1-1/4\"",
        TrapezeRodDiameter.OneOneHalf => "1-1/2\"",
        _ => "3/8\"",
    };

    private static string FinishCode(TrapezeFinish finish) => finish switch
    {
        TrapezeFinish.PreGalvanized => "PG",
        TrapezeFinish.HotDipGalvanized => "HDG",
        TrapezeFinish.Stainless => "SS",
        TrapezeFinish.Copper => "CU",
        TrapezeFinish.Painted => "PT",
        _ => "PG",
    };

    private static string FinishDisplay(TrapezeFinish finish) => finish switch
    {
        TrapezeFinish.PreGalvanized => "pre-galv",
        TrapezeFinish.HotDipGalvanized => "HDG",
        TrapezeFinish.Stainless => "stainless",
        TrapezeFinish.Copper => "copper",
        TrapezeFinish.Painted => "painted",
        _ => "pre-galv",
    };
}
