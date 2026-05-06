using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Core.Routing;

namespace ElectricalComponentSandbox.Tests.Conduit;

public class ConduitRoutingWorkflowTests
{
    private static ConduitModelStore CreateStore()
    {
        var store = new ConduitModelStore();
        var type = new ConduitType
        {
            IsWithFitting = true,
            SizeSettings = ConduitSizeSettings.CreateDefaultEMT()
        };
        store.AddType(type);
        store.Settings.DefaultConduitTypeId = type.Id;
        return store;
    }

    [Fact]
    public void ResolveRoutingDefaults_InvalidTradeSize_FallsBackToTypeTradeSizeAndMaterial()
    {
        var store = new ConduitModelStore();
        var type = new ConduitType
        {
            Standard = ConduitMaterialType.PVC,
            SizeSettings = new ConduitSizeSettings { Standard = ConduitMaterialType.PVC }
        };
        type.SizeSettings.AddSize(new ConduitSize { TradeSize = "1" });
        store.AddType(type);
        store.Settings.DefaultConduitTypeId = type.Id;

        var defaults = store.ResolveRoutingDefaults(type.Id, "9", null);

        Assert.Equal(type.Id, defaults.ConduitTypeId);
        Assert.Equal("1", defaults.TradeSize);
        Assert.Equal(ConduitMaterialType.PVC, defaults.Material);
    }

    [Fact]
    public void CreateRunFromSegments_TypeWithoutFittings_SkipsAutomaticFittings()
    {
        var store = new ConduitModelStore();
        var type = new ConduitType
        {
            IsWithFitting = false,
            SizeSettings = ConduitSizeSettings.CreateDefaultEMT()
        };
        store.AddType(type);

        var segments = new List<ConduitSegment>
        {
            new() { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(5, 0, 0), ConduitTypeId = type.Id, TradeSize = "1/2" },
            new() { StartPoint = new XYZ(5, 0, 0), EndPoint = new XYZ(5, 5, 0), ConduitTypeId = type.Id, TradeSize = "1/2" }
        };

        var run = store.CreateRunFromSegments(segments);

        Assert.Empty(run.FittingIds);
    }

    [Fact]
    public void AutoRoute_AutoRiseDropDisabled_FlattensPathToSpecifiedElevation()
    {
        var store = new ConduitModelStore();
        var type = new ConduitType();
        store.AddType(type);
        store.Settings.DefaultConduitTypeId = type.Id;

        var service = new AutoRouteService(store);
        var pathway = new List<XYZ>
        {
            new(0, 0, 0),
            new(5, 0, 12),
            new(10, 0, 3)
        };

        var run = service.AutoRoute(pathway, new RoutingOptions
        {
            ConduitTypeId = type.Id,
            TradeSize = "1/2",
            Elevation = 8,
            AutoRiseDrop = false
        });

        var zValues = run.GetSegments(store)
            .SelectMany(s => new[] { s.StartPoint.Z, s.EndPoint.Z })
            .Distinct()
            .ToList();

        Assert.Single(zValues);
        Assert.Equal(8, zValues[0], 6);
    }

    [Fact]
    public void AutoRoute_UsePathfinding_RoutesAroundObstacle()
    {
        var store = CreateStore();
        var type = store.GetAllTypes().Single();
        var service = new AutoRouteService(store);

        var run = service.AutoRoute(
            new List<XYZ>
            {
                new(0, 0, 10),
                new(10, 0, 10)
            },
            new RoutingOptions
            {
                ConduitTypeId = type.Id,
                UsePathfinding = true,
                RoutingBounds = new ObstacleBox(new XYZ(-1, -3, 9), new XYZ(11, 3, 11)),
                Obstacles = new List<ObstacleBox>
                {
                    new(new XYZ(4, -1, 9), new XYZ(6, 1, 11))
                },
                VoxelSize = 1.0,
                Elevation = 10,
                AutoRiseDrop = true
            });

        Assert.True(run.SegmentIds.Count > 1);

        foreach (var segment in run.GetSegments(store))
        {
            Assert.False(segment.StartPoint.X > 4 && segment.StartPoint.X < 6 && Math.Abs(segment.StartPoint.Y) < 1.0);
            Assert.False(segment.EndPoint.X > 4 && segment.EndPoint.X < 6 && Math.Abs(segment.EndPoint.Y) < 1.0);
        }
    }

    [Fact]
    public void AutoRouteParallel_CreatesParallelRunsFromCenterPath()
    {
        var store = CreateStore();
        var type = store.GetAllTypes().Single();
        var service = new AutoRouteService(store);

        var runs = service.AutoRouteParallel(
            new List<XYZ>
            {
                new(0, 0, 10),
                new(10, 0, 10)
            },
            runCount: 3,
            spacing: 2.0,
            options: new RoutingOptions
            {
                ConduitTypeId = type.Id,
                Elevation = 10,
                AutoRiseDrop = true
            });

        Assert.Equal(3, runs.Count);

        var yOffsets = runs
            .Select(r => r.GetSegments(store).Single().StartPoint.Y)
            .OrderBy(y => y)
            .ToList();

        Assert.Equal(-2.0, yOffsets[0], 6);
        Assert.Equal(0.0, yOffsets[1], 6);
        Assert.Equal(2.0, yOffsets[2], 6);
    }

    [Fact]
    public void AutoRouteParallel_WithPathfinding_RoutesEachRunAroundObstacle()
    {
        var store = CreateStore();
        var type = store.GetAllTypes().Single();
        var service = new AutoRouteService(store);

        var runs = service.AutoRouteParallel(
            new List<XYZ>
            {
                new(0, 0, 10),
                new(10, 0, 10)
            },
            runCount: 2,
            spacing: 2.0,
            options: new RoutingOptions
            {
                ConduitTypeId = type.Id,
                UsePathfinding = true,
                RoutingBounds = new ObstacleBox(new XYZ(-1, -4, 9), new XYZ(11, 4, 11)),
                Obstacles = new List<ObstacleBox>
                {
                    new(new XYZ(4, -2, 9), new XYZ(6, 2, 11))
                },
                VoxelSize = 1.0,
                Elevation = 10,
                AutoRiseDrop = true
            });

        Assert.Equal(2, runs.Count);
        Assert.All(runs, run => Assert.True(run.SegmentIds.Count > 1));
    }
}
