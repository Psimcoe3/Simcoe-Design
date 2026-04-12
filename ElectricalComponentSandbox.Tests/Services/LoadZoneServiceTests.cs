using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class LoadZoneServiceTests
{
    // ── Polygon Area ────────────────────────────────────────────

    [Fact]
    public void CalculatePolygonArea_UnitSquare_Returns1()
    {
        var pts = new List<XYZ>
        {
            new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)
        };
        Assert.Equal(1.0, LoadZoneService.CalculatePolygonArea(pts), 6);
    }

    [Fact]
    public void CalculatePolygonArea_10x20Rectangle_Returns200()
    {
        var pts = new List<XYZ>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 20, 0), new(0, 20, 0)
        };
        Assert.Equal(200.0, LoadZoneService.CalculatePolygonArea(pts), 6);
    }

    [Fact]
    public void CalculatePolygonArea_Triangle_ReturnsHalfBaseTimesHeight()
    {
        // right triangle with base=6, height=4
        var pts = new List<XYZ>
        {
            new(0, 0, 0), new(6, 0, 0), new(0, 4, 0)
        };
        Assert.Equal(12.0, LoadZoneService.CalculatePolygonArea(pts), 6);
    }

    [Fact]
    public void CalculatePolygonArea_LShape_CorrectArea()
    {
        // L-shaped polygon: 10×10 with 5×5 notch cut
        var pts = new List<XYZ>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 5, 0),
            new(5, 5, 0), new(5, 10, 0), new(0, 10, 0)
        };
        Assert.Equal(75.0, LoadZoneService.CalculatePolygonArea(pts), 6);
    }

    [Fact]
    public void CalculatePolygonArea_FewerThan3Points_ReturnsZero()
    {
        Assert.Equal(0, LoadZoneService.CalculatePolygonArea(new List<XYZ> { new(0, 0, 0), new(1, 0, 0) }));
        Assert.Equal(0, LoadZoneService.CalculatePolygonArea(new List<XYZ>()));
    }

    [Fact]
    public void CalculatePolygonArea_CounterClockwise_ReturnsSameAsClockwise()
    {
        var cw = new List<XYZ> { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) };
        var ccw = new List<XYZ>(cw);
        ccw.Reverse();
        Assert.Equal(
            LoadZoneService.CalculatePolygonArea(cw),
            LoadZoneService.CalculatePolygonArea(ccw));
    }

    // ── CreateZone ──────────────────────────────────────────────

    [Fact]
    public void CreateZone_SetsLevelAndDensity()
    {
        var pts = new[] { new XYZ(0, 0, 0), new XYZ(10, 0, 0), new XYZ(10, 10, 0), new XYZ(0, 10, 0) };
        var zone = LoadZoneService.CreateZone(pts, "Level 1", LoadClassification.Lighting, 3.5);

        Assert.Equal("Level 1", zone.Level);
        Assert.Single(zone.LoadDensities);
        Assert.Equal(3.5, zone.LoadDensities[LoadClassification.Lighting]);
    }

    [Fact]
    public void CreateZone_ZeroDensity_NoDensitiesAdded()
    {
        var pts = new[] { new XYZ(0, 0, 0), new XYZ(1, 0, 0), new XYZ(1, 1, 0) };
        var zone = LoadZoneService.CreateZone(pts, "Level 1");
        Assert.Empty(zone.LoadDensities);
    }

    // ── CalculateZoneLoad ───────────────────────────────────────

    [Fact]
    public void CalculateZoneLoad_DensityTimesArea()
    {
        // 100 sq ft zone at 3.5 W/ft² → 350 VA
        var zone = new LoadZone
        {
            Name = "Office A",
            BoundaryPoints = { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) },
            LoadDensities = { [LoadClassification.Lighting] = 3.5 }
        };

        var result = LoadZoneService.CalculateZoneLoad(zone);
        Assert.Equal(100.0, result.AreaSqFt, 6);
        Assert.Equal(350.0, result.TotalLoadVA, 6);
        Assert.Single(result.ClassificationLoads);
        Assert.Equal(LoadClassification.Lighting, result.ClassificationLoads[0].Classification);
    }

    [Fact]
    public void CalculateZoneLoad_MultipleClassifications()
    {
        // 200 sq ft zone: Lighting 2 W/ft² + Power 5 W/ft²
        var zone = new LoadZone
        {
            BoundaryPoints = { new(0, 0, 0), new(20, 0, 0), new(20, 10, 0), new(0, 10, 0) },
            LoadDensities =
            {
                [LoadClassification.Lighting] = 2.0,
                [LoadClassification.Power] = 5.0
            }
        };

        var result = LoadZoneService.CalculateZoneLoad(zone);
        Assert.Equal(200.0, result.AreaSqFt, 6);
        Assert.Equal(1400.0, result.TotalLoadVA, 6); // (200×2) + (200×5)
        Assert.Equal(2, result.ClassificationLoads.Count);
    }

    [Fact]
    public void CalculateZoneLoad_NoDensities_ZeroLoad()
    {
        var zone = new LoadZone
        {
            BoundaryPoints = { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) }
        };

        var result = LoadZoneService.CalculateZoneLoad(zone);
        Assert.Equal(100.0, result.AreaSqFt, 6);
        Assert.Equal(0, result.TotalLoadVA);
    }

    // ── SumZoneLoads ────────────────────────────────────────────

    [Fact]
    public void SumZoneLoads_MultipleZones_AggregatesCorrectly()
    {
        var z1 = new LoadZone
        {
            Name = "Zone A",
            BoundaryPoints = { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) },
            LoadDensities = { [LoadClassification.Lighting] = 3.0 }
        };
        var z2 = new LoadZone
        {
            Name = "Zone B",
            BoundaryPoints = { new(0, 0, 0), new(20, 0, 0), new(20, 5, 0), new(0, 5, 0) },
            LoadDensities = { [LoadClassification.Lighting] = 2.0, [LoadClassification.Power] = 4.0 }
        };

        var summary = LoadZoneService.SumZoneLoads(new[] { z1, z2 });
        Assert.Equal(200.0, summary.TotalAreaSqFt, 6); // 100 + 100
        Assert.Equal(900.0, summary.TotalLoadVA, 6);   // 300 + (200 + 400)
        Assert.Equal(500.0, summary.ClassificationTotals[LoadClassification.Lighting]); // 300 + 200
        Assert.Equal(400.0, summary.ClassificationTotals[LoadClassification.Power]);
        Assert.Equal(2, summary.ZoneResults.Count);
    }

    [Fact]
    public void SumZoneLoads_EmptyList_ReturnsZeros()
    {
        var summary = LoadZoneService.SumZoneLoads(Array.Empty<LoadZone>());
        Assert.Equal(0, summary.TotalAreaSqFt);
        Assert.Equal(0, summary.TotalLoadVA);
        Assert.Empty(summary.ClassificationTotals);
    }

    // ── Zone boundary persistence (model) ───────────────────────

    [Fact]
    public void LoadZone_BoundaryPersistenceRoundTrip()
    {
        var zone = new LoadZone
        {
            Id = "z1",
            Name = "Server Room",
            Level = "L2",
            Phase = "A",
            BoundaryPoints = { new(0, 0, 0), new(15, 0, 0), new(15, 12, 0), new(0, 12, 0) },
            LoadDensities = { [LoadClassification.Power] = 25.0 }
        };

        Assert.Equal("z1", zone.Id);
        Assert.Equal("Server Room", zone.Name);
        Assert.Equal("L2", zone.Level);
        Assert.Equal("A", zone.Phase);
        Assert.Equal(4, zone.BoundaryPoints.Count);
        Assert.Equal(25.0, zone.LoadDensities[LoadClassification.Power]);
    }

    [Fact]
    public void ProjectModel_HasLoadZones()
    {
        var pm = new ProjectModel();
        Assert.NotNull(pm.LoadZones);
        Assert.Empty(pm.LoadZones);
    }

    // ── AnalyzePanelLoad with zones ─────────────────────────────

    [Fact]
    public void AnalyzePanelLoad_NoZones_PreliminaryZoneLoadIsZero()
    {
        var svc = new ElectricalCalculationService();
        var schedule = CreateMinimalSchedule();

        var result = svc.AnalyzePanelLoad(schedule, null, Array.Empty<LoadZone>());

        Assert.Equal(0, result.PreliminaryZoneLoadVA);
        Assert.Equal(result.TotalDemandVA, result.CombinedLoadVA);
    }

    [Fact]
    public void AnalyzePanelLoad_WithZones_AddsPreliminaryLoad()
    {
        var svc = new ElectricalCalculationService();
        var schedule = CreateMinimalSchedule();

        var zone = new LoadZone
        {
            BoundaryPoints = { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) },
            LoadDensities = { [LoadClassification.Lighting] = 5.0 }
        };

        var result = svc.AnalyzePanelLoad(schedule, null, new[] { zone });

        Assert.Equal(500.0, result.PreliminaryZoneLoadVA, 1);
        Assert.Equal(result.TotalDemandVA + 500.0, result.CombinedLoadVA, 1);
    }

    [Fact]
    public void AnalyzePanelLoad_ZonesMergeClassificationTotals()
    {
        var svc = new ElectricalCalculationService();
        var schedule = new PanelSchedule
        {
            PanelName = "LP-Z",
            MainBreakerAmps = 200,
            BusAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1000,
                         DemandFactor = 1.0, LoadClassification = LoadClassification.Lighting }
            }
        };

        var zone = new LoadZone
        {
            BoundaryPoints = { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) },
            LoadDensities = { [LoadClassification.Lighting] = 3.0 }
        };

        var result = svc.AnalyzePanelLoad(schedule, null, new[] { zone });

        // Circuit contributes 1000 VA Lighting, zone adds 300 VA Lighting
        Assert.Equal(1300.0, result.ClassificationTotals[LoadClassification.Lighting], 1);
    }

    [Fact]
    public void AnalyzePanelLoad_ZoneAddsNewClassification()
    {
        var svc = new ElectricalCalculationService();
        var schedule = CreateMinimalSchedule(); // Power circuits only

        var zone = new LoadZone
        {
            BoundaryPoints = { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) },
            LoadDensities = { [LoadClassification.HVAC] = 8.0 }
        };

        var result = svc.AnalyzePanelLoad(schedule, null, new[] { zone });

        Assert.True(result.ClassificationTotals.ContainsKey(LoadClassification.HVAC));
        Assert.Equal(800.0, result.ClassificationTotals[LoadClassification.HVAC], 1);
    }

    [Fact]
    public void AnalyzePanelLoad_LegacyOverload_ZeroZoneFields()
    {
        // The original 2-param overload should produce zero-zone fields
        var svc = new ElectricalCalculationService();
        var schedule = CreateMinimalSchedule();

        var result = svc.AnalyzePanelLoad(schedule);

        Assert.Equal(0, result.PreliminaryZoneLoadVA);
        Assert.Equal(0, result.CombinedLoadVA); // init default
    }

    // ── NEC typical density scenarios ───────────────────────────

    [Fact]
    public void NecLightingDensity_Office_3_5WPerSqFt()
    {
        // NEC Table 220.12: Office = 3.5 VA/ft²; 1000 sq ft office
        var zone = new LoadZone
        {
            Name = "Office",
            BoundaryPoints = { new(0, 0, 0), new(50, 0, 0), new(50, 20, 0), new(0, 20, 0) },
            LoadDensities = { [LoadClassification.Lighting] = 3.5 }
        };

        var result = LoadZoneService.CalculateZoneLoad(zone);
        Assert.Equal(1000.0, result.AreaSqFt, 6);
        Assert.Equal(3500.0, result.TotalLoadVA, 6);
    }

    [Fact]
    public void NecLightingDensity_Warehouse_0_25WPerSqFt()
    {
        // NEC 220.12: Warehouse = 0.25 VA/ft²; 5000 sq ft
        var zone = new LoadZone
        {
            Name = "Warehouse",
            BoundaryPoints = { new(0, 0, 0), new(100, 0, 0), new(100, 50, 0), new(0, 50, 0) },
            LoadDensities = { [LoadClassification.Lighting] = 0.25 }
        };

        var result = LoadZoneService.CalculateZoneLoad(zone);
        Assert.Equal(5000.0, result.AreaSqFt, 6);
        Assert.Equal(1250.0, result.TotalLoadVA, 6);
    }

    // ── helpers ──────────────────────────────────────────────────

    private static PanelSchedule CreateMinimalSchedule() => new()
    {
        PanelName = "LP-1",
        MainBreakerAmps = 200,
        BusAmps = 200,
        VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
        Circuits = new List<Circuit>
        {
            new() { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1800, DemandFactor = 1.0 }
        }
    };
}
