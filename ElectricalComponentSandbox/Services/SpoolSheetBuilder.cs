using System.Globalization;
using System.Text;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Persistence;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Spool sheet template families — one per eVolve SMC_11x17_Spool_*.rfa
/// template. The builder picks the matching template by analyzing the run's
/// bend pattern and the supporting hanger tier count.
/// </summary>
public enum SpoolSheetTemplate
{
    StraightSection,
    Stub90,
    Kicked90,
    Offset,
    TwoPieceOffset,
    Saddle3Point,
    Saddle4Point,
    Hangers1Tier,
    Hangers2Tier,
    Hangers3Tier,
    Hangers4Tier,
    MiniHangers,
    MultiSchedule
}

/// <summary>
/// Title-block fields for a generated spool sheet.
/// </summary>
public sealed class SpoolSheetTitleBlock
{
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectNumber { get; init; } = string.Empty;
    public string SheetNumber { get; init; } = string.Empty;
    public string SheetTitle { get; init; } = string.Empty;
    public string DrawnBy { get; init; } = Environment.UserName;
    public DateTime DrawnDateUtc { get; init; } = DateTime.UtcNow;
    public string DrawingScale { get; init; } = "NTS";
    public string SpoolPackage { get; init; } = string.Empty;
    public string Status { get; init; } = "Drawn";
}

/// <summary>
/// Cut-list row for a single conduit segment on the spool.
/// </summary>
public sealed record SpoolCutListRow(
    int Item,
    string SegmentId,
    string TradeSize,
    ConduitMaterialType Material,
    double GrossLengthInches,
    double CutLengthInches,
    string Notes);

/// <summary>
/// Hanger schedule row for the spool. One row per hanger placed on the run.
/// </summary>
public sealed record SpoolHangerScheduleRow(
    int Item,
    string HangerId,
    int TierCount,
    string StrutDescription,
    string RodDescription,
    int ConduitCount,
    double TotalRodLengthInches);

/// <summary>
/// Complete spool sheet artifact: title block, classified template,
/// bend schedule (with DimA–F and Mark1–4), cut list, hanger schedule,
/// trapeze hardware BOM, and conduit/fitting BOM. Render-agnostic — a
/// dedicated drawing renderer or CSV exporter consumes the same data.
/// </summary>
public sealed class SpoolSheet
{
    public SpoolSheetTemplate Template { get; init; }
    public SpoolSheetTitleBlock TitleBlock { get; init; } = new();
    public string RunId { get; init; } = string.Empty;
    public string TradeSize { get; init; } = string.Empty;
    public ConduitMaterialType Material { get; init; }
    public double GrossLengthFeet { get; init; }
    public double AdjustedLengthFeet { get; init; }
    public BendSchedule BendSchedule { get; init; } = default!;
    public IReadOnlyList<SpoolCutListRow> CutList { get; init; } = Array.Empty<SpoolCutListRow>();
    public IReadOnlyList<SpoolHangerScheduleRow> HangerSchedule { get; init; } = Array.Empty<SpoolHangerScheduleRow>();
    public TrapezeBom TrapezeBom { get; init; } = new();
    public IReadOnlyList<SpoolBomEntry> ConduitBom { get; init; } = Array.Empty<SpoolBomEntry>();
    public string AuditTrace { get; init; } = string.Empty;
}

/// <summary>
/// Builds a print-ready spool sheet for a conduit run. The builder:
/// <list type="number">
///   <item>computes the run takeoff (gross + adjusted footage, deducts);</item>
///   <item>computes the per-bend schedule (DimA–F, Mark1–4 per bend);</item>
///   <item>classifies the run pattern and hanger tier count into a template;</item>
///   <item>generates a cut list, hanger schedule, and BOMs;</item>
///   <item>packages everything into a <see cref="SpoolSheet"/> that downstream
///         renderers can lay out on the matching <c>SMC_11x17_Spool_*</c> template.</item>
/// </list>
/// </summary>
public sealed class SpoolSheetBuilder
{
    private readonly ConduitModelStore _store;

    public SpoolSheetBuilder(ConduitModelStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Builds the spool sheet for a single run. <paramref name="hangers"/>
    /// may be empty for unsupported runs (e.g. wall-mount stubs).
    /// </summary>
    public SpoolSheet Build(
        string runId,
        IReadOnlyList<HangerComponent>? hangers = null,
        SpoolSheetTitleBlock? titleBlock = null)
    {
        var run = _store.GetRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        var takeoff = ConduitTakeoffService.ComputeRunTakeoff(_store, runId);
        var bends = BendScheduleService.ComputeForRun(_store, runId);
        var template = ClassifyTemplate(bends, hangers ?? Array.Empty<HangerComponent>());

        var cutList = BuildCutList(run);
        var hangerSchedule = BuildHangerSchedule(hangers ?? Array.Empty<HangerComponent>());
        var trapezeBom = TrapezeBomService.GenerateBom(hangers ?? Array.Empty<HangerComponent>());
        var conduitBom = BuildConduitBom(run);

        var resolvedTitle = titleBlock ?? new SpoolSheetTitleBlock
        {
            SheetTitle = $"Spool — {run.RunId}",
            SheetNumber = $"SP-{run.RunId}",
        };

        string audit = string.Format(
            CultureInfo.InvariantCulture,
            "run={0}; template={1}; bends={2}; segments={3}; grossFt={4:F3}; adjustedFt={5:F3}; hangers={6}; cutListRows={7}",
            run.RunId, template, bends.Bends.Count, cutList.Count,
            takeoff.GrossLengthFeet, takeoff.AdjustedLengthFeet,
            hangerSchedule.Count, cutList.Count);

        return new SpoolSheet
        {
            Template = template,
            TitleBlock = resolvedTitle,
            RunId = run.RunId,
            TradeSize = run.TradeSize,
            Material = run.Material,
            GrossLengthFeet = takeoff.GrossLengthFeet,
            AdjustedLengthFeet = takeoff.AdjustedLengthFeet,
            BendSchedule = bends,
            CutList = cutList,
            HangerSchedule = hangerSchedule,
            TrapezeBom = trapezeBom,
            ConduitBom = conduitBom,
            AuditTrace = audit,
        };
    }

    // ── Template classification ──────────────────────────────────────────

    /// <summary>
    /// Picks the matching SMC_11x17_Spool_* template from the bend pattern
    /// and the number of hanger tiers on the run. Hanger templates take
    /// precedence when the run is supported and otherwise straight, because
    /// the shop sheet's primary purpose is to show the trapeze configuration.
    /// </summary>
    public static SpoolSheetTemplate ClassifyTemplate(BendSchedule bends, IReadOnlyList<HangerComponent> hangers)
    {
        ArgumentNullException.ThrowIfNull(bends);

        int maxTiers = hangers.Where(h => h.Trapeze != null).Select(h => h.Trapeze!.TierCount).DefaultIfEmpty(0).Max();

        // A run with no bends + hanger support → render on a hanger template.
        if (bends.OverallPattern == BendSchedulePattern.Straight && maxTiers > 0)
        {
            return maxTiers switch
            {
                1 => SpoolSheetTemplate.Hangers1Tier,
                2 => SpoolSheetTemplate.Hangers2Tier,
                3 => SpoolSheetTemplate.Hangers3Tier,
                _ => SpoolSheetTemplate.Hangers4Tier,
            };
        }

        var byBendPattern = bends.OverallPattern switch
        {
            BendSchedulePattern.Straight => SpoolSheetTemplate.StraightSection,
            BendSchedulePattern.Stub90 => SpoolSheetTemplate.Stub90,
            BendSchedulePattern.Kick90 => SpoolSheetTemplate.Kicked90,
            BendSchedulePattern.Offset => SpoolSheetTemplate.Offset,
            BendSchedulePattern.OffsetTwoPiece => SpoolSheetTemplate.TwoPieceOffset,
            BendSchedulePattern.Saddle3Point => SpoolSheetTemplate.Saddle3Point,
            BendSchedulePattern.Saddle4Point => SpoolSheetTemplate.Saddle4Point,
            _ => SpoolSheetTemplate.MultiSchedule,
        };

        // Two consecutive offset rows in the schedule indicates the
        // OffsetTwoPiece variant (a routing around two stacked obstructions).
        if (byBendPattern == SpoolSheetTemplate.Offset
            && bends.Bends.Count(b => b.Pattern == BendSchedulePattern.Offset) >= 2)
        {
            return SpoolSheetTemplate.TwoPieceOffset;
        }

        return byBendPattern;
    }

    // ── Cut list ─────────────────────────────────────────────────────────

    private List<SpoolCutListRow> BuildCutList(ConduitRun run)
    {
        var rows = new List<SpoolCutListRow>();
        var segments = run.GetSegments(_store).ToList();
        var fittings = run.GetFittings(_store).ToList();
        int item = 1;
        foreach (var seg in segments)
        {
            double grossIn = seg.Length * 12.0;
            double deductIn = DeductForSegment(seg.Id, fittings, run.Material);
            double cutIn = Math.Max(0, grossIn - deductIn);
            rows.Add(new SpoolCutListRow(
                Item: item++,
                SegmentId: seg.Id,
                TradeSize: seg.TradeSize,
                Material: seg.Material,
                GrossLengthInches: Math.Round(grossIn, 3),
                CutLengthInches: Math.Round(cutIn, 3),
                Notes: deductIn > 0 ? "bend deduct applied" : string.Empty));
        }
        return rows;
    }

    /// <summary>
    /// Charges each fitting's deduct evenly across the segments it connects:
    /// half each for an inline bend, full for a stub-end bend.
    /// </summary>
    private static double DeductForSegment(string segmentId, List<ConduitFitting> fittings, ConduitMaterialType material)
    {
        double total = 0;
        foreach (var f in fittings)
        {
            if (!f.ConnectedSegmentIds.Contains(segmentId)) continue;
            double d = f.DeductLength > 0
                ? f.DeductLength
                : f.Type switch
                {
                    FittingType.Elbow90 => ConduitTakeoffService.GetDeduct90(f.TradeSize, material),
                    FittingType.Elbow45 => ConduitTakeoffService.GetDeduct45(f.TradeSize, material),
                    _ => 0,
                };
            int connectedCount = Math.Max(1, f.ConnectedSegmentIds.Count);
            total += d / connectedCount;
        }
        return total;
    }

    // ── Hanger schedule ──────────────────────────────────────────────────

    private static List<SpoolHangerScheduleRow> BuildHangerSchedule(IReadOnlyList<HangerComponent> hangers)
    {
        var rows = new List<SpoolHangerScheduleRow>();
        int item = 1;
        foreach (var h in hangers)
        {
            var trapeze = h.Trapeze;
            int tierCount = trapeze?.TierCount ?? 0;
            string strutDesc = trapeze != null && trapeze.Tiers.Count > 0
                ? $"{StrutDepthLabel(trapeze.Tiers[0].StrutDepth)} x {trapeze.Tiers[0].StrutLengthInches:F1}\""
                : $"Rod {h.RodDiameter:F3}\" x {h.RodLength:F1}\"";
            string rodDesc = trapeze != null && trapeze.Rods.Count > 0
                ? $"({trapeze.Rods.Count}) {RodSizeLabel(trapeze.Rods[0].Diameter)} rod"
                : $"Rod {h.RodDiameter:F3}\"";
            int conduitCount = trapeze?.Tiers.Sum(t => t.ConduitCount) ?? 0;
            double totalRodLen = trapeze?.Rods.Sum(r => r.LengthInches) ?? h.RodLength;
            rows.Add(new SpoolHangerScheduleRow(
                Item: item++,
                HangerId: h.Id,
                TierCount: tierCount,
                StrutDescription: strutDesc,
                RodDescription: rodDesc,
                ConduitCount: conduitCount,
                TotalRodLengthInches: Math.Round(totalRodLen, 3)));
        }
        return rows;
    }

    private static string StrutDepthLabel(TrapezeStrutDepth d) => d switch
    {
        TrapezeStrutDepth.SevenEighths => "7/8\"",
        TrapezeStrutDepth.OneFiveEighths => "1-5/8\"",
        TrapezeStrutDepth.TwoSevenSixteenths => "2-7/16\"",
        TrapezeStrutDepth.ThreeOneQuarter => "3-1/4\"",
        _ => "1-5/8\"",
    };

    private static string RodSizeLabel(TrapezeRodDiameter d) => d switch
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

    // ── Conduit / fitting BOM ────────────────────────────────────────────

    private List<SpoolBomEntry> BuildConduitBom(ConduitRun run)
    {
        var entries = new Dictionary<string, SpoolBomEntry>(StringComparer.Ordinal);
        foreach (var seg in run.GetSegments(_store))
        {
            string key = $"Conduit|{seg.Material}|{seg.TradeSize}";
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new SpoolBomEntry
                {
                    ItemType = "Conduit",
                    Description = $"{seg.Material} {seg.TradeSize}\" conduit",
                    TradeSize = seg.TradeSize,
                    Material = seg.Material.ToString(),
                };
                entries[key] = entry;
            }
            entry.Quantity++;
            entry.TotalLengthInches += seg.Length * 12.0;
        }
        foreach (var fit in run.GetFittings(_store))
        {
            string key = $"Fitting|{fit.Type}|{fit.TradeSize}";
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new SpoolBomEntry
                {
                    ItemType = "Fitting",
                    Description = $"{fit.Type} {fit.TradeSize}\"",
                    TradeSize = fit.TradeSize,
                    Material = "Steel",
                };
                entries[key] = entry;
            }
            entry.Quantity++;
        }
        return entries.Values
            .OrderBy(e => e.ItemType, StringComparer.Ordinal)
            .ThenBy(e => e.TradeSize, StringComparer.Ordinal)
            .ToList();
    }

    // ── CSV export ───────────────────────────────────────────────────────

    /// <summary>
    /// Exports the spool sheet as a CSV report. Useful as a placeholder
    /// renderer before a graphical SMC_11x17 layout is wired up, and useful
    /// long-term as a fab-shop quick-print.
    /// </summary>
    public static string ExportCsv(SpoolSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var sb = new StringBuilder();
        sb.AppendLine($"# Spool Sheet — {sheet.TitleBlock.SheetNumber} ({sheet.Template})");
        sb.AppendLine($"# Run: {sheet.RunId}  Size: {sheet.TradeSize}  Material: {sheet.Material}");
        sb.AppendLine($"# Gross: {sheet.GrossLengthFeet:F2} ft  Adjusted: {sheet.AdjustedLengthFeet:F2} ft");
        sb.AppendLine();

        sb.AppendLine("## Bend Schedule");
        sb.AppendLine("Bend,Pattern,Angle1,Angle2,Size,Deduct,Mark1,Mark2,Mark3,Mark4,DimA,DimB,DimC,DimD,DimE,DimF,Bender,Notes");
        foreach (var b in sheet.BendSchedule.Bends)
        {
            sb.AppendLine(string.Join(',',
                b.BendNumber,
                b.Pattern,
                F(b.Angle1Degrees), F(b.Angle2Degrees),
                Csv(b.TradeSize),
                F(b.DeductInches),
                F(b.Mark1Inches), F(b.Mark2Inches), F(b.Mark3Inches), F(b.Mark4Inches),
                F(b.DimAInches), F(b.DimBInches), F(b.DimCInches), F(b.DimDInches), F(b.DimEInches), F(b.DimFInches),
                Csv(b.BenderType),
                Csv(b.Notes)));
        }

        sb.AppendLine();
        sb.AppendLine("## Cut List");
        sb.AppendLine("Item,Segment,Size,Material,Gross(in),Cut(in),Notes");
        foreach (var c in sheet.CutList)
        {
            sb.AppendLine(string.Join(',',
                c.Item, Csv(c.SegmentId), Csv(c.TradeSize), c.Material,
                F(c.GrossLengthInches), F(c.CutLengthInches), Csv(c.Notes)));
        }

        sb.AppendLine();
        sb.AppendLine("## Hanger Schedule");
        sb.AppendLine("Item,Hanger,Tiers,Strut,Rod,Conduits,TotalRodLength(in)");
        foreach (var h in sheet.HangerSchedule)
        {
            sb.AppendLine(string.Join(',',
                h.Item, Csv(h.HangerId), h.TierCount,
                Csv(h.StrutDescription), Csv(h.RodDescription),
                h.ConduitCount, F(h.TotalRodLengthInches)));
        }

        sb.AppendLine();
        sb.AppendLine("## Trapeze BOM");
        sb.AppendLine("Code,Description,Qty,Unit,TotalLength(in)");
        foreach (var line in sheet.TrapezeBom.Lines)
        {
            sb.AppendLine(string.Join(',',
                Csv(line.ItemCode), Csv(line.Description),
                line.Quantity, Csv(line.Unit), F(line.TotalLengthInches)));
        }

        sb.AppendLine();
        sb.AppendLine("## Conduit BOM");
        sb.AppendLine("Type,Description,Size,Material,Qty,TotalLength(in)");
        foreach (var entry in sheet.ConduitBom)
        {
            sb.AppendLine(string.Join(',',
                Csv(entry.ItemType), Csv(entry.Description),
                Csv(entry.TradeSize), Csv(entry.Material),
                entry.Quantity, F(entry.TotalLengthInches)));
        }

        return sb.ToString();
    }

    private static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
