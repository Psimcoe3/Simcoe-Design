using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Services.Dimensioning;

namespace ElectricalComponentSandbox.Tests.Services.Dimensioning;

public sealed class DimensionSnapSelectionServiceTests
{
    [Fact]
    public void SelectBestCandidate_PrefersHigherPrioritySnapKind()
    {
        var service = new DimensionSnapSelectionService();
        var candidates = new List<DimensionSnapCandidateInfo>
        {
            new()
            {
                Kind = DimensionSnapKind.Face,
                ElementId = "A",
                WorldPoint = new Point3D(0, 0, 0),
                LocalPoint = new Point3D(0, 0, 0),
                ScreenDistancePx = 2,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = true
            },
            new()
            {
                Kind = DimensionSnapKind.Point,
                ElementId = "B",
                WorldPoint = new Point3D(1, 0, 0),
                LocalPoint = new Point3D(1, 0, 0),
                ScreenDistancePx = 4,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = true
            }
        };

        var best = service.SelectBestCandidate(candidates, new DimensionSnapSelectionContext
        {
            SnapTolerancePx = 12,
            RequestedKind = DimensionSnapKind.None
        });

        Assert.NotNull(best);
        Assert.Equal(DimensionSnapKind.Point, best!.Kind);
    }

    [Fact]
    public void SelectBestCandidate_AutoModePrefersIntersectionOverLowerPriorityFace()
    {
        var service = new DimensionSnapSelectionService();
        var candidates = new List<DimensionSnapCandidateInfo>
        {
            new()
            {
                Kind = DimensionSnapKind.Face,
                ElementId = "A",
                WorldPoint = new Point3D(0, 0, 0),
                LocalPoint = new Point3D(0, 0, 0),
                ScreenDistancePx = 10,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = true
            },
            new()
            {
                Kind = DimensionSnapKind.Intersection,
                ElementId = null,
                WorldPoint = new Point3D(1, 1, 0),
                LocalPoint = default,
                ScreenDistancePx = 6,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = false
            }
        };

        var best = service.SelectBestCandidate(candidates, new DimensionSnapSelectionContext
        {
            SnapTolerancePx = 12,
            RequestedKind = DimensionSnapKind.None
        });

        Assert.NotNull(best);
        Assert.Equal(DimensionSnapKind.Intersection, best!.Kind);
        Assert.Null(best.ElementId);
    }

    [Fact]
    public void SelectBestCandidate_RespectsRequestedKindFilter()
    {
        var service = new DimensionSnapSelectionService();
        var candidates = new List<DimensionSnapCandidateInfo>
        {
            new()
            {
                Kind = DimensionSnapKind.Point,
                ElementId = "A",
                WorldPoint = new Point3D(0, 0, 0),
                LocalPoint = new Point3D(0, 0, 0),
                ScreenDistancePx = 2,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = true
            },
            new()
            {
                Kind = DimensionSnapKind.Edge,
                ElementId = "B",
                WorldPoint = new Point3D(1, 0, 0),
                LocalPoint = new Point3D(1, 0, 0),
                ScreenDistancePx = 5,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = true
            }
        };

        var best = service.SelectBestCandidate(candidates, new DimensionSnapSelectionContext
        {
            SnapTolerancePx = 12,
            RequestedKind = DimensionSnapKind.Edge
        });

        Assert.NotNull(best);
        Assert.Equal(DimensionSnapKind.Edge, best!.Kind);
    }

    [Fact]
    public void SelectBestCandidate_RequestedKindFallsBackWhenRequestedKindIsUnavailable()
    {
        var service = new DimensionSnapSelectionService();
        var candidates = new List<DimensionSnapCandidateInfo>
        {
            new()
            {
                Kind = DimensionSnapKind.Point,
                ElementId = "A",
                WorldPoint = new Point3D(0, 0, 0),
                LocalPoint = new Point3D(0, 0, 0),
                ScreenDistancePx = 3,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = true
            },
            new()
            {
                Kind = DimensionSnapKind.Face,
                ElementId = "B",
                WorldPoint = new Point3D(1, 0, 0),
                LocalPoint = new Point3D(1, 0, 0),
                ScreenDistancePx = 2,
                IsValidForDimension = true,
                IsVisibleInView = true,
                HasStableReference = true
            }
        };

        var best = service.SelectBestCandidate(candidates, new DimensionSnapSelectionContext
        {
            SnapTolerancePx = 12,
            RequestedKind = DimensionSnapKind.Edge
        });

        Assert.NotNull(best);
        Assert.Equal(DimensionSnapKind.Point, best!.Kind);
        Assert.Equal("A", best.ElementId);
    }

    [Fact]
    public void SelectBestCandidate_UsesHysteresisToReduceFlicker()
    {
        var service = new DimensionSnapSelectionService();
        var sticky = new DimensionSnapCandidateInfo
        {
            Kind = DimensionSnapKind.Point,
            ElementId = "A",
            WorldPoint = new Point3D(0, 0, 0),
            LocalPoint = new Point3D(0, 0, 0),
            ScreenDistancePx = 5.2,
            IsValidForDimension = true,
            IsVisibleInView = true,
            HasStableReference = true
        };

        var almostBetter = new DimensionSnapCandidateInfo
        {
            Kind = DimensionSnapKind.Point,
            ElementId = "B",
            WorldPoint = new Point3D(0.1, 0, 0),
            LocalPoint = new Point3D(0.1, 0, 0),
            ScreenDistancePx = 5.0,
            IsValidForDimension = true,
            IsVisibleInView = true,
            HasStableReference = true
        };

        var best = service.SelectBestCandidate([sticky, almostBetter], new DimensionSnapSelectionContext
        {
            SnapTolerancePx = 12,
            RequestedKind = DimensionSnapKind.None,
            LastPreviewSnap = sticky
        });

        Assert.NotNull(best);
        Assert.Equal("A", best!.ElementId);
    }

    [Fact]
    public void SelectBestCandidate_UsesWorldPointHysteresisForIntersectionCandidates()
    {
        var service = new DimensionSnapSelectionService();
        var sticky = new DimensionSnapCandidateInfo
        {
            Kind = DimensionSnapKind.Intersection,
            ElementId = null,
            WorldPoint = new Point3D(5, 5, 0),
            LocalPoint = default,
            ScreenDistancePx = 9.2,
            IsValidForDimension = true,
            IsVisibleInView = true,
            HasStableReference = false
        };

        var almostBetter = new DimensionSnapCandidateInfo
        {
            Kind = DimensionSnapKind.Intersection,
            ElementId = null,
            WorldPoint = new Point3D(5.01, 5.01, 0),
            LocalPoint = default,
            ScreenDistancePx = 4.4,
            IsValidForDimension = true,
            IsVisibleInView = true,
            HasStableReference = false
        };

        var best = service.SelectBestCandidate([sticky, almostBetter], new DimensionSnapSelectionContext
        {
            SnapTolerancePx = 12,
            RequestedKind = DimensionSnapKind.None,
            LastPreviewSnap = sticky
        });

        Assert.NotNull(best);
        Assert.Equal(new Point3D(5, 5, 0), best!.WorldPoint);
        Assert.Null(best.ElementId);
    }
}
