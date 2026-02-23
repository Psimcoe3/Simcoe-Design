using System.Windows.Media.Media3D;

namespace ElectricalComponentSandbox.Services.Dimensioning;

public enum DimensionSnapKind
{
    None = 0,
    Point = 1,
    Edge = 2,
    Face = 3,
    Center = 4,
    Intersection = 5
}

public sealed record DimensionSnapCandidateInfo
{
    public DimensionSnapKind Kind { get; init; }
    public string? ElementId { get; init; }
    public Point3D WorldPoint { get; init; }
    public Point3D LocalPoint { get; init; }
    public double ScreenDistancePx { get; init; }
    public bool IsVisibleInView { get; init; } = true;
    public bool IsValidForDimension { get; init; } = true;
    public bool HasStableReference { get; init; }
    public double Score { get; init; }
}

public sealed class DimensionPlacementState
{
    public DimensionSnapCandidateInfo? FirstReference { get; set; }
    public DimensionSnapCandidateInfo? SecondReference { get; set; }
    public DimensionSnapCandidateInfo? LastPreviewSnap { get; set; }

    public bool IsPlacing => FirstReference != null && SecondReference == null;

    public void Reset()
    {
        FirstReference = null;
        SecondReference = null;
        LastPreviewSnap = null;
    }
}

public sealed class DimensionSnapSelectionContext
{
    public double SnapTolerancePx { get; init; } = 12.0;
    public DimensionSnapKind RequestedKind { get; init; } = DimensionSnapKind.None;
    public DimensionSnapCandidateInfo? LastPreviewSnap { get; init; }
}

public sealed class DimensionSnapSelectionService
{
    private const double ScreenDistanceWeight = 3.0;
    private const double StableReferenceBoost = 30.0;
    private const double VisibleInViewBoost = 20.0;
    private const double SnapStickinessBoost = 15.0;
    private const double SnapSwitchThreshold = 8.0;

    public DimensionSnapCandidateInfo? SelectBestCandidate(
        IReadOnlyList<DimensionSnapCandidateInfo> candidates,
        DimensionSnapSelectionContext context)
    {
        if (candidates.Count == 0)
            return null;

        var eligible = candidates
            .Where(candidate => candidate.IsVisibleInView)
            .Where(candidate => candidate.IsValidForDimension)
            .Where(candidate => candidate.ScreenDistancePx <= context.SnapTolerancePx)
            .ToList();

        if (eligible.Count == 0)
            return null;

        if (context.RequestedKind != DimensionSnapKind.None)
        {
            var requested = eligible.Where(candidate => candidate.Kind == context.RequestedKind).ToList();
            if (requested.Count > 0)
                eligible = requested;
        }

        var scored = eligible
            .Select(candidate => candidate with
            {
                Score = ScoreCandidate(candidate, context.LastPreviewSnap)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        if (scored.Count == 0)
            return null;

        var best = scored[0];
        if (context.LastPreviewSnap == null)
            return best;

        var previousCandidate = scored.FirstOrDefault(candidate => SameReference(candidate, context.LastPreviewSnap));
        if (previousCandidate == null)
            return best;

        return (best.Score - previousCandidate.Score) < SnapSwitchThreshold
            ? previousCandidate
            : best;
    }

    private static double ScoreCandidate(DimensionSnapCandidateInfo candidate, DimensionSnapCandidateInfo? lastPreviewSnap)
    {
        var score = GetPriorityWeight(candidate.Kind);
        score -= candidate.ScreenDistancePx * ScreenDistanceWeight;

        if (candidate.HasStableReference)
            score += StableReferenceBoost;

        if (candidate.IsVisibleInView)
            score += VisibleInViewBoost;

        if (lastPreviewSnap != null && SameReference(candidate, lastPreviewSnap))
            score += SnapStickinessBoost;

        return score;
    }

    private static double GetPriorityWeight(DimensionSnapKind kind)
    {
        return kind switch
        {
            DimensionSnapKind.Point => 120.0,
            DimensionSnapKind.Intersection => 115.0,
            DimensionSnapKind.Center => 110.0,
            DimensionSnapKind.Edge => 100.0,
            DimensionSnapKind.Face => 95.0,
            _ => 50.0
        };
    }

    private static bool SameReference(DimensionSnapCandidateInfo a, DimensionSnapCandidateInfo b)
    {
        if (a.Kind != b.Kind)
            return false;

        if (!string.Equals(a.ElementId, b.ElementId, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(a.ElementId))
            return (a.LocalPoint - b.LocalPoint).Length <= 1e-4;

        return (a.WorldPoint - b.WorldPoint).Length <= 1e-4;
    }
}
