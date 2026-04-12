using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class CableTrayTests
{
    // ── CableSpec ────────────────────────────────────────────────

    [Fact]
    public void CableSpec_AreaSqIn_ComputesCorrectly()
    {
        var spec = new CableSpec { OuterDiameterInches = 1.0, Quantity = 1 };
        Assert.Equal(Math.PI / 4, spec.AreaSqIn, 6);
    }

    [Fact]
    public void CableSpec_TotalAreaSqIn_MultipliesByQuantity()
    {
        var spec = new CableSpec { OuterDiameterInches = 1.0, Quantity = 5 };
        Assert.Equal(Math.PI / 4 * 5, spec.TotalAreaSqIn, 6);
    }

    [Fact]
    public void CableSpec_Defaults()
    {
        var spec = new CableSpec();
        Assert.Equal(0, spec.OuterDiameterInches);
        Assert.Equal(1, spec.Quantity);
        Assert.Equal(string.Empty, spec.Label);
    }

    // ── CableTraySegment ─────────────────────────────────────────

    [Fact]
    public void CableTraySegment_Length_Computed()
    {
        var seg = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        Assert.Equal(10.0, seg.Length, 6);
    }

    [Fact]
    public void CableTraySegment_Direction_Normalized()
    {
        var seg = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(6, 0, 0)
        };
        Assert.True(seg.Direction.IsAlmostEqualTo(XYZ.BasisX));
    }

    [Fact]
    public void CableTraySegment_Defaults()
    {
        var seg = new CableTraySegment();
        Assert.Equal(12.0, seg.Width);
        Assert.Equal(4.0, seg.Depth);
        Assert.Equal(CableTrayType.Ladder, seg.TrayType);
        Assert.Equal("Level 1", seg.LevelId);
    }

    // ── CableTrayFitting ─────────────────────────────────────────

    [Fact]
    public void CableTrayFitting_Defaults()
    {
        var fitting = new CableTrayFitting();
        Assert.Equal(12.0, fitting.Width);
        Assert.Equal(4.0, fitting.Depth);
        Assert.Empty(fitting.ConnectedSegmentIds);
    }

    // ── CableTrayRun ─────────────────────────────────────────────

    [Fact]
    public void CableTrayRun_Defaults()
    {
        var run = new CableTrayRun();
        Assert.Equal(12.0, run.Width);
        Assert.Equal(4.0, run.Depth);
        Assert.Equal(CableTrayType.Ladder, run.TrayType);
        Assert.Equal(0, run.FillPercent);
        Assert.NotEmpty(run.Id);
        Assert.Empty(run.SegmentIds);
        Assert.Empty(run.FittingIds);
    }

    // ── CableTrayFillService ─────────────────────────────────────

    [Theory]
    [InlineData(CableTrayType.Ladder, 50.0)]
    [InlineData(CableTrayType.VentilatedTrough, 50.0)]
    [InlineData(CableTrayType.SolidBottom, 40.0)]
    [InlineData(CableTrayType.Channel, 50.0)]
    [InlineData(CableTrayType.Wire, 50.0)]
    [InlineData(CableTrayType.SingleRail, 50.0)]
    public void GetMaxFillPercent_ByTrayType(CableTrayType trayType, double expected)
    {
        Assert.Equal(expected, CableTrayFillService.GetMaxFillPercent(trayType));
    }

    [Fact]
    public void GetTrayArea_WidthTimesDepth()
    {
        Assert.Equal(48.0, CableTrayFillService.GetTrayArea(12, 4));
        Assert.Equal(96.0, CableTrayFillService.GetTrayArea(24, 4));
    }

    [Fact]
    public void CalculateFill_BasicLadder_UnderLimit()
    {
        var cables = new[]
        {
            new CableSpec { OuterDiameterInches = 1.0, Quantity = 10 }
        };
        // 10 cables × π/4 sq in each ≈ 7.854 sq in
        // Tray area = 12 × 4 = 48 sq in → fill ≈ 16.36%
        var result = CableTrayFillService.CalculateFill(12, 4, CableTrayType.Ladder, cables);

        Assert.Equal(48.0, result.TrayAreaSqIn);
        Assert.Equal(10, result.CableCount);
        Assert.True(result.FillPercent < 50);
        Assert.False(result.ExceedsCode);
        Assert.Equal(50.0, result.MaxAllowedFillPercent);
        Assert.Equal("NEC 392.22", result.NecReference);
    }

    [Fact]
    public void CalculateFill_OverLimit_ExceedsCode()
    {
        // Pack a small solid-bottom tray with many cables to exceed 40%
        var cables = new[]
        {
            new CableSpec { OuterDiameterInches = 2.0, Quantity = 10 }
        };
        // 10 cables × π sq in each ≈ 31.42 sq in
        // Tray area = 12 × 4 = 48 sq in → fill ≈ 65.45% > 40%
        var result = CableTrayFillService.CalculateFill(12, 4, CableTrayType.SolidBottom, cables);

        Assert.True(result.ExceedsCode);
        Assert.True(result.FillPercent > 40);
        Assert.Equal(40.0, result.MaxAllowedFillPercent);
    }

    [Fact]
    public void CalculateFill_NoCables_ZeroFill()
    {
        var result = CableTrayFillService.CalculateFill(12, 4, CableTrayType.Ladder, Array.Empty<CableSpec>());

        Assert.Equal(0, result.FillPercent);
        Assert.Equal(0, result.CableCount);
        Assert.Equal(0, result.TotalCableAreaSqIn);
        Assert.False(result.ExceedsCode);
    }

    [Fact]
    public void CalculateFill_ZeroTrayArea_ZeroFill()
    {
        var cables = new[] { new CableSpec { OuterDiameterInches = 1.0, Quantity = 5 } };
        var result = CableTrayFillService.CalculateFill(0, 0, CableTrayType.Ladder, cables);

        Assert.Equal(0, result.FillPercent);
    }

    [Fact]
    public void CalculateFill_MultipleCableSpecs()
    {
        var cables = new[]
        {
            new CableSpec { OuterDiameterInches = 0.5, Quantity = 4, Label = "MC 12/2" },
            new CableSpec { OuterDiameterInches = 0.75, Quantity = 6, Label = "MC 10/3" }
        };
        var result = CableTrayFillService.CalculateFill(12, 4, CableTrayType.Ladder, cables);

        Assert.Equal(10, result.CableCount);
        double expectedArea = 4 * Math.PI * Math.Pow(0.25, 2) + 6 * Math.PI * Math.Pow(0.375, 2);
        Assert.Equal(expectedArea, result.TotalCableAreaSqIn, 4);
    }

    [Fact]
    public void RecommendTrayWidth_FindsSmallestStandard()
    {
        // Small load fits in 6"
        var cables = new[] { new CableSpec { OuterDiameterInches = 0.5, Quantity = 2 } };
        double? width = CableTrayFillService.RecommendTrayWidth(4, CableTrayType.Ladder, cables);

        Assert.NotNull(width);
        Assert.Equal(6.0, width);
    }

    [Fact]
    public void RecommendTrayWidth_NeedsLargerTray()
    {
        // Many cables need a wider tray
        var cables = new[] { new CableSpec { OuterDiameterInches = 2.0, Quantity = 20 } };
        double? width = CableTrayFillService.RecommendTrayWidth(4, CableTrayType.Ladder, cables);

        if (width != null)
            Assert.True(width >= 12);
    }

    [Fact]
    public void RecommendTrayWidth_ReturnsNull_WhenNothingFits()
    {
        // Enormous cable load that no standard width can handle
        var cables = new[] { new CableSpec { OuterDiameterInches = 4.0, Quantity = 100 } };
        double? width = CableTrayFillService.RecommendTrayWidth(4, CableTrayType.SolidBottom, cables);

        Assert.Null(width);
    }

    // ── NEC 392 Scenario Tests ───────────────────────────────────

    [Fact]
    public void NEC392_Ladder_At50PercentBoundary()
    {
        // Exactly 50% fill in a ladder tray should NOT exceed code
        // Tray area = 12 × 4 = 48, so need 24 sq in of cable
        // Cable OD to produce 24 sq in: area = π(d/2)² → d = 2√(24/π) ≈ 5.53"
        double neededDiameter = 2 * Math.Sqrt(24 / Math.PI);
        var cables = new[] { new CableSpec { OuterDiameterInches = neededDiameter, Quantity = 1 } };
        var result = CableTrayFillService.CalculateFill(12, 4, CableTrayType.Ladder, cables);

        Assert.Equal(50.0, result.FillPercent);
        Assert.False(result.ExceedsCode);
    }

    [Fact]
    public void NEC392_SolidBottom_Lower40Threshold()
    {
        // 45% fill in solid bottom should exceed (limit = 40%)
        // Need 21.6 sq in of cable area for 45%
        double neededDiameter = 2 * Math.Sqrt(21.6 / Math.PI);
        var cables = new[] { new CableSpec { OuterDiameterInches = neededDiameter, Quantity = 1 } };
        var result = CableTrayFillService.CalculateFill(12, 4, CableTrayType.SolidBottom, cables);

        Assert.Equal(45.0, result.FillPercent);
        Assert.True(result.ExceedsCode);
    }

    // ── CableTrayRunService ──────────────────────────────────────

    [Fact]
    public void CreateRunFromSegments_SingleSegment_NoFittings()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(20, 0, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg });

        Assert.Single(run.SegmentIds);
        Assert.Empty(run.FittingIds);
        Assert.Equal(12.0, run.Width);
        Assert.Equal(4.0, run.Depth);
        Assert.True(store.Segments.ContainsKey(seg.Id));
    }

    [Fact]
    public void CreateRunFromSegments_TwoSegments_90DegBend_InsertsFitting()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg1 = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var seg2 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(10, 10, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg1, seg2 });

        Assert.Equal(2, run.SegmentIds.Count);
        Assert.Single(run.FittingIds);

        var fitting = store.Fittings[run.FittingIds[0]];
        Assert.Equal(CableTrayFittingType.Elbow90, fitting.Type);
        Assert.True(fitting.Location.IsAlmostEqualTo(new XYZ(10, 0, 0)));
        Assert.InRange(fitting.AngleDegrees, 85, 95); // ~90°
    }

    [Fact]
    public void CreateRunFromSegments_TwoSegments_45DegBend_InsertsFitting()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg1 = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var seg2 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(20, 5, 0)   // ~26.57° from horizontal
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg1, seg2 });

        Assert.Single(run.FittingIds);
        var fitting = store.Fittings[run.FittingIds[0]];
        Assert.Equal(CableTrayFittingType.Elbow45, fitting.Type);
    }

    [Fact]
    public void CreateRunFromSegments_CollinearSegments_NoFitting()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg1 = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var seg2 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(20, 0, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg1, seg2 });

        Assert.Empty(run.FittingIds);
    }

    [Fact]
    public void CreateRunFromSegments_ThreeSegments_TwoBends()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg1 = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var seg2 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(10, 10, 0)
        };
        var seg3 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 10, 0),
            EndPoint = new XYZ(0, 10, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg1, seg2, seg3 });

        Assert.Equal(3, run.SegmentIds.Count);
        Assert.Equal(2, run.FittingIds.Count);
    }

    [Fact]
    public void GetTotalLength_SumsSegments()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg1 = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var seg2 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(10, 5, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg1, seg2 });
        double total = CableTrayRunService.GetTotalLength(store, run);

        Assert.Equal(15.0, total, 6);
    }

    [Fact]
    public void GetSegments_ReturnsInOrder()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg1 = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var seg2 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(10, 10, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg1, seg2 });
        var segments = CableTrayRunService.GetSegments(store, run);

        Assert.Equal(2, segments.Count);
        Assert.Equal(seg1.Id, segments[0].Id);
        Assert.Equal(seg2.Id, segments[1].Id);
    }

    [Fact]
    public void GetFittings_ReturnsInOrder()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg1 = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var seg2 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 0, 0),
            EndPoint = new XYZ(10, 10, 0)
        };
        var seg3 = new CableTraySegment
        {
            StartPoint = new XYZ(10, 10, 0),
            EndPoint = new XYZ(0, 10, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg1, seg2, seg3 });
        var fittings = CableTrayRunService.GetFittings(store, run);

        Assert.Equal(2, fittings.Count);
    }

    [Fact]
    public void CreateRunFromSegments_PropagatesTrayProperties()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };

        var run = CableTrayRunService.CreateRunFromSegments(
            store, new[] { seg },
            width: 24.0, depth: 6.0, trayType: CableTrayType.SolidBottom);

        Assert.Equal(24.0, run.Width);
        Assert.Equal(6.0, run.Depth);
        Assert.Equal(CableTrayType.SolidBottom, run.TrayType);
    }

    // ── Cable Tray Schedule ──────────────────────────────────────

    [Fact]
    public void GenerateCableTraySchedule_BasicRunsWithoutFill()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(20, 0, 0)
        };
        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg });
        run.RunId = "CT-001";

        var svc = new ScheduleTableService();
        var table = svc.GenerateCableTraySchedule(new[] { run }, store);

        Assert.Equal("CABLE TRAY SCHEDULE", table.Title);
        Assert.Equal(6, table.Columns.Count); // No FILL % column
        Assert.Single(table.Rows);
        Assert.Equal("CT-001", table.Rows[0][0]);
        Assert.Contains("Ladder", table.Rows[0][1]);
        Assert.Equal("20.0", table.Rows[0][3]);
    }

    [Fact]
    public void GenerateCableTraySchedule_WithFillResults()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var run = CableTrayRunService.CreateRunFromSegments(store, new[] { seg });
        run.RunId = "CT-002";

        var fillResults = new Dictionary<string, CableTrayFillResult>
        {
            [run.Id] = new CableTrayFillResult
            {
                TrayAreaSqIn = 48,
                TotalCableAreaSqIn = 30,
                FillPercent = 62.5,
                MaxAllowedFillPercent = 50,
                CableCount = 10,
                ExceedsCode = true,
                NecReference = "NEC 392.22"
            }
        };

        var svc = new ScheduleTableService();
        var table = svc.GenerateCableTraySchedule(new[] { run }, store, fillResults);

        Assert.Equal(7, table.Columns.Count); // Now has FILL % column
        Assert.Contains("FILL %", table.Columns[6].Header);
        Assert.Contains("62.5%", table.Rows[0][6]);
        Assert.Contains("!", table.Rows[0][6]);
    }

    [Fact]
    public void GenerateCableTraySchedule_SizeColumn_ShowsWidthByDepth()
    {
        var store = new CableTrayRunService.CableTrayStore();
        var seg = new CableTraySegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(10, 0, 0)
        };
        var run = CableTrayRunService.CreateRunFromSegments(
            store, new[] { seg }, width: 24, depth: 6);
        run.RunId = "CT-003";

        var svc = new ScheduleTableService();
        var table = svc.GenerateCableTraySchedule(new[] { run }, store);

        Assert.Equal("24\"×6\"", table.Rows[0][2]);
    }

    // ── ProjectModel integration ─────────────────────────────────

    [Fact]
    public void ProjectModel_CableTrayRuns_DefaultEmpty()
    {
        var project = new ProjectModel();
        Assert.NotNull(project.CableTrayRuns);
        Assert.Empty(project.CableTrayRuns);
    }

    [Fact]
    public void ProjectModel_CableTrayRuns_PersistsRuns()
    {
        var project = new ProjectModel();
        var run = new CableTrayRun { RunId = "CT-001" };
        project.CableTrayRuns.Add(run);
        Assert.Single(project.CableTrayRuns);
        Assert.Equal("CT-001", project.CableTrayRuns[0].RunId);
    }
}
